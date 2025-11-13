using UnityEngine;
using System.IO;
using Wave.Essence.Eye;

public class EyeTrackerRecorder : MonoBehaviour
{
    public bool simulateInEditor = true;
    public string folderPath = @"D:\Unity project\DataLogs";
    public string eyeDataFile = "EyeDataLog.csv";        // 连续眼动数据
    public string trialEventFile = "TrialEventLog.csv";   // 每张图片呈现时的事件记录


    private StreamWriter eyeWriter;
    private StreamWriter eventWriter;
    private float startTime;


    void Start()
    {
        Directory.CreateDirectory(folderPath);
        // ===== 创建两个 CSV 文件 =====
        string eyePath = Path.Combine(folderPath, eyeDataFile);
        string eventPath = Path.Combine(folderPath, trialEventFile);

        eyeWriter = new StreamWriter(eyePath, false);
        eventWriter = new StreamWriter(eventPath, false);

        // ===== 写入表头 =====
        eyeWriter.WriteLine("Time,LeftPupil,RightPupil,AvgPupil,GazeOriginX,GazeOriginY,GazeOriginZ,DirX,DirY,DirZ");
        eventWriter.WriteLine("Time,Trial,ImageIndex,ImageName,IsTarget,TargetPosition");

        startTime = Time.realtimeSinceStartup;

        Debug.Log("Eye tracking log file: " + eyePath);
        Debug.Log("Trial event log file: " + eventPath);

        if (EyeManager.Instance != null)
            EyeManager.Instance.EnableEyeTracking = true;
    }

    void Update()
    {
        float time = Time.realtimeSinceStartup - startTime;

        float leftPupil = -1f, rightPupil = -1f;
        Vector3 origin = Vector3.zero, dir = Vector3.forward;

        if (Application.isEditor && simulateInEditor)
        {
            // 模拟模式（无设备）
            leftPupil = 3.0f + Mathf.Sin(Time.time) * 0.1f;
            rightPupil = 3.0f + Mathf.Cos(Time.time) * 0.1f;
        }
        else if (EyeManager.Instance != null && EyeManager.Instance.IsEyeTrackingAvailable())
        {
            EyeManager.Instance.GetLeftEyePupilDiameter(out leftPupil);
            EyeManager.Instance.GetRightEyePupilDiameter(out rightPupil);
            EyeManager.Instance.GetCombinedEyeOrigin(out origin);
            EyeManager.Instance.GetCombindedEyeDirectionNormalized(out dir);
        }

        float avgPupil = (leftPupil > 0 && rightPupil > 0) ? (leftPupil + rightPupil) / 2f : -1f;
        eyeWriter.WriteLine($"{time:F3},{leftPupil:F3},{rightPupil:F3},{avgPupil:F3},{origin.x:F4},{origin.y:F4},{origin.z:F4},{dir.x:F4},{dir.y:F4},{dir.z:F4}");
    }


    public void LogTrialFrame(int trial, int imageIndex, string imageName, bool isTarget, int targetPosition)
    {
        float time = Time.realtimeSinceStartup - startTime;
        eventWriter.WriteLine($"{time:F3},{trial},{imageIndex},{imageName},{isTarget},{targetPosition}");
        eventWriter.Flush();
    }


    void OnApplicationQuit()
    {
        eyeWriter?.Close();
        eventWriter?.Close();
    }
}
