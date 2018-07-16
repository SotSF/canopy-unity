using UnityEngine;
using System.Collections;
using System.Linq;
using System;

public class PatternManager : MonoBehaviour
{
    public Material canopyMaterial;

    public Light lightCaster;

    public float period;
    public float cycles;

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

        var RingsPattern = transform.Find("RingsPattern").GetComponent<Pattern>();
        SelectPattern(RingsPattern);
        patterns = GetComponentsInChildren<Pattern>();
    }

    void SelectPattern(Pattern pattern)
    {
        if (pattern != null)
        {
            activePattern = pattern;
            var textures = canopyMaterial.GetTexturePropertyNames();
            foreach (string tex in textures)
            {
                canopyMaterial.SetTexture(tex, pattern.patternTexture);
            }
        }
    }

    private void OnValidate()
    {
        if (lastPattern != activePattern)
        {
            SelectPattern(activePattern);
            lastPattern = activePattern;
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
            lightCaster.color = Color.Lerp(lightCaster.color, color, 0.25f);
        }
    }

    void Update()
    {
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
