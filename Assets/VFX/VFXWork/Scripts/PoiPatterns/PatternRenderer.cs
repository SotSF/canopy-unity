using UnityEngine;
using System.Collections;

namespace Lightsale.Products.Smartsticks
{
    public class PatternRenderer : MonoBehaviour
    {
        LineRenderer lPath;
        LineRenderer rPath;
        PoiPatternSet pattern;
        public float lHandStartPhase;
        public float rHandStartPhase;
        public bool connectEnds = true;

        public float DrawAmount = 1;
        public int pointCount = 100;
        // Use this for initialization
        void Start()
        {
            DrawPattern(DrawAmount);
        }

        private void OnValidate()
        {
            DrawPattern(DrawAmount);
        }

        public void DrawPattern(float drawAmount = 1)
        {
            var paths = GetComponentsInChildren<LineRenderer>();
            lPath = paths[0];
            rPath = paths[1];
            Vector3[] lPoints = new Vector3[pointCount];
            Vector3[] rPoints = new Vector3[pointCount];
            float scale = 2;
            for (int i = 0; i < pointCount; i++)
            {
                float armLength = scale*6.5f;
                float poiLength = scale*5.0f;
                float t = (drawAmount *i) / (float)pointCount;
                rPoints[i] = pattern.leftPath.PositionAtTime(t+rHandStartPhase, armLength, poiLength);
                lPoints[i] = pattern.rightPath.PositionAtTime(t+lHandStartPhase, armLength, poiLength);
            }

            lPath.loop = connectEnds;
            rPath.loop = connectEnds;

            lPath.positionCount = pointCount;
            rPath.positionCount = pointCount;
            lPath.SetPositions(lPoints);
            rPath.SetPositions(rPoints);
        }
    }
}