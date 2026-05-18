using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class SystemFlow : MonoBehaviour
{
    public enum ExperimentMode
    {
        None,
        User,
        Attacker
    }

    private enum FlowState
    {
        Welcome,
        RoleSelection,
        PracticeIdle,
        PracticePlaying,
        BeforeBegin,
        SessionReady,
        SessionPlaying,
        SessionProgress,
        SessionEnd,
        FinalComplete,
        Thanks
    }

    [System.Serializable]
    public class SessionConfig
    {
        [Header("Basic")]
        public string sessionName;                 // A / B / C / D
        public int speedMs = 100;

        [Tooltip("How many times this session is played in User Mode.")]
        [Min(1)]
        public int userRuns = 1;

        [Tooltip("How many times this session is played in Attacker Mode. If <= 0, userRuns is used.")]
        [Min(0)]
        public int attackerRuns = 1;

        [Header("User Mode Pages")]
        public GameObject pageReady;
        public GameObject pageProgress;
        public GameObject pageEnd;                 // Break / End page
        public TextMeshProUGUI progressText;

        [Header("Attacker Mode Pages - optional")]
        [Tooltip("Only assign this if Attacker Mode uses a different Panel.")]
        public GameObject attackerPageReady;

        [Tooltip("Only assign this if Attacker Mode uses a different Panel.")]
        public GameObject attackerPageProgress;

        [Tooltip("Only assign this if Attacker Mode uses a different Panel.")]
        public GameObject attackerPageEnd;

        public TextMeshProUGUI attackerProgressText;

        [Header("Attacker Mode Images - optional, for shared panels")]
        [Tooltip("If Attacker Mode reuses pageReady, assign the attacker background image here.")]
        public Sprite attackerReadySprite;

        [Tooltip("If Attacker Mode reuses pageProgress, assign the attacker background image here.")]
        public Sprite attackerProgressSprite;

        [Tooltip("If Attacker Mode reuses pageEnd, assign the attacker background image here.")]
        public Sprite attackerEndSprite;
    }

    [Header("References")]
    public RSVP player;
    public TextMeshProUGUI participantIdHeader;

    [Header("Global Pages")]
    public GameObject pageWelcome;
    public GameObject pageRoleSelection;
    public GameObject pagePractice;
    public GameObject pageBeforeBegin;

    [Header("Final Pages")]
    public GameObject pageFinalComplete;
    public GameObject pageThanks;

    [Header("Attacker Final Pages - optional")]
    public GameObject attackerPageFinalComplete;
    public GameObject attackerPageThanks;

    [Header("Attacker Final Images - optional, for shared panels")]
    public Sprite attackerFinalCompleteSprite;
    public Sprite attackerThanksSprite;

    [Header("Sessions")]
    public SessionConfig[] sessions;

    [Header("Balanced Latin Square - Speed Counterbalancing")]
    [Tooltip("If enabled, SessionConfig.speedMs is ignored for main sessions and speed is assigned by Balanced Latin Square.")]
    public bool useBalancedLatinSquareSpeeds = true;

    [Tooltip("The four speed conditions. Keep this as 50, 100, 150, 200 ms unless your experiment changes. The counterbalancing seed is always read from RSVP.fixedSequenceSeed.")]
    public int[] balancedSpeedMs = new int[] { 50, 100, 150, 200 };

    private int[] activeBalancedSpeedOrderMs;

    private int currentSessionIndex = -1;
    private int runsDone = 0;
    private ExperimentMode currentMode = ExperimentMode.None;
    private FlowState currentState = FlowState.Welcome;

    private readonly Dictionary<Image, Sprite> originalSprites = new Dictionary<Image, Sprite>();

    void Start()
    {
        CacheOriginalSprites();

        if (player != null)
            player.OnSequenceFinished += HandleSequenceFinished;

        currentMode = ExperimentMode.None;
        currentSessionIndex = -1;
        runsDone = 0;
        currentState = FlowState.Welcome;

        ShowOnly(pageWelcome);
    }

    void OnDestroy()
    {
        if (player != null)
            player.OnSequenceFinished -= HandleSequenceFinished;
    }

    // Welcome -> Role Selection
    public void OnClickBegin()
    {
        currentMode = ExperimentMode.None;
        currentSessionIndex = -1;
        runsDone = 0;

        currentState = FlowState.RoleSelection;
        ShowOnly(pageRoleSelection);
    }

    // Role Selection -> Practice page
    public void OnClickSelectUser()
    {
        currentMode = ExperimentMode.User;
        currentSessionIndex = -1;
        runsDone = 0;

        if (player != null && player.eyeRecorder != null)
            player.eyeRecorder.SetActiveMode(EyeRecorder.RecordingMode.User);

        currentState = FlowState.PracticeIdle;
        ShowOnly(pagePractice);
    }

    // Role Selection -> Session A Ready directly in Attacker Mode
    public void OnClickSelectAttacker()
    {
        currentMode = ExperimentMode.Attacker;
        currentSessionIndex = -1;
        runsDone = 0;

        if (player != null && player.eyeRecorder != null)
            player.eyeRecorder.SetActiveMode(EyeRecorder.RecordingMode.Attacker);

        StartMainSessions();
    }

    // Practice page: Back -> Role Selection
    public void OnClickPracticeBack()
    {
        currentMode = ExperimentMode.None;
        currentSessionIndex = -1;
        runsDone = 0;

        currentState = FlowState.RoleSelection;
        ShowOnly(pageRoleSelection);
    }

    // Practice page: Continue -> play practice RSVP, then go to Before You Begin
    public void OnClickPracticeContinue()
    {
        currentMode = ExperimentMode.User;

        if (player == null)
        {
            Debug.LogError("SystemFlow: RSVP is not assigned.");
            return;
        }

        if (player.eyeRecorder != null)
            player.eyeRecorder.SetActiveMode(EyeRecorder.RecordingMode.User);

        currentState = FlowState.PracticePlaying;
        SetHeaderVisible(false);
        player.StartPracticeSequence();
    }

    // Practice page: Skip -> Before You Begin
    public void OnClickPracticeSkip()
    {
        currentMode = ExperimentMode.User;

        if (player != null && player.eyeRecorder != null)
            player.eyeRecorder.SetActiveMode(EyeRecorder.RecordingMode.User);

        ShowBeforeBeginOrStartSessions();
    }

    // Before You Begin page: Next -> Session A
    public void OnClickBeforeBeginNext()
    {
        currentMode = ExperimentMode.User;

        if (player != null && player.eyeRecorder != null)
            player.eyeRecorder.SetActiveMode(EyeRecorder.RecordingMode.User);

        StartMainSessions();
    }

    // Before You Begin page: Back -> Practice page
    public void OnClickBeforeBeginBack()
    {
        currentMode = ExperimentMode.User;
        currentSessionIndex = -1;
        runsDone = 0;

        if (player != null && player.eyeRecorder != null)
            player.eyeRecorder.SetActiveMode(EyeRecorder.RecordingMode.User);

        currentState = FlowState.PracticeIdle;
        ShowOnly(pagePractice);
    }

    // Session A Ready page: Back
    // User Mode -> Before You Begin
    // Attacker Mode -> Role Selection
    public void OnClickSessionReadyBack()
    {
        if (currentState != FlowState.SessionReady)
        {
            Debug.LogWarning("SystemFlow: SessionReadyBack can only be used on a Session Ready page.");
            return;
        }

        if (currentSessionIndex != 0)
        {
            Debug.LogWarning("SystemFlow: OnClickSessionReadyBack is intended for Session A Ready only.");
            return;
        }

        runsDone = 0;

        if (currentMode == ExperimentMode.User)
        {
            ShowBeforeBeginOrStartSessions();
        }
        else if (currentMode == ExperimentMode.Attacker)
        {
            currentMode = ExperimentMode.None;
            currentSessionIndex = -1;

            currentState = FlowState.RoleSelection;
            ShowOnly(pageRoleSelection);
        }
        else
        {
            currentSessionIndex = -1;
            currentState = FlowState.RoleSelection;
            ShowOnly(pageRoleSelection);
        }
    }

    // Ready page button: start current session
    public void OnClickStartCurrentSession()
    {
        if (!IsSessionIndexValid()) return;

        RefreshSubjectIdHeader();
        runsDone = 0;

        var s = sessions[currentSessionIndex];
        currentState = FlowState.SessionPlaying;
        StartCurrentSessionSequence(s);
    }

    // Ready page button: skip current session and go to next session ready page
    // Attach this only to Session A/B/C ready pages, not the final session.
    public void OnClickSkipToNextSessionReady()
    {
        if (currentState != FlowState.SessionReady)
        {
            Debug.LogWarning("SystemFlow: SkipToNextSessionReady can only be used on a Session Ready page.");
            return;
        }

        if (!IsSessionIndexValid())
            return;

        if (currentSessionIndex >= sessions.Length - 1)
        {
            Debug.LogWarning("SystemFlow: Current session is the last session. There is no next session to skip to.");
            return;
        }

        currentSessionIndex++;
        runsDone = 0;

        RefreshSubjectIdHeader();
        SetHeaderVisible(true);

        currentState = FlowState.SessionReady;
        ShowOnly(GetReadyPage(sessions[currentSessionIndex]));
    }

    // Ready page button: go back to previous session ready page
    // Attach this only to Session B/C/D ready pages, not Session A.
    public void OnClickBackToPreviousSessionReady()
    {
        if (currentState != FlowState.SessionReady)
        {
            Debug.LogWarning("SystemFlow: BackToPreviousSessionReady can only be used on a Session Ready page.");
            return;
        }

        if (!IsSessionIndexValid())
            return;

        if (currentSessionIndex <= 0)
        {
            Debug.LogWarning("SystemFlow: Current session is the first session. There is no previous session to go back to.");
            return;
        }

        currentSessionIndex--;
        runsDone = 0;

        RefreshSubjectIdHeader();
        SetHeaderVisible(true);

        currentState = FlowState.SessionReady;
        ShowOnly(GetReadyPage(sessions[currentSessionIndex]));
    }

    // Progress page button: continue current session run
    public void OnClickContinueProgress()
    {
        if (!IsSessionIndexValid()) return;

        RefreshSubjectIdHeader();

        var s = sessions[currentSessionIndex];

        if (runsDone < GetCurrentModeRuns(s))
        {
            currentState = FlowState.SessionPlaying;
            StartCurrentSessionSequence(s);
        }
    }

    // End / Break page button: next session
    public void OnClickContinueAfterEnd()
    {
        RefreshSubjectIdHeader();

        currentSessionIndex++;

        if (currentSessionIndex >= sessions.Length)
        {
            SetHeaderVisible(true);
            currentState = FlowState.FinalComplete;
            ShowOnly(GetFinalCompletePage());
            return;
        }

        runsDone = 0;
        currentState = FlowState.SessionReady;
        ShowOnly(GetReadyPage(sessions[currentSessionIndex]));
    }

    // Final complete page button -> Thanks
    public void OnClickFinish()
    {
        RefreshSubjectIdHeader();
        SetHeaderVisible(true);

        currentState = FlowState.Thanks;
        ShowOnly(GetThanksPage());
    }

    private void ShowBeforeBeginOrStartSessions()
    {
        if (pageBeforeBegin != null)
        {
            currentState = FlowState.BeforeBegin;
            ShowOnly(pageBeforeBegin);
        }
        else
        {
            Debug.LogWarning("SystemFlow: Page Before Begin is not assigned. Starting Session A directly.");
            StartMainSessions();
        }
    }

    private void StartMainSessions()
    {
        if (sessions == null || sessions.Length == 0)
        {
            Debug.LogError("SystemFlow: No sessions configured.");
            return;
        }

        activeBalancedSpeedOrderMs = BuildBalancedSpeedOrder();

        currentSessionIndex = 0;
        runsDone = 0;

        RefreshSubjectIdHeader();
        SetHeaderVisible(true);

        currentState = FlowState.SessionReady;
        ShowOnly(GetReadyPage(sessions[0]));
    }

    private void StartCurrentSessionSequence(SessionConfig s)
    {
        if (player == null)
        {
            Debug.LogError("SystemFlow: RSVP is not assigned.");
            return;
        }

        if (player.eyeRecorder != null)
        {
            player.eyeRecorder.SetActiveMode(
                currentMode == ExperimentMode.Attacker
                    ? EyeRecorder.RecordingMode.Attacker
                    : EyeRecorder.RecordingMode.User
            );
        }

        int speedMs = GetSpeedForCurrentSession(s);
        Debug.Log($"SystemFlow: starting {s.sessionName} with {speedMs} ms. Counterbalance seed = {GetCounterbalanceSeed()}");
        player.StartSessionSequence(s.sessionName, speedMs);
    }

    private int GetSpeedForCurrentSession(SessionConfig s)
    {
        if (!useBalancedLatinSquareSpeeds)
            return s.speedMs;

        if (activeBalancedSpeedOrderMs == null || activeBalancedSpeedOrderMs.Length == 0)
            activeBalancedSpeedOrderMs = BuildBalancedSpeedOrder();

        if (activeBalancedSpeedOrderMs == null || activeBalancedSpeedOrderMs.Length == 0)
            return s.speedMs;

        int speedIndex = PositiveModulo(currentSessionIndex, activeBalancedSpeedOrderMs.Length);
        return activeBalancedSpeedOrderMs[speedIndex];
    }

    private int[] BuildBalancedSpeedOrder()
    {
        if (!useBalancedLatinSquareSpeeds)
            return null;

        if (balancedSpeedMs == null || balancedSpeedMs.Length == 0)
        {
            Debug.LogWarning("SystemFlow: balancedSpeedMs is empty. Falling back to SessionConfig.speedMs.");
            return null;
        }

        int n = balancedSpeedMs.Length;

        if (n % 2 != 0)
        {
            Debug.LogWarning("SystemFlow: Balanced Latin Square requires an even number of conditions. Falling back to SessionConfig.speedMs.");
            return null;
        }

        int[] firstRow = BuildBalancedLatinSquareFirstRow(n);
        int row = PositiveModulo(GetCounterbalanceSeed(), n);
        int[] order = new int[n];

        for (int i = 0; i < n; i++)
        {
            int conditionIndex = (firstRow[i] + row) % n;
            order[i] = balancedSpeedMs[conditionIndex];
        }

        Debug.Log($"SystemFlow: Balanced Latin Square row {row}: {string.Join(", ", order)} ms");
        return order;
    }

    private int[] BuildBalancedLatinSquareFirstRow(int n)
    {
        int[] row = new int[n];
        int left = 0;
        int right = n - 1;

        row[0] = left++;

        for (int i = 1; i < n; i++)
        {
            if (i % 2 == 1)
                row[i] = left++;
            else
                row[i] = right--;
        }

        return row;
    }

    private int GetCounterbalanceSeed()
    {
        if (player != null)
            return player.fixedSequenceSeed;

        Debug.LogWarning("SystemFlow: RSVP player is not assigned. Counterbalancing seed falls back to 0.");
        return 0;
    }

    private int PositiveModulo(int value, int modulo)
    {
        if (modulo <= 0) return 0;

        int result = value % modulo;
        return result < 0 ? result + modulo : result;
    }

    private void HandleSequenceFinished()
    {
        if (currentState == FlowState.PracticePlaying)
        {
            ShowBeforeBeginOrStartSessions();
            return;
        }

        if (currentState != FlowState.SessionPlaying || !IsSessionIndexValid())
            return;

        var s = sessions[currentSessionIndex];
        runsDone++;

        UpdateProgressText(s);

        if (runsDone >= GetCurrentModeRuns(s))
        {
            if (currentSessionIndex == sessions.Length - 1)
            {
                SetHeaderVisible(true);
                currentState = FlowState.FinalComplete;
                ShowOnly(GetFinalCompletePage());
            }
            else
            {
                currentState = FlowState.SessionEnd;
                ShowOnly(GetEndPage(s));
            }
        }
        else
        {
            currentState = FlowState.SessionProgress;
            ShowOnly(GetProgressPage(s));
        }
    }

    private int GetCurrentModeRuns(SessionConfig s)
    {
        if (currentMode == ExperimentMode.Attacker)
        {
            if (s.attackerRuns > 0)
                return Mathf.Max(1, s.attackerRuns);

            return Mathf.Max(1, s.userRuns);
        }

        return Mathf.Max(1, s.userRuns);
    }

    private GameObject GetReadyPage(SessionConfig s)
    {
        if (currentMode == ExperimentMode.Attacker && s.attackerPageReady != null)
            return s.attackerPageReady;

        return s.pageReady;
    }

    private GameObject GetProgressPage(SessionConfig s)
    {
        if (currentMode == ExperimentMode.Attacker && s.attackerPageProgress != null)
            return s.attackerPageProgress;

        return s.pageProgress;
    }

    private GameObject GetEndPage(SessionConfig s)
    {
        if (currentMode == ExperimentMode.Attacker && s.attackerPageEnd != null)
            return s.attackerPageEnd;

        return s.pageEnd;
    }

    private GameObject GetFinalCompletePage()
    {
        if (currentMode == ExperimentMode.Attacker && attackerPageFinalComplete != null)
            return attackerPageFinalComplete;

        return pageFinalComplete;
    }

    private GameObject GetThanksPage()
    {
        if (currentMode == ExperimentMode.Attacker && attackerPageThanks != null)
            return attackerPageThanks;

        return pageThanks;
    }

    private void RefreshSubjectIdHeader()
    {
        if (participantIdHeader == null || player == null || player.eyeRecorder == null)
            return;

        if (currentMode == ExperimentMode.Attacker)
            participantIdHeader.text = $"Attacker: {player.eyeRecorder.AttackerId}";
        else if (currentMode == ExperimentMode.User)
            participantIdHeader.text = $"Participant: {player.eyeRecorder.ParticipantId}";
        else
            participantIdHeader.text = "";
    }

    private void SetHeaderVisible(bool visible)
    {
        if (participantIdHeader != null)
            participantIdHeader.gameObject.SetActive(visible);
    }

    private void UpdateHeaderForTarget(GameObject target)
    {
        if (participantIdHeader == null)
            return;

        bool shouldShowHeader =
            target != pageWelcome &&
            target != pageRoleSelection &&
            target != pagePractice &&
            target != pageBeforeBegin &&
            currentMode != ExperimentMode.None;

        if (shouldShowHeader)
        {
            RefreshSubjectIdHeader();
            SetHeaderVisible(true);
        }
        else
        {
            SetHeaderVisible(false);
        }
    }

    private void UpdateProgressText(SessionConfig s)
    {
        int total = GetCurrentModeRuns(s);

        if (s.progressText != null)
            s.progressText.text = $"{runsDone} / {total}";

        if (s.attackerProgressText != null)
            s.attackerProgressText.text = $"{runsDone} / {total}";
    }

    private bool IsSessionIndexValid()
    {
        return sessions != null && currentSessionIndex >= 0 && currentSessionIndex < sessions.Length;
    }

    private void ShowOnly(GameObject target)
    {
        RestoreAllOriginalSprites();

        if (pageWelcome) pageWelcome.SetActive(target == pageWelcome);
        if (pageRoleSelection) pageRoleSelection.SetActive(target == pageRoleSelection);
        if (pagePractice) pagePractice.SetActive(target == pagePractice);
        if (pageBeforeBegin) pageBeforeBegin.SetActive(target == pageBeforeBegin);

        if (pageFinalComplete) pageFinalComplete.SetActive(target == pageFinalComplete);
        if (pageThanks) pageThanks.SetActive(target == pageThanks);

        if (attackerPageFinalComplete) attackerPageFinalComplete.SetActive(target == attackerPageFinalComplete);
        if (attackerPageThanks) attackerPageThanks.SetActive(target == attackerPageThanks);

        if (sessions != null)
        {
            foreach (var s in sessions)
            {
                if (s == null) continue;

                if (s.pageReady) s.pageReady.SetActive(target == s.pageReady);
                if (s.pageProgress) s.pageProgress.SetActive(target == s.pageProgress);
                if (s.pageEnd) s.pageEnd.SetActive(target == s.pageEnd);

                if (s.attackerPageReady) s.attackerPageReady.SetActive(target == s.attackerPageReady);
                if (s.attackerPageProgress) s.attackerPageProgress.SetActive(target == s.attackerPageProgress);
                if (s.attackerPageEnd) s.attackerPageEnd.SetActive(target == s.attackerPageEnd);
            }
        }

        ApplyAttackerSpriteIfNeeded(target);
        UpdateHeaderForTarget(target);
    }

    private void ApplyAttackerSpriteIfNeeded(GameObject target)
    {
        if (target == null || currentMode != ExperimentMode.Attacker)
            return;

        if (sessions != null)
        {
            foreach (var s in sessions)
            {
                if (s == null) continue;

                if (target == s.pageReady && s.attackerReadySprite != null)
                {
                    SetPageSprite(target, s.attackerReadySprite);
                    return;
                }

                if (target == s.pageProgress && s.attackerProgressSprite != null)
                {
                    SetPageSprite(target, s.attackerProgressSprite);
                    return;
                }

                if (target == s.pageEnd && s.attackerEndSprite != null)
                {
                    SetPageSprite(target, s.attackerEndSprite);
                    return;
                }
            }
        }

        if (target == pageFinalComplete && attackerFinalCompleteSprite != null)
        {
            SetPageSprite(target, attackerFinalCompleteSprite);
            return;
        }

        if (target == pageThanks && attackerThanksSprite != null)
        {
            SetPageSprite(target, attackerThanksSprite);
        }
    }

    private void SetPageSprite(GameObject page, Sprite sprite)
    {
        if (page == null || sprite == null)
            return;

        Image img = page.GetComponent<Image>();

        if (img == null)
            img = page.GetComponentInChildren<Image>(true);

        if (img == null)
        {
            Debug.LogWarning($"SystemFlow: No Image component found on page '{page.name}'. Cannot switch attacker sprite.");
            return;
        }

        if (!originalSprites.ContainsKey(img))
            originalSprites.Add(img, img.sprite);

        img.sprite = sprite;
    }

    private void CacheOriginalSprites()
    {
        CachePageSprite(pageWelcome);
        CachePageSprite(pageRoleSelection);
        CachePageSprite(pagePractice);
        CachePageSprite(pageBeforeBegin);
        CachePageSprite(pageFinalComplete);
        CachePageSprite(pageThanks);
        CachePageSprite(attackerPageFinalComplete);
        CachePageSprite(attackerPageThanks);

        if (sessions == null) return;

        foreach (var s in sessions)
        {
            if (s == null) continue;

            CachePageSprite(s.pageReady);
            CachePageSprite(s.pageProgress);
            CachePageSprite(s.pageEnd);

            CachePageSprite(s.attackerPageReady);
            CachePageSprite(s.attackerPageProgress);
            CachePageSprite(s.attackerPageEnd);
        }
    }

    private void CachePageSprite(GameObject page)
    {
        if (page == null) return;

        Image img = page.GetComponent<Image>();

        if (img == null)
            img = page.GetComponentInChildren<Image>(true);

        if (img != null && !originalSprites.ContainsKey(img))
            originalSprites.Add(img, img.sprite);
    }

    private void RestoreAllOriginalSprites()
    {
        foreach (var pair in originalSprites)
        {
            if (pair.Key != null)
                pair.Key.sprite = pair.Value;
        }
    }
}