using System.Collections;
using UnityEngine;

public class ImageSequencePlayer : MonoBehaviour
{
    public static float playStartTime = 0f;

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

    private Material screenMat;
    private Texture2D blackTex;
    private Texture2D grayTex;
    private Texture2D crosshairTex;

    private bool hasStarted = false;

    void Start()
    {
        screenMat = screenRenderer.material;

        blackTex = new Texture2D(1, 1);
        blackTex.SetPixel(0, 0, Color.black);
        blackTex.Apply();

        grayTex = customGrayTexture;
        crosshairTex = GenerateCrosshair(256, 256);

        // shuffle category order
        Shuffle(categoryBlocks);

        // shuffle images
        foreach (var block in categoryBlocks)
        {
            Shuffle(block.images);
        }

        SetTexture(blackTex);
    }

    public void StartSequence()
    {
        if (hasStarted) return;
        hasStarted = true;

        playStartTime = Time.realtimeSinceStartup;

        playCanvas?.SetActive(false);
        eyeRecorder.StartRecording();

        StartCoroutine(MainRoutine());
    }

    IEnumerator MainRoutine()
    {
        // Baseline
        SetBlock(ContinuousEyeRecorder.BlockType.Baseline);
        SetTexture(grayTex);
        ShowCrosshair(true);
        yield return new WaitForSecondsRealtime(10f);
        ShowCrosshair(false);

        // Trials
        for (int trial = 0; trial < categoryBlocks.Length; trial++)
        {
            yield return StartCoroutine(PlayTrial(trial + 1));

            if (trial < categoryBlocks.Length - 1)
                yield return StartCoroutine(ShowInterval(2f));
        }

        // End
        SetBlock(ContinuousEyeRecorder.BlockType.Baseline);
        SetTexture(blackTex);
        ShowCrosshair(true);
        yield return new WaitForSecondsRealtime(10f);
        ShowCrosshair(false);
        SetTexture(blackTex);

        yield return new WaitForSecondsRealtime(2f);

        eyeRecorder.StopRecording();
        hasStarted = false;
        playCanvas?.SetActive(true);
    }

    IEnumerator PlayTrial(int trialNumber)
    {
        CategoryBlock block = categoryBlocks[trialNumber - 1];

        ContinuousEyeRecorder.CurrentBlock = ContinuousEyeRecorder.BlockType.Stimulus;
        ContinuousEyeRecorder.CurrentTrial = trialNumber;
        ContinuousEyeRecorder.CurrentCategory = block.name;

        for (int i = 0; i < block.images.Length; i++)
        {
            SetTexture(block.images[i]);

            ContinuousEyeRecorder.CurrentIndex = i + 1;
            ContinuousEyeRecorder.CurrentImageName = block.images[i].name;

            yield return new WaitForSecondsRealtime(0.1f);  // 100 ms
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
