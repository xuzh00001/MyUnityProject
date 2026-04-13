using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using TMPro;

public class RSVPSequencePlayer : MonoBehaviour
{
    public static float playStartTime = 0f;

    public System.Action OnSequenceFinished;

    public XRRigLock rigLock;
    public RoomLockToCamera roomLockToCamera;

    public Renderer screenRenderer;
    public RSVPEyeRecorder eyeRecorder;
    public GameObject playCanvas;
    public Texture2D customGrayTexture;
    public Texture2D crosshairTexture;

    [System.Serializable]
    public class CategoryBlock
    {
        public string name;
        public Texture2D targetImage;
        public Texture2D[] allImages;
    }

    public CategoryBlock[] categoryBlocks;

    private CategoryBlock[] originalBlocks;

    private Dictionary<int, CategoryBlock[]> cachedOrders
        = new Dictionary<int, CategoryBlock[]>();

    private Dictionary<int, Texture2D[][]> cachedSequences
        = new Dictionary<int, Texture2D[][]>();

    private Material screenMat;
    private Texture2D blackTex;
    private Texture2D grayTex;
    private int currentSpeedMs;
    private float imageInterval;

    private int selectedSpeedMs = -1;
    private bool hasStarted = false;

    const int defaultImagesPerTrial = 20;

    CategoryBlock[] CloneAndShuffleBlocks(CategoryBlock[] original)
    {
        CategoryBlock[] clone = new CategoryBlock[original.Length];

        for (int i = 0; i < original.Length; i++)
        {
            clone[i] = new CategoryBlock
            {
                name = original[i].name,
                targetImage = original[i].targetImage,
                allImages = original[i].allImages != null
                    ? (Texture2D[])original[i].allImages.Clone()
                    : new Texture2D[0]
            };

            Shuffle(clone[i].allImages);
        }

        Shuffle(clone);
        return clone;
    }

    void Start()
    {
        screenMat = screenRenderer.material;

        blackTex = new Texture2D(1, 1);
        blackTex.SetPixel(0, 0, Color.black);
        blackTex.Apply();

        grayTex = customGrayTexture;

        originalBlocks = categoryBlocks;

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

        if (eyeRecorder == null)
        {
            Debug.LogError("Eye recorder is not assigned!");
            return;
        }

        if (string.IsNullOrWhiteSpace(eyeRecorder.ParticipantId))
        {
            Debug.LogError("Participant ID is empty.");
            return;
        }

        StartSequenceWithSpeed(selectedSpeedMs);
    }

    public void StartSequenceWithSpeed(int speedMs)
    {
        if (hasStarted) return;

        currentSpeedMs = speedMs;
        imageInterval = speedMs / 1000f;

        if (!cachedOrders.ContainsKey(speedMs))
        {
            cachedOrders[speedMs] = CloneAndShuffleBlocks(originalBlocks);

            var blocks = cachedOrders[speedMs];
            Texture2D[][] sequences = new Texture2D[blocks.Length][];

            for (int i = 0; i < blocks.Length; i++)
            {
                sequences[i] = BuildSequence(blocks[i]);
            }

            cachedSequences[speedMs] = sequences;

            Debug.Log($"Created new order + sequences for {speedMs}ms");
        }

        categoryBlocks = cachedOrders[speedMs];

        eyeRecorder.SetSpeed(speedMs);

        playCanvas.SetActive(false);
        StartSequence();
    }

    public void StartSequence()
    {
        if (hasStarted) return;
        hasStarted = true;

        playStartTime = Time.realtimeSinceStartup;
        eyeRecorder.StartRecording();

        StartCoroutine(MainRoutine());
    }

    IEnumerator MainRoutine()
    {
        SetBlock(RSVPEyeRecorder.BlockType.Baseline);
        ShowCrosshair(true);

        yield return new WaitForSecondsRealtime(3f);
        rigLock.LockRig();
        roomLockToCamera.LockRoomToCamera();
        yield return new WaitForSecondsRealtime(2f);
        ShowCrosshair(false);

        for (int trial = 0; trial < categoryBlocks.Length; trial++)
        {
            yield return StartCoroutine(PlayTrial(trial + 1));

            if (trial < categoryBlocks.Length - 1)
                yield return StartCoroutine(ShowInterval(2f));
        }

        SetBlock(RSVPEyeRecorder.BlockType.Interval);
        SetTexture(grayTex);
        yield return new WaitForSecondsRealtime(3f);

        eyeRecorder.StopRecording();
        hasStarted = false;

        if (playCanvas != null)
            playCanvas.SetActive(true);

        rigLock.UnlockRig();
        roomLockToCamera.UnlockRoom();

        OnSequenceFinished?.Invoke();
    }

    IEnumerator PlayTrial(int trialNumber)
    {
        var block = categoryBlocks[trialNumber - 1];

        RSVPEyeRecorder.CurrentBlock = RSVPEyeRecorder.BlockType.Stimulus;
        RSVPEyeRecorder.CurrentTrial = trialNumber;
        RSVPEyeRecorder.CurrentCategory = block.name;
        RSVPEyeRecorder.CurrentTargetImageName = block.targetImage.name;

        Texture2D[] sequence = cachedSequences[currentSpeedMs][trialNumber - 1];

        int targetPos = FindTarget(sequence, block.targetImage);
        RSVPEyeRecorder.CurrentTargetPosition = targetPos + 1;

        for (int i = 0; i < sequence.Length; i++)
        {
            Texture2D currentImage = sequence[i];
            bool isTarget = currentImage == block.targetImage;

            SetTexture(currentImage);

            RSVPEyeRecorder.CurrentIndex = i + 1;
            RSVPEyeRecorder.CurrentImageName = currentImage.name;
            RSVPEyeRecorder.CurrentIsTarget = isTarget ? 1 : 0;

            yield return new WaitForSecondsRealtime(imageInterval);

            // add 100ms gray screen after each images
            if (i < sequence.Length - 1)
            {
                SetBlock(RSVPEyeRecorder.BlockType.Interval);
                SetTexture(grayTex);

                yield return new WaitForSecondsRealtime(0.1f);

                RSVPEyeRecorder.CurrentBlock = RSVPEyeRecorder.BlockType.Stimulus;
                RSVPEyeRecorder.CurrentTrial = trialNumber;
                RSVPEyeRecorder.CurrentCategory = block.name;
                RSVPEyeRecorder.CurrentTargetImageName = block.targetImage.name;
                RSVPEyeRecorder.CurrentTargetPosition = targetPos + 1;
            }
        }

        ClearStimulusState();
    }

    Texture2D[] BuildSequence(CategoryBlock block)
    {
        int baseLength = defaultImagesPerTrial;
        Texture2D[] baseSequence = new Texture2D[baseLength];

        int targetPosition = Random.Range(5, 15);

        List<Texture2D> pool = new List<Texture2D>();

        foreach (var img in block.allImages)
        {
            if (img != null && img != block.targetImage)
                pool.Add(img);
        }

        Shuffle(pool);

        int distractorIndex = 0;

        for (int i = 0; i < baseLength; i++)
        {
            if (i == targetPosition)
            {
                baseSequence[i] = block.targetImage;
            }
            else
            {
                if (distractorIndex < pool.Count)
                    baseSequence[i] = pool[distractorIndex++];
                else
                    baseSequence[i] = pool[Random.Range(0, pool.Count)];
            }
        }

        return baseSequence;
    }

    int FindTarget(Texture2D[] seq, Texture2D target)
    {
        for (int i = 0; i < seq.Length; i++)
        {
            if (seq[i] == target)
                return i;
        }
        return -1;
    }

    IEnumerator ShowInterval(float t)
    {
        SetBlock(RSVPEyeRecorder.BlockType.Interval);
        SetTexture(grayTex);
        yield return new WaitForSecondsRealtime(t);
    }

    void SetBlock(RSVPEyeRecorder.BlockType block)
    {
        RSVPEyeRecorder.CurrentBlock = block;
        ClearStimulusState();
    }

    void ClearStimulusState()
    {
        RSVPEyeRecorder.CurrentTrial = -1;
        RSVPEyeRecorder.CurrentCategory = "NA";
        RSVPEyeRecorder.CurrentIndex = -1;
        RSVPEyeRecorder.CurrentImageName = "NA";
        RSVPEyeRecorder.CurrentIsTarget = 0;
        RSVPEyeRecorder.CurrentTargetImageName = "NA";
        RSVPEyeRecorder.CurrentTargetPosition = -1;
    }

    void SetTexture(Texture tex)
    {
        screenMat.mainTexture = tex;
    }

    void ShowCrosshair(bool enable)
    {
        if (enable)
            screenMat.mainTexture = crosshairTexture;
    }

    void Shuffle<T>(IList<T> array)
    {
        for (int i = 0; i < array.Count; i++)
        {
            int j = Random.Range(i, array.Count);
            (array[i], array[j]) = (array[j], array[i]);
        }
    }
}