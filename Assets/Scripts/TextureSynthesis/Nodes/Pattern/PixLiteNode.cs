using NodeEditorFramework;
using SecretFire.TextureSynth;
using UnityEngine;
using NativeWebSocket;
using System;


[Node(false, "Signal/PixLite")]
public class PixLiteNode : TickingNode
{
    public const string ID = "PixLiteNode";
    public override string GetID { get { return ID; } }

    public override string Title { get { return "PixLite"; } }


    WebSocket websocket;

    public float prevBrightness;
    public float brightness;

    [ValueConnectionKnob("Brightness", Direction.In, "Float")]
    public ValueConnectionKnob brightnessKnob;

    private Vector2 _DefaultSize = new Vector2(180, 180);

    public override Vector2 DefaultSize => _DefaultSize;

    public override void DoInit()
    {
        // Open WebSocket connection to PixLite
        websocket = new WebSocket("ws://192.168.50.5/v1.5?user=admin&auth=47DEQpj8HBSa-_TImW-5JCeuQeRkm5NMpJWZG3hSuFU");
        websocket.OnOpen += () =>
        {
            Debug.Log("Connection open!");
        };

        websocket.OnError += (e) =>
        {
            Debug.Log("Error! " + e);
        };

        websocket.OnClose += (e) =>
        {
            Debug.Log("Connection closed!");
        };

        websocket.Connect();
    }


    public override void NodeGUI()
    {
        GUILayout.BeginVertical();
        FloatKnobOrSlider(ref brightness, 0, 1, brightnessKnob);
        GUILayout.Label("Brightness: " + brightness);
        GUILayout.Label("Brightness to PixLite: " + (int)(brightness * 31));
        GUILayout.Label("WebSocket state: " + Enum.GetName(typeof(WebSocketState), websocket.State));
        if (GUILayout.Button("Reconnect") && websocket.State != WebSocketState.Open)
        {
            websocket.Connect();
        }
        GUILayout.EndVertical();

        if (GUI.changed)
            NodeEditor.curNodeCanvas.OnNodeChange(this);
    }

    public override bool DoCalc()
    {
        // Scale brightness to 0-31
        var brightnessInt = (int)(brightness * 31);
        var prevBrightnessInt = (int)(prevBrightness * 31);

        if (brightnessInt != prevBrightnessInt)
        {
            prevBrightness = brightness;
            if (websocket.State == WebSocketState.Open)
            {
                string json = $@"
{{
  ""req"": ""configChange"",
  ""id"": 7,
  ""params"": {{
    ""action"": ""apply"",
    ""config"": {{
      ""pix"": {{
        ""pixType"": ""SK9822"",
        ""colorType"": ""RGB"",
        ""freq"": 1000,
        ""expand"": false,
        ""inFormat"": ""8Bit"",
        ""gammaOn"": true,
        ""gamma"": [
          2,
          2,
          2
        ],
        ""ditherOn"": false,
        ""holdLastFrm"": false,
        ""curCtrlGbl"": {brightnessInt}
      }}
    }}
  }}
}}";

                websocket.SendText(json);
            }
        }

        return true;
    }
}
