using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class ImageSequencePlayer : MonoBehaviour
{
    public static float playStartTime = 0f;
    
    public Renderer screenRenderer;
    public ImageEventRecorder eventRecorder;
    public ContinuousEyeRecorder eyeRecorder;

    public Texture2D customGrayTexture;

    // public Texture2D[] targetTextures;
    // public Texture2D[] nonTargetTextures;
    public Texture2D[] Category0;
    public Texture2D[] Category1;
    public Texture2D[] Category2;
    public Texture2D[] Category3;
    public Texture2D[] Category4;
    public Texture2D[] Category5;
    public Texture2D[] Category6;
    public Texture2D[] Category7;
    public Texture2D[] Category8;
    public Texture2D[] Category9;

    private Material screenMat;
    private Texture2D blackTex;
    private Texture2D grayTex;
    private Texture2D crosshairTex;

    // private List<Texture2D> targets;
    // private List<Texture2D> nonTargets;
    private List<List<Texture2D>> categories = new List<List<Texture2D>>();

    // private int nonTargetIndex = 0;

    private bool hasStarted = false;
    public GameObject playCanvas;

    void Start()
    {
        Time.timeScale = 1f;

        screenMat = screenRenderer.material;

        // black screen
        blackTex = new Texture2D(1, 1);
        blackTex.SetPixel(0, 0, Color.black);
        blackTex.Apply();

        grayTex = customGrayTexture;
        // generate crosshair texture
        crosshairTex = GenerateCrosshair(256, 256);

        // targets = new List<Texture2D>(targetTextures);
        // nonTargets = new List<Texture2D>(nonTargetTextures);
        // Shuffle(targets);
        // Shuffle(nonTargets);
        categories.Add(new List<Texture2D>(Category0));
        categories.Add(new List<Texture2D>(Category1));
        categories.Add(new List<Texture2D>(Category2));
        categories.Add(new List<Texture2D>(Category3));
        categories.Add(new List<Texture2D>(Category4));
        categories.Add(new List<Texture2D>(Category5));
        categories.Add(new List<Texture2D>(Category6));
        categories.Add(new List<Texture2D>(Category7));
        categories.Add(new List<Texture2D>(Category8));
        categories.Add(new List<Texture2D>(Category9));


        // StartCoroutine(MainRoutine());
        SetTexture(blackTex);
    }

    public void StartSequence()
    {
        if (hasStarted) return;
        hasStarted = true;

        Shuffle(categories);

        for (int c = 0; c < categories.Count; c++)
        {
            Shuffle(categories[c]);
        }

        // reset image and time
        // nonTargetIndex = 0;
        playStartTime = Time.realtimeSinceStartup;

        // hide Canvas
        if (playCanvas != null)
            playCanvas.SetActive(false);

        eyeRecorder?.StartRecording();
        eventRecorder?.StartRecording();

        StartCoroutine(MainRoutine());
    }

    public void ShowPlayButton()
    {
        if (playCanvas != null)
            playCanvas.SetActive(true);
    }

    IEnumerator MainRoutine()
    {
        // 10s fixation cross before all blocks
        SetTexture(grayTex);
        ShowCrosshair(true);
        yield return new WaitForSecondsRealtime(10f);
        ShowCrosshair(false);

        // Run blocks 1
        // yield return StartCoroutine(RunBlock());
        // Shuffle(targets);
        // 5s block interval
        // yield return ShowBlack(5f);
        // run block 2
        // yield return StartCoroutine(RunBlock());

        for (int c = 0; c < 10; c++)
        {
            yield return StartCoroutine(PlayCategory(c));

            if (c < 9)
                yield return ShowGray(2f);
        }

        // 10s fixation cross after all blocks
        SetTexture(blackTex);
        ShowCrosshair(true);
        yield return new WaitForSecondsRealtime(10f);
        ShowCrosshair(false);

        // 2s black screen in the end
        yield return ShowBlack(2f);

        // stop eye-tracking
        if (eyeRecorder != null)
            eyeRecorder.StopRecording();
        
        // stop image-recording
        if (eventRecorder != null)
            eventRecorder.StopRecording();

        // play again button
        hasStarted = false;
        ShowPlayButton();
    }


    IEnumerator PlayCategory(int categoryIndex)
    {
        List<Texture2D> imgs = categories[categoryIndex];

        for (int i = 0; i < imgs.Count; i++)
        {
            Texture2D tex = imgs[i];
            SetTexture(tex);

            eventRecorder?.RecordEvent(categoryIndex, i + 1, tex.name);

            yield return new WaitForSecondsRealtime(0.1f); // 100ms
        }
    }


    IEnumerator ShowGray(float t)
    {
        SetTexture(grayTex);
        yield return new WaitForSecondsRealtime(t);
    }

    IEnumerator ShowBlack(float t)
    {
        SetTexture(blackTex);
        yield return new WaitForSecondsRealtime(t);
    }

    void SetTexture(Texture tex)
    {
        screenMat.mainTexture = tex;
    }

    Texture2D GenerateCrosshair(int w, int h)
    {
        Texture2D tex = new Texture2D(w, h);
        Color bg = Color.black;
        Color fg = Color.white;

        int lineLength = w / 20;
        int thickness = w / 175;

        // fill bg
        for (int y = 0; y < h; y++)
        for (int x = 0; x < w; x++)
            tex.SetPixel(x, y, bg);

        int cx = w / 2;
        int cy = h / 2;

        // vertical line
        for (int y = cy - lineLength; y <= cy + lineLength; y++)
        for (int k = -thickness; k <= thickness; k++)
            tex.SetPixel(cx + k, y, fg);

        // horizontal line
        for (int x = cx - lineLength; x <= cx + lineLength; x++)
        for (int k = -thickness; k <= thickness; k++)
            tex.SetPixel(x, cy + k, fg);

        tex.Apply();
        return tex;
    }

    void ShowCrosshair(bool enable)
    {
        if (enable)
            screenMat.mainTexture = crosshairTex;
    }

    void Shuffle<T>(List<T> list)
    {
        for (int i = 0; i < list.Count; i++)
        {
            int j = Random.Range(i, list.Count);
            (list[i], list[j]) = (list[j], list[i]);
        }
    }
}
