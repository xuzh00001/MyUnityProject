using System.IO;
using UnityEngine;
using VIVE.OpenXR;
using VIVE.OpenXR.EyeTracker;
using System;
using System.Threading;
using System.Collections.Concurrent;
 
public class ContinuousEyeRecorder : MonoBehaviour
{
    [SerializeField] private int targetFrameRate = 120;
 
    private string participantId;
    public string ParticipantId => participantId;
    public int CurrentRunIndex => runIndex;
 
    private StreamWriter writer;
    private bool isRecording = false;
 
    private int speedMs = 100;
    private int runIndex = 0;
 
    private int frameCounter = 0;
    private float recordStartTime = 0f;
 
    // use two Threads (one for sampling, one for writing)
    private Thread samplerThread;
    private Thread writerThread;
    private readonly ConcurrentQueue<string> writeQueue = new ConcurrentQueue<string>();
 
    public static int CurrentTrial = -1;
    public static string CurrentCategory = "NA";
    public static int CurrentIndex = -1;
    public static string CurrentImageName = "NA";
    public static BlockType CurrentBlock = BlockType.Baseline;
 
    private bool leftBlinkMask = false;
    private bool rightBlinkMask = false;
 
    void Awake()
    {
        QualitySettings.vSyncCount = 0;
        Application.targetFrameRate = targetFrameRate;
        participantId = GenerateParticipantId();
        Debug.Log($"Participant ID (new session): {participantId}");
    }
 
    string GenerateParticipantId()
    {
        return "P_" + Guid.NewGuid().ToString("N").Substring(0, 8).ToUpper();
    }
 
    public enum BlockType
    {
        Baseline,
        Stimulus,
        Interval
    }
 
    public void SetSpeed(int ms)
    {
        speedMs = ms;
    }
 
    public void StartRecording()
    {
        if (isRecording) return;
 
        string root = Application.persistentDataPath;
        string folder = Path.Combine(root, participantId);
 
        if (!Directory.Exists(folder))
            Directory.CreateDirectory(folder);
 
        runIndex = GetNextRunIndex(folder);
 
        string fileName = $"{participantId}_{speedMs}ms_run-{runIndex:D2}.csv";
        string filePath = Path.Combine(folder, fileName);
 
        writer = new StreamWriter(filePath, false);
        writer.WriteLine(
            "Time," +
            "LeftPupilRaw,RightPupilRaw," +
            "LeftPupilBlink,RightPupilBlink," +
            "LeftIsBlink,RightIsBlink," +
            "Block,Trial,Category,Index,ImageID,Speed,ParticipantID"
        );
        writer.Flush();
 
        frameCounter = 0;
        recordStartTime = Time.realtimeSinceStartup;
 
        isRecording = true;
 
        // start sampler Thread
        samplerThread = new Thread(SamplerLoop);
        samplerThread.IsBackground = true;
        samplerThread.Start();
 
        // start writer Thread
        writerThread = new Thread(WriterLoop);
        writerThread.IsBackground = true;
        writerThread.Start();

    }
 
    public void StopRecording()
    {
        isRecording = false;
 
        samplerThread?.Join();
        writerThread?.Join();
 
        writer?.Flush();
        writer?.Close();
        writer = null;

        writeQueue.Clear(); 
 
        float duration = Time.realtimeSinceStartup - recordStartTime;
        float fps = frameCounter / duration;
 
        Debug.Log("Recording stopped.");
        Debug.Log($"Total samples: {frameCounter}");
        Debug.Log($"Duration: {duration:F2}s");
        Debug.Log($"Effective Rate: {fps:F2} Hz");
    }
 
    // Sampling thread
    private void SamplerLoop()
    {
        while (isRecording)
        {
            float leftRaw, rightRaw;
            float leftBlinkAware, rightBlinkAware;
            int leftIsBlink, rightIsBlink;
 
            GetPupilData(
                out leftRaw,
                out rightRaw,
                out leftBlinkAware,
                out rightBlinkAware,
                out leftIsBlink,
                out rightIsBlink
            );
 
            float time = Time.realtimeSinceStartup - recordStartTime;
 
            string trialStr = CurrentTrial > 0 ? CurrentTrial.ToString() : "NA";
            string indexStr = CurrentIndex > 0 ? CurrentIndex.ToString() : "NA";
            string category = string.IsNullOrEmpty(CurrentCategory) ? "NA" : CurrentCategory;
            string image = string.IsNullOrEmpty(CurrentImageName) ? "NA" : CurrentImageName;
            string block = CurrentBlock.ToString();
 
            string line =
                $"{time:F4}," +
                $"{leftRaw},{rightRaw}," +
                $"{leftBlinkAware},{rightBlinkAware}," +
                $"{leftIsBlink},{rightIsBlink}," +
                $"{block},{trialStr},{category},{indexStr},{image},{speedMs},{participantId}";
 
            writeQueue.Enqueue(line);
            Interlocked.Increment(ref frameCounter);
 
            // Samples are taken every 4ms (250Hz polling).
            Thread.Sleep(4);
        }
    }
 
    // Write thread
    private void WriterLoop()
    {
        while (isRecording || !writeQueue.IsEmpty)
        {
            if (writeQueue.TryDequeue(out string line))
            {
                writer.WriteLine(line);
            }
            else
            {
                Thread.Sleep(1);
            }
        }
    }
 
    // No longer using Update
    void Update() { }
 
    int GetNextRunIndex(string folder)
    {
        int maxRun = 0;
 
        if (!Directory.Exists(folder))
            return 1;
 
        var files = Directory.GetFiles(folder, $"{participantId}_{speedMs}ms_run-*.csv");
 
        foreach (var file in files)
        {
            string name = Path.GetFileNameWithoutExtension(file);
            int idx = name.LastIndexOf("run-");
            if (idx >= 0 &&
                int.TryParse(name.Substring(idx + 4), out int run))
            {
                maxRun = Mathf.Max(maxRun, run);
            }
        }
 
        return maxRun + 1;
    }
 
    // Helper Functions
    private bool IsBlinkFrame(
        XrEyePositionHTC eye,
        ref bool blinkMask,
        XrSingleEyePupilDataHTC pupil
    )
    {
#if UNITY_ANDROID && !UNITY_EDITOR
        XrSingleEyeGeometricDataHTC[] geometrics = null;
        XR_HTC_eye_tracker.Interop.GetEyeGeometricData(out geometrics);
 
        if (geometrics == null || geometrics.Length < 2)
            return blinkMask;
 
        var g = geometrics[(int)eye];
        if (!g.isValid)
            return blinkMask;
 
        bool isBlink =
            g.eyeOpenness < 0.2f &&
            g.eyeSqueeze < 0.6f;
 
        blinkMask = isBlink;
        return blinkMask;
#else
        return false;
#endif
    }
 
    private void GetPupilData(
        out float leftRaw,
        out float rightRaw,
        out float leftBlinkAware,
        out float rightBlinkAware,
        out int leftIsBlink,
        out int rightIsBlink)
    {
#if UNITY_ANDROID && !UNITY_EDITOR
        XrSingleEyePupilDataHTC[] pupils = null;
        XR_HTC_eye_tracker.Interop.GetEyePupilData(out pupils);
 
        leftRaw = rightRaw = -1f;
        leftBlinkAware = rightBlinkAware = -1f;
        leftIsBlink = rightIsBlink = 0;
 
        if (pupils != null && pupils.Length >= 2)
        {
            var L = pupils[(int)XrEyePositionHTC.XR_EYE_POSITION_LEFT_HTC];
            var R = pupils[(int)XrEyePositionHTC.XR_EYE_POSITION_RIGHT_HTC];
 
            if (L.isDiameterValid)
                leftRaw = L.pupilDiameter;
 
            if (R.isDiameterValid)
                rightRaw = R.pupilDiameter;
 
            bool leftBlink = IsBlinkFrame(
                XrEyePositionHTC.XR_EYE_POSITION_LEFT_HTC,
                ref leftBlinkMask,
                L
            );
 
            bool rightBlink = IsBlinkFrame(
                XrEyePositionHTC.XR_EYE_POSITION_RIGHT_HTC,
                ref rightBlinkMask,
                R
            );
 
            leftIsBlink = leftBlink ? 1 : 0;
            rightIsBlink = rightBlink ? 1 : 0;
 
            leftBlinkAware = (!leftBlink && L.isDiameterValid) ? L.pupilDiameter : -1f;
            rightBlinkAware = (!rightBlink && R.isDiameterValid) ? R.pupilDiameter : -1f;
        }
#else
        leftRaw = rightRaw = 3.0f;
        leftBlinkAware = rightBlinkAware = 3.0f;
        leftIsBlink = rightIsBlink = 0;
#endif
    }
 
    void OnDestroy()
    {
        StopRecording();
    }
}
