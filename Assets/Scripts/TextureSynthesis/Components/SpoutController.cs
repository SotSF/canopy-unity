using Klak.Spout;

using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class SpoutController : MonoBehaviour
{
    private SpoutSender _sender;
    private SpoutSender sender
    {
        get
        {
            if (_sender == null)
            {
                _sender = gameObject.AddComponent<SpoutSender>();
            }
            return _sender;
        }
    }

    public void SendAlpha(bool send)
    {
        sender.alphaSupport = send;
    }

    public void SetName(string newName)
    {
        gameObject.name = newName;
    }

    public void RefreshSender()
    {
        if (sender != null)
        {
            sender.sourceTexture = null;
            Destroy(sender);
        }
        _sender = null;
    }

    public void AttachTexture(RenderTexture source)
    {
        sender.sourceTexture = source;
    }

    public void DetachTexture()
    {
        sender.sourceTexture = null;
    }
}
