using System.Collections.Generic;
using System.Linq;
using NodeEditorFramework;
using NodeEditorFramework.Utilities;
using SecretFire.TextureSynth;
using UnityEngine;

[Node(false, "Filter/Merge")]
public class MergeNode : TextureSynthNode
{
    public override string GetID => "MergeNode";
    public override string Title { get { return "Merge"; } }

    private const int MaxLayers = 8;
    private const int DefaultTextureSlots = 2;
    private const float SimpleWidth = 280f;
    private const float SimpleHeight = 240f;
    private const float LayerMinWidth = 280f;
    private const float LayerMinHeight = 220f;
    private const float LayerColumnWidth = 56f;
    private const float LayerColumnStartX = 30f;
    private const float LayerWidthPadding = 60f;
    private const float LayerRowHeight = 28f;
    private const float LayerFooterHeight = 130f;
    private const float HiddenPortPosition = -10000f;

    private Vector2 _DefaultSize = new Vector2(SimpleWidth, SimpleHeight);
    public override Vector2 DefaultSize
    {
        get
        {
            SetSize();
            return _DefaultSize;
        }
    }
    public override Vector2 MinSize => DefaultSize;

    [ValueConnectionKnob("outputTex", Direction.Out, typeof(Texture), NodeSide.Bottom)]
    public ValueConnectionKnob outputTexKnob;

    public float crossfader = 0;

    // layerOrder[v] = dynamic texture-port index displayed at visual position v.
    // layerOpacities[v] = manual opacity for that visual layer when its float port is unconnected.
    public List<int> layerOrder = new List<int>();
    public List<float> layerOpacities = new List<float>();

    private ComputeShader patternShader;
    private int layerKernel;
    private int fadeKernel;
    private Vector2Int outputSize = Vector2Int.zero;
    private RenderTexture outputTex;
    public RadioButtonSet mergeModeSelection;

    private List<ValueConnectionKnob> TexturePorts =>
        dynamicConnectionPorts.OfType<ValueConnectionKnob>().Where(IsTexturePort).ToList();
    private List<ValueConnectionKnob> FloatPorts =>
        dynamicConnectionPorts.OfType<ValueConnectionKnob>().Where(IsFloatPort).ToList();

    private int TexturePortCount => TexturePorts.Count;
    private int FloatPortCount => FloatPorts.Count;
    private ValueConnectionKnob TexturePort(int index) => TexturePorts[index];
    private ValueConnectionKnob FloatPort(int index) => FloatPorts[index];
    private bool isLayersMode =>
        mergeModeSelection != null && mergeModeSelection.Selected == "Layers";

    private bool IsTexturePort(ValueConnectionKnob port) =>
        typeof(Texture).IsAssignableFrom(port.valueType);
    private bool IsFloatPort(ValueConnectionKnob port) =>
        port.valueType == typeof(float);
    private bool IsLayerActive(int layerIndex) =>
        layerIndex >= 0 && layerIndex < TexturePortCount && TexturePort(layerIndex).connected();
    private int activeLayerCount =>
        Enumerable.Range(0, TexturePortCount).Count(IsLayerActive);
    private int targetTexturePortCount
    {
        get
        {
            if (isLayersMode)
                return Mathf.Min(Mathf.Max(DefaultTextureSlots, activeLayerCount + 1), MaxLayers);
            return Mathf.Max(DefaultTextureSlots, Mathf.Min(TexturePortCount, MaxLayers));
        }
    }
    private int targetFloatPortCount
    {
        get
        {
            if (isLayersMode)
                return targetTexturePortCount;

            int connectedFloatCount = FloatPorts.Count(port => port.connected());
            return Mathf.Max(1, connectedFloatCount);
        }
    }

    public override void DoInit()
    {
        patternShader = Resources.Load<ComputeShader>("NodeShaders/MergeFilter");
        layerKernel = patternShader.FindKernel("LayerKernel");
        fadeKernel = patternShader.FindKernel("FadeKernel");
        if (mergeModeSelection == null || mergeModeSelection.names.Count == 0)
        {
            mergeModeSelection = new RadioButtonSet(0, "Simple", "Layers");
        }
        SetPortCount();
        SetSize();
    }

    private void InitializeRenderTexture()
    {
        if (outputTex != null) outputTex.Release();
        outputTex = new RenderTexture(outputSize.x, outputSize.y, 0);
        outputTex.enableRandomWrite = true;
        outputTex.Create();
    }

    private void ReleaseOutput()
    {
        outputTexKnob.ResetValue();
        outputSize = Vector2Int.zero;
        if (outputTex != null) outputTex.Release();
    }

    private void EnsureLists()
    {
        if (layerOrder == null)
            layerOrder = new List<int>();
        if (layerOpacities == null)
            layerOpacities = new List<float>();
    }

    private ValueConnectionKnob CreateTexturePort()
    {
        var texAttr = new ValueConnectionKnobAttribute(
            "Layer", Direction.In, typeof(Texture), NodeSide.Top);
        return CreateValueConnectionKnob(texAttr);
    }

    private ValueConnectionKnob CreateFloatPort()
    {
        var opAttr = new ValueConnectionKnobAttribute(
            "opacity", Direction.In, typeof(float), NodeSide.Left);
        return CreateValueConnectionKnob(opAttr);
    }

    private void DeleteTexturePort(int textureIndex)
    {
        var texPorts = TexturePorts;
        var floatPorts = FloatPorts;
        if (textureIndex < 0 || textureIndex >= texPorts.Count)
            return;

        DeleteConnectionPort(texPorts[textureIndex]);
        if (textureIndex < floatPorts.Count)
            DeleteConnectionPort(floatPorts[textureIndex]);

        for (int i = layerOrder.Count - 1; i >= 0; i--)
        {
            if (layerOrder[i] == textureIndex)
            {
                layerOrder.RemoveAt(i);
                if (i < layerOpacities.Count)
                    layerOpacities.RemoveAt(i);
            }
            else if (layerOrder[i] > textureIndex)
            {
                layerOrder[i]--;
            }
        }
    }

    private void CompactLayerTexturePorts()
    {
        bool compacted;
        do
        {
            compacted = false;
            for (int i = TexturePortCount - 2; i >= 0; i--)
            {
                bool hasActiveAfter = false;
                for (int j = i + 1; j < TexturePortCount; j++)
                {
                    if (IsLayerActive(j))
                    {
                        hasActiveAfter = true;
                        break;
                    }
                }

                if (!IsLayerActive(i) && hasActiveAfter)
                {
                    DeleteTexturePort(i);
                    compacted = true;
                    break;
                }
            }
        }
        while (compacted);
    }

    private void TrimTexturePorts()
    {
        while (TexturePortCount > MaxLayers)
            DeleteTexturePort(TexturePortCount - 1);

        while (TexturePortCount > targetTexturePortCount)
        {
            int lastIndex = TexturePortCount - 1;
            if (IsLayerActive(lastIndex))
                break;
            DeleteTexturePort(lastIndex);
        }
    }

    private void TrimFloatPorts()
    {
        while (FloatPortCount > targetFloatPortCount)
        {
            int lastIndex = FloatPortCount - 1;
            if (FloatPort(lastIndex).connected())
                break;
            DeleteConnectionPort(FloatPort(lastIndex));
        }
    }

    private void ReconcileLayerOrder()
    {
        for (int v = layerOrder.Count - 1; v >= 0; v--)
        {
            int layerIndex = layerOrder[v];
            if (layerIndex < 0 || layerIndex >= TexturePortCount || !IsLayerActive(layerIndex))
            {
                layerOrder.RemoveAt(v);
                if (v < layerOpacities.Count)
                    layerOpacities.RemoveAt(v);
            }
        }

        for (int i = 0; i < TexturePortCount; i++)
        {
            if (IsLayerActive(i) && !layerOrder.Contains(i))
            {
                layerOrder.Add(i);
                layerOpacities.Add(1f);
            }
        }

        while (layerOpacities.Count < layerOrder.Count) layerOpacities.Add(1f);
        while (layerOpacities.Count > layerOrder.Count) layerOpacities.RemoveAt(layerOpacities.Count - 1);
    }

    private void SetPortCount()
    {
        EnsureLists();

        if (isLayersMode)
            CompactLayerTexturePorts();

        TrimTexturePorts();

        while (TexturePortCount < targetTexturePortCount)
            CreateTexturePort();

        while (FloatPortCount < targetFloatPortCount)
            CreateFloatPort();

        TrimFloatPorts();
        ReconcileLayerOrder();
        SetSize();
    }

    private void SetSize()
    {
        int visibleTextureSlots = Mathf.Max(DefaultTextureSlots, Mathf.Min(TexturePortCount, MaxLayers));
        float width = Mathf.Max(SimpleWidth, LayerWidthPadding + LayerColumnWidth * visibleTextureSlots);

        if (!isLayersMode)
        {
            _DefaultSize = new Vector2(width, SimpleHeight);
            return;
        }

        float height = Mathf.Max(LayerMinHeight, LayerFooterHeight + LayerRowHeight * activeLayerCount);
        _DefaultSize = new Vector2(Mathf.Max(width, LayerMinWidth), height);
    }

    private void SwapLayers(int va, int vb)
    {
        if (va < 0 || vb < 0 || va >= layerOrder.Count || vb >= layerOrder.Count) return;
        int tmpO = layerOrder[va]; layerOrder[va] = layerOrder[vb]; layerOrder[vb] = tmpO;
        float tmpOp = layerOpacities[va]; layerOpacities[va] = layerOpacities[vb]; layerOpacities[vb] = tmpOp;
    }

    private void ParkPort(ValueConnectionKnob port)
    {
        if (port != null && !port.connected())
            port.SetPosition(HiddenPortPosition, NodeSide.Left);
    }

    private void SetPortSide(ValueConnectionKnob port, NodeSide side)
    {
        // Keep the last repaint's sidePosition intact; resetting it during Layout
        // desyncs the visible knob from the hitbox used by the next input event.
        if (port != null && port.side != side)
            port.SetPosition(port.sidePosition, side);
    }

    public override void NodeGUI()
    {
        SetPortCount();
        DrawTexturePorts();

        GUILayout.BeginVertical();

        GUILayout.BeginHorizontal();
        RadioButtons(mergeModeSelection);
        GUILayout.EndHorizontal();

        if (isLayersMode)
            DrawLayersUI();
        else
            DrawSimpleUI();

        GUILayout.FlexibleSpace();
        GUILayout.BeginHorizontal();
        GUILayout.FlexibleSpace();
        GUILayout.Box(outputTex, GUILayout.MaxWidth(64), GUILayout.MaxHeight(64));
        GUILayout.EndHorizontal();
        GUILayout.Space(4);
        GUILayout.EndVertical();

        outputTexKnob.SetPosition(_DefaultSize.x - 40);

        if (GUI.changed)
            NodeEditor.curNodeCanvas.OnNodeChange(this);
    }

    private void DrawTexturePorts()
    {
        if (!isLayersMode)
        {
            for (int i = 0; i < TexturePortCount && i < MaxLayers; i++)
            {
                TexturePort(i).SetPosition(LayerColumnStartX + i * LayerColumnWidth, NodeSide.Top);
            }
            return;
        }

        // In layers mode, texture-port columns follow the user's visual layer order so
        // connections track the up/down reorder buttons. Inactive ports trail after in
        // port order so they remain available as connection targets.
        HashSet<int> placed = new();
        int column = 0;
        for (int v = 0; v < layerOrder.Count && column < MaxLayers; v++)
        {
            int layerIndex = layerOrder[v];
            if (layerIndex < 0 || layerIndex >= TexturePortCount) continue;
            TexturePort(layerIndex).SetPosition(LayerColumnStartX + column * LayerColumnWidth, NodeSide.Top);
            placed.Add(layerIndex);
            column++;
        }
        for (int i = 0; i < TexturePortCount && column < MaxLayers; i++)
        {
            if (placed.Contains(i)) continue;
            TexturePort(i).SetPosition(LayerColumnStartX + column * LayerColumnWidth, NodeSide.Top);
            column++;
        }
    }

    private void DrawSimpleUI()
    {
        ValueConnectionKnob crossfadePort = FloatPort(0);
        SetPortSide(crossfadePort, NodeSide.Left);

        GUILayout.BeginHorizontal();
        GUILayout.Space(5);
        crossfadePort.DisplayLayout(new GUIContent("Crossfade"));
        if (!crossfadePort.connected())
            crossfader = RTEditorGUI.Slider(crossfader, 0, 1);
        else
            crossfader = crossfadePort.GetValue<float>();
        GUILayout.EndHorizontal();

        for (int i = 1; i < FloatPortCount; i++)
        {
            ValueConnectionKnob port = FloatPort(i);
            if (port.connected())
                DrawConnectedInactiveFloatPort(i, port);
            else
                ParkPort(port);
        }
    }

    private void DrawConnectedInactiveFloatPort(int index, ValueConnectionKnob port)
    {
        SetPortSide(port, NodeSide.Left);
        GUILayout.BeginHorizontal();
        GUILayout.Space(5);
        GUILayout.Label(string.Format("L{0} opacity", index), GUILayout.Width(80));
        port.SetPosition();
        GUILayout.Label(port.GetValue<float>().ToString("F2"));
        GUILayout.EndHorizontal();
    }

    private void DrawLayersUI()
    {
        HashSet<int> visibleFloatPorts = new HashSet<int>();

        GUILayout.Space(8);
        // One row per active layer: [opacity knob][L# label][slider/value][^][v]
        for (int v = 0; v < layerOrder.Count; v++)
        {
            int layerIndex = layerOrder[v];
            if (layerIndex < 0 || layerIndex >= FloatPortCount)
                continue;

            visibleFloatPorts.Add(layerIndex);
            var opPort = FloatPort(layerIndex);

            GUILayout.BeginHorizontal();
            GUILayout.Space(8);
            GUILayout.Label(string.Format("L{0}", v), GUILayout.Width(24));
            SetPortSide(opPort, NodeSide.Left);
            opPort.SetPosition();
            if (opPort.connected())
            {
                GUILayout.Label(opPort.GetValue<float>().ToString("F2"));
            }
            else
            {
                layerOpacities[v] = RTEditorGUI.Slider(layerOpacities[v], 0f, 1f);
            }
            GUI.enabled = v > 0;
            if (GUILayout.Button("^", GUILayout.Width(22)))
                SwapLayers(v, v - 1);
            GUI.enabled = v < layerOrder.Count - 1;
            if (GUILayout.Button("v", GUILayout.Width(22)))
                SwapLayers(v, v + 1);
            GUI.enabled = true;
            GUILayout.EndHorizontal();
        }

        for (int i = 0; i < FloatPortCount; i++)
        {
            if (visibleFloatPorts.Contains(i))
                continue;

            ValueConnectionKnob port = FloatPort(i);
            if (port.connected())
                DrawConnectedInactiveFloatPort(i, port);
            else
                ParkPort(port);
        }
    }

    public override bool DoCalc()
    {
        SetPortCount();
        if (isLayersMode)
            return DoCalcLayers();
        return DoCalcSimple();
    }

    private bool DoCalcSimple()
    {
        if (TexturePortCount < 2 || FloatPortCount < 1)
        {
            ReleaseOutput();
            return true;
        }

        ValueConnectionKnob texLPort = TexturePort(0);
        Texture texL = texLPort.GetValue<Texture>();
        if (!texLPort.connected() || texL == null)
        {
            ReleaseOutput();
            return true;
        }

        ValueConnectionKnob texRPort = TexturePort(1);
        Texture texR = texRPort.GetValue<Texture>();
        if (!texRPort.connected() || texR == null)
        {
            ReleaseOutput();
            return true;
        }

        var inputSize = new Vector2Int(texL.width, texL.height);
        if (inputSize != outputSize)
        {
            outputSize = inputSize;
            InitializeRenderTexture();
        }

        patternShader.SetInt("width", outputSize.x);
        patternShader.SetInt("height", outputSize.y);

        ValueConnectionKnob crossfadePort = FloatPort(0);
        crossfader = crossfadePort.connected() ? crossfadePort.GetValue<float>() : crossfader;
        patternShader.SetFloat("crossfader", crossfader);

        patternShader.SetTexture(fadeKernel, "texL", texL);
        patternShader.SetTexture(fadeKernel, "texR", texR);
        patternShader.SetTexture(fadeKernel, "outputTex", outputTex);

        DispatchKernel(fadeKernel);
        outputTexKnob.SetValue(outputTex);
        return true;
    }

    private bool DoCalcLayers()
    {
        // Gather active layers in visual order. layerOrder[0] is the topmost layer.
        List<Texture> texs = new List<Texture>(MaxLayers);
        List<float> ops = new List<float>(MaxLayers);
        for (int v = 0; v < layerOrder.Count && texs.Count < MaxLayers; v++)
        {
            int layerIndex = layerOrder[v];
            if (layerIndex < 0 || layerIndex >= TexturePortCount || layerIndex >= FloatPortCount)
                continue;

            var texPort = TexturePort(layerIndex);
            if (!texPort.connected()) continue;
            Texture t = texPort.GetValue<Texture>();
            if (t == null) continue;
            texs.Add(t);

            var opPort = FloatPort(layerIndex);
            float op = opPort.connected()
                ? opPort.GetValue<float>()
                : (v < layerOpacities.Count ? layerOpacities[v] : 1f);
            ops.Add(op);
        }

        if (texs.Count == 0)
        {
            ReleaseOutput();
            return true;
        }

        Texture firstTex = texs[0];
        var inputSize = new Vector2Int(firstTex.width, firstTex.height);
        if (inputSize != outputSize)
        {
            outputSize = inputSize;
            InitializeRenderTexture();
        }

        patternShader.SetInt("width", outputSize.x);
        patternShader.SetInt("height", outputSize.y);
        patternShader.SetInt("layerCount", texs.Count);

        // Bind every slot. Unused slots get a black texture and 0 opacity so stale shader state
        // from previous dispatches cannot affect the current output.
        Texture black = Texture2D.blackTexture;
        for (int i = 0; i < MaxLayers; i++)
        {
            string texName = "layer" + i;
            string opName = "opacity" + i;
            if (i < texs.Count)
            {
                patternShader.SetTexture(layerKernel, texName, texs[i]);
                patternShader.SetFloat(opName, Mathf.Clamp01(ops[i]));
            }
            else
            {
                patternShader.SetTexture(layerKernel, texName, black);
                patternShader.SetFloat(opName, 0f);
            }
        }
        patternShader.SetTexture(layerKernel, "outputTex", outputTex);

        DispatchKernel(layerKernel);
        outputTexKnob.SetValue(outputTex);
        return true;
    }

    private void DispatchKernel(int kernel)
    {
        uint tx, ty, tz;
        patternShader.GetKernelThreadGroupSizes(kernel, out tx, out ty, out tz);
        var threadGroupX = Mathf.CeilToInt(((float)outputSize.x) / tx);
        var threadGroupY = Mathf.CeilToInt(((float)outputSize.y) / ty);
        patternShader.Dispatch(kernel, threadGroupX, threadGroupY, 1);
    }
}
