
using NodeEditorFramework;
using NodeEditorFramework.Utilities;
using SecretFire.TextureSynth;

using System.Linq;

using UnityEngine;

[Node(false, "Filter/TextureMux")]
public class TextureMuxNode : TickingNode
{
    public override string GetID => "TextureMux";
    public override string Title { get { return "TextureMux"; } }

    public override Vector2 DefaultSize => new Vector2((2 + targetPortCount) * 90, 200);
    public override bool AutoLayout => true;

    [ValueConnectionKnob("outputTex", Direction.Out, typeof(Texture), NodeSide.Bottom)]
    public ValueConnectionKnob outputTexKnob;

    [ValueConnectionKnob("control", Direction.In, typeof(bool), NodeSide.Left)]
    public ValueConnectionKnob controlKnob;


    [ValueConnectionKnob("autoplay", Direction.In, typeof(bool), NodeSide.Left)]
    public ValueConnectionKnob autoplayKnob;

    public bool fade = true;
    public bool autoplay = false;
    // Time in seconds to autoplay cycle
    public float cycleTime = 150;
    public float cycleFadeTime = 2;

    private ComputeShader patternShader;
    private int fadeKernel;
    private Vector2Int outputSize = Vector2Int.zero;
    private RenderTexture outputTex;
    public RadioButtonSet mergeModeSelection;

    public int activeTextureIndex = 0;
    private int targetPortCount => activePortCount + 1;
    private int activePortCount => dynamicConnectionPorts.Where(port => port.connected()).Count();
    private int openPortIndex => activePortCount;

    private int lastTextureIndex;
    private float lastCycleTime;
    public float fadeBeginTime;
    private bool fading = false;

    private void Awake(){
        patternShader = Resources.Load<ComputeShader>("NodeShaders/TexMuxFade");
        fadeKernel = patternShader.FindKernel("FadeKernel");
        if (mergeModeSelection == null || mergeModeSelection.names.Count == 0)
        {
            mergeModeSelection = new RadioButtonSet(0, "Simple", "Layers");
        }
    }

    private void SetPortCount()
    {
        // Keep one open slot at the bottom of the input list
        // Adjust the active signal index if necessary
        if (dynamicConnectionPorts.Count > targetPortCount)
        {
            for (int i = 0; i < dynamicConnectionPorts.Count - 1; i++)
            {
                var port = (ValueConnectionKnob)dynamicConnectionPorts[i];
                if (!port.connected())
                {
                    DeleteConnectionPort(i);
                    if (activeTextureIndex > i)
                        activeTextureIndex--;
                    else if (activeTextureIndex == i)
                        activeTextureIndex = 0;
                }
            }
        }
        else if (dynamicConnectionPorts.Count < targetPortCount)
        {
            ValueConnectionKnobAttribute outKnobAttribs = new ValueConnectionKnobAttribute("Add input", Direction.In, typeof(Texture), NodeSide.Top);
            while (dynamicConnectionPorts.Count < targetPortCount)
                CreateValueConnectionKnob(outKnobAttribs);
        }
    }

    private void InitializeRenderTexture()
    {
        
        if (outputTex != null)
        {
            outputTex.Release();
        }
        outputTex = new RenderTexture(outputSize.x, outputSize.y, 0);
        outputTex.enableRandomWrite = true;
        outputTex.Create();

    }

    GUILayoutOption expandFalse = GUILayout.ExpandWidth(false);
    GUILayoutOption width50 = GUILayout.Width(50);
    GUILayoutOption width100 = GUILayout.Width(100);
    GUILayoutOption height50 = GUILayout.Height(50);
    public override void NodeGUI()
    {
        SetPortCount();

        // Top-to-bottom
        GUILayout.BeginVertical();

        GUILayout.BeginHorizontal();
        //Control inputs
        GUILayout.BeginVertical();
        GUILayout.Label("Control", expandFalse, width50);
        controlKnob.SetPosition();
        //controlKnob.DisplayLayout();
        GUILayout.Label("Autoplay", expandFalse, width50);
        autoplayKnob.SetPosition();
        fade = RTEditorGUI.Toggle(fade, "Fade?");
        GUILayout.EndVertical();
        // Input image ports left to right
        for (int i = 0; i < targetPortCount - 1; i++)
        {
            GUILayout.BeginVertical();
            var port = (ValueConnectionKnob)dynamicConnectionPorts[i];
            GUILayout.Space(4);
            GUILayout.Box(port.GetValue<Texture>(), width50, height50);
            //GUILayout.Label(string.Format("Tex {0}", i));
            if (i == activeTextureIndex)
            {
                GUILayout.Label("Active", width50);
            }
            else
            {
                if (GUILayout.Button("Activate", width50, expandFalse))
                {
                    lastTextureIndex = activeTextureIndex;
                    if (fade)
                    {
                        fadeBeginTime = Time.time;
                        fading = true;
                        lastCycleTime = Time.time;
                    }
                    activeTextureIndex = i;
                }
            }
            port.SetPosition();
            GUILayout.EndVertical();
        }
        GUILayout.Label("Add input", expandFalse, width50);
        ((ValueConnectionKnob)dynamicConnectionPorts[openPortIndex]).SetPosition();
        GUILayout.EndHorizontal();


        GUILayout.BeginHorizontal();

        //Autoplay button
        string label = autoplay ? "Stop autoplay" : "Start autoplay";
        if (GUILayout.Button(label, width100))
        {
            ToggleAutoplay();
        }

        // Output image below
        //GUILayout.Space(DefaultSize.x - 74);
        if (activePortCount > 0)
        {
            var port = (ValueConnectionKnob)dynamicConnectionPorts[activeTextureIndex];
            GUILayout.Box(outputTex, width50, height50);
            //GUILayout.Space(4);
        }
        outputTexKnob.SetPosition();
        GUILayout.EndHorizontal();

        GUILayout.EndVertical();

        if (GUI.changed)
            NodeEditor.curNodeCanvas.OnNodeChange(this);
    }

    private void NextImage()
    {
        lastTextureIndex = activeTextureIndex;
        activeTextureIndex = (activeTextureIndex + 1) % activePortCount;
        if (fade)
        {
            fadeBeginTime = Time.time;
            fading = true;
        }
        lastCycleTime = Time.time;
    }

    private void ToggleAutoplay()
    {
        autoplay = !autoplay;
        if (autoplay)
        {
            lastCycleTime = Time.time;
        }
    }

    Vector2Int inputSize = new Vector2Int(0, 0);
    public override bool Calculate()
    {
        if (targetPortCount > 1)
        {
            if ((autoplay && ((Time.time - lastCycleTime) > cycleTime)) || controlKnob.GetValue<bool>())
            {
                NextImage();
            }
            if (autoplayKnob.GetValue<bool>())
            {
                ToggleAutoplay();
            }

            var activePort = (ValueConnectionKnob)dynamicConnectionPorts[activeTextureIndex];
            Texture activeTex = activePort.GetValue<Texture>();
            if (activeTex != null)
            {
                inputSize.x = activeTex.width;
                inputSize.y = activeTex.height;
                if (inputSize != outputSize)
                {
                    outputSize = inputSize;
                    InitializeRenderTexture();
                }
            }
            if (fading && outputTex != null && patternShader != null)
            {
                var lastPort = (ValueConnectionKnob)dynamicConnectionPorts[lastTextureIndex];
                Texture lastTex = lastPort.GetValue<Texture>();
                patternShader.SetFloat("width", outputTex.width);
                patternShader.SetFloat("height", outputTex.height);

                patternShader.SetFloat("crossfader", (Time.time - lastCycleTime) / cycleFadeTime);
                patternShader.SetTexture(fadeKernel, "texL", lastTex);
                patternShader.SetTexture(fadeKernel, "texR", activeTex);
                patternShader.SetTexture(fadeKernel, "outputTex", outputTex);

                uint tx, ty, tz;
                patternShader.GetKernelThreadGroupSizes(fadeKernel, out tx, out ty, out tz);
                var threadGroupX = Mathf.CeilToInt(((float)outputSize.x) / tx);
                var threadGroupY = Mathf.CeilToInt(((float)outputSize.y) / ty);
                patternShader.Dispatch(fadeKernel, threadGroupX, threadGroupY, 1);
                if (Time.time - fadeBeginTime > cycleFadeTime)
                {
                    fading = false;
                }
            } else
            {
                Graphics.Blit(activeTex, outputTex);
            }
        }
        else
        {
            if (outputSize != Vector2Int.zero)
            {
                outputTexKnob.ResetValue();
                outputSize = Vector2Int.zero;

                if (outputTex != null)
                    outputTex.Release();
                return true;
            }
        }
        

        patternShader.SetInt("width", outputSize.x);
        patternShader.SetInt("height", outputSize.y);

        //switch (mergeModeSelection.Selected)
        //{
        //    case "Simple":
        //        crossfader = crossfaderKnob.connected() ? crossfaderKnob.GetValue<float>() : crossfader;
        //        patternShader.SetFloat("crossfader", crossfader);
        //        kernel = fadeKernel;
        //        break;
        //    case "Layers":
        //        kernel = layerKernel;
        //        break;
        //}

        //patternShader.SetTexture(kernel, "texL", texL);
        //patternShader.SetTexture(kernel, "texR", texR);
        //patternShader.SetTexture(kernel, "outputTex", outputTex);

        //uint tx,ty,tz;
        //patternShader.GetKernelThreadGroupSizes(kernel, out tx, out ty, out tz);
        //var threadGroupX = Mathf.CeilToInt(((float)outputSize.x) / tx);
        //var threadGroupY = Mathf.CeilToInt(((float)outputSize.y) / ty);
        //patternShader.Dispatch(kernel, threadGroupX, threadGroupY, 1);
        outputTexKnob.SetValue(outputTex);

        return true;
    }
}
