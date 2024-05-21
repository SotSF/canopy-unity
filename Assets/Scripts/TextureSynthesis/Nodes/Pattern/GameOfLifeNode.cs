
using NodeEditorFramework;
using NodeEditorFramework.Utilities;
using SecretFire.TextureSynth;
using UnityEngine;

[Node(false, "Filter/GameOfLife")]
public class GameOfLifeNode : TickingNode
{
    public override string GetID => "GameOfLifeNode";
    public override string Title { get { return "GameOfLife"; } }
    private Vector2 _DefaultSize = new Vector2(150, 150);

    public override Vector2 DefaultSize => _DefaultSize;
    [ValueConnectionKnob("gameState", Direction.In, typeof(Texture), NodeSide.Top)]
    public ValueConnectionKnob gameStateKnob;

    [ValueConnectionKnob("outputTex", Direction.Out, typeof(Texture), NodeSide.Bottom)]
    public ValueConnectionKnob outputTexKnob;

    private ComputeShader patternShader;
    private int patternKernel;
    private Vector2Int outputSize = new Vector2Int(75, 96);
    public RenderTexture outputState;
    private RenderTexture inputState;

    private void Awake(){
        patternShader = Resources.Load<ComputeShader>("NodeShaders/GameOfLifePattern");
        patternKernel = patternShader.FindKernel("GameOfLife");
        InitializeRenderTexture();
    }

    private void InitializeRenderTexture()
    {
        outputState = new RenderTexture(outputSize.x, outputSize.y, 0);
        outputState.enableRandomWrite = true;
        outputState.useMipMap = false;
        outputState.autoGenerateMips = false;
        outputState.enableRandomWrite = true;
        outputState.filterMode = FilterMode.Point;
        outputState.wrapMode = TextureWrapMode.Clamp;
        outputState.Create();
        inputState = new RenderTexture(outputSize.x, outputSize.y, 0);
        inputState.enableRandomWrite = true;
        inputState.useMipMap = false;
        inputState.autoGenerateMips = false;
        inputState.enableRandomWrite = true;
        inputState.filterMode = FilterMode.Point;
        inputState.wrapMode = TextureWrapMode.Clamp;
        inputState.Create();
    }

    bool running = false;
    public override void NodeGUI()
    {
        gameStateKnob.SetPosition(20);

        GUILayout.BeginVertical();
        if (gameStateKnob.connected()) {
            if (GUILayout.Button("Apply state"))
            {
                Graphics.Blit(gameStateKnob.GetValue<Texture>(), inputState);
                Debug.Log("State applied");
            }
        }
        string label = running ? "Stop" : "Run";
        if (GUILayout.Button(label))
        {
            running = !running;
        }

        GUILayout.BeginHorizontal();
        GUILayout.FlexibleSpace();
        GUILayout.Box(inputState, GUILayout.MaxWidth(64), GUILayout.MaxHeight(64));
        GUILayout.Box(outputState, GUILayout.MaxWidth(64), GUILayout.MaxHeight(64));
        GUILayout.EndHorizontal();

        GUILayout.Space(4);
        GUILayout.EndVertical();

        outputTexKnob.SetPosition(DefaultSize.x-20);
        if (GUI.changed)
            NodeEditor.curNodeCanvas.OnNodeChange(this);
    }
    
    public override bool Calculate()
    {
        if (running)
        {
            patternShader.SetInt("width", outputSize.x);
            patternShader.SetInt("height", outputSize.y);
            patternShader.SetTexture(patternKernel, "gameState", inputState);
            patternShader.SetTexture(patternKernel, "outputTex", outputState);
            uint tx, ty, tz;
            patternShader.GetKernelThreadGroupSizes(patternKernel, out tx, out ty, out tz);
            var threadGroupX = Mathf.CeilToInt(((float)outputSize.x) / tx);
            var threadGroupY = Mathf.CeilToInt(((float)outputSize.y) / ty);
            patternShader.Dispatch(patternKernel, threadGroupX, threadGroupY, 1);
            outputTexKnob.SetValue(outputState);
            Graphics.Blit(outputState, inputState);
        }
        return true;
    }
}
