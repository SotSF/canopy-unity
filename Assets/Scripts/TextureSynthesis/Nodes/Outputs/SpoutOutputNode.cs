
using NodeEditorFramework;
using NodeEditorFramework.Utilities;
using SecretFire.TextureSynth;
using UnityEngine;

[Node(false, "Output/Spout")]
public class SpoutOutputNode : TextureSynthNode
{
    public override string GetID => "SpoutOutput";
    public override string Title { get { return "SpoutOutput"; } }

    public override Vector2 DefaultSize { get { return new Vector2(200, 150); } }

    [ValueConnectionKnob("inputTex", Direction.In, typeof(Texture), NodeSide.Top)]
    public ValueConnectionKnob inputTexKnob;

    private GameObject prefab;
    private RenderTexture outputTex;

    private Vector2Int outputSize = new Vector2Int(1920, 1080);
    private SpoutController spoutController;

    public string name = "spoutSender";
    public bool sendAlpha = false;

    private void Awake()
    {
        prefab = Resources.Load<GameObject>("Prefabs/SpoutSender");
    }

    public override void NodeGUI()
    {
        inputTexKnob.SetPosition(20);
        GUILayout.BeginVertical();
        var newName = GUILayout.TextField(name);
        if (newName != name)
        {
            name = newName;
            spoutController.SetName(name);
            spoutController.RefreshSender();
        }
        // Bottom row: output image
        GUILayout.BeginHorizontal();
        var newAlpha = GUILayout.Toggle(sendAlpha, "Send alpha");
        if (newAlpha != sendAlpha)
        {
            spoutController.SendAlpha(newAlpha);
            sendAlpha = newAlpha;
        }
        GUILayout.FlexibleSpace();
        GUILayout.Box(outputTex, GUILayout.MaxWidth(64), GUILayout.MaxHeight(64));
        GUILayout.EndHorizontal();
        GUILayout.Space(4);
        GUILayout.EndVertical();

        if (GUI.changed)
            NodeEditor.curNodeCanvas.OnNodeChange(this);
    }

    private void InitializeRenderTexture()
    {
        if (spoutController == null)
        {
            spoutController = Instantiate(prefab).GetComponent<SpoutController>();
        } else
        {
            spoutController.RefreshSender();
        }
        spoutController.SetName(name);
        if (outputTex != null)
        {
            outputTex.Release();
        }
        outputTex = new RenderTexture(outputSize.x, outputSize.y, 24);
        outputTex.enableRandomWrite = true;
        outputTex.Create();
        spoutController.AttachTexture(outputTex);
    }

    public override bool Calculate()
    {
        Texture tex = inputTexKnob.GetValue<Texture>();
        if (!inputTexKnob.connected() || tex == null)
        { // Reset outputs if no texture is available
            outputSize = Vector2Int.zero;
            if (outputTex != null)
            {
                outputTex.Release();
                spoutController.RefreshSender();
            }
            return true;
        }

        var inputSize = new Vector2Int(tex.width, tex.height);
        if (inputSize != outputSize)
        {
            outputSize = inputSize;
            InitializeRenderTexture();
        }
        
        if (inputTexKnob.connected() && tex != null)
        {
            Graphics.Blit(tex, outputTex);
        }
        return true;
    }
}
