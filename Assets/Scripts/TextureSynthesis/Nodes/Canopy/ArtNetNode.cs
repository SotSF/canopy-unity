
using NodeEditorFramework;
using NodeEditorFramework.Utilities;
using SecretFire.TextureSynth;

using System.Collections.Generic;
using System.Linq;

using UnityEngine;

[Node(false, "Output/ArtNet")]
public class ArtNetNode : TickingNode
{
    public override string GetID => "ArtNetNode";
    public override string Title { get { return "ArtNet"; } }

    public override Vector2 DefaultSize { get { return new Vector2(220, 180); } }

    [ValueConnectionKnob("inputTex", Direction.In, typeof(Texture), NodeSide.Top)]
    public ValueConnectionKnob inputTexKnob;

    private Vector2Int outputSize = Vector2Int.zero;
    private RenderTexture buffer;

    private int universe;
    private string ip;
    private byte[] universe0 = new byte[512];
    private byte[] universe1 = new byte[512];
    private byte[] universe2 = new byte[512];
    private List<byte[]> universes;
    private const int numPixels = 448;

    DmxController controller;
    public void Awake()
    {
        controller = GameObject.Find("DMXController").GetComponent<DmxController>();
        ip = controller.remoteIP;
        universes = new List<byte[]>() { universe0, universe1, universe2 };
    }

    private void InitializeRenderTexture()
    {
        if (buffer != null)
        {
            buffer.Release();
        }
        buffer = new RenderTexture(outputSize.x, outputSize.y, 24);
        buffer.enableRandomWrite = true;
        buffer.Create();
    }

    public override void NodeGUI()
    {
        inputTexKnob.SetPosition(20);
        GUILayout.BeginVertical();

        GUILayout.BeginHorizontal();
        ip = RTEditorGUI.TextField(new GUIContent("IP"), ip);
        if (GUILayout.Button("Set IP"))
        {
            controller.remoteIP = ip;
        }
        GUILayout.EndHorizontal();

        universe = RTEditorGUI.IntSlider(universe, 0, 6);

        GUILayout.FlexibleSpace();
        GUILayout.BeginHorizontal();
        GUILayout.FlexibleSpace();
        GUILayout.EndHorizontal();
        GUILayout.Space(4);
        GUILayout.EndVertical();
        if (GUI.changed)
            NodeEditor.curNodeCanvas.OnNodeChange(this);
    }

    public void setPixel(int pixel, Color32 color)
    {
        var u = 0;
        var startOffset = 0;
        if (pixel < 170)
        {
            u = 0;
            startOffset = (pixel * 3) % 512;
        }
        else if (pixel < 340)
        {
            u = 1;
            startOffset = ((pixel - 170) * 3) % 512;
        }
        else
        {
            u = 2;
            startOffset = ((pixel - 340) * 3) % 512;
        }
        var universe = universes[u];
        universe[startOffset + 0] = color.r;
        universe[startOffset + 1] = color.g;
        universe[startOffset + 2] = color.b;
        //Debug.LogFormat("Pixel: {0}, UniverseIndex: {1}, StartOffset: {2}, Color: {3}", pixel, endUniverseIndex, startOffset, color);
    }

    int[] rows = {
        5,   // 1
        6,   // 2
        7,   // 3
        6,   // 4
        8,   // 5
        8,   // 6
        9,   // 7
        9,   // 8 
        11,  // 9
        11,  // 10
        12,  // 11
        12,  // 12
        13,  // 13
        14,  // 14
        15,  // 15
        15,  // 16
        17,  // 17
        17,  // 18
        17,  // 19
        18,  // 20
        18,  // 21
        20,  // 22
        19,  // 23
        19,  // 24
        20,  // 25
        22,  // 26
        20,  // 27
        22,  // 28
        22,  // 29
        24,  // 30
        12}; // 31?

    int[] offsets = {
        0,   // 1
        0,   // 2
        0,   // 3
        0,   // 4
        0,   // 5
        0,   // 6
        0,   // 7
        -1,   // 8 
        0,  // 9
        -1,  // 10
        0,  // 11
        0,  // 12
        0,  // 13
        -1,  // 14
        0,  // 15
        -1,  // 16
        0,  // 17
        -1,  // 18
        0,  // 19
        -1,  // 20
        0,  // 21
        -2,  // 22
        0,  // 23
        0,  // 24
        0,  // 25
        -1,  // 26
        2,  // 27
        0,  // 28
        1,  // 29
        0,  // 30
        0}; // 31?
    public void FillFromTexture(Texture2D tex)
    {
        for (int r = 0; r < rows.Length; r++)
        {
            for (int c = 0; c < rows[r]; c++)
            {
                int index = rows.Where((value, i) => i < r).Sum() + c;
                var col = c;
                if (r % 2 == 1)
                {
                    col = rows[r] - c;
                }
                col = col + offsets[r];
                Color32 color = tex.GetPixel(col, r);
                setPixel(index, color);
            }
        }
    }

    public void FillByRows()
    {
        float h = 0;
        float s = 1;
        float v = .7f;
        var pixelIndex = 0;
        for (int r = 0; r < rows.Length; r++)
        {
            for (int c = 0; c< rows[r]; c++)
            {
                Color color = Color.HSVToRGB(h, s, v);
                pixelIndex = rows.Where((value, i) => i < r).Sum() + c;
                setPixel(pixelIndex, color);
            }
            h = (h + 0.6f) % 1;
        }
        for (int i = pixelIndex; i < numPixels; i++)
        {
            setPixel(i, Color.red);
        }
    }

    public void FillRainbow()
    {
        for (int i = 0; i < numPixels; i++)
        {
            var hue = ((float)i) / numPixels;
            var saturation = (i % 10) / 10.0f;
            var value = 1;
            Color c = Color.HSVToRGB(hue, saturation, value);
            setPixel(i, c);
        }
    }

    public void SendDMX()
    {
        controller.Send(0, universe0);
        controller.Send(1, universe1);
        controller.Send(2, universe2);
    }

    public override bool Calculate()
    {
        Texture tex = inputTexKnob.GetValue<Texture>();
        if (!inputTexKnob.connected() || tex == null)
        { // Reset outputs if no texture is available
            if (buffer != null)
                buffer.Release();
            outputSize = Vector2Int.zero;
            return true;
        }
        var inputSize = new Vector2Int(tex.width, tex.height);
        if (inputSize != outputSize)
        {
            outputSize = inputSize;
            InitializeRenderTexture();
        }
        Graphics.Blit(tex, buffer);
        Texture2D tex2d = buffer.ToTexture2D();
        FillFromTexture(tex2d);
        SendDMX();
        Destroy(tex2d);
        return true;
    }
}
