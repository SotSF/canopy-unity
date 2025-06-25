using NodeEditorFramework;
using NodeEditorFramework.TextureComposer;
using SecretFire.TextureSynth;
using System.Linq;
using UnityEngine;

[Node(false, "Filter/Target Hue")]
public class TargetHueFilterNode : TextureSynthNode
{
    public const string ID = "targetHueFilterNode";
    public override string GetID { get { return ID; } }

    public override string Title { get { return "Target Hue Filter"; } }
    private Vector2 _DefaultSize = new Vector2(170, 100);

    public override Vector2 DefaultSize => _DefaultSize;

    [ValueConnectionKnob("Texture", Direction.In, typeof(Texture), NodeSide.Top, 20)]
    public ValueConnectionKnob textureInputKnob;

    [ValueConnectionKnob("Texture", Direction.Out, typeof(Texture), NodeSide.Bottom, 40)]
    public ValueConnectionKnob textureOutputKnob;

    [ValueConnectionKnob("Target Hue", Direction.In, "Float")]
    public ValueConnectionKnob targetHueKnob;

    public float targetHue = 0.5f; // Default to middle of hue wheel

    // Compute shader resources
    private ComputeShader hueShiftShader;
    private int analyzeKernelId;
    private int applyKernelId;

    // Buffers for analysis
    private ComputeBuffer bucketsBuffer;

    // Arrays for reading back data
    private uint[] buckets;

    // Render texture
    private RenderTexture outputTex;
    private Vector2Int outputSize = Vector2Int.zero;
    private Vector2Int inputSize;

    private const int HUE_BUCKETS = 10;

    public override void DoInit()
    {
        hueShiftShader = Resources.Load<ComputeShader>("NodeShaders/TargetHueFilter");
        analyzeKernelId = hueShiftShader.FindKernel("CSAnalyzeHue");
        applyKernelId = hueShiftShader.FindKernel("CSApplyHueShift");

        inputSize = new Vector2Int(0, 0);

        // Initialize analysis arrays
        buckets = new uint[HUE_BUCKETS];

        InitializeBuffers();
    }

    private void InitializeBuffers()
    {
        // Release existing buffers
        ReleaseBuffers();

        // Create compute buffers for analysis
        bucketsBuffer = new ComputeBuffer(HUE_BUCKETS, sizeof(uint));

        // Clear buffers
        ClearAnalysisBuffers();
    }

    private void ClearAnalysisBuffers()
    {
        // Clear the arrays
        System.Array.Clear(buckets, 0, HUE_BUCKETS);

        // Upload cleared data to GPU
        bucketsBuffer.SetData(buckets);
    }

    private void InitializeRenderTexture()
    {
        if (outputTex != null)
        {
            outputTex.Release();
        }
        outputTex = new RenderTexture(outputSize.x, outputSize.y, 24);
        outputTex.enableRandomWrite = true;
        outputTex.Create();
    }

    private void ReleaseBuffers()
    {
        bucketsBuffer?.Release();
    }

    public override void NodeGUI()
    {
        GUILayout.BeginVertical();
        textureInputKnob.DisplayLayout();
        FloatKnobOrSlider(ref targetHue, 0, 1, targetHueKnob);
        textureOutputKnob.DisplayLayout();
        GUILayout.EndVertical();

        if (GUI.changed)
            NodeEditor.curNodeCanvas.OnNodeChange(this);
    }

    public override bool DoCalc()
    {
        Texture tex = textureInputKnob.GetValue<Texture>();
        if (!textureInputKnob.connected() || tex == null)
        {
            // Reset outputs if no texture is available
            if (outputTex != null)
                outputTex.Release();
            textureOutputKnob.ResetValue();
            outputSize = Vector2Int.zero;
            return true;
        }

        inputSize.x = tex.width;
        inputSize.y = tex.height;
        if (inputSize != outputSize)
        {
            outputSize = inputSize;
            InitializeRenderTexture();
        }

        // Get target hue from knob if connected
        if (targetHueKnob.connected())
        {
            targetHue = targetHueKnob.GetValue<float>();
        }

        // Clear analysis buffers for fresh analysis
        ClearAnalysisBuffers();

        // Calculate thread groups
        var threadGroupX = Mathf.CeilToInt(tex.width / 16.0f);
        var threadGroupY = Mathf.CeilToInt(tex.height / 16.0f);

        // STEP 1: Analyze hue distribution
        hueShiftShader.SetTexture(analyzeKernelId, "InputTex", tex);
        hueShiftShader.SetInts("TextureSize", tex.width, tex.height);
        hueShiftShader.SetBuffer(analyzeKernelId, "GlobalHueBuckets", bucketsBuffer);

        hueShiftShader.Dispatch(analyzeKernelId, threadGroupX, threadGroupY, 1);

        // STEP 2: CPU analysis - read back results and find dominant hue
        bucketsBuffer.GetData(buckets);

        // Find the most populous hue buckets and calculate weighted average
        // Sort buckets by count (simple bubble sort for small array)
        var bucketIndices = new int[HUE_BUCKETS];
        for (int i = 0; i < HUE_BUCKETS; i++) bucketIndices[i] = i;

        // Sort indices by bucket counts (descending)
        for (int i = 0; i < HUE_BUCKETS - 1; i++)
        {
            for (int j = 0; j < HUE_BUCKETS - 1 - i; j++)
            {
                if (buckets[bucketIndices[j]] < buckets[bucketIndices[j + 1]])
                {
                    int temp = bucketIndices[j];
                    bucketIndices[j] = bucketIndices[j + 1];
                    bucketIndices[j + 1] = temp;
                }
            }
        }

        // Calculate weighted average of top 3 buckets (or fewer if they have zero count)
        float totalWeight = 0;
        float weightedHueSum = 0;
        int bucketsToConsider = Mathf.Min(3, HUE_BUCKETS);

        for (int i = 0; i < bucketsToConsider; i++)
        {
            int bucketIndex = bucketIndices[i];
            uint count = buckets[bucketIndex];

            if (count == 0) break; // No more populated buckets

            float bucketCenter = (bucketIndex + 0.5f) * 0.1f;
            float weight = count;

            // Handle hue wraparound for averaging (e.g., averaging 0.05 and 0.95 should give 0.0, not 0.5)
            if (i > 0)
            {
                float prevHue = weightedHueSum / totalWeight;
                float diff = bucketCenter - prevHue;

                // If difference > 0.5, the bucket is on the "other side" of the hue circle
                if (diff > 0.5f) bucketCenter -= 1.0f;
                else if (diff < -0.5f) bucketCenter += 1.0f;
            }

            weightedHueSum += bucketCenter * weight;
            totalWeight += weight;
        }

        float dominantHue = totalWeight > 0 ? weightedHueSum / totalWeight : 0.5f;

        // Ensure dominant hue is in [0, 1) range
        if (dominantHue < 0) dominantHue += 1.0f;
        if (dominantHue >= 1.0f) dominantHue -= 1.0f;

        // Debug output
        int primaryBucket = bucketIndices[0];
        uint maxCount = buckets[primaryBucket];
        //Debug.Log($"Target: {targetHue:F3}, Dominant: {dominantHue:F3}, Primary Bucket: {primaryBucket}, Count: {maxCount}");


        // Calculate hue shift, handling wraparound properly
        float hueShift = targetHue - dominantHue;

        // Choose the shorter path around the hue circle
        if (hueShift > 0.5f)
            hueShift -= 1.0f;
        else if (hueShift < -0.5f)
            hueShift += 1.0f;

        //Debug.Log($"Calculated shift: {hueShift:F3}");

        // STEP 3: Apply the hue shift
        hueShiftShader.SetFloat("CalculatedHueShift", hueShift);
        hueShiftShader.SetTexture(applyKernelId, "InputTex", tex);
        hueShiftShader.SetTexture(applyKernelId, "OutputTex", outputTex);
        hueShiftShader.SetInts("TextureSize", tex.width, tex.height);

        hueShiftShader.Dispatch(applyKernelId, threadGroupX, threadGroupY, 1);

        // Assign output
        textureOutputKnob.SetValue(outputTex);

        return true;
    }
}