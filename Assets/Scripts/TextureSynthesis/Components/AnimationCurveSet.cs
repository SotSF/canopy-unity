using UnityEngine;
using System.Collections;
using System.Collections.Generic;

public class AnimationCurveSet : MonoBehaviour
{
    public static AnimationCurveSet instance;
    public List<AnimationCurve> curves;

    private void Awake()
    {
        instance = this;
    }

    // Update is called once per frame
    void Update()
    {

    }
}
