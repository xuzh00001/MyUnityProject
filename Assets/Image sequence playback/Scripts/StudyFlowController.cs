using UnityEngine;
using TMPro;

public class StudyFlowController : MonoBehaviour
{
    [System.Serializable]
    public class SessionConfig
    {
        public string sessionName;                 // A / B / C

        public GameObject pageReady;
        public GameObject pageProgress;
        public GameObject pageEnd;                 // Break

        public TextMeshProUGUI progressText;

        public int speedMs;
        public int targetRuns;
    }

    public ImageSequencePlayer player;
    public TextMeshProUGUI participantIdHeader;
    public GameObject pageWelcome;
    public GameObject pageFinalComplete;   // All sessions complete
    public GameObject pageThanks;

    public SessionConfig[] sessions;

    private int currentSessionIndex = -1;
    private int runsDone = 0;

    void Start()
    {
        player.OnSequenceFinished += HandleSequenceFinished;

        ShowOnly(pageWelcome);
        participantIdHeader.gameObject.SetActive(false);
    }

    void OnDestroy()
    {
        if (player != null)
            player.OnSequenceFinished -= HandleSequenceFinished;
    }


    // Welcome to Session 0 Ready
    public void OnClickBegin()
    {
        participantIdHeader.gameObject.SetActive(true);
        RefreshParticipantId();

        currentSessionIndex = 0;
        runsDone = 0;

        ShowOnly(sessions[0].pageReady);
    }

    // Ready to Start session
    public void OnClickStartCurrentSession()
    {
        RefreshParticipantId();
        runsDone = 0;

        var s = sessions[currentSessionIndex];
        player.StartSequenceWithSpeed(s.speedMs);
    }

    // Progress to Continue
    public void OnClickContinueProgress()
    {
        RefreshParticipantId();

        var s = sessions[currentSessionIndex];

        if (runsDone < s.targetRuns)
        {
            player.StartSequenceWithSpeed(s.speedMs);
        }
    }

    // End page to Next session
    public void OnClickContinueAfterEnd()
    {
        RefreshParticipantId();

        currentSessionIndex++;

        if (currentSessionIndex >= sessions.Length)
        {
            ShowOnly(pageWelcome);
            participantIdHeader.gameObject.SetActive(false);
            return;
        }

        runsDone = 0;
        ShowOnly(sessions[currentSessionIndex].pageReady);
    }

    // Player Callback

    private void HandleSequenceFinished()
    {
        var s = sessions[currentSessionIndex];
        runsDone++;

        UpdateProgressText(s);

        if (runsDone >= s.targetRuns)
        {
            // Session finished
            if (currentSessionIndex == sessions.Length - 1)
            {
                // Session（C）
                participantIdHeader.gameObject.SetActive(false);
                ShowOnly(pageFinalComplete);
            }
            else
            {
                ShowOnly(s.pageEnd);
            }
        }
        else
        {
            ShowOnly(s.pageProgress);
        }
    }

    public void OnClickFinish()
    {
        ShowOnly(pageThanks);
    }

    // Helpers

    private void RefreshParticipantId()
    {
        if (participantIdHeader == null || player == null || player.eyeRecorder == null) return;
        participantIdHeader.text = $"Participant: {player.eyeRecorder.ParticipantId}";
    }

    private void UpdateProgressText(SessionConfig s)
    {
        if (s.progressText != null)
            s.progressText.text = $"{runsDone} / {s.targetRuns}";
    }

    private void ShowOnly(GameObject target)
    {
        if (pageWelcome) pageWelcome.SetActive(target == pageWelcome);
        if (pageFinalComplete) pageFinalComplete.SetActive(target == pageFinalComplete);
        if (pageThanks) pageThanks.SetActive(target == pageThanks);

        foreach (var s in sessions)
        {
            if (s.pageReady)    s.pageReady.SetActive(target == s.pageReady);
            if (s.pageProgress) s.pageProgress.SetActive(target == s.pageProgress);
            if (s.pageEnd)      s.pageEnd.SetActive(target == s.pageEnd);
        }
    }

}
