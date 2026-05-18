using System.IO;
using UnityEngine;
using VIVE.OpenXR;
using VIVE.OpenXR.EyeTracker;
using System;
using System.Threading;
using System.Collections.Concurrent;

public class EyeRecorder : MonoBehaviour
{
    public enum RecordingMode
    {
        User,
        Attacker
    }

    [SerializeField] private int targetFrameRate = 120;

    [Header("Fill manually in Inspector")]
    [SerializeField] private string participantId = "";
    [SerializeField] private string attackerId = "";

    public string ParticipantId => participantId;
    public string AttackerId => attackerId;
    public string ActiveSubjectId => activeMode == RecordingMode.Attacker ? attackerId : participantId;
    public RecordingMode ActiveMode => activeMode;
    public int CurrentRunIndex => runIndex;

    private RecordingMode activeMode = RecordingMode.User;
    private string currentSessionName = "NA";
    private bool currentIsPractice = false;

    private StreamWriter writer;
    private bool isRecording = false;

    private int speedMs = 100;
    private int runIndex = 0;

    private int frameCounter = 0;
    private float recordStartTime = 0f;

    private Thread samplerThread;
    private Thread writerThread;
    private readonly ConcurrentQueue<string> writeQueue = new ConcurrentQueue<string>();

    public static int CurrentTrial = -1;
    public static string CurrentCategory = "NA";
    public static int CurrentIndex = -1;
    public static string CurrentImageName = "NA";
    public static BlockType CurrentBlock = BlockType.Baseline;

    public static int CurrentIsTarget = 0;
    public static string CurrentTargetImageName = "NA";
    public static int CurrentTargetPosition = -1;

    private bool leftBlinkMask = false;
    private bool rightBlinkMask = false;

    public enum BlockType
    {
        Baseline,
        Stimulus,
        Interval
    }

    void Awake()
    {
        QualitySettings.vSyncCount = 0;
        Application.targetFrameRate = targetFrameRate;

        if (string.IsNullOrWhiteSpace(participantId))
            Debug.LogWarning("Participant ID is empty. Please fill it manually in the Inspector.");
        else
            Debug.Log($"Participant ID: {participantId}");

        if (string.IsNullOrWhiteSpace(attackerId))
            Debug.LogWarning("Attacker ID is empty. Please fill it manually in the Inspector if you use Attacker Mode.");
        else
            Debug.Log($"Attacker ID: {attackerId}");
    }

    public void SetActiveMode(RecordingMode mode)
    {
        activeMode = mode;
    }

    public void SetRunContext(string sessionName, bool isPractice)
    {
        currentSessionName = string.IsNullOrWhiteSpace(sessionName) ? "NA" : SanitizeFilePart(sessionName.Trim());
        currentIsPractice = isPractice;
    }

    public void SetSpeed(int ms)
    {
        speedMs = ms;
    }

    public void StartRecording()
    {
        if (isRecording) return;

        string subjectId = ActiveSubjectId;
        if (string.IsNullOrWhiteSpace(subjectId))
        {
            Debug.LogError($"Cannot start recording: {activeMode} ID is empty.");
            return;
        }

        subjectId = SanitizeFilePart(subjectId.Trim());
        string fileType = GetFileTypeLabel();

        string root = Application.persistentDataPath;
        string folder = Path.Combine(root, subjectId);

        if (!Directory.Exists(folder))
            Directory.CreateDirectory(folder);

        string filePrefix = $"{subjectId}_{fileType}_{speedMs}ms";
        runIndex = GetNextRunIndex(folder, filePrefix);

        string fileName = $"{filePrefix}_run-{runIndex:D2}.csv";
        string filePath = Path.Combine(folder, fileName);

        writer = new StreamWriter(filePath, false);
        writer.WriteLine(
            "Time," +
            "LeftPupilRaw,RightPupilRaw," +
            "LeftPupilBlink,RightPupilBlink," +
            "LeftIsBlink,RightIsBlink," +
            "Block,Trial,Category,Index,ImageID," +
            "IsTarget,TargetImageID,TargetPosition," +
            "Speed,ParticipantID"
        );
        writer.Flush();

        frameCounter = 0;
        recordStartTime = Time.realtimeSinceStartup;

        isRecording = true;

        samplerThread = new Thread(SamplerLoop);
        samplerThread.IsBackground = true;
        samplerThread.Start();

        writerThread = new Thread(WriterLoop);
        writerThread.IsBackground = true;
        writerThread.Start();

        Debug.Log($"Recording started: {filePath}");
    }

    public void StopRecording()
    {
        if (!isRecording && writer == null) return;

        isRecording = false;

        samplerThread?.Join();
        writerThread?.Join();

        writer?.Flush();
        writer?.Close();
        writer = null;

        while (writeQueue.TryDequeue(out _)) { }

        float duration = Time.realtimeSinceStartup - recordStartTime;
        float fps = duration > 0f ? frameCounter / duration : 0f;

        Debug.Log("Recording stopped.");
        Debug.Log($"Total samples: {frameCounter}");
        Debug.Log($"Duration: {duration:F2}s");
        Debug.Log($"Effective Rate: {fps:F2} Hz");
    }

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

            string trial = CurrentTrial > 0 ? CurrentTrial.ToString() : "NA";
            string index = CurrentIndex > 0 ? CurrentIndex.ToString() : "NA";
            string category = string.IsNullOrEmpty(CurrentCategory) ? "NA" : CurrentCategory;
            string image = string.IsNullOrEmpty(CurrentImageName) ? "NA" : CurrentImageName;
            string block = CurrentBlock.ToString();
            int isTarget = CurrentIsTarget;
            string targetImage = string.IsNullOrEmpty(CurrentTargetImageName) ? "NA" : CurrentTargetImageName;
            string targetPosition = CurrentTargetPosition > 0 ? CurrentTargetPosition.ToString() : "NA";
            string subjectId = ActiveSubjectId;

            string line =
                $"{time:F4}," +
                $"{leftRaw},{rightRaw}," +
                $"{leftBlinkAware},{rightBlinkAware}," +
                $"{leftIsBlink},{rightIsBlink}," +
                $"{block},{trial},{category},{index},{image}," +
                $"{isTarget},{targetImage},{targetPosition}," +
                $"{speedMs},{subjectId}";

            writeQueue.Enqueue(line);
            Interlocked.Increment(ref frameCounter);

            Thread.Sleep(4);
        }
    }

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

    void Update() { }

    private string GetFileTypeLabel()
    {
        if (currentIsPractice)
            return "Practice";

        if (activeMode == RecordingMode.Attacker)
            return "Attacker";

        return "User";
    }

    int GetNextRunIndex(string folder, string filePrefix)
    {
        int maxRun = 0;

        if (!Directory.Exists(folder))
            return 1;

        var files = Directory.GetFiles(folder, $"{filePrefix}_run-*.csv");

        foreach (var file in files)
        {
            string name = Path.GetFileNameWithoutExtension(file);
            int idx = name.LastIndexOf("run-");
            if (idx >= 0 && int.TryParse(name.Substring(idx + 4), out int run))
            {
                maxRun = Mathf.Max(maxRun, run);
            }
        }

        return maxRun + 1;
    }

    private string SanitizeFilePart(string value)
    {
        if (string.IsNullOrWhiteSpace(value)) return "NA";

        foreach (char c in Path.GetInvalidFileNameChars())
            value = value.Replace(c, '_');

        value = value.Replace(' ', '_');
        return value;
    }

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
