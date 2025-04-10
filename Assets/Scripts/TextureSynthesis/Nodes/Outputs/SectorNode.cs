
using NodeEditorFramework;
using NodeEditorFramework.Utilities;
using SecretFire.TextureSynth;

using System.Collections.Generic;
using System.Linq;

using UnityEngine;

[Node(false, "Output/Sector")]
public class SectorNode : TickingNode
{
    public override string GetID => "SectorNode";
    public override string Title { get { return "Sector"; } }
    private Vector2 _DefaultSize = new Vector2(220, 180);

    public override Vector2 DefaultSize => _DefaultSize;

    [ValueConnectionKnob("inputTex", Direction.In, typeof(Texture), NodeSide.Top)]
    public ValueConnectionKnob inputTexKnob;

    private Vector2Int outputSize = Vector2Int.zero;
    private RenderTexture buffer;

    private string ip;
    private byte[] universe0 = new byte[510];
    private byte[] universe1 = new byte[510];
    private byte[] universe2 = new byte[324];
    private List<byte[]> universes;
    private const int numPixels = 448;

    DmxController controller;
    public override void DoInit()
    {
        var controllerObj = GameObject.Find("DMXController");
        if (controllerObj != null) {
            controller = controllerObj.GetComponent<DmxController>();
            ip = controller.remoteIP;
        }
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
        targetFPS = RTEditorGUI.Slider(targetFPS, 0.5f, 60);
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
        -1,  // 8 
        0,   // 9
        -1,  // 10
        1,   // 11
        0,   // 12
        0,   // 13
        0,  // 14
        0,   // 15
        -1,  // 16
        0,   // 17
        -1,  // 18
        -2,   // 19
        2,  // 20
        -1,   // 21
        2,  // 22
        -1,   // 23
        2,   // 24
        -1,   // 25
        2,  // 26
        0,   // 27
        3,   // 28
        0,   // 29
        3,   // 30
        -6};  // 31?
    public void FillFromTexture(Texture2D tex)
    {
        for (int r = 0; r < rows.Length; r++)
        {
            int startcol = tex.width/2-rows[r]/2;
            for (int c = 0; c < rows[r]; c++)
            {
                int index = rows.Where((value, i) => i < r).Sum() + c;
                var col = c;
                if (r % 2 == 1)
                {
                    col = rows[r] - c;
                }
                col = col + offsets[r];
                Color32 color = tex.GetPixel(startcol+col, r);
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

    //byte seq = 1;
    bool dmxAlive = true;
    public void SendDMX()
    {
        if (dmxAlive){
            try
            {
                controller.Send(0, universe0);
                //controller.Send(1, seq, universe1);
                controller.Send(1, universe1);
                //seq++;
                //if (seq == 0)
                //{
                //    seq = 1;
                //}
                //Debug.Log("Values: <" + string.Join(", ", universe1) + ">");
                //this.TimedDebug("Values: <"+ string.Join(", ", universe1)+">", .5f);
                controller.Send(2, universe2);
            }
            catch (System.Exception err)
            {
                // DMX is down, ignore
                Debug.Log($"Couldn't send ArtNet due to {err.Message}, disabling DMX");
                dmxAlive = false;
            }
        }
    }

    float lastCalced = 0;
    public float targetFPS = 60;
    public override bool DoCalc()
    {
        if (Time.time - lastCalced > (1 / targetFPS))
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
            lastCalced = Time.time;
        }
        return true;
    }
}
