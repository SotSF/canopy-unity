
using NodeEditorFramework;
using NodeEditorFramework.Utilities;
using SecretFire.TextureSynth;

using System.Collections.Generic;
using System.Linq;

using UnityEngine;

[Node(false, "Output/CanopyArtnet")]
public class CanopyArtnetNode : TickingNode
{
    public override string GetID => "CanopyArtnetNode";
    public override string Title { get { return "CanopyArtNet"; } }

    public override Vector2 DefaultSize { get { return new Vector2(220, 180); } }

    [ValueConnectionKnob("inputTex", Direction.In, typeof(Texture), NodeSide.Top)]
    public ValueConnectionKnob inputTexKnob;

    private RenderTexture buffer;

    private int universe;
    private string ip;
    private List<byte[]> universes;
    private const int numPixels = 7200;
    private int numUniverses = 48;

    private ComputeShader canopyMainShader;
    private ComputeBuffer dataBuffer;
    private Vector3[] colorData;
    private int mainKernel;

    public bool polarize;
    int frameindex = 0;

    DmxController controller;
    public void Awake()
    {
        controller = GameObject.Find("DMXController").GetComponent<DmxController>();
        ip = controller.remoteIP;
        universes = new List<byte[]>(numUniverses);
        for (int i = 0; i < numUniverses; i++)
        {
            universes.Add(new byte[512]);
        }
        InitializeRenderTexture();
    }

    private void InitializeRenderTexture()
    {
        buffer = new RenderTexture(Constants.PIXELS_PER_STRIP, Constants.NUM_STRIPS, 24);
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

    // Transform from strip [0-95] to port [0-15] space
    private int stripToPort(int r)
    {
        return r / 6;
    }

    // Transform from strip [0-95] to index within the port [0-5]
    private int stripIndexInPort(int r)
    {
        return r % 6;
    }

    private int pixelIndexInPort(int r, int c)
    {
        var stripIndex = stripIndexInPort(r);
        var passedPixels = Constants.PIXELS_PER_STRIP * stripIndex;
        if (r % 2 == 1)
        {
            // Zig-zag odd numbered strips
            return passedPixels + ((Constants.PIXELS_PER_STRIP-1) - c);
        }
        return passedPixels + c;
    }

    private int pixelIndexToUniverseIndex(int pixIndex)
    {
        if (pixIndex < 170)
            return 0;
        if (pixIndex < 340)
            return 1;
        return 2;
    }

    public void setPixel(int r, int c, Color32 color)
    {
        var port = stripToPort(r);
        var pixelIndex = pixelIndexInPort(r, c);
        var portUniverseIndex = pixelIndexToUniverseIndex(pixelIndex);
        var universeIndex = (3 * port) + portUniverseIndex;
        var universe = universes[universeIndex];

        var startOffset = (pixelIndex - (170 * portUniverseIndex)) * 3;

        universe[startOffset + 0] = color.r;
        universe[startOffset + 1] = color.g;
        universe[startOffset + 2] = color.b;
    }

    public void FillFromTexture(Texture2D tex)
    {
        for (int r = 0; r < Constants.NUM_STRIPS; r++)
        {
            for (int c = 0; c < Constants.PIXELS_PER_STRIP; c++)
            {
                var col = c;
                Color32 color = tex.GetPixel(col, r);
                setPixel(r,c, color);
            }
        }
    }


    public void SendDMX()
    {
        for (short i = 0; i < numUniverses; i++)
        {
            controller.Send(i, universes[i]);
        }
    }

    public override bool Calculate()
    {
        Texture inputTex = inputTexKnob.GetValue<Texture>();
        if (!inputTexKnob.connected() || inputTex == null)
        {
            return true;
        }
        Graphics.Blit(inputTex, buffer);
        Texture2D tex2d = buffer.ToTexture2D();
        FillFromTexture(tex2d);
        SendDMX();
        Destroy(tex2d);
        frameindex++;
        return true;
    }
}
