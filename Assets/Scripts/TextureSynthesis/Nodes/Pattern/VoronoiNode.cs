using System;
using System.Collections.Generic;
using System.Linq;
using NodeEditorFramework;
using NodeEditorFramework.TextureComposer;
using NodeEditorFramework.Utilities;
using SecretFire.TextureSynth;
using UnityEngine;
using UnityEngine.Video;
using Random = UnityEngine.Random;

[Node(false, "Pattern/Voronoi")]
public class VoronoiNode : TickingNode
{
    public const string ID = "voronoiNode";
    public override string GetID { get { return ID; } }
    public override string Title { get { return "Voronoi"; } }
    private Vector2 _DefaultSize = new Vector2(250, 250);

    public override Vector2 DefaultSize => _DefaultSize;

    [ValueConnectionKnob("Out", Direction.Out, typeof(Texture), NodeSide.Bottom, 40)]
    public ValueConnectionKnob textureOutputKnob;

    [ValueConnectionKnob("Speed", Direction.In, "Float")]
    public ValueConnectionKnob speedKnob;

    [ValueConnectionKnob("GravityForce", Direction.In, "Float")]
    public ValueConnectionKnob gravityKnob;

    [ValueConnectionKnob("RepulsionForce", Direction.In, "Float")]
    public ValueConnectionKnob repulsionKnob;

    private RenderTexture outputTex;
    private ComputeShader patternShader;
    private Vector2Int outputSize = new Vector2Int(128, 128);
    private int patternKernel;
    const short maxPoints = 64;
    private const float velocityFactor = 200.0f;
    private float[] pointBuffer = new float[2*maxPoints];
    public float speed;
    public float G = .0098f / velocityFactor;
    public float R = .0006f / velocityFactor;
    public bool useGravity = false;
    public bool useRepulsion = false;

    public class VoronoiPoint
    {
        public Vector2 position;
        public Vector2 velocity;
        public VoronoiPoint()
        { }
        public override string ToString()
        {
            return $"Position: ({position.x:0.00}, {position.y:0.00}) Velocity: ({velocity.x:0.000},{velocity.y:0.000})";
        }
    }
    private List<VoronoiPoint> points;

    public override void DoInit()
    {
        patternShader = Resources.Load<ComputeShader>("NodeShaders/VoronoiPattern");
        patternKernel = patternShader.FindKernel("PatternKernel");
        points = new List<VoronoiPoint>();
        InitializeRenderTexture();
        patternShader.SetTexture(patternKernel, "OutputTex", outputTex);
        SetRandomNormalizedPoints();
    }

    private void InitializeRenderTexture()
    {
        outputTex = new RenderTexture(outputSize.x, outputSize.y, 24);
        outputTex.enableRandomWrite = true;
        outputTex.Create();
    }

    public override void NodeGUI()
    {
        GUILayout.BeginVertical();
        GUILayout.BeginHorizontal();
        GUILayout.BeginVertical();
        FloatKnobOrSlider(ref speed, 0, 1, speedKnob);
        FloatKnobOrSlider(ref G, 0, 1, gravityKnob);
        FloatKnobOrSlider(ref R, 0, 1, repulsionKnob);
        GUILayout.EndVertical();
        GUILayout.BeginVertical();
        useGravity = RTEditorGUI.Toggle(useGravity, "Use gravity");
        useRepulsion = RTEditorGUI.Toggle(useRepulsion, "Use repulsion");
        GUILayout.EndVertical();
        GUILayout.EndHorizontal();
        GUILayout.Box(outputTex, GUILayout.MaxHeight(100));
        GUILayout.EndVertical();
        textureOutputKnob.DisplayLayout();
        if (GUI.changed)
            NodeEditor.curNodeCanvas.OnNodeChange(this);
    }

    private void SetRandomNormalizedPoints()
    {
        for (int i = 0; i < maxPoints; i++)
        {
            points.Add(new VoronoiPoint()
            {
                position = new Vector2(Random.value, Random.value),
                velocity = new Vector2(Random.Range(-1.0f,1.0f), Random.Range(-1.0f, 1.0f)) / velocityFactor
            });
        }
    }

    private void FillPointBuffer()
    {
        for (int i = 0; i < maxPoints; i++)
        {
            pointBuffer[2* i] = points[i].position.x;
            pointBuffer[2* i + 1] = points[i].position.y;
            i++;
        }
    }

    Vector2 midpoint = new Vector2(0.5f, 0.5f);
    private void MovePoints()
    {
        for (int i = 0; i < maxPoints; i++)
        {
            if (useGravity)
            {
                Vector2 centerVec = midpoint - points[i].position;
                points[i].velocity += centerVec * ( G * (1 / Vector2.SqrMagnitude(centerVec)));
            }
            if (useRepulsion)
            {
                for (int j = 0; j < maxPoints; j++)
                {
                    if (i == j) continue;
                    Vector2 thisToOther = points[j].position - points[i].position;
                    if (Vector2.SqrMagnitude(thisToOther) < 0.025f)
                    {
                        points[i].velocity += -thisToOther * (R * 1 / Vector2.SqrMagnitude(thisToOther));
                    }
                }
            }
            points[i].position += points[i].velocity * speed;
            //if (point.position.x > 1)
            //    point.position.x %= 1;
            //else if (point.position.x < 0)
            //    point.position.x += 1;
            //if (point.position.y > 1)
            //    point.position.y %= 1;
            //else if (point.position.y < 0)
            //    point.position.y += 1;
        }
    }

    public override bool DoCalc()
    {
        MovePoints();
        FillPointBuffer();
        if (speedKnob.connected())
        {
            speed = speedKnob.GetValue<float>();
        }
        patternShader.SetInt("width", outputTex.width);
        patternShader.SetInt("height", outputTex.height);
        patternShader.SetInt("numPoints", maxPoints);
        patternShader.SetFloats("points", pointBuffer);
        patternShader.SetTexture(patternKernel, "OutputTex", outputTex);
        uint tx, ty, tz;
        patternShader.GetKernelThreadGroupSizes(patternKernel, out tx, out ty, out tz);
        patternShader.Dispatch(patternKernel, Mathf.CeilToInt(outputTex.width / tx), Mathf.CeilToInt(outputTex.height / ty), 1);
        textureOutputKnob.SetValue(outputTex);
        return true;
    }
}