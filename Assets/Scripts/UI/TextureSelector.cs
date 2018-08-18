using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class TextureSelector : MonoBehaviour, IDropHandler {

    public Texture tex = null;
    RawImage imageDisplay;

    void Start () {
        imageDisplay = GetComponent<RawImage>();
	}

    public void OnDrop(PointerEventData eventData)
    {
        if (TextureTile.dragObject != null)
        {
            TextureTile texTile = TextureTile.dragObject.GetComponent<TextureTile>();
            tex = texTile.tex;
            imageDisplay.texture = tex;
        }
    }
}
