using System.IO;
using UnityEngine;

public class ImageEventRecorder : MonoBehaviour
{
    private string logPath;
    private StreamWriter writer;

    public void StartRecording()
    {
        string folder = Application.persistentDataPath;
        string timestamp = System.DateTime.Now.ToString("yyyyMMdd_HHmmss");
        string fileName = $"ImageData_{timestamp}.csv";
        
        logPath = Path.Combine(folder, fileName);

        writer = new StreamWriter(logPath, false);
        writer.WriteLine("Time,Category,Index,ImageName");
        writer.Flush();

        Debug.Log("ImageData.csv Path: " + logPath);
    }

    public void StopRecording()
    {
        if (writer != null)
        {
            writer.Flush();
            writer.Close();
            writer = null;
        }
        Debug.Log("Image event recording stopped.");
    }

    public void RecordEvent(int category, int index, string imageName)
    {
        if (writer == null) return;
        
        float t = Time.realtimeSinceStartup - ImageSequencePlayer.playStartTime;
        string time = t.ToString("F4");

        writer.WriteLine($"{time},{category},{index},{imageName}");
        writer.Flush();
    }

    void OnDestroy()
    {
        writer?.Close();
    }
}
