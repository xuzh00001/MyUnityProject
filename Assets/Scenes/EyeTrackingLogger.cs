using UnityEngine;
using Wave.Essence.Eye;  // Wave 5.6 SDK 命名空间
using System.IO;

public class EyeTrackingLogger : MonoBehaviour
{
    private string logFilePath;

    void Start()
    {
        // 启用眼动追踪
        if (EyeManager.Instance != null)
        {
            EyeManager.Instance.EnableEyeTracking = true;
            Debug.Log("Eye tracking enabled.");
        }
        else
        {
            Debug.LogError("EyeManager not found in scene!");
        }

        // 创建日志文件
        logFilePath = Path.Combine(Application.persistentDataPath, "EyeTrackingLog.csv");
        File.WriteAllText(logFilePath, 
            "Time,LeftEyeDir.x,LeftEyeDir.y,LeftEyeDir.z,RightEyeDir.x,RightEyeDir.y,RightEyeDir.z,CombinedDir.x,CombinedDir.y,CombinedDir.z\n");
    }

    void Update()
    {
        if (EyeManager.Instance == null)
            return;

        // 如果眼动追踪不可用，跳过
        if (!EyeManager.Instance.IsEyeTrackingAvailable())
        {
            Debug.Log("Eye tracking not available.");
            return;
        }

        // 获取眼动追踪数据
        Vector3 leftDir = Vector3.zero;
        Vector3 rightDir = Vector3.zero;
        Vector3 combinedDir = Vector3.zero;

        EyeManager.Instance.GetLeftEyeDirectionNormalized(out leftDir);
        EyeManager.Instance.GetRightEyeDirectionNormalized(out rightDir);
        EyeManager.Instance.GetCombindedEyeDirectionNormalized(out combinedDir);

        // 输出到控制台（用于调试）
        Debug.Log($"Left: {leftDir}, Right: {rightDir}, Combined: {combinedDir}");

        // 写入 CSV
        string line = $"{Time.time:F2},{leftDir.x:F4},{leftDir.y:F4},{leftDir.z:F4},{rightDir.x:F4},{rightDir.y:F4},{rightDir.z:F4},{combinedDir.x:F4},{combinedDir.y:F4},{combinedDir.z:F4}\n";
        File.AppendAllText(logFilePath, line);
    }
}