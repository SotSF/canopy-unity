using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class TextureTile : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler
{
    public static GameObject dragObject;
    public Texture tex;

    Vector3 dragStart;

    private void Start()
    {
        tex = GetComponentInChildren<RawImage>().texture;
    }

    public void OnBeginDrag(PointerEventData eventData)
    {
        dragStart = transform.position;
        dragObject = gameObject;
    }

    public void OnDrag(PointerEventData eventData)
    {
        transform.position = eventData.position;
    }

    public void OnEndDrag(PointerEventData eventData)
    {
        transform.position = dragStart;
        dragObject = null;
    }
}
