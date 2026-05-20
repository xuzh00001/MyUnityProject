using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class RSVP : MonoBehaviour
{
    public static float playStartTime = 0f;

    public System.Action OnSequenceFinished;

    [Header("VR / Display")]
    public XRRigLock rigLock;
    public RoomLockToCamera roomLockToCamera;
    public Renderer screenRenderer;
    public EyeRecorder eyeRecorder;
    public GameObject playCanvas;
    public Texture2D customGrayTexture;
    public Texture2D crosshairTexture;

    [Header("Stimulus Image Display")]
    [Tooltip("Assign the Renderer of the child Quad / Plane used only for RSVP stimulus images.")]
    public Renderer stimulusImageRenderer;

    [Header("Main RSVP Sessions")]
    public CategoryBlock[] categoryBlocks;

    [Header("Practice RSVP")]
    [Tooltip("Put two practice categories here. Each category can contain practice images. No target image is used in practice.")]
    public PracticeCategoryBlock[] practiceCategoryBlocks;
    public int practiceSpeedMs = 50;
    public float practiceImageGapSeconds = 0.1f;

    [Header("Timing")]
    public float imageGapSeconds = 0.1f;
    public float baselineCrosshairSeconds = 3f;
    public float lockAfterCrosshairSeconds = 2f;
    public float categoryIntervalSeconds = 2f;
    public float endingGraySeconds = 3f;

    [Header("Deterministic Sequence")]
    [Tooltip("Same seed means same image order and same target positions, independent of User/Attacker ID and APK rebuild.")]
    public int fixedSequenceSeed = 20251010;

    [System.Serializable]
    public class CategoryBlock
    {
        public string name;
        public Texture2D targetImage;
        public Texture2D[] allImages;
    }

    [System.Serializable]
    public class PracticeCategoryBlock
    {
        public string name;
        public Texture2D[] allImages;
    }

    private enum SequenceKind
    {
        Session,
        Practice
    }

    private CategoryBlock[] originalBlocks;
    private PracticeCategoryBlock[] originalPracticeBlocks;

    private readonly Dictionary<string, CategoryBlock[]> cachedOrders = new Dictionary<string, CategoryBlock[]>();
    private readonly Dictionary<string, int[]> cachedTargetPositions = new Dictionary<string, int[]>();

    private CategoryBlock[] activeBlocks;
    private Material screenMat;
    private Material stimulusImageMat;
    private Texture2D blackTex;
    private Texture2D grayTex;

    private int currentSpeedMs;
    private float imageInterval;
    private float currentGapSeconds;
    private string currentSessionName = "NA";
    private string currentOrderKey = "NA";
    private SequenceKind currentSequenceKind = SequenceKind.Session;

    private int selectedSpeedMs = -1;
    private bool hasStarted = false;

    private const int defaultImagesPerTrial = 20;

    void Start()
    {
        if (screenRenderer == null)
        {
            Debug.LogError("RSVP: screenRenderer is not assigned.");
            return;
        }

        screenMat = screenRenderer.material;

        if (stimulusImageRenderer != null)
        {
            stimulusImageMat = stimulusImageRenderer.material;
            stimulusImageRenderer.gameObject.SetActive(false);
        }

        blackTex = new Texture2D(1, 1);
        blackTex.SetPixel(0, 0, Color.black);
        blackTex.Apply();

        grayTex = customGrayTexture != null ? customGrayTexture : blackTex;

        originalBlocks = categoryBlocks;
        originalPracticeBlocks = practiceCategoryBlocks;
        activeBlocks = originalBlocks;

        HideStimulusImage();
        SetTexture(blackTex);
    }

    public void SelectSpeed(int speedMs)
    {
        selectedSpeedMs = speedMs;
        Debug.Log($"Speed selected: {speedMs} ms");
    }

    public void StartTest()
    {
        if (hasStarted) return;

        if (selectedSpeedMs < 0)
        {
            Debug.LogWarning("No speed selected!");
            return;
        }

        StartSequenceWithSpeed(selectedSpeedMs);
    }

    public void StartSequenceWithSpeed(int speedMs)
    {
        StartSessionSequence($"Speed_{speedMs}", speedMs);
    }

    public void StartSessionSequence(string sessionName, int speedMs)
    {
        if (hasStarted) return;

        if (!CanStartWithCurrentId()) return;

        currentSequenceKind = SequenceKind.Session;
        currentSessionName = string.IsNullOrWhiteSpace(sessionName) ? "Session" : sessionName.Trim();
        currentSpeedMs = speedMs;
        imageInterval = speedMs / 1000f;
        currentGapSeconds = imageGapSeconds;

        // Important:
        // Image order depends only on fixedSequenceSeed + sessionName.
        // It does NOT depend on speed, participant ID, attacker ID, runIndex, or build.
        currentOrderKey = MakeOrderKey(currentSessionName);

        EnsureSessionOrderCached(currentOrderKey, currentSessionName);
        activeBlocks = cachedOrders[currentOrderKey];
        categoryBlocks = activeBlocks;

        eyeRecorder.SetRunContext(currentSessionName, false);
        eyeRecorder.SetSpeed(speedMs);

        if (playCanvas != null)
            playCanvas.SetActive(false);

        StartSequence();
    }

    public void StartPracticeSequence()
    {
        if (hasStarted) return;

        if (!CanStartWithCurrentId()) return;

        currentSequenceKind = SequenceKind.Practice;
        currentSessionName = "Practice";
        currentSpeedMs = practiceSpeedMs;
        imageInterval = practiceSpeedMs / 1000f;
        currentGapSeconds = practiceImageGapSeconds;

        currentOrderKey = MakeOrderKey("Practice");

        EnsurePracticeOrderCached(currentOrderKey);
        activeBlocks = cachedOrders[currentOrderKey];

        eyeRecorder.SetRunContext("Practice", true);
        eyeRecorder.SetSpeed(practiceSpeedMs);

        if (playCanvas != null)
            playCanvas.SetActive(false);

        StartSequence();
    }

    public void StartSequence()
    {
        if (hasStarted) return;
        hasStarted = true;

        playStartTime = Time.realtimeSinceStartup;

        HideStimulusImage();
        SetBlock(EyeRecorder.BlockType.Baseline);
        ShowCrosshair(true);

        if (eyeRecorder != null)
            eyeRecorder.StartRecording();

        StartCoroutine(MainRoutine());
    }

    private bool CanStartWithCurrentId()
    {
        if (eyeRecorder == null)
        {
            Debug.LogError("Eye recorder is not assigned!");
            return false;
        }

        if (string.IsNullOrWhiteSpace(eyeRecorder.ActiveSubjectId))
        {
            Debug.LogError("Current subject ID is empty. Fill Participant ID or Attacker ID in the Inspector.");
            return false;
        }

        return true;
    }

    IEnumerator MainRoutine()
    {
        yield return new WaitForSecondsRealtime(baselineCrosshairSeconds);

        if (rigLock != null)
            rigLock.LockRig();

        if (roomLockToCamera != null)
            roomLockToCamera.LockRoomToCamera();

        yield return new WaitForSecondsRealtime(lockAfterCrosshairSeconds);

        HideStimulusImage();
        ShowCrosshair(false);

        if (activeBlocks != null)
        {
            for (int trial = 0; trial < activeBlocks.Length; trial++)
            {
                if (currentSequenceKind == SequenceKind.Practice)
                    yield return StartCoroutine(PlayPracticeTrial(trial + 1));
                else
                    yield return StartCoroutine(PlaySessionTrial(trial + 1));

                if (trial < activeBlocks.Length - 1)
                    yield return StartCoroutine(ShowInterval(categoryIntervalSeconds));
            }
        }

        SetBlock(EyeRecorder.BlockType.Interval);
        HideStimulusImage();
        SetTexture(grayTex);
        yield return new WaitForSecondsRealtime(endingGraySeconds);

        if (eyeRecorder != null)
            eyeRecorder.StopRecording();

        hasStarted = false;

        HideStimulusImage();

        if (playCanvas != null)
            playCanvas.SetActive(true);

        if (rigLock != null)
            rigLock.UnlockRig();

        if (roomLockToCamera != null)
            roomLockToCamera.UnlockRoom();

        OnSequenceFinished?.Invoke();
    }

    IEnumerator PlaySessionTrial(int trialNumber)
    {
        var block = activeBlocks[trialNumber - 1];
        if (block == null) yield break;

        int[] targetPositions = cachedTargetPositions[currentOrderKey];
        int targetPos = targetPositions[trialNumber - 1];
        Texture2D[] sequence = BuildSessionSequence(block, targetPos);

        for (int i = 0; i < sequence.Length; i++)
        {
            Texture2D currentImage = sequence[i];
            bool isTarget = currentImage != null && block.targetImage != null && currentImage == block.targetImage;

            EyeRecorder.CurrentBlock = EyeRecorder.BlockType.Stimulus;
            EyeRecorder.CurrentTrial = trialNumber;
            EyeRecorder.CurrentCategory = SafeName(block.name);
            EyeRecorder.CurrentTargetImageName = block.targetImage != null ? block.targetImage.name : "NA";
            EyeRecorder.CurrentTargetPosition = targetPos + 1;
            EyeRecorder.CurrentIndex = i + 1;
            EyeRecorder.CurrentImageName = currentImage != null ? currentImage.name : "NA";
            EyeRecorder.CurrentIsTarget = isTarget ? 1 : 0;

            ShowStimulusImage(currentImage != null ? currentImage : grayTex);
            yield return new WaitForSecondsRealtime(imageInterval);

            if (i < sequence.Length - 1)
            {
                SetBlock(EyeRecorder.BlockType.Interval);
                HideStimulusImage();
                SetTexture(grayTex);
                yield return new WaitForSecondsRealtime(currentGapSeconds);
            }
        }

        HideStimulusImage();
        ClearStimulusState();
    }

    IEnumerator PlayPracticeTrial(int trialNumber)
    {
        var block = activeBlocks[trialNumber - 1];
        if (block == null) yield break;

        Texture2D[] sequence = BuildPracticeSequence(block);

        for (int i = 0; i < sequence.Length; i++)
        {
            Texture2D currentImage = sequence[i];

            EyeRecorder.CurrentBlock = EyeRecorder.BlockType.Stimulus;
            EyeRecorder.CurrentTrial = trialNumber;
            EyeRecorder.CurrentCategory = SafeName(block.name);
            EyeRecorder.CurrentTargetImageName = "NA";
            EyeRecorder.CurrentTargetPosition = -1;
            EyeRecorder.CurrentIndex = i + 1;
            EyeRecorder.CurrentImageName = currentImage != null ? currentImage.name : "NA";
            EyeRecorder.CurrentIsTarget = 0;

            ShowStimulusImage(currentImage != null ? currentImage : grayTex);
            yield return new WaitForSecondsRealtime(imageInterval);

            if (i < sequence.Length - 1)
            {
                SetBlock(EyeRecorder.BlockType.Interval);
                HideStimulusImage();
                SetTexture(grayTex);
                yield return new WaitForSecondsRealtime(currentGapSeconds);
            }
        }

        HideStimulusImage();
        ClearStimulusState();
    }

    private void EnsureSessionOrderCached(string key, string sessionName)
    {
        if (cachedOrders.ContainsKey(key) && cachedTargetPositions.ContainsKey(key))
            return;

        DeterministicRandom rng = CreateDeterministicRandom(key);

        CategoryBlock[] blocks = CloneAndShuffleBlocks(originalBlocks, rng);
        cachedOrders[key] = blocks;

        int[] targetPositions = new int[blocks.Length];

        for (int i = 0; i < blocks.Length; i++)
            targetPositions[i] = GetTargetPosition(rng);

        cachedTargetPositions[key] = targetPositions;

        Debug.Log($"Created deterministic RSVP image order for {sessionName}. Seed = {fixedSequenceSeed}, key = {key}");
        Debug.Log($"Order: {DescribeBlockOrder(blocks)}");
    }

    private void EnsurePracticeOrderCached(string key)
    {
        if (cachedOrders.ContainsKey(key))
            return;

        DeterministicRandom rng = CreateDeterministicRandom(key);
        cachedOrders[key] = CloneAndShufflePracticeBlocks(originalPracticeBlocks, rng);

        Debug.Log($"Created deterministic practice order. Seed = {fixedSequenceSeed}, key = {key}");
        Debug.Log($"Practice order: {DescribeBlockOrder(cachedOrders[key])}");
    }

    private CategoryBlock[] CloneAndShuffleBlocks(CategoryBlock[] original, DeterministicRandom rng)
    {
        if (original == null) return new CategoryBlock[0];

        CategoryBlock[] clone = new CategoryBlock[original.Length];

        for (int i = 0; i < original.Length; i++)
        {
            CategoryBlock src = original[i];

            clone[i] = new CategoryBlock
            {
                name = src != null ? src.name : "NA",
                targetImage = src != null ? src.targetImage : null,
                allImages = src != null && src.allImages != null
                    ? (Texture2D[])src.allImages.Clone()
                    : new Texture2D[0]
            };

            Shuffle(clone[i].allImages, rng);
        }

        Shuffle(clone, rng);
        return clone;
    }

    private CategoryBlock[] CloneAndShufflePracticeBlocks(PracticeCategoryBlock[] original, DeterministicRandom rng)
    {
        if (original == null) return new CategoryBlock[0];

        CategoryBlock[] clone = new CategoryBlock[original.Length];

        for (int i = 0; i < original.Length; i++)
        {
            PracticeCategoryBlock src = original[i];

            clone[i] = new CategoryBlock
            {
                name = src != null ? src.name : "Practice",
                targetImage = null,
                allImages = src != null && src.allImages != null
                    ? (Texture2D[])src.allImages.Clone()
                    : new Texture2D[0]
            };

            Shuffle(clone[i].allImages, rng);
        }

        Shuffle(clone, rng);
        return clone;
    }

    private Texture2D[] BuildSessionSequence(CategoryBlock block, int targetPosition)
    {
        int baseLength = defaultImagesPerTrial;
        Texture2D[] sequence = new Texture2D[baseLength];
        List<Texture2D> pool = new List<Texture2D>();

        if (block.allImages != null)
        {
            foreach (var img in block.allImages)
            {
                if (img != null && img != block.targetImage)
                    pool.Add(img);
            }
        }

        int idx = 0;

        for (int i = 0; i < baseLength; i++)
        {
            if (i == targetPosition && block.targetImage != null)
            {
                sequence[i] = block.targetImage;
            }
            else if (pool.Count > 0)
            {
                sequence[i] = pool[idx % pool.Count];
                idx++;
            }
            else
            {
                sequence[i] = block.targetImage != null ? block.targetImage : grayTex;
            }
        }

        return sequence;
    }

    private Texture2D[] BuildPracticeSequence(CategoryBlock block)
    {
        if (block.allImages == null || block.allImages.Length == 0)
            return new Texture2D[0];

        List<Texture2D> images = new List<Texture2D>();

        foreach (var img in block.allImages)
        {
            if (img != null)
                images.Add(img);
        }

        return images.ToArray();
    }

    private int GetTargetPosition(DeterministicRandom rng)
    {
        int baseLength = defaultImagesPerTrial;

        int minIndex;
        int maxIndex;

        if (currentSpeedMs == 50 || currentSpeedMs == 100)
        {
            float stimulusDuration = currentSpeedMs / 1000f + imageGapSeconds;
            int forbiddenCount = Mathf.CeilToInt(1.0f / stimulusDuration);
            minIndex = forbiddenCount;
            maxIndex = baseLength - forbiddenCount - 1;
        }
        else if (currentSpeedMs == 150)
        {
            minIndex = 4;
            maxIndex = baseLength - 8 - 1;
        }
        else if (currentSpeedMs == 200)
        {
            minIndex = 4;
            maxIndex = baseLength - 7 - 1;
        }
        else
        {
            minIndex = 4;
            maxIndex = baseLength - 4 - 1;
        }

        if (minIndex >= maxIndex)
        {
            Debug.LogWarning("Invalid target range -> fallback to full sequence.");
            minIndex = 0;
            maxIndex = baseLength - 1;
        }

        return rng.Next(minIndex, maxIndex + 1);
    }

    IEnumerator ShowInterval(float t)
    {
        SetBlock(EyeRecorder.BlockType.Interval);
        HideStimulusImage();
        SetTexture(grayTex);
        yield return new WaitForSecondsRealtime(t);
    }

    private void SetBlock(EyeRecorder.BlockType block)
    {
        EyeRecorder.CurrentBlock = block;
        ClearStimulusState();
    }

    private void ClearStimulusState()
    {
        EyeRecorder.CurrentTrial = -1;
        EyeRecorder.CurrentCategory = "NA";
        EyeRecorder.CurrentIndex = -1;
        EyeRecorder.CurrentImageName = "NA";
        EyeRecorder.CurrentIsTarget = 0;
        EyeRecorder.CurrentTargetImageName = "NA";
        EyeRecorder.CurrentTargetPosition = -1;
    }

    private void SetTexture(Texture tex)
    {
        if (screenMat != null)
            screenMat.mainTexture = tex;
    }

    private void ShowStimulusImage(Texture tex)
    {
        SetTexture(grayTex);

        if (stimulusImageRenderer == null || stimulusImageMat == null)
        {
            SetTexture(tex);
            return;
        }

        stimulusImageMat.mainTexture = tex;
        stimulusImageRenderer.gameObject.SetActive(true);
    }

    private void HideStimulusImage()
    {
        if (stimulusImageRenderer != null)
            stimulusImageRenderer.gameObject.SetActive(false);
    }

    private void ShowCrosshair(bool enable)
    {
        HideStimulusImage();

        if (enable)
            SetTexture(crosshairTexture != null ? crosshairTexture : grayTex);
        else
            SetTexture(grayTex);
    }

    private string MakeOrderKey(string sessionName)
    {
        string safeSession = string.IsNullOrWhiteSpace(sessionName)
            ? "SESSION"
            : sessionName.Trim().ToUpperInvariant();

        return $"IMAGE_ORDER_{safeSession}";
    }

    private DeterministicRandom CreateDeterministicRandom(string key)
    {
        int keyHash = StableHash(key);
        int seed = fixedSequenceSeed ^ keyHash;
        return new DeterministicRandom(seed);
    }

    private int StableHash(string text)
    {
        unchecked
        {
            int hash = 23;

            if (!string.IsNullOrEmpty(text))
            {
                for (int i = 0; i < text.Length; i++)
                    hash = hash * 31 + text[i];
            }

            return hash;
        }
    }

    private string SafeName(string value)
    {
        return string.IsNullOrWhiteSpace(value) ? "NA" : value;
    }

    private void Shuffle<T>(IList<T> array, DeterministicRandom rng)
    {
        if (array == null || rng == null) return;

        for (int i = 0; i < array.Count; i++)
        {
            int j = rng.Next(i, array.Count);
            T temp = array[i];
            array[i] = array[j];
            array[j] = temp;
        }
    }

    private string DescribeBlockOrder(CategoryBlock[] blocks)
    {
        if (blocks == null || blocks.Length == 0)
            return "Empty";

        List<string> names = new List<string>();

        foreach (var block in blocks)
        {
            if (block == null)
                names.Add("NULL");
            else
                names.Add(SafeName(block.name));
        }

        return string.Join(" -> ", names);
    }

    private class DeterministicRandom
    {
        private uint state;

        public DeterministicRandom(int seed)
        {
            state = seed == 0 ? 2463534242u : unchecked((uint)seed);
        }

        private uint NextUInt()
        {
            uint x = state;
            x ^= x << 13;
            x ^= x >> 17;
            x ^= x << 5;
            state = x;
            return x;
        }

        public int Next(int minInclusive, int maxExclusive)
        {
            if (maxExclusive <= minInclusive)
                return minInclusive;

            uint range = (uint)(maxExclusive - minInclusive);
            return minInclusive + (int)(NextUInt() % range);
        }
    }
}