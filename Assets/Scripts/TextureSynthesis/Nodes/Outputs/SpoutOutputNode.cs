
using NodeEditorFramework;
using NodeEditorFramework.Utilities;
using SecretFire.TextureSynth;
using UnityEngine;

[Node(false, "Output/Spout")]
public class SpoutOutputNode : TextureSynthNode
{
    public override string GetID => "SpoutOutput";
    public override string Title { get { return "SpoutOutput"; } }

    private Vector2 _DefaultSize = new Vector2(200, 150);
    public override Vector2 DefaultSize => _DefaultSize;

    [ValueConnectionKnob("inputTex", Direction.In, typeof(Texture), NodeSide.Top)]
    public ValueConnectionKnob inputTexKnob;

    private GameObject prefab;
    private RenderTexture outputTex;

    private Vector2Int outputSize = new Vector2Int(1920, 1080);
    private SpoutController _spoutController;
    private SpoutController spoutController {
        get
        {
            if (_spoutController == null)
            {
                _spoutController = Instantiate(prefab).GetComponent<SpoutController>();
            }
            return _spoutController;
        }
    }

    public string spoutSenderName = "spoutSender";
    public bool sendAlpha = false;

    public override void DoInit()
    {
        prefab = Resources.Load<GameObject>("Prefabs/SpoutSender");
    }

    public override void NodeGUI()
    {
        inputTexKnob.SetPosition(20);
        GUILayout.BeginVertical();
        var newName = GUILayout.TextField(spoutSenderName);
        if (newName != spoutSenderName)
        {
            spoutSenderName = newName;
            spoutController.SetName(spoutSenderName);
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

        spoutController.RefreshSender();
        spoutController.SetName(spoutSenderName);
        if (outputTex != null)
        {
            outputTex.Release();
        }
        outputTex = new RenderTexture(outputSize.x, outputSize.y, 24);
        outputTex.enableRandomWrite = true;
        outputTex.Create();
        spoutController.AttachTexture(outputTex);
    }

    public override bool DoCalc()
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
