
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
    private Vector2 _DefaultSize = new Vector2(220, 180);

    public override Vector2 DefaultSize => _DefaultSize;

    [ValueConnectionKnob("inputTex", Direction.In, typeof(Texture), NodeSide.Top)]
    public ValueConnectionKnob inputTexKnob;

    public bool useDoubleDensity = true;

    private RenderTexture buffer;

    private string ip;
    private List<byte[]> universes;
    private int numUniverses = 96;

    public bool flipMirrorDirection = true;
    public int mirrorOffset = 27;
    int frameindex = 0;

    const int singleDensitypixelsPerStrip = 76;
    const int doubleDensityPixelsPerStrip = 151;

    private int pixelsPerStrip
    {
        get 
        {
            return useDoubleDensity ? doubleDensityPixelsPerStrip : singleDensitypixelsPerStrip;
        }
    }

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
        buffer = new RenderTexture(pixelsPerStrip, Constants.NUM_STRIPS, 24);
        buffer.enableRandomWrite = true;
        buffer.Create();
    }

    private GUIContent IPLabel = new GUIContent("IP");
    public override void NodeGUI()
    {
        inputTexKnob.SetPosition(20);
        GUILayout.BeginVertical();

        GUILayout.BeginHorizontal();
        ip = RTEditorGUI.TextField(IPLabel, ip);
        if (GUILayout.Button("Set IP"))
        {
            controller.remoteIP = ip;
        }
        GUILayout.EndHorizontal();
        
        mirrorOffset = RTEditorGUI.IntSlider("Mirror offset", mirrorOffset, 0, 95);
        flipMirrorDirection = RTEditorGUI.Toggle(flipMirrorDirection, "Flip mirror direction");
        useDoubleDensity = RTEditorGUI.Toggle(useDoubleDensity, "Use double density");
        GUILayout.Label($"FPS: {fps}");
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
        var passedPixels = (pixelsPerStrip-1) * stripIndex;
        if (r % 2 == 1)
        {
            // Zig-zag odd numbered strips
            return passedPixels + ((pixelsPerStrip-1) - c);
        }
        return passedPixels + c;
    }

    private int pixelIndexToUniverseIndex(int pixIndex)
    {
        //if (pixIndex < 170)
        //    return 0;
        //if (pixIndex < 340)
        //    return 1;
        //return 2;
        return pixIndex / 170;
    }

    public void setPixel(int r, int c, Color32 color)
    {
        // Special case behavior for infinity mirror at the innermost ring
        // Mirror is tailed off #12
        // It can thus use the end of universe 72
        if (c == 0)
        {
            var universeIndex = 71;
            var pixelIndex = (r + mirrorOffset) % 96;
            if (flipMirrorDirection)
            {
                pixelIndex = ((95 - r) + mirrorOffset) % 96;
            }
            var startOffset = 0;
            if (true)
            {
                //There are 50 LEDs in the last universe of the final strip
                startOffset = (pixelIndex + 50) * 3;
            }

            var universe = universes[universeIndex];
            universe[startOffset + 0] = color.r;
            universe[startOffset + 1] = color.g;
            universe[startOffset + 2] = color.b;
        } else
        {
            var port = stripToPort(r);
            var pixelIndex = pixelIndexInPort(r, c);
            var portUniverseIndex = pixelIndexToUniverseIndex(pixelIndex);
            var universeIndex = (6 * port) + portUniverseIndex;
            var universe = universes[universeIndex];

            var startOffset = (pixelIndex - (170 * portUniverseIndex)) * 3;

            universe[startOffset + 0] = color.r;
            universe[startOffset + 1] = color.g;
            universe[startOffset + 2] = color.b;
        }
    }

    public void FillFromTexture(Texture2D tex)
    {
        for (int r = 0; r < Constants.NUM_STRIPS; r++)
        {
            for (int c = 0; c < pixelsPerStrip; c++)
            {
                var col = c;
                Color32 color = tex.GetPixel(col, r);
                setPixel(r,c, color);
            }
        }
    }


    bool dmxAlive = true;
    public void SendDMX()
    {
        if (dmxAlive)
        {
            try
            {
                if (lastSendTime != 0)
                {
                    var deltaFrame = Time.time - lastSendTime;
                    fps = 1/deltaFrame;
                }
                for (short i = 0; i < numUniverses; i++)
                {
                    controller.Send(i, universes[i]);
                }
                lastSendTime = Time.time;
            }
            catch (System.Exception err)
            {
                dmxAlive = false;
                Debug.LogException(err);
            }
        }
    }

    float lastSendTime;
    float fps = 24;

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
