using UnityEngine;
using UnityEditor;
using System.Collections.Generic;
using sotsf.canopy.patterns;
using System.Text;
using System.Linq;
using System.Text.RegularExpressions;

/* Generates code for a new node & optional associated shader based on passed in parameters, 
 * thus reducing boilerplate. Relies heavily on StringBuilders + csharp string interpolation, ie the 
 * $"foo {bar.name}" form as well as multiline literals, ie the @"" form, and sometimes 
 * their combination, ie $@"". */
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
    [Tooltip("Whether the node is ticking (update per frame) or not (update only when inputs change)")]
    public NodeType nodeType;
    [Tooltip("What template for generating input/output ports to use. Filters take an input and produce an output; generators produce an output without an input.")]
    public NodeTemplate template = NodeTemplate.Custom;
    [Tooltip("How big the node should be by default")]
    public Vector2 defaultSize = new Vector2(150, 100);
    [Tooltip("Generate a matching ComputeShader? Usually used for rendering textures.")]
    public bool generateShader;
    [Header("Node inputs")]
    [Tooltip("Inputs to the node. Increase the count to add more. More can be added manually post-creation.")]
    public PatternParameter[] inputs;
    [Header("Node outputs")]
    [Tooltip("Outputs from the node. Increase the count to add more. More can be added manually post-creation.")]
    public PatternParameter[] outputs;

    private NodeTemplate oldTemplate = NodeTemplate.Custom;
    private string shaderDir = "Assets/Scripts/TextureSynthesis/Resources/NodeShaders/";
    private string nodeDir = "Assets/Scripts/TextureSynthesis/Nodes/";

    #region NodeGeneration

    private Dictionary<ParamType, string> paramCSharpTypes = new Dictionary<ParamType, string>() {
        { ParamType.BOOL,  "bool"},
        { ParamType.FLOAT, "float"},
        { ParamType.INT,   "int"},
        { ParamType.FLOAT4, "Vector4" },
        { ParamType.TEX,   "Texture"}
    };

    /* Generates the annotated I/O port declarations for the node*/
    string GenerateNodePorts()
    {
        StringBuilder portDecls = new StringBuilder();
        foreach (var input in inputs)
        {
            string dir = "In";
            string side = input.paramType == ParamType.TEX ? "Top" : "Left";
            string nodePortDecl = $@"
    [ValueConnectionKnob(""{input.name}"", Direction.{dir}, typeof({paramCSharpTypes[input.paramType]}), NodeSide.{side})]
    public ValueConnectionKnob {input.name}Knob;";
            portDecls.AppendLine(nodePortDecl);
        }
        foreach (var output in outputs)
        {
            string dir = "Out";
            string side = output.paramType == ParamType.TEX ? "Bottom" : "Right";
            string nodePortDecl = $@"
    [ValueConnectionKnob(""{output.name}"", Direction.{dir}, typeof({paramCSharpTypes[output.paramType]}), NodeSide.{side})]
    public ValueConnectionKnob {output.name}Knob;";
            portDecls.AppendLine(nodePortDecl);
        }
        return CollapseMultilines(portDecls.ToString());
    }

    private string shaderVarName = "patternShader";
    private string kernelVarName = "patternKernel";
    private string shaderKernelName = "PatternKernel";


    /* Generates the member variable declarations for the Node */
    string GenerateNodeVars()
    {
        StringBuilder varDecls = new StringBuilder();
        varDecls.AppendLine();
        if (generateShader)
        {
            varDecls.AppendLine($@"
    private ComputeShader {shaderVarName};
    private int {kernelVarName};");
        }
        if (hasTexOutputs)
        {
            varDecls.AppendLine("    private Vector2Int outputSize = Vector2Int.zero;");
        }
        foreach (var sliderInput in sliderInputs)
        {
            string initialVal = sliderInput.paramType == ParamType.INT ? sliderInput.defaultInt.ToString() : sliderInput.defaultFloat.ToString();
            varDecls.AppendLine($"    private {paramCSharpTypes[sliderInput.paramType]} {sliderInput.name} = {initialVal};");
        }
        foreach (var numericOut in numericOutputs)
        {
            varDecls.AppendLine($"    private {paramCSharpTypes[numericOut.paramType]} {numericOut.name};");
        }
        foreach (var texOut in texOutputs)
        {
            varDecls.AppendLine($"    public RenderTexture {texOut.name};");
        }
        return CollapseMultilines(varDecls.ToString());
    }

    /* Generates the node's Awake() method.
     * Only necessary if the node has a matching compute shader that must be located*/
    string GenerateNodeAwake()
    {
        string shaderPath = $"NodeShaders/{nodeName}{suffix}";
        string awake = generateShader ? $@"
    private void Awake(){{
        {shaderVarName} = Resources.Load<ComputeShader>(""{shaderPath}"");
        {kernelVarName} = {shaderVarName}.FindKernel(""{shaderKernelName}"");
    }}
" : "";
        StringBuilder texInitializations = new StringBuilder();
        foreach (var outTex in texOutputs)
        {

            texInitializations.AppendLine($@"
        if ({outTex.name} != null)
        {{
            {outTex.name}.Release();
        }}
        {outTex.name} = new RenderTexture(outputSize.x, outputSize.y, 0);
        {outTex.name}.enableRandomWrite = true;
        {outTex.name}.Create();");
        }
        string init = hasTexOutputs ? $@"
    private void InitializeRenderTexture()
    {{
        {texInitializations.ToString()}
    }}" : "";
        return CollapseMultilines(awake + init);
    }

    /* Generates the node's NodeGUI method */
    string GenerateNodeGUI()
    {
        // Layout input tex knobs across top with SetPosition()
        int j = 0;
        int margin = 20;
        StringBuilder inputTexKnobPlacements = new StringBuilder();
        if (hasTexInputs)
        {
            inputTexKnobPlacements.AppendLine();
            foreach (var inTex in texInputs)
            {
                int xOffset = margin + j * 40;
                string knobPlacement = $"        {inTex.name}Knob.SetPosition({xOffset});";
                inputTexKnobPlacements.AppendLine(knobPlacement);
                j++;
            }
        }

        // Layout output tex knobs across bottom with SetPosition()
        j = 0;
        StringBuilder outputTexKnobPlacements = new StringBuilder();
        if (hasTexOutputs)
        {
            outputTexKnobPlacements.AppendLine();
            foreach (var outTex in texOutputs)
            {
                string knobPlacement = $"        {outTex.name}Knob.SetPosition(DefaultSize.x-20);";
                outputTexKnobPlacements.AppendLine(knobPlacement);
                j++;
            }
        }

        // Show sliders for params which useRange when not connected to an input
        StringBuilder inputNumerics = new StringBuilder();
        foreach (var inputNum in sliderInputs)
        {
            string rangeMin = inputNum.paramType == ParamType.INT ? inputNum.minInt.ToString() : inputNum.minFloat.ToString();
            string rangeMax = inputNum.paramType == ParamType.INT ? inputNum.maxInt.ToString() : inputNum.maxFloat.ToString();
            string inputGUI = $@"
        {inputNum.name}Knob.DisplayLayout();
        if (!{inputNum.name}Knob.connected())
        {{
            {inputNum.name} = RTEditorGUI.Slider({inputNum.name}, {rangeMin}, {rangeMax});
        }} else
        {{
            {inputNum.name} = {inputNum.name}Knob.GetValue<{paramCSharpTypes[inputNum.paramType]}>();
        }}";
            inputNumerics.AppendLine(inputGUI);
        }
        int w = 64;
        int h = 64;
        StringBuilder outputTexVizBox = new StringBuilder();
        if (hasTexOutputs)
        {
            outputTexVizBox.AppendLine($@"GUILayout.FlexibleSpace();
        GUILayout.BeginHorizontal();
        GUILayout.FlexibleSpace();
        GUILayout.Box({texOutputs.First().name}, GUILayout.MaxWidth({w}), GUILayout.MaxHeight({h}));
        GUILayout.EndHorizontal();
        GUILayout.Space(4);");
        }

        string nodeGUI = $@"
    public override void NodeGUI()
    {{
        {inputTexKnobPlacements.ToString()}
        GUILayout.BeginVertical();
        {inputNumerics.ToString()}
        {outputTexVizBox.ToString()}
        GUILayout.EndVertical();
        {outputTexKnobPlacements.ToString()}
        if (GUI.changed)
            NodeEditor.curNodeCanvas.OnNodeChange(this);
    }}";
        return CollapseMultilines(nodeGUI);
    }

    /* Generates the node's Calculate() method */
    string GenerateCalculate()
    {
        // Generate the texture resetting code for filter-types where there is a tex input
        StringBuilder texInputGuards = new StringBuilder();
        var firstOut = hasTexOutputs ? texOutputs.First() : null;
        string outputReset = firstOut != null ? $@"
            if ({firstOut.name} != null)
                {firstOut.name}.Release();" : "";

        foreach (var inTex in texInputs)
        {
            texInputGuards.AppendLine($@"
        Texture {inTex.name} = {inTex.name}Knob.GetValue<Texture>();
        if (!{inTex.name}Knob.connected () || {inTex.name} == null)
        {{
            {firstOut.name}Knob.ResetValue();
            outputSize = Vector2Int.zero;
            {outputReset}
            return true;
        }}");
        }

        if (hasTexInputs && hasTexOutputs)
        {
            texInputGuards.AppendLine($@"        
        var inputSize = new Vector2Int({texInputs.First().name}.width, {texInputs.First().name}.height);
        if (inputSize != outputSize){{
            outputSize = inputSize;
            InitializeRenderTexture();
        }}");
        }

        // Generate the numeric assignments for slider+port controlled vars
        StringBuilder numericAssigns = new StringBuilder();
        foreach (var numericInput in sliderInputs)
        {
            string assign = $"        {numericInput.name} = {numericInput.name}Knob.connected() ? {numericInput.name}Knob.GetValue<{paramCSharpTypes[numericInput.paramType]}>(): {numericInput.name};";
            numericAssigns.AppendLine(assign);
        }

        // Bind vars to the shader (if it exists) and execute it
        string bindAndExecute = "";
        if (generateShader)
        {
            StringBuilder paramPasses = new StringBuilder();

            // Bind default height/width params
            if (hasTexOutputs)
            {
                paramPasses.AppendLine($"        {shaderVarName}.SetInt(\"width\", outputSize.x);");
                paramPasses.AppendLine($"        {shaderVarName}.SetInt(\"height\", outputSize.y);");
            }

            // Bind int/float params
            var passedNumerics = numericInputs.Where(i => i.passToShader);
            foreach (var param in passedNumerics)
            {
                string method = "SetInt";
                if (param.paramType == ParamType.FLOAT)
                    method = "SetFloat";
                string paramBinding = $"        {shaderVarName}.{method}(\"{param.name}\", {param.name});";
                paramPasses.AppendLine(paramBinding);
            }

            // Bind tex params
            var passedTextures = texInputs.Where(i => i.passToShader).ToList();
            passedTextures.AddRange(texOutputs.Where(o => o.passToShader));
            foreach (var param in passedTextures)
            {
                string texBinding = $"        {shaderVarName}.SetTexture({kernelVarName}, \"{param.name}\", {param.name});";
                paramPasses.AppendLine(texBinding);
            }
            bindAndExecute = $@"{paramPasses.ToString()}
        uint tx,ty,tz;
        patternShader.GetKernelThreadGroupSizes({kernelVarName}, out tx, out ty, out tz);
        var threadGroupX = Mathf.CeilToInt(((float)outputSize.x) / tx);
        var threadGroupY = Mathf.CeilToInt(((float)outputSize.y) / ty);
        {shaderVarName}.Dispatch({kernelVarName}, threadGroupX, threadGroupY, 1);";
        }

        // Assign node output values
        StringBuilder outputAssigns = new StringBuilder();
        foreach (var output in outputs)
        {
            outputAssigns.AppendLine($"        {output.name}Knob.SetValue({output.name});");
        }

        // Generate full method 
        string calculate = $@"
    public override bool Calculate()
    {{
{texInputGuards}
{numericAssigns.ToString()}
{bindAndExecute}
{outputAssigns.ToString()}
        return true;
    }}";
        return CollapseMultilines(calculate);
    }

    /* Removes the multiple empty lines left behind when a given section of code isn't generated */
    string CollapseMultilines(string input, int depth = 1)
    {
        switch (depth)
        {
            case 0:
                input = Regex.Replace(input, @"(\s*((\r\n)|(\n))){3}", "\r\n\r\n");
                break;
            case 1:
                input = Regex.Replace(input, @"(\s*((\r\n)|(\n))){2,}", "\r\n");
                break;

        }
        return input;
    }

    /* Generate the code representing the full Node file */
    string GenerateNodeFullCode()
    {
        string menuFolder = (!hasTexInputs && !hasTexOutputs) ? "Signal" : hasTexInputs ? "Filter" : "Pattern";
        string parentClass = nodeType == NodeType.TickingNode ? "TickingNode" : "Node";
        string fullNode =  $@"
using NodeEditorFramework;
using NodeEditorFramework.Utilities;
using SecretFire.TextureSynth;
using UnityEngine;

[Node(false, ""{menuFolder}/{nodeName}"")]
public class {nodeName}Node : {parentClass}
{{
    public override string GetID => ""{nodeName}Node"";
    public override string Title {{ get {{ return ""{nodeName}""; }} }}

    public override Vector2 DefaultSize {{ get {{ return new Vector2({defaultSize.x}, {defaultSize.y}); }} }}

    {GenerateNodePorts()}
    {GenerateNodeVars()}
    {GenerateNodeAwake()}
    {GenerateNodeGUI()}
    {GenerateCalculate()}
}}
";
        return CollapseMultilines(fullNode, 0);
    }
    #endregion

    #region ShaderGeneration
    // {0}: var name
    // {1}: optional RW declaration modifier for tex params
    private Dictionary<ParamType, string> paramShaderDecls = new Dictionary<ParamType, string>() {
        { ParamType.BOOL,  "bool {0};"},
        { ParamType.FLOAT, "float {0};"},
        { ParamType.INT,   "int {0};"},
        { ParamType.FLOAT4, "float4 {0};" },
        { ParamType.TEX,   "{1}Texture2D<float4> {0};"}
    };

    /* Generates a basic shader with the wired in parameters. */
    string GenerateShaderCode()
    {
        StringBuilder inputDecls = new StringBuilder();
        StringBuilder outputDecls = new StringBuilder();
        foreach (var input in inputs.Where(i => i.passToShader))
        {
            if (input.paramType == ParamType.TEX)
                inputDecls.AppendLine(string.Format(paramShaderDecls[input.paramType], input.name, ""));
            else
                inputDecls.AppendLine(string.Format(paramShaderDecls[input.paramType], input.name));
        }
        foreach (var output in outputs.Where(o => o.passToShader))
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
        return $@"
#pragma kernel PatternKernel

{inputDecls.ToString()}
{outputDecls.ToString()}
[numthreads(16, 16, 1)]
void PatternKernel(uint3 id : SV_DispatchThreadID)
{{
    // Declare a color which is solid red, return it.
    float4 result = float4(1,0,0,1);
    {returnDecl}
}}
";
    }
    #endregion

    /* Adds the Unity Tools menu item for the wizard*/
    [MenuItem("Tools/NodeSystem/Create new Node")]
    static void CreateWizard()
    {
        var wiz = DisplayWizard<NodeWizard>("Create Node", "Create", "Debug");
        wiz.inputs = new PatternParameter[0];
        wiz.outputs = new PatternParameter[0];
    }

    void OnWizardCreate()
    {
        if (nodeName == null || nodeName == "")
        {
            Debug.LogError("Node name is required!");
            return;
        }
        if (generateShader)
        {
            System.IO.File.WriteAllText($"{shaderDir}/{nodeName}{suffix}.compute", GenerateShaderCode());
        }
        var nodePath = $"{nodeDir}/{nodeName}Node.cs";
        System.IO.File.WriteAllText(nodePath, GenerateNodeFullCode());
        AssetDatabase.Refresh();
        AssetDatabase.SaveAssets();
        var nodeAsset = AssetDatabase.LoadAssetAtPath<Object>(nodePath);
        Selection.activeObject = nodeAsset;
        EditorGUIUtility.PingObject(nodeAsset);
    }

    private void OnWizardOtherButton()
    {
        if (generateShader)
            Debug.Log("Shader:\n\n" + GenerateShaderCode());
        Debug.Log("Node:\n\n" + GenerateNodeFullCode());
    }

    /* Properties for quick access to various types of input/outputs */
    IEnumerable<PatternParameter> texInputs => inputs.Where(i => i.paramType == ParamType.TEX);
    bool hasTexInputs => texInputs.Count() > 0;

    IEnumerable<PatternParameter> texOutputs => outputs.Where(o => o.paramType == ParamType.TEX);
    bool hasTexOutputs => texOutputs.Count() > 0;

    IEnumerable<PatternParameter> numericInputs => inputs.Where(i => i.paramType == ParamType.INT || i.paramType == ParamType.FLOAT);
    IEnumerable<PatternParameter> sliderInputs => numericInputs.Where(i => i.useRange);
    bool hasSliderInputs => sliderInputs.Count() > 0;

    IEnumerable<PatternParameter> numericOutputs => outputs.Where(o => o.paramType == ParamType.INT || o.paramType == ParamType.FLOAT);
    string suffix => hasTexInputs && hasTexOutputs ? "Filter" : "Pattern";

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
                    defaultSize = new Vector2(150, 100);
                    inputs = new PatternParameter[1];
                    outputs = new PatternParameter[1];
                    sigInput = new PatternParameter()
                    {
                        name = "inputSignal",
                        input = true,
                        paramType = ParamType.FLOAT,
                        useRange = true,
                        minFloat = 0,
                        maxFloat = 1,
                    };
                    sigOutput = new PatternParameter()
                    {
                        name = "outputSignal",
                        input = false,
                        paramType = ParamType.FLOAT,
                        useRange = false,
                    };
                    inputs[0] = sigInput;
                    outputs[0] = sigOutput;
                    generateShader = false;
                    break;
                case NodeTemplate.SignalGenerator:
                    defaultSize = new Vector2(150, 100);
                    inputs = new PatternParameter[0];
                    outputs = new PatternParameter[1];
                    sigOutput = new PatternParameter()
                    {
                        name = "outputSignal",
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
                    defaultSize = new Vector2(200, 200);
                    inputs = new PatternParameter[1];
                    outputs = new PatternParameter[1];
                    texInput = new PatternParameter()
                    {
                        name = "inputTex",
                        input = true,
                        passToShader = true,
                        paramType = ParamType.TEX,
                    };
                    texOutput = new PatternParameter()
                    {
                        name = "outputTex",
                        input = false,
                        passToShader = true,
                        paramType = ParamType.TEX,
                        useRange = false,
                    };
                    inputs[0] = texInput;
                    outputs[0] = texOutput;
                    generateShader = true;
                    break;
                case NodeTemplate.TextureGenerator:
                    defaultSize = new Vector2(200, 200);
                    inputs = new PatternParameter[0];
                    outputs = new PatternParameter[1];
                    texOutput = new PatternParameter()
                    {
                        name = "outputTex",
                        input = false,
                        passToShader = true,
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