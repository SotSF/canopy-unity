using UnityEngine;
using UnityEditor;
using System.Collections.Generic;
using sotsf.canopy.patterns;
using System.Text;
using System.Linq;

public class NodeWizard : ScriptableWizard
{
    public enum NodeType
    {
        Node,
        TickingNode
    }

    public enum NodeTemplate
    {
        SignalGenerator,
        SignalFilter,
        TextureGenerator,
        TextureFilter,
        Custom
    }

    [Tooltip("The name the node will have.")]
    public string nodeName;

    [Tooltip("Whether the node is ticking (ie update per frame) or not (update only when inputs change)")]
    public NodeType nodeType;

    [Tooltip("What template for generating input/output ports to use.")]
    public NodeTemplate template = NodeTemplate.Custom;
    [Tooltip("How big the node should be by default")]
    public Vector2 defaultSize = new Vector2(150, 100);
    [Tooltip("Generate a matching ComputeShader?")]
    public bool generateShader;
    [Tooltip("Inputs to the node. Increase the count to add more.")]
    public PatternParameter[] inputs;
    [Tooltip("Outputs from the node. Increase the count to add more.")]
    public PatternParameter[] outputs;

    private NodeTemplate oldTemplate = NodeTemplate.Custom;
    private string patternDir = "Assets/PatternSystem/Patterns/";

    /* Source for the shader decls/body, to include wired-up parameters */

    // {0}: var name
    // {1}: optional RW declaration modifier for tex params
    private Dictionary<ParamType, string> paramShaderDecls = new Dictionary<ParamType, string>() {
        { ParamType.BOOL,  "bool {0};"},
        { ParamType.FLOAT, "float {0};"},
        { ParamType.INT,   "int {0};"},
        { ParamType.TEX,   "{1}Texture2D<float4> {0};"}
    };

    private Dictionary<ParamType, string> paramCSharpTypes = new Dictionary<ParamType, string>() {
        { ParamType.BOOL,  "bool"},
        { ParamType.FLOAT, "float"},
        { ParamType.INT,   "int"},
        { ParamType.TEX,   "Texture"}
    };


    // {0}: param declarations
    // {1}: return declaration
    private string shaderSource = @"
#pragma kernel PatternKernel

{0}

[numthreads(16, 16, 1)]
void PatternKernel(uint3 id : SV_DispatchThreadID)
{{
    // Declare a color which is solid red, return it.
    float4 result = float4(1,0,0,1);
    {1}
}}
";


    /* Source for the node decls/body, to include input/output ports etc */

    // {0}: nodeName
    // {1}: nodeType
    // {2}: size.x
    // {3}: size.y
    // {4}: portDeclarations
    // {5}: paramDeclarations
    // {6}: Awake/InitializeRenderTexture declaration: initialize pattern shader (resources.load)/kernel, optionally assign outputTex
    private string nodeSource = @"
using NodeEditorFramework;
using UnityEngine;

public class {0}Node : {1}
{{

    public override string GetID => ""{0}+Node"";
    public override string Title {{ get {{ return ""{0}""; }} }}

    public override Vector2 DefaultSize {{ get {{ return new Vector2({2}, {3}); }} }}

    {4}

    {5}
    
    {6}

    public override void NodeGUI(){{
        GUILayout.BeginVertical();
        GUILayout.EndVertical();

        if (GUI.changed)
            NodeEditor.curNodeCanvas.OnNodeChange(this);
    }}

    public override bool Calculate()
    {{
        return true;
    }}
}}
";
 
    [MenuItem("Tools/NodeSystem/Create new Node")]
    static void CreateWizard()
    {
        var wiz = DisplayWizard<NodeWizard>("Create Node", "Create", "Debug");
        wiz.inputs = new PatternParameter[0];
        wiz.outputs = new PatternParameter[0];
    }

    string GenerateNodeCode()
    {
        StringBuilder code = new StringBuilder();

        // {0}: param name
        // {1}: In/Out direction
        // {2}: C# param type (Texture, float, etc)
        // {3}: Node side (Bottom, Left, etc)
        StringBuilder inputPortDecls = new StringBuilder();
        foreach (var input in inputs)
        {
            string dir = "In";
            string side = input.paramType == ParamType.TEX ? "Top" : "Left";
            string nodePortDecl = $@"
            [ValueConnectionKnob(""{input.name}"", Direction.{dir}, typeof({paramCSharpTypes[input.paramType]}), NodeSide.{side})]
            public ValueConnectionKnob {input.name}{dir}Knob;";
            inputPortDecls.AppendLine(nodePortDecl);
        }
        foreach (var output in outputs)
        {
            string dir = "In";
            string side = output.paramType == ParamType.TEX ? "Top" : "Left";
            string nodePortDecl = $@"
            [ValueConnectionKnob(""{output.name}"", Direction.{dir}, typeof({paramCSharpTypes[output.paramType]}), NodeSide.{side})]
            public ValueConnectionKnob {output.name}{dir}Knob;";
            inputPortDecls.AppendLine(nodePortDecl);
        }
        code.Append(nodeSource);
        return code.ToString();
    }

    string GenerateShaderCode()
    {
        StringBuilder inputDecls = new StringBuilder();
        StringBuilder outputDecls = new StringBuilder();
        foreach (var input in inputs)
        {
            if (input.paramType == ParamType.TEX)
                inputDecls.AppendLine(string.Format(paramShaderDecls[input.paramType], input.name, ""));
            else
                inputDecls.AppendLine(string.Format(paramShaderDecls[input.paramType], input.name));
        }
        foreach (var output in outputs)
        {
            if (output.paramType == ParamType.TEX)
                outputDecls.AppendLine(string.Format(paramShaderDecls[output.paramType], output.name, "RW"));
            else
                outputDecls.AppendLine(string.Format(paramShaderDecls[output.paramType], output.name));
        }
        string returnDecl = "";
        var outputTextures = outputs.Where(p => p.paramType == ParamType.TEX);
        if (outputTextures != null && outputTextures.Count() > 0)
        {
            returnDecl = $"{outputTextures.First().name}[id.xy] = result;";
        }
        return string.Format(shaderSource, inputDecls.ToString() + outputDecls.ToString(), returnDecl);   
    }

    void OnWizardCreate()
    {
        if (nodeName == null || nodeName == "")
        {
            Debug.LogError("Node name is required!");
            return;
        }
        string sourceShaderFile = patternDir + "Rotate.compute";
        string destShaderFile = patternDir + "NewPattern.compute";

        if (generateShader)
        {
            System.IO.File.Copy(sourceShaderFile, destShaderFile, true);
        }
        AssetDatabase.Refresh();
        AssetDatabase.SaveAssets();
    }
    private void OnWizardOtherButton()
    {
        Debug.Log("Shader:");
        Debug.Log(GenerateShaderCode());
        Debug.Log("Node:");
        Debug.Log(GenerateNodeCode());
    }
    void OnWizardUpdate()
    {
        helpString = "Create new node";

        PatternParameter sigInput=null, sigOutput=null, texInput=null, texOutput=null;
        // Add pattern parameters for a given set of styles.
        if (template != oldTemplate)
        {
            oldTemplate = template;
            switch (template)
            {
                case NodeTemplate.SignalFilter:
                    inputs = new PatternParameter[1];
                    outputs = new PatternParameter[1];
                    sigInput = new PatternParameter()
                    {
                        name = "InputSignal",
                        input = true,
                        paramType = ParamType.FLOAT,
                        useRange = true,
                        minFloat = 0,
                        maxFloat = 1,
                    };
                    sigOutput = new PatternParameter()
                    {
                        name = "OutputSignal",
                        input = false,
                        paramType = ParamType.FLOAT,
                        useRange = false,
                    };
                    inputs[0] = sigInput;
                    outputs[0] = sigOutput;
                    generateShader = false;
                    break;
                case NodeTemplate.SignalGenerator:
                    inputs = new PatternParameter[0];
                    outputs = new PatternParameter[1];
                    sigOutput = new PatternParameter()
                    {
                        name = "OutputSignal",
                        input = true,
                        paramType = ParamType.FLOAT,
                        useRange = true,
                        minFloat = 0,
                        maxFloat = 1,
                    };
                    outputs[0] = sigOutput;
                    generateShader = false;
                    break;
                case NodeTemplate.TextureFilter:
                    inputs = new PatternParameter[1];
                    outputs = new PatternParameter[1];
                    texInput = new PatternParameter()
                    {
                        name = "InputTex",
                        input = true,
                        paramType = ParamType.TEX,
                    };
                    texOutput = new PatternParameter()
                    {
                        name = "OutputTex",
                        input = false,
                        paramType = ParamType.TEX,
                        useRange = false,
                    };
                    inputs[0] = texInput;
                    outputs[0] = texOutput;
                    generateShader = true;
                    break;
                case NodeTemplate.TextureGenerator:
                    inputs = new PatternParameter[0];
                    outputs = new PatternParameter[1];
                    texOutput = new PatternParameter()
                    {
                        name = "OutputTex",
                        input = false,
                        paramType = ParamType.TEX,
                        useRange = false,
                    };
                    outputs[0] = texOutput;
                    generateShader = true;
                    break;
                case NodeTemplate.Custom:
                    break;
            }
        }
        if (inputs != null) foreach (var param in inputs)
        {
            param.input = true;
        }
        if (outputs != null) foreach (var param in outputs)
        {
            param.input = false;
        }
    }
}