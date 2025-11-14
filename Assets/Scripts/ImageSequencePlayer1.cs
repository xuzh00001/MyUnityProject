using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class ImageSequencePlayer1 : MonoBehaviour
{
    public Renderer screenRenderer; // point to MeshRenderer of Screen_new
    public EyeTrackerRecorder eyeRecorder;
    public TargetEventLogger targetLogger;


    private Material screenMat;
    private Texture2D blackTex;
    private List<Texture2D> targets;
    private List<Texture2D> nonTargets;
    private int nonTargetIndex = 0;

    void Start()
    {
        // initialize materials and black screen
        screenMat = screenRenderer.material;
        blackTex = new Texture2D(1, 1);
        blackTex.SetPixel(0, 0, Color.black);
        blackTex.Apply();

        // load images from Resources
        targets = new List<Texture2D>(Resources.LoadAll<Texture2D>("Stimuli/Targets"));
        nonTargets = new List<Texture2D>(Resources.LoadAll<Texture2D>("Stimuli/NonTargets"));

        // shuffle the order of non-target and target images
        Shuffle(nonTargets);
        Shuffle(targets);

        // start playing
        StartCoroutine(PlayBlock());
    }

    IEnumerator PlayBlock()
    {
        // 5 trials in one block
        for (int t = 0; t < 5; t++)
        {
            // 2s black screen before each trial
            yield return ShowBlack(2f);
            
            // take the t-th target image (already been randomized).
            Texture2D target = targets[t];

            // take 9 non-target images
            List<Texture2D> trialImages = new List<Texture2D>();
            for (int i = 0; i < 9; i++)
                trialImages.Add(nonTargets[nonTargetIndex++]);

            // randomly insert target image
            int targetPos = Random.Range(0, 10);
            trialImages.Insert(targetPos, target);
            Debug.Log($"Trial {t + 1}: Target = {target.name}, Position = {targetPos + 1}/10");

            // run 10 images
            for (int i = 0; i < trialImages.Count; i++)
            {
                yield return ShowBlack(0.1f);
                ShowTexture(trialImages[i]);

                bool isTarget = (i == targetPos);

                // record eye datas
                if (eyeRecorder != null)
                    eyeRecorder.LogTrialFrame(t + 1, i + 1, trialImages[i].name, isTarget, targetPos + 1);

                // if target, record the time
                if (isTarget && targetLogger != null)
                    targetLogger.LogTargetEvent(t + 1, trialImages[i].name, targetPos + 1);

                yield return new WaitForSecondsRealtime(0.1f);
            }
        }

        yield return ShowBlack(2f);
        Debug.Log("Block Finished!");
    }

    IEnumerator ShowBlack(float time)
    {
        ShowTexture(blackTex);
        yield return new WaitForSecondsRealtime(time);
    }

    void ShowTexture(Texture tex)
    {
        screenMat.mainTexture = tex;
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
