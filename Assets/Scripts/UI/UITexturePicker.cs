using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;

public class UITexturePicker : MonoBehaviour, IDropHandler {

    Texture pickedTexture;

    // Use this for initialization
    void Start () {
	}
	
	// Update is called once per frame
	void Update () {
		
	}

    public void OnDrop(PointerEventData eventData)
    {
        if (TextureTile.dragObject != null)
        {
            TextureTile texTile = TextureTile.dragObject.GetComponent<TextureTile>();
            pickedTexture = texTile.tex;
            Debug.Log(pickedTexture);
        }
    }
}
