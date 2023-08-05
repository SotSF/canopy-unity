using UnityEngine;
using System.Collections;
using System;
using System.Collections.Generic;

#if UNITY_EDITOR
using UnityEditor;
#endif

namespace Lightsale.Products.Smartsticks
{
    public class PoiPatternManager : MonoBehaviour
    {
        public static PoiPatternManager instance;
        private LineRenderer rightLines;
        private LineRenderer leftLines;
        public const float POVTrailTime = 0.24f;

        float oldperiod;
        float _period;
        public float period { get { return _period; } set {
                oldperiod = _period;
                _period = value;
                SetPathArmParameters();
            }
        }
        float _armLength;
        public float armLength { get { return _armLength; } set { _armLength = value; SetPathArmParameters(); } }
        float _poiLength;
        public float poiLength { get { return _poiLength; } set { _poiLength = value; SetPathArmParameters(); } }

        public Transform left;
        public Transform right;

        public PoiPatternSet poiPatternSet;

        public FirstOrderPoiPath leftPath;
        public FirstOrderPoiPath rightPath;

        public bool funTrails = false;

        public bool patternRunning = true;
        LineRenderer line;

        [HideInInspector]
        public string patternPath;
        private Coroutine SummoningRoutine;

#if UNITY_EDITOR

        public void UsePatternEditor(PoiPatternSet newPattern)
        {
            patternPath = AssetDatabase.GetAssetPath(newPattern);
            UsePoiPattern(newPattern);
        }

        private void OnValidate()
        {
            UsePatternEditor(poiPatternSet);
            SetPathArmParameters();
        }
#endif

        private void Awake()
        {
            instance = this;
            armLength = 0.5f;
            poiLength = 0.6f;
            oldperiod = 4;
            _period = 4;
            period = 4;
            rightLines = transform.Find("RightLines").GetComponent<LineRenderer>();
            leftLines = transform.Find("LeftLines").GetComponent<LineRenderer>();
        }

        private void SetPathArmParameters()
        {

            leftPath = poiPatternSet.leftPath;
            rightPath = poiPatternSet.rightPath;

            leftPath.armLength = armLength;
            rightPath.armLength = armLength;
            leftPath.poiLength = poiLength;
            rightPath.poiLength = poiLength;
            // offset new phase to match old phase in period
            // phase 0 - 1

            //t2 = (p2 * (x+t1)/p1)-x
            if (oldperiod != 0 && leftPath.period != period)
            {
                var newPhaseL = (period * (Time.time + leftPath.patternPhase) / oldperiod) - Time.time;
                var newPhaseR = (period * (Time.time + rightPath.patternPhase) / oldperiod) - Time.time;
                leftPath.patternPhase = newPhaseL;
                rightPath.patternPhase = newPhaseR;
            }
            leftPath.period = period;
            rightPath.period = period;
        }

        public void UsePoiPattern(PoiPatternSet newPattern)
        {
            patternRunning = true;
            poiPatternSet = newPattern;
            SetPathArmParameters();
        }

        void Update()
        {
            if (patternRunning)
            {
                List<Vector3> leftPoints = leftPath.UpdateTransform(left);
                List<Vector3> rightPoints = rightPath.UpdateTransform(right);

                leftPoints.Add(transform.position);
                rightPoints.Add(transform.position);

                leftLines.SetPositions(leftPoints.ToArray());
                rightLines.SetPositions(rightPoints.ToArray());
            }
        }
    }
}