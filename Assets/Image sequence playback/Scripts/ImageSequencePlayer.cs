using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using TMPro;

public class ImageSequencePlayer : MonoBehaviour
{
    public static float playStartTime = 0f;

    public XRRigLock rigLock;
    public RoomLockToCamera roomLockToCamera;
    public TextMeshProUGUI participantIdText;

    public Renderer screenRenderer;
    public ContinuousEyeRecorder eyeRecorder;
    public GameObject playCanvas;
    public Texture2D customGrayTexture;

    [System.Serializable]
    public class CategoryBlock
    {
        public string name;
        public Texture2D[] images;
    }
    
    public CategoryBlock[] categoryBlocks;

    private Dictionary<int, CategoryBlock[]> cachedOrders
        = new Dictionary<int, CategoryBlock[]>();

    // runtime
    private Material screenMat;
    private Texture2D blackTex;
    private Texture2D grayTex;
    private Texture2D crosshairTex;

    private int currentSpeedMs;
    private float imageInterval;

    private int selectedSpeedMs = -1;
    private bool hasStarted = false;

    CategoryBlock[] CloneAndShuffleBlocks(CategoryBlock[] original)
    {
        // clone blocks
        CategoryBlock[] clone = new CategoryBlock[original.Length];
        for (int i = 0; i < original.Length; i++)
        {
            clone[i] = new CategoryBlock
            {
                name = original[i].name,
                images = (Texture2D[])original[i].images.Clone()
            };

            Shuffle(clone[i].images);
        }

        // shuffle block order
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
        crosshairTex = GenerateCrosshair(256, 256);

        if (participantIdText != null && eyeRecorder != null)
        {
            participantIdText.text = $"Participant: {eyeRecorder.ParticipantId}";
        }

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
        if (hasStarted) return;

        currentSpeedMs = speedMs;
        imageInterval = speedMs / 1000f;

        if (!cachedOrders.ContainsKey(speedMs))
        {
            cachedOrders[speedMs] = CloneAndShuffleBlocks(categoryBlocks);
            Debug.Log($"Created new order for {speedMs}ms");
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

        // rigLock.LockRig();
        // roomLockToCamera.LockRoomToCamera();
        participantIdText.gameObject.SetActive(false);

        StartCoroutine(MainRoutine());
    }

    IEnumerator MainRoutine()
    {
        // baseline 10s in total
        SetBlock(ContinuousEyeRecorder.BlockType.Baseline);
        ShowCrosshair(true);
        // Lock after 3s
        yield return new WaitForSecondsRealtime(3f);
        rigLock.LockRig();
        roomLockToCamera.LockRoomToCamera();
        yield return new WaitForSecondsRealtime(7f);
        ShowCrosshair(false);

        // trials
        for (int trial = 0; trial < categoryBlocks.Length; trial++)
        {
            yield return StartCoroutine(PlayTrial(trial + 1));
            if (trial < categoryBlocks.Length - 1)
                yield return StartCoroutine(ShowInterval(2f));
        }

        // end interval
        SetBlock(ContinuousEyeRecorder.BlockType.Interval);
        SetTexture(grayTex);
        yield return new WaitForSecondsRealtime(5f);

        SetTexture(blackTex); 
        yield return new WaitForSecondsRealtime(1f);

        eyeRecorder.StopRecording();
        hasStarted = false;
        playCanvas.SetActive(true);
        
        rigLock.UnlockRig();
        roomLockToCamera.UnlockRoom();
        participantIdText.gameObject.SetActive(true);
        foreach (var btn in FindObjectsOfType<SpeedButtonUI>())
        {
            btn.UpdateCount();
        }
    }

    IEnumerator PlayTrial(int trialNumber)
    {
        var block = categoryBlocks[trialNumber - 1];

        ContinuousEyeRecorder.CurrentBlock = ContinuousEyeRecorder.BlockType.Stimulus;
        ContinuousEyeRecorder.CurrentTrial = trialNumber;
        ContinuousEyeRecorder.CurrentCategory = block.name;

        for (int i = 0; i < block.images.Length; i++)
        {
            SetTexture(block.images[i]);
            ContinuousEyeRecorder.CurrentIndex = i + 1;
            ContinuousEyeRecorder.CurrentImageName = block.images[i].name;

            yield return new WaitForSecondsRealtime(imageInterval);
        }

        ClearStimulusState();
    }

    IEnumerator ShowInterval(float t)
    {
        SetBlock(ContinuousEyeRecorder.BlockType.Interval);
        SetTexture(grayTex);
        yield return new WaitForSecondsRealtime(t);
    }

    void SetBlock(ContinuousEyeRecorder.BlockType block)
    {
        ContinuousEyeRecorder.CurrentBlock = block;
        ClearStimulusState();
    }

    void ClearStimulusState()
    {
        ContinuousEyeRecorder.CurrentTrial = -1;
        ContinuousEyeRecorder.CurrentCategory = "NA";
        ContinuousEyeRecorder.CurrentIndex = -1;
        ContinuousEyeRecorder.CurrentImageName = "NA";
    }

    void SetTexture(Texture tex)
    {
        screenMat.mainTexture = tex;
    }

    void ShowCrosshair(bool enable)
    {
        if (enable)
            screenMat.mainTexture = crosshairTex;
    }

    void Shuffle<T>(T[] array)
    {
        for (int i = 0; i < array.Length; i++)
        {
            int j = Random.Range(i, array.Length);
            (array[i], array[j]) = (array[j], array[i]);
        }
    }

    Texture2D GenerateCrosshair(int w, int h)
    {
        Texture2D tex = new Texture2D(w, h);
        Color bg = Color.black;
        Color fg = Color.white;

        int lineLength = w / 20;
        int thickness = w / 175;

        for (int y = 0; y < h; y++)
        for (int x = 0; x < w; x++)
            tex.SetPixel(x, y, bg);

        int cx = w / 2;
        int cy = h / 2;

        for (int y = cy - lineLength; y <= cy + lineLength; y++)
        for (int k = -thickness; k <= thickness; k++)
            tex.SetPixel(cx + k, y, fg);

        for (int x = cx - lineLength; x <= cx + lineLength; x++)
        for (int k = -thickness; k <= thickness; k++)
            tex.SetPixel(x, cy + k, fg);

        tex.Apply();
        return tex;
    }
}
