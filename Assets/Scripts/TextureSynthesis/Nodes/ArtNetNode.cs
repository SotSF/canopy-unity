
using NodeEditorFramework;
using NodeEditorFramework.Utilities;
using SecretFire.TextureSynth;
using UnityEngine;

[Node(false, "Canopy/ArtNet")]
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

    DmxController controller;
    public void Awake()
    {
        controller = GameObject.Find("DMXController").GetComponent<DmxController>();
        ip = controller.remoteIP;
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
    
    public void SendDMX(Texture2D tex)
    {
        byte[] data = new byte[512];
        for (int x = 0; x < 144; x++)
        {
            int offset = 3 * x;
            Color32 c = tex.GetPixel(x, tex.height / 2);
            data[offset + 0] = c.r;
            data[offset + 1] = c.g;
            data[offset + 2] = c.b;
        }
        controller.Send((short)universe, data);
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
        SendDMX(tex2d);
        return true;
    }
}
