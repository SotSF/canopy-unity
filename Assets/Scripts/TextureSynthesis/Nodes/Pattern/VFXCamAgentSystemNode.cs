using NodeEditorFramework;
using NodeEditorFramework.Utilities;
using SecretFire.TextureSynth;

using System.Linq;

using UnityEngine;
using UnityEngine.VFX;
using UnityEngine.VFX.Utility;

[Node(false, "Pattern/VFXCamAgentSystem")]
public class VFXCamAgentSystemNode : TickingNode
{
    public override string GetID => "VFXCamAgentSystem";
    public override string Title { get { return "VFXCamAgentSystem"; } }
    private Vector2 _DefaultSize = new Vector2(200, 200);

    public override Vector2 DefaultSize => _DefaultSize;


    [ValueConnectionKnob("outputTex", Direction.Out, typeof(Texture), NodeSide.Bottom)]
    public ValueConnectionKnob outputTexKnob;

    [ValueConnectionKnob("inputTex", Direction.In, typeof(Texture), NodeSide.Top)]
    public ValueConnectionKnob inputTexKnob;
    ExposedProperty inputTexProp;

    [ValueConnectionKnob("emissionRate", Direction.In, typeof(float), NodeSide.Left)]
    public ValueConnectionKnob emissionRateKnob;
    public float emissionRate = 3;
    ExposedProperty emissionRateProp;

    [ValueConnectionKnob("sizeMultiplier", Direction.In, typeof(float), NodeSide.Left)]
    public ValueConnectionKnob sizeKnob;
    public float particleSize = 9;
    ExposedProperty sizeProp;

    [ValueConnectionKnob("vortexSpeed", Direction.In, typeof(float), NodeSide.Left)]
    public ValueConnectionKnob vortexSpeedKnob;
    public float vortexSpeed = 40;
    ExposedProperty vortexSpeedProp;

    [ValueConnectionKnob("rotationSpeed", Direction.In, typeof(float), NodeSide.Left)]
    public ValueConnectionKnob rotationSpeedKnob;
    public float rotationSpeed = 0.5f;
    ExposedProperty rotationSpeedProp;



    private Vector2Int outputSize = Vector2Int.zero;
    //private float speedFactor = 1;
    private RenderTexture outputTex;

    private Camera cam;
    private GameObject sceneObj;
    private VisualEffect effect;

    public override void DoInit()
    {
        //vfxPrefab = Resources.Load<Transform>("Prefabs/");
        sceneObj = GameObject.Find("VFXCam");
        cam = sceneObj.GetComponentsInChildren<Camera>().First();
        outputTex = cam.targetTexture;
        effect = sceneObj.GetComponentsInChildren<VisualEffect>().First();
        inputTexProp = "InputTex";
        emissionRateProp = "EmissionRate";
        sizeProp = "SizeMultiplier";
        vortexSpeedProp = "VortexSpeed";
        rotationSpeedProp = "RotationSpeed";
    }


    public override void NodeGUI()
    {
        GUILayout.BeginVertical();
        inputTexKnob.DisplayLayout();

        FloatKnobOrSlider(ref emissionRate, 0, 100, emissionRateKnob);
        FloatKnobOrSlider(ref particleSize, 0, 10, sizeKnob);
        FloatKnobOrSlider(ref vortexSpeed, -100, 100, vortexSpeedKnob);
        FloatKnobOrSlider(ref rotationSpeed, 0, 1, rotationSpeedKnob);

        GUILayout.FlexibleSpace();
        GUILayout.BeginHorizontal();
        GUILayout.FlexibleSpace();
        GUILayout.Box(outputTex, GUILayout.MaxWidth(64), GUILayout.MaxHeight(64));
        GUILayout.EndHorizontal();
        GUILayout.Space(4);

        GUILayout.EndVertical();

        outputTexKnob.SetPosition(180);

        if (GUI.changed)
            NodeEditor.curNodeCanvas.OnNodeChange(this);
    }

    public override bool DoCalc()
    {
        emissionRate = emissionRateKnob.connected() ? emissionRateKnob.GetValue<float>() : emissionRate;
        particleSize = sizeKnob.connected() ? sizeKnob.GetValue<float>() : particleSize;
        vortexSpeed = vortexSpeedKnob.connected() ? vortexSpeedKnob.GetValue<float>() : vortexSpeed;
        rotationSpeed = rotationSpeedKnob.connected() ? rotationSpeedKnob.GetValue<float>() : rotationSpeed;
        Texture tex = inputTexKnob.GetValue<Texture>();
        if (inputTexKnob.connected() && tex != null)
        {
            effect.SetTexture(inputTexProp, tex);
        }
        effect.SetFloat(emissionRateProp, emissionRate);
        effect.SetFloat(sizeProp, particleSize);
        effect.SetFloat(vortexSpeedProp, vortexSpeed);
        effect.SetFloat(rotationSpeedProp, rotationSpeed);
        outputTexKnob.SetValue(outputTex);
        return true;
    }
}
