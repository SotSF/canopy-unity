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

    public enum NodeStyle
    {
        SignalGenerator,
        SignalFilter,
        TextureGenerator,
        TextureFilter,
        Custom
    }

    public string nodeName;
    public NodeType nodeType;
    public NodeStyle style = NodeStyle.Custom;
    public Vector2 defaultSize = new Vector2(150, 100);
    public bool generateShader;
    public PatternParameter[] inputs;
    public PatternParameter[] outputs;

    private NodeStyle oldStyle = NodeStyle.Custom;
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
{
    // Declare a color which is solid red, return it.
    float4 result = float4(1,0,0,1);
    {1};
}
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
{

    public override string GetID => ""{0}+Node"";
    public override string Title { get { return ""{0}""; } }

    public override Vector2 DefaultSize { get { return new Vector2({2}, {3}); } }

    {4}

    {5}
    
    {6}

    public override void NodeGUI(){
        GUILayout.BeginVertical();
        GUILayout.EndVertical();

        if (GUI.changed)
            NodeEditor.curNodeCanvas.OnNodeChange(this);
    }

    public override bool Calculate()
    {
        return true;
    }
}
";
 
    [MenuItem("Tools/NodeSystem/Create new Node")]
    static void CreateWizard()
    {
        DisplayWizard<NodeWizard>("Create Node", "Create");
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
        code.Append("");
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

    void OnWizardUpdate()
    {
        helpString = "Create new node";

        PatternParameter sigInput=null, sigOutput=null, texInput=null, texOutput=null;
        // Add pattern parameters for a given set of styles.
        if (style != oldStyle)
        {
            oldStyle = style;
            switch (style)
            {
                case NodeStyle.SignalFilter:
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
                case NodeStyle.SignalGenerator:
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
                case NodeStyle.TextureFilter:
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
                case NodeStyle.TextureGenerator:
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
                case NodeStyle.Custom:
                    break;
            }
        }
        foreach (var param in inputs)
        {
            param.input = true;
        }
        foreach (var param in outputs)
        {
            param.input = false;
        }
    }
}