using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class ImageSequencePlayer : MonoBehaviour
{
    [Header("Images to Display")]
    public List<Texture2D> images;     // 图片序列
    public float displayTime = 2f;     // 每张图片显示时间

    private Renderer screenRenderer;
    private int currentIndex = 0;

    void Start()
    {
        screenRenderer = GetComponent<Renderer>();
        if (images.Count > 0)
            StartCoroutine(PlaySequence());
        else
            Debug.LogWarning("No images assigned!");
    }

    IEnumerator PlaySequence()
    {
        while (true)
        {
            screenRenderer.material.mainTexture = images[currentIndex];
            yield return new WaitForSeconds(displayTime);
            currentIndex = (currentIndex + 1) % images.Count;
        }
    }
}
