using NodeEditorFramework;
using NodeEditorFramework.Utilities;
using SecretFire.TextureSynth;
using UnityEngine;

/// <summary>
/// Rotates a texture about its center — the angular counterpart to Pan/Offset. In Angle
/// mode the input is the rotation in degrees; in Angular velocity mode it's degrees per
/// second, integrated each frame. Rotation happens in pixel space, so non-square textures
/// turn without skewing. Points that land outside the source after rotating follow the
/// edge mode: mirror / repeat / clamp the source, or transparent.
/// </summary>
[Node(false, "Filter/Rotate")]
public class RotateNode : TickingNode
{
    public override string GetID => "RotateNode";
    public override string Title => "Rotate";

    private Vector2 _DefaultSize = new Vector2(200, 230);
    public override Vector2 DefaultSize => _DefaultSize;

    [ValueConnectionKnob("In", Direction.In, typeof(Texture), NodeSide.Top, 100)]
    public ValueConnectionKnob textureInputKnob;

    [ValueConnectionKnob("Out", Direction.Out, typeof(Texture), NodeSide.Bottom, 100)]
    public ValueConnectionKnob textureOutputKnob;

    [ValueConnectionKnob("Angle", Direction.In, typeof(float))]
    public ValueConnectionKnob rotationKnob;

    [ValueConnectionKnob("Reset", Direction.In, typeof(bool))]
    public ValueConnectionKnob resetKnob;

    const string AngleMode = "Angle";
    const string VelocityMode = "Angular velocity";
    const float MaxVelocity = 360f; // slider range, degrees per second

    public RadioButtonSet rotationMode;
    public RadioButtonSet edgeMode;
    public bool smooth = true;
    public float input;     // degrees (Angle) or degrees/sec (Angular velocity)
    public float angle;     // current rotation in degrees, restored on canvas load

    private ComputeShader rotateShader;
    private int rotateKernel;
    private RenderTexture outputTex;
    private int lastIntegratedFrame = -1;

    GUIContent smoothLabel = new GUIContent("Smooth", "Bilinear filtering; off samples the nearest source pixel");
    GUILayoutOption previewWidth = GUILayout.MaxWidth(64);
    GUILayoutOption previewHeight = GUILayout.MaxHeight(64);

    public override void DoInit()
    {
        rotateShader = Resources.Load<ComputeShader>("NodeShaders/RotateFilter");
        rotateKernel = rotateShader.FindKernel("Rotate");
        EnsureSettings();
    }

    // NodeGUI can draw before DoInit (edit mode), so both paths make sure the sets exist
    void EnsureSettings()
    {
        if (rotationMode == null || rotationMode.names == null || rotationMode.names.Count == 0)
        {
            rotationMode = new RadioButtonSet(0, AngleMode, VelocityMode);
        }
        if (edgeMode == null || edgeMode.names == null || edgeMode.names.Count == 0)
        {
            edgeMode = new RadioButtonSet(2, "Mirror", "Repeat", "Clamp", "Transparent");
        }
    }

    private void OnDestroy()
    {
        if (outputTex != null)
        {
            outputTex.Release();
            outputTex = null;
        }
    }

    public override void NodeGUI()
    {
        EnsureSettings();
        bool velocity = rotationMode.Selected == VelocityMode;
        rotationKnob.name = velocity ? "Speed" : "Angle";

        GUILayout.BeginVertical();

        GUILayout.BeginHorizontal();
        GUILayout.Space(4);
        GUILayout.BeginVertical();
        GUILayout.Label("Mode");
        RadioButtons(rotationMode);
        // Spaced off: the toggle draws like the radio buttons and read as a third mode
        GUILayout.Space(8);
        smooth = RTEditorGUI.Toggle(smooth, smoothLabel);
        GUILayout.EndVertical();
        GUILayout.FlexibleSpace();
        GUILayout.BeginVertical();
        GUILayout.Label("Edges");
        RadioButtons(edgeMode);
        GUILayout.EndVertical();
        GUILayout.Space(4);
        GUILayout.EndHorizontal();

        if (velocity)
        {
            FloatKnobOrSlider(ref input, -MaxVelocity, MaxVelocity, rotationKnob);
        }
        else
        {
            FloatKnobOrSlider(ref input, 0f, 360f, rotationKnob);
        }

        GUILayout.BeginHorizontal();
        GUILayout.BeginVertical();
        GUILayout.Label($"Angle: {angle:0.0}°");
        if (EventKnobOrButton("Reset", resetKnob))
        {
            ResetRotation();
        }
        GUILayout.EndVertical();
        GUILayout.FlexibleSpace();
        GUILayout.Box(outputTex, previewWidth, previewHeight);
        GUILayout.EndHorizontal();

        GUILayout.EndVertical();

        if (GUI.changed)
            NodeEditor.curNodeCanvas.OnNodeChange(this);
    }

    void ResetRotation()
    {
        angle = 0f;
        input = 0f;
    }

    void UpdateAngle()
    {
        if (float.IsNaN(input) || float.IsInfinity(input))
        {
            input = 0f;
        }
        if (rotationMode.Selected == VelocityMode)
        {
            // OnNodeChange can re-run Calculate within a frame: integrate once per frame
            if (Time.frameCount != lastIntegratedFrame)
            {
                lastIntegratedFrame = Time.frameCount;
                angle += input * Time.deltaTime;
            }
        }
        else
        {
            angle = input;
        }
        // Keep the accumulated angle small so float precision doesn't decay over long spins
        angle = Mathf.Repeat(angle, 360f);
    }

    int WrapModeIndex()
    {
        switch (edgeMode.Selected)
        {
            case "Mirror": return 0;
            case "Repeat": return 1;
            case "Transparent": return 3;
            default: return 2;
        }
    }

    public override bool DoCalc()
    {
        EnsureSettings();
        Texture tex = textureInputKnob.GetValue<Texture>();
        if (!textureInputKnob.connected() || tex == null)
        {
            textureOutputKnob.ResetValue();
            return true;
        }

        if (resetKnob.connected() && resetKnob.GetValue<bool>())
        {
            ResetRotation();
        }
        if (rotationKnob.connected())
        {
            input = rotationKnob.GetValue<float>();
        }
        UpdateAngle();

        if (outputTex == null || outputTex.width != tex.width || outputTex.height != tex.height)
        {
            if (outputTex != null) outputTex.Release();
            outputTex = new RenderTexture(tex.width, tex.height, 0);
            outputTex.enableRandomWrite = true;
            outputTex.Create();
        }

        float radians = angle * Mathf.Deg2Rad;
        rotateShader.SetInt("width", tex.width);
        rotateShader.SetInt("height", tex.height);
        rotateShader.SetFloats("rotation", Mathf.Cos(radians), Mathf.Sin(radians));
        rotateShader.SetInt("wrapMode", WrapModeIndex());
        rotateShader.SetBool("smooth", smooth);
        rotateShader.SetTexture(rotateKernel, "InputTex", tex);
        rotateShader.SetTexture(rotateKernel, "OutputTex", outputTex);
        rotateShader.Dispatch(rotateKernel, Mathf.CeilToInt(tex.width / 16f), Mathf.CeilToInt(tex.height / 16f), 1);

        textureOutputKnob.SetValue(outputTex);
        return true;
    }
}
