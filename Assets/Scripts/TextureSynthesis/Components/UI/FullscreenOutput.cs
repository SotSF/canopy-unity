using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class FullscreenOutput : MonoBehaviour
{
    public static FullscreenOutput instance;
    public static bool isAttached;

    public Canvas canvas;
    public RawImage image;
    public Texture imageSource;
    private Vector2 outputSize;

    private void Awake()
    {
        instance = this;
        canvas = GetComponentInChildren<Canvas>();
        image = GetComponentInChildren<RawImage>();
    }

    public void ClearTexture()
    {
        imageSource = null;
        image.texture = null;
        isAttached = false;
    }

    public void AttachTexture(Texture input, int displayTarget=1) {
        imageSource = input;
        image.texture = input;
        MultidisplayManager.instance.ActivateDisplay(displayTarget);
        canvas.targetDisplay = displayTarget;
        isAttached = true;
    }

    public void SetOutputSize(Vector2 size)
    {
        image.rectTransform.sizeDelta = size;
        image.color = Color.white;
    }

    public void SetOutputSize(int width, int height)
    {
        SetOutputSize(new Vector2(width, height));
    }
}