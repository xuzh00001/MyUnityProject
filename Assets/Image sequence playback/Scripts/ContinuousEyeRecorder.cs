using System.IO;
using UnityEngine;
using VIVE.OpenXR;
using VIVE.OpenXR.EyeTracker;
using System;
 
public class ContinuousEyeRecorder : MonoBehaviour
{
    private string participantId;
    public string ParticipantId => participantId;
    public int CurrentRunIndex => runIndex;
 
    private StreamWriter writer;
    private bool isRecording = false;
 
    private int speedMs = 100;
    private int runIndex = 0;
 
    public static int CurrentTrial = -1;
    public static string CurrentCategory = "NA";
    public static int CurrentIndex = -1;
    public static string CurrentImageName = "NA";
    public static BlockType CurrentBlock = BlockType.Baseline;
 
    // Blink mask (per eye)
    private bool leftBlinkMask  = false;
    private bool rightBlinkMask = false;
 
    void Awake()
    {
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
        if (writer != null) return;
 
        string root = Application.persistentDataPath;
        string folder = Path.Combine(root, participantId);
 
        if (!Directory.Exists(folder))
            Directory.CreateDirectory(folder);
 
        runIndex = GetNextRunIndex(folder);
 
        string fileName = $"{participantId}_{speedMs}ms_run-{runIndex:D2}.csv";
        string filePath = Path.Combine(folder, fileName);
 
        writer = new StreamWriter(filePath, false);
        writer.WriteLine(
            "Time,LeftPupil,RightPupil,Block,Trial,Category,Index,ImageID,Speed,ParticipantID"
        );
        writer.Flush();
 
        isRecording = true;
        Debug.Log($"Recording started: {fileName}");
    }
 
    public void StopRecording()
    {
        isRecording = false;
        writer?.Flush();
        writer?.Close();
        writer = null;
 
        Debug.Log("Recording stopped.");
    }
 
    void Update()
    {
        if (!isRecording || writer == null) return;
 
        float left, right;
        GetPupilData(out left, out right);
 
        float time = Time.realtimeSinceStartup - ImageSequencePlayer.playStartTime;
 
        string trialStr = CurrentTrial > 0 ? CurrentTrial.ToString() : "NA";
        string indexStr = CurrentIndex > 0 ? CurrentIndex.ToString() : "NA";
        string category = string.IsNullOrEmpty(CurrentCategory) ? "NA" : CurrentCategory;
        string image = string.IsNullOrEmpty(CurrentImageName) ? "NA" : CurrentImageName;
        string block = CurrentBlock.ToString();
 
        writer.WriteLine(
            $"{time:F4},{left},{right},{block},{trialStr},{category},{indexStr},{image},{speedMs},{participantId}"
        );
    }
 
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
    

    public int GetRunCountForSpeed(int speedMs)
    {
        string root = Application.persistentDataPath;
        string folder = Path.Combine(root, participantId);

        if (!Directory.Exists(folder))
            return 0;

        var files = Directory.GetFiles(folder, $"{participantId}_{speedMs}ms_run-*.csv");
        return files.Length;
    }


    // Blink detection
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
            !pupil.isDiameterValid
            || g.eyeOpenness < 0.15f
            || g.eyeSqueeze > 0.8f;
 
        blinkMask = isBlink;
        return blinkMask;
#else
        return false;
#endif
    }
 
    // Eye tracking data
    private void GetPupilData(out float left, out float right)
    {
#if UNITY_ANDROID && !UNITY_EDITOR
        XrSingleEyePupilDataHTC[] pupils = null;
        XR_HTC_eye_tracker.Interop.GetEyePupilData(out pupils);
 
        left = right = -1f;
 
        if (pupils != null && pupils.Length >= 2)
        {
            var L = pupils[(int)XrEyePositionHTC.XR_EYE_POSITION_LEFT_HTC];
            var R = pupils[(int)XrEyePositionHTC.XR_EYE_POSITION_RIGHT_HTC];
 
            bool leftBlink =
                IsBlinkFrame(
                    XrEyePositionHTC.XR_EYE_POSITION_LEFT_HTC,
                    ref leftBlinkMask,
                    L
                );
 
            bool rightBlink =
                IsBlinkFrame(
                    XrEyePositionHTC.XR_EYE_POSITION_RIGHT_HTC,
                    ref rightBlinkMask,
                    R
                );
 
            if (!leftBlink && L.isDiameterValid)
                left = L.pupilDiameter;
            else
                left = -1f;
 
            if (!rightBlink && R.isDiameterValid)
                right = R.pupilDiameter;
            else
                right = -1f;
        }
#else
        // Editor mock data
        left = 3.0f;
        right = 3.0f;
#endif
    }
 
    void OnDestroy()
    {
        writer?.Close();
    }
}