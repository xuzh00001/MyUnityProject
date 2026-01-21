using TMPro;
using UnityEngine;

public class SpeedButtonUI : MonoBehaviour
{
    public int speedMs;
    public TextMeshProUGUI countText;

    public ImageSequencePlayer player;
    public ContinuousEyeRecorder recorder;

    void OnEnable()
    {
        UpdateCount();
    }

    public void OnSelectSpeed()
    {
        player.SelectSpeed(speedMs);
    }

    public void UpdateCount()
    {
        int count = recorder.GetRunCountForSpeed(speedMs);
        countText.text = $"Tested: {count}";
    }
}
