using UnityEngine;
using System.Collections;
using System.Linq;
using System;
using UnityEditor;

public class PatternManager : MonoBehaviour
{
    public Material canopyMaterial;

    public Light lightCaster;

    public ComputeShader basePatternShader;
    public Pattern basePatternComponent;

    public float period;
    public float cycles;
    public float brightness;

    const int FLOAT_BYTES = 4;
    const int VEC3_LENGTH = 3;

    [HideInInspector]
    public Vector3[] colorData;

    [HideInInspector]
    public ComputeBuffer dataBuffer;


    public Pattern activePattern;
    private Pattern lastPattern;
    private Pattern[] patterns;

    void Start()
    {
        // Initialize shader communication buffer
        dataBuffer = new ComputeBuffer(75*96, FLOAT_BYTES * VEC3_LENGTH);
        colorData = new Vector3[75 * 96];

        patterns = GetComponentsInChildren<Pattern>();
        SelectPattern(patterns[0]);
    }

    void SelectPattern(Pattern pattern)
    {
        if (pattern != null)
        {
            if (activePattern != null)
                activePattern.presenting = false;
            lastPattern = pattern;
            activePattern = pattern;
            var textures = canopyMaterial.GetTexturePropertyNames();
            foreach (string tex in textures)
            {
                canopyMaterial.SetTexture(tex, pattern.patternTexture);
            }
        }
        activePattern.presenting = true;
    }

    public void CreateNewPattern()
    {
        string patternDir = "Assets/PatternSystem/Patterns/";
        Shader displayShader = Shader.Find("PatternDisplayShaderGraph");
        Material material = new Material(displayShader);
        AssetDatabase.CreateAsset(material, patternDir+"PatternMaterials/NewPatternMaterial.mat");
        Pattern patternObj = Instantiate(basePatternComponent);
        string sourceShaderFile = patternDir+"Rotate.compute";
        string destShaderFile = patternDir+"NewPattern.compute";
        System.IO.File.Copy(sourceShaderFile, destShaderFile, true);
        AssetDatabase.Refresh();
        ComputeShader patternShader = AssetDatabase.LoadAssetAtPath<ComputeShader>(destShaderFile);
        patternObj.transform.SetParent(transform);
        patternObj.transform.localPosition = Vector3.zero;
        patternObj.patternShader = patternShader;
        patternObj.patternMaterial = material;
        patternObj.GetComponent<MeshRenderer>().sharedMaterial = material;
        patternObj.name = "NewPatternDisplay";
        AssetDatabase.SaveAssets();
        ArrangePatternDisplays();
    }

    public void ArrangePatternDisplays()
    {
        var patterns = GetComponentsInChildren<Pattern>();
        float theta = 0;
        Vector3 offset = 4.2f*Vector3.forward;
        for (int i = 0; i < patterns.Length; i++)
        {
            var pattern = patterns[i];
            pattern.transform.localPosition = Quaternion.Euler(0, theta, 0) * offset;
            theta += Mathf.Atan2(1.2f, offset.magnitude) * Mathf.Rad2Deg;

            //LookAt() leaves the display facing backwards, so flip it 180 afterwards
            pattern.transform.LookAt(transform);
            pattern.transform.rotation *= Quaternion.Euler(0, 180, 0);
        }
    }

    private void OnValidate()
    {
        if (lastPattern != activePattern)
        {
            SelectPattern(activePattern);
            Debug.Log("Selected " + activePattern);
        }
    }

    public void NextPattern()
    {
        var index = Array.IndexOf(patterns, activePattern);
        int next = (index + 1) % patterns.Length;
        SelectPattern(patterns[next]);
    }

    public void PreviousPattern()
    {
        var index = Array.IndexOf(patterns, activePattern);
        int next = (index - 1) % patterns.Length;
        SelectPattern(patterns[next]);
    }

    public void SetLightColor(Color color)
    {
        if (lightCaster != null)
        {
            lightCaster.color = Color.Lerp(lightCaster.color, color, 0.5f);
        }
    }

    void Update()
    {
        //period = 4 * (Mathf.Sin(Time.time)/2 + 2);
        if (Input.GetKeyDown(KeyCode.RightArrow))
        {
            NextPattern();
        }
        if (Input.GetKeyDown(KeyCode.LeftArrow))
        {
            PreviousPattern();
        }
        if (Input.GetMouseButtonUp(0))
        {
            var ray = Camera.main.ScreenPointToRay(Input.mousePosition);
            RaycastHit hit;
            Debug.Log("Mouse up, ray = " + ray);
            if (Physics.Raycast(ray, out hit))
            {
                SelectPattern(hit.transform.GetComponent<Pattern>());
                Debug.LogFormat("Hit: {0}", hit.transform.gameObject.name);
            }
        }
    }
}
