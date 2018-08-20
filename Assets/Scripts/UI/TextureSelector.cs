using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class TextureSelector : MonoBehaviour, IDropHandler {

    public Texture imageIcon;
    public Texture tex = null;
    public UnityEvent textureSelected;

    private Button clearButton;
    private RawImage imageDisplay;

    void Start () {
        imageDisplay = GetComponent<RawImage>();
        clearButton = GetComponentInChildren<Button>(true);
        clearButton.onClick.AddListener(Clear);
    }

    public void Clear()
    {
        tex = null;
        clearButton.gameObject.SetActive(false);
        imageDisplay.texture = imageIcon;
        textureSelected.Invoke();
    }

    public void OnDrop(PointerEventData eventData)
    {
        if (TextureTile.dragObject != null)
        {
            TextureTile texTile = TextureTile.dragObject.GetComponent<TextureTile>();
            tex = texTile.tex;
            imageDisplay.texture = tex;
            textureSelected.Invoke();
            clearButton.gameObject.SetActive(true);
        }
    }
}
