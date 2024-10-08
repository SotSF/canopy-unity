
using NodeEditorFramework;
using NodeEditorFramework.Utilities;
using SecretFire.TextureSynth;

using System.Collections.Generic;
using System.Linq;

using UnityEngine;

[Node(false, "Pattern/PixelLocationFinder")]
public class PixelLocationFinderNode : TickingNode
{
    public override string GetID => "PixelLocationFinder";
    public override string Title { get { return "PixelLocationFinder"; } }
    private Vector2 _DefaultSize = new Vector2(220, 180); 

    public override Vector2 DefaultSize => _DefaultSize;

    private string ip;
    private byte[] universe0 = new byte[512];
    private byte[] universe1 = new byte[512];
    private byte[] universe2 = new byte[512];

    private float lastCycleTime;
    private float litTime = .1f;
    private int index = 0;

    private bool running = false;
    private const int numPixels = 448;
    //private const int numPixels = 173;

    private List<byte[]> universes;

    DmxController controller;
    public override void DoInit()
    {
        controller = GameObject.Find("DMXController").GetComponent<DmxController>();
        ip = controller.remoteIP;
        lastCycleTime = Time.time;
        universes = new List<byte[]>(){ universe0, universe1, universe2 };
    }

    public override void NodeGUI()
    {
        GUILayout.BeginVertical();

        GUILayout.BeginHorizontal();
        ip = RTEditorGUI.TextField(new GUIContent("IP"), ip);
        if (GUILayout.Button("Set IP"))
        {
            controller.remoteIP = ip;
        }
        var label = running ? "Stop" : "Start";
        if (GUILayout.Button(label))
        {
            running = !running;
        }
        if (GUILayout.Button("Reset"))
        {
            index = 0;
        }
        GUILayout.EndHorizontal();

        litTime = RTEditorGUI.Slider("Lit time", litTime, 0.1f, 2f);
        GUILayout.FlexibleSpace();
        GUILayout.BeginHorizontal();
        GUILayout.FlexibleSpace();
        GUILayout.EndHorizontal();
        GUILayout.Space(4);
 
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
        } else if (pixel < 340)
        {
            u = 1;
            startOffset = ((pixel - 170) * 3) % 512;
        } else
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

    public void SendDMX()
    {
        // Clear previous pixel
        var lastIndex = index == 0 ? numPixels-1 : index - 1;
        setPixel(lastIndex, Color.black);

        // Color current pixel white
        setPixel(index, Color.white);

        controller.Send(0, universe0);
        controller.Send(1, universe1);
        controller.Send(2, universe2);
    }

    public override bool DoCalc()
    {
        if (running && Time.time - lastCycleTime > litTime)
        {
            SendDMX();
            index = ++index % numPixels;
            //if (index == numPixels)
            //{
            //    index = numPixels - 4;
            //} else
            //{
            //    ++index;
            //}
            lastCycleTime = Time.time;
        }
        return true;
    }
}
