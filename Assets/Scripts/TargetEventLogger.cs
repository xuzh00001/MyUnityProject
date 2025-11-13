using System.IO;
using UnityEngine;

public class TargetEventLogger : MonoBehaviour
{
    public string folderPath = @"D:\Unity project\DataLogs";
    private StreamWriter writer;
    private float startTime;

    void Start()
    {
        Directory.CreateDirectory(folderPath);
        string path = Path.Combine(folderPath, "TargetLog.csv");
        writer = new StreamWriter(path, false);
        writer.WriteLine("Time,Trial,TargetName,TargetPosition");
        startTime = Time.realtimeSinceStartup;
        Debug.Log("Target log file: " + path);
    }

    public void LogTargetEvent(int trial, string targetName, int targetPosition)
    {
        float time = Time.realtimeSinceStartup - startTime;
        writer.WriteLine($"{time:F3},{trial},{targetName},{targetPosition}");
        writer.Flush(); // 确保即使中途退出也不会丢数据
    }

    void OnApplicationQuit()
    {
        writer?.Close();
    }
}
