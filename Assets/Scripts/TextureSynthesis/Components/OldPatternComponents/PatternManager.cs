using UnityEngine;
using System.Collections;
using System.Linq;
using System;
using UnityEditor;
using UnityEngine.Networking;
using sotsf.canopy.patterns;

public class PatternManager : MonoBehaviour
{
    public static PatternManager instance { get; private set; }
    public Material canopyMaterial;

    private Light lightCaster;

    public Pattern basePatternComponent;

    public float period;
    public float cycles;
    [Range(-1,1)]
    public float hue;
    [Range(0, 1)]
    public float saturation;
    [Range(0, 1)]
    public float brightness;

    [HideInInspector]
    public float brightnessMod = 0;
    [HideInInspector]
    public bool pusherConnected;
    [HideInInspector]
    public bool highPerformance;

    const int FLOAT_BYTES = 4;
    const int VEC3_LENGTH = 3;

    public Pattern activePattern;
    private Pattern[] patterns;


    private void Awake()
    {
        instance = this;
    }

    void Start()
    {
        patterns = GetComponentsInChildren<Pattern>();
        //Invoke("ChooseRandomPattern", .1f);
        SelectPattern(patterns[0]);
        StartCoroutine(CheckForAPI());
        lightCaster = Canopy.instance.GetComponentInChildren<Light>();
    }
    public void ChooseRandomPattern()
    {
        System.Random rand = new System.Random();
        SelectPattern(patterns[rand.Next(patterns.Length)]);
    }
    public void SelectPattern(Pattern pattern)
    {
        if (pattern != null)
        {
            if (activePattern != null)
                activePattern.presenting = false;
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
        Pattern patternObj = Instantiate(basePatternComponent);
        string sourceShaderFile = patternDir+"Rotate.compute";
        string destShaderFile = patternDir+"NewPattern.compute";
        System.IO.File.Copy(sourceShaderFile, destShaderFile, true);
        AssetDatabase.Refresh();
        ComputeShader patternShader = AssetDatabase.LoadAssetAtPath<ComputeShader>(destShaderFile);
        patternObj.transform.SetParent(transform);
        patternObj.transform.localPosition = Vector3.zero;
        patternObj.transform.localScale = Vector3.one;
        patternObj.patternShader = patternShader;
        
        patternObj.name = "NewPattern";
        AssetDatabase.SaveAssets();
        ArrangePatternDisplays();
    }
#endif 
    public void ArrangePatternDisplays()
    {
        const int margin = 8;
        int ySpacing = Constants.NUM_STRIPS + margin;
        int xSpacing = Constants.PIXELS_PER_STRIP + margin;
        var patterns = GetComponentsInChildren<Pattern>();

        int currentY = 0;
        int currentX = 0;

        int maxY = 8;

        for (int i = 0; i < patterns.Length; i++)
        {
            var pattern = patterns[i];
            var rect = pattern.GetComponent<RectTransform>();
            rect.anchoredPosition = new Vector2(currentX * xSpacing, -currentY * ySpacing);
            if (currentY == maxY-1)
            {
                currentY = 0;
                currentX++;
            } else
            {
                currentY++;
            }
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
        bool wasConnected = false;
        float lastChecked = 0;
        while (true)
        {
            if (lastChecked > 2)
            {
                var req = new UnityWebRequest(pingEndpoint);
                yield return req.SendWebRequest();
                pusherConnected = req.responseCode == 200;
                if (pusherConnected != wasConnected)
                {
                    Debug.LogFormat("Canopy API {0}", pusherConnected ? "connected" : "not connected");
                    wasConnected = pusherConnected;
                }
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
