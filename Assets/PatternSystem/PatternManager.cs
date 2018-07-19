using UnityEngine;
using System.Collections;
using System.Linq;
using System;
using UnityEditor;
using UnityEngine.Networking;

public class PatternManager : MonoBehaviour
{
    public static PatternManager instance { get; private set; }
    public Material canopyMaterial;

    public Light lightCaster;

    public Pattern basePatternComponent;

    public float period;
    public float cycles;
    public float brightness;

    [HideInInspector]
    public float brightnessMod = 0;
    [HideInInspector]
    public bool pusherConnected;

    const int FLOAT_BYTES = 4;
    const int VEC3_LENGTH = 3;

    public Pattern activePattern;
    private Pattern lastPattern;
    private Pattern[] patterns;

    private void Awake()
    {
        instance = this;
    }

    void Start()
    {
        patterns = GetComponentsInChildren<Pattern>();
        Invoke("ChooseRandomPattern", .1f);
        StartCoroutine(CheckForAPI());
    }
    public void ChooseRandomPattern()
    {
        System.Random rand = new System.Random();
        SelectPattern(patterns[rand.Next(patterns.Length)]);
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

#if UNITY_EDITOR
    public void CreateNewPattern()
    {
        string patternDir = "Assets/PatternSystem/Patterns/";
        Shader displayShader = Shader.Find("PatternDisplayShaderGraph");
        Pattern patternObj = Instantiate(basePatternComponent);
        string sourceShaderFile = patternDir+"Rotate.compute";
        string destShaderFile = patternDir+"NewPattern.compute";
        System.IO.File.Copy(sourceShaderFile, destShaderFile, true);
        AssetDatabase.Refresh();
        ComputeShader patternShader = AssetDatabase.LoadAssetAtPath<ComputeShader>(destShaderFile);
        patternObj.transform.SetParent(transform);
        patternObj.transform.localPosition = Vector3.zero;
        patternObj.patternShader = patternShader;

        patternObj.name = "NewPatternDisplay";
        AssetDatabase.SaveAssets();
        ArrangePatternDisplays();
    }
#endif 
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

    public void NextPattern()
    {
        var index = Array.IndexOf(patterns, activePattern);
        int next = (index + 1) % patterns.Length;
        SelectPattern(patterns[next]);
    }

    public void PreviousPattern()
    {
        var index = Array.IndexOf(patterns, activePattern);
        int next = index == 0 ? patterns.Length-1 : (index - 1) % patterns.Length;
        SelectPattern(patterns[next]);
    }

    public void SetLightColor(Color color)
    {
        if (lightCaster != null)
        {
            lightCaster.color = Color.Lerp(lightCaster.color, color, 0.5f);
        }
    }

    IEnumerator CheckForAPI()
    {
        Uri pingEndpoint = new Uri("http://localhost:8080/api/ping");
        float lastChecked = 0;
        while (true)
        {
            if (lastChecked > 2)
            {
                var req = new UnityWebRequest(pingEndpoint);
                yield return req.SendWebRequest();
                pusherConnected = req.responseCode == 200;
                Debug.LogFormat("Canopy API {0}", pusherConnected ? "connected" : "not connected");
                lastChecked = 0;
            }
            lastChecked += Time.deltaTime;
            yield return null;
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
            if (Physics.Raycast(ray, out hit))
            {
                SelectPattern(hit.transform.GetComponent<Pattern>());
            }
        }
        brightnessMod = Mathf.Abs(Input.GetAxis("BrightnessMod")) * (1 - brightness);
    }
}
