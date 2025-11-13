using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class ImageSequencePlayer1 : MonoBehaviour
{
    public Renderer screenRenderer; // 指向 Screen_new 的 MeshRenderer
    public EyeTrackerRecorder eyeRecorder;
    public TargetEventLogger targetLogger;


    private Material screenMat;
    private Texture2D blackTex;
    private List<Texture2D> targets;
    private List<Texture2D> nonTargets;
    private int nonTargetIndex = 0;

    void Start()
    {
        // 初始化材质与黑屏
        screenMat = screenRenderer.material;
        blackTex = new Texture2D(1, 1);
        blackTex.SetPixel(0, 0, Color.black);
        blackTex.Apply();

        // 从 Resources 里自动加载图片
        targets = new List<Texture2D>(Resources.LoadAll<Texture2D>("Stimuli/Targets"));
        nonTargets = new List<Texture2D>(Resources.LoadAll<Texture2D>("Stimuli/NonTargets"));

        // 打乱非目标和目标图片顺序
        Shuffle(nonTargets);
        Shuffle(targets);

        // 启动播放
        StartCoroutine(PlayBlock());
    }

    IEnumerator PlayBlock()
    {
        // 一个 block，5 个 trial
        for (int t = 0; t < 5; t++)
        {
            // 每个 trial 前的黑屏 2 秒
            yield return ShowBlack(2f);
            
            // 取第 t 张（因为已被随机化）
            Texture2D target = targets[t];

            // 取 9 张非目标图
            List<Texture2D> trialImages = new List<Texture2D>();
            for (int i = 0; i < 9; i++)
                trialImages.Add(nonTargets[nonTargetIndex++]);

            // 随机插入目标图
            int targetPos = Random.Range(0, 10);
            trialImages.Insert(targetPos, target);
            Debug.Log($"Trial {t + 1}: Target = {target.name}, Position = {targetPos + 1}/10");

            // 依次显示 10 张图片
            for (int i = 0; i < trialImages.Count; i++)
            {
                yield return ShowBlack(0.1f);
                ShowTexture(trialImages[i]);

                bool isTarget = (i == targetPos);

                // 记录眼动数据
                if (eyeRecorder != null)
                    eyeRecorder.LogTrialFrame(t + 1, i + 1, trialImages[i].name, isTarget, targetPos + 1);

                // 如果是 target，就记录时间点
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
