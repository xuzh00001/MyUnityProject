using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class ImageSequencePlayer : MonoBehaviour
{
    public static float playStartTime = 0f;
    
    public Renderer screenRenderer;
    public ImageEventRecorder eventRecorder;
    public ContinuousEyeRecorder eyeRecorder;

    public Texture2D[] targetTextures;
    public Texture2D[] nonTargetTextures;

    private Material screenMat;
    private Texture2D blackTex;
    private Texture2D crosshairTex;

    private List<Texture2D> targets;
    private List<Texture2D> nonTargets;
    private int nonTargetIndex = 0;

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

        // generate crosshair texture
        crosshairTex = GenerateCrosshair(256, 256);

        targets = new List<Texture2D>(targetTextures);
        nonTargets = new List<Texture2D>(nonTargetTextures);

        Shuffle(targets);
        Shuffle(nonTargets);

        // StartCoroutine(MainRoutine());
        SetTexture(blackTex);
    }

    public void StartSequence()
    {
        if (hasStarted) return;
        hasStarted = true;

        // reset image and time
        nonTargetIndex = 0;
        playStartTime = Time.realtimeSinceStartup;

        // hide Canvas
        if (playCanvas != null)
            playCanvas.SetActive(false);

        // start eye-tracking
        if (eyeRecorder != null)
            eyeRecorder.StartRecording();

        // start image-recording
        if (eventRecorder != null)
            eventRecorder.StartRecording();

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
        SetTexture(blackTex);
        ShowCrosshair(true);
        yield return new WaitForSecondsRealtime(10f);
        ShowCrosshair(false);

        // Run blocks 1
        yield return StartCoroutine(RunBlock());
        Shuffle(targets);
        // 5s block interval
        yield return ShowBlack(5f);
        // run block 2
        yield return StartCoroutine(RunBlock());

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


    IEnumerator RunBlock()
    {
        // 2s black screen with crosshair
        SetTexture(blackTex);
        ShowCrosshair(true);
        yield return new WaitForSecondsRealtime(2f);
        ShowCrosshair(false);

        // 5 trials
        for (int t = 0; t < 5; t++)
        {
            Texture2D target = targets[t];

            List<Texture2D> trialImages = new List<Texture2D>();
            for (int i = 0; i < 9; i++)
                trialImages.Add(nonTargets[nonTargetIndex++]);

            int targetPos = Random.Range(0, 10);
            trialImages.Insert(targetPos, target);

            Debug.Log($"Trial {t+1}: Target = {target.name}, Position = {targetPos+1}");

            for (int i = 0; i < 10; i++)
            {
                Texture2D img = trialImages[i];
                SetTexture(img);

                bool isTarget = (i == targetPos);

                eventRecorder?.RecordEvent(
                    t + 1, i + 1, img.name, isTarget, targetPos + 1
                );

                yield return new WaitForSecondsRealtime(0.175f);
                // 0.1s after each image
                // yield return ShowBlack(0.1f);
            }
            
            // 2s black screen after trial (without crosshair)
            yield return ShowBlack(2f);
        }

        Debug.Log("Block finished.");
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
