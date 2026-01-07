using System.IO;
using UnityEngine;
using VIVE.OpenXR;
using VIVE.OpenXR.EyeTracker;
using System;


public class ContinuousEyeRecorder : MonoBehaviour
{
    private string participantId;

    public string ParticipantId => participantId;

    private StreamWriter writer;
    private bool isRecording = false;

    private int speedMs = 100;
    private int runIndex = 0;

    public static int CurrentTrial = -1;
    public static string CurrentCategory = "NA";
    public static int CurrentIndex = -1;
    public static string CurrentImageName = "NA";
    public static BlockType CurrentBlock = BlockType.Baseline;
    
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

    // Eye tracking

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

            if (L.isDiameterValid) left = L.pupilDiameter;
            if (R.isDiameterValid) right = R.pupilDiameter;
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
