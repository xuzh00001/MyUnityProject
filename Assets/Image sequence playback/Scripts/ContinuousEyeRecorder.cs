using System.IO;
using UnityEngine;
using VIVE.OpenXR;
using VIVE.OpenXR.EyeTracker;

public class ContinuousEyeRecorder : MonoBehaviour
{
    private string logPath;
    private StreamWriter writer;

    private bool isRecording = false;

    public void StartRecording()
    {
        // if writer closed, create new file
        if (writer == null)
        {
            string folder = Application.persistentDataPath;
            string timestamp = System.DateTime.Now.ToString("yyyyMMdd_HHmmss");
            string fileName = $"PupilData_{timestamp}.csv";

            logPath = Path.Combine(folder, fileName);

            writer = new StreamWriter(logPath, false);
            writer.WriteLine("Time,LeftPupil,RightPupil");
            writer.Flush();

            Debug.Log("Pupil data file created: " + logPath);
        }
        
        isRecording = true;
        Debug.Log("Eye tracking recording started.");
    }

    public void StopRecording()
    {
        isRecording = false;

        if (writer != null)
        {
            writer.Flush();
            writer.Close();
            writer = null;
        }

        Debug.Log("Eye tracking recording stopped.");
    }

    void Update()
    {
        if (!isRecording || writer == null) return;
        
        float left, right;
        GetPupilData(out left, out right);

        float time = Time.realtimeSinceStartup - ImageSequencePlayer.playStartTime;
        string timeStr = time.ToString("F4");

        writer.WriteLine($"{time},{left},{right}");
        writer.Flush();
    }

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
        left = 3f;
        right = 3f;
#endif
    }

    void OnDestroy()
    {
        writer?.Close();
    }
}
