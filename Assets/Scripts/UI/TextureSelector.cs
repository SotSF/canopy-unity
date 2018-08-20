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
    private RawImage _imageDisplay;
    private RawImage imageDisplay {
        get {
            if (_imageDisplay == null)
            {
                _imageDisplay = GetComponent<RawImage>();
            }
            return _imageDisplay;
        }
    }

    void Start () {
        _imageDisplay = GetComponent<RawImage>();
        clearButton = GetComponentInChildren<Button>(true);
        clearButton.onClick.AddListener(Clear);
    }

    public void Clear()
    {
        SelectTexture(null);
        clearButton.gameObject.SetActive(false);
    }

    public void SelectTexture(Texture t)
    {
        tex = t;
        if (t != null)
        {
            imageDisplay.texture = t;
        }
        else
        {
            imageDisplay.texture = imageIcon;
        }
        textureSelected.Invoke();
    }

    public void OnDrop(PointerEventData eventData)
    {
        if (TextureTile.dragObject != null)
        {
            TextureTile texTile = TextureTile.dragObject.GetComponent<TextureTile>();
            SelectTexture(texTile.tex);
            clearButton.gameObject.SetActive(true);
        }
    }
}
