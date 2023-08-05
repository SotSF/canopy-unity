using System;
using System.Collections.Generic;
using UnityEngine;

namespace Lightsale.Products.Smartsticks
{
    public enum Spin
    {
        InSpin,
        AntiSpin
    }

    public enum Timing
    {
        Same,
        Quarter,
        Split
    }

    public enum Direction
    {
        Clockwise,
        Anticlockwise
    }

    [System.Serializable]
    public abstract class PoiPath
    {
        [Range(0, 1)]
        public float armPhase;
        [Range(0, 1)]
        public float poiPhase;
        public float patternPhase = 0;

        [HideInInspector]
        public float period;
        public int count;
        public abstract List<Vector3> UpdateTransform(Transform transform);
    }

    [System.Serializable]
    public class FirstOrderPoiPath : PoiPath
    {
        [HideInInspector]
        public float armLength;
        [HideInInspector]
        public float poiLength;

        public Direction direction;
        public Spin spin;

        public Vector3 PositionAtTime(float t, float armLength, float poiLength)
        {
            var handRadians = 2 * Mathf.PI * (t + armPhase);
            if (direction == Direction.Clockwise)
            {
                handRadians *= -1;
            }
            var handX = armLength * Mathf.Cos(handRadians);
            var handY = armLength * Mathf.Sin(handRadians);

            var poiRadians = 2 * Mathf.PI * (t*count + poiPhase);
            if (direction == Direction.Clockwise && spin == Spin.InSpin || direction == Direction.Anticlockwise && spin == Spin.AntiSpin)
            {
                poiRadians *= -1;
            }
            var poiX = handX + poiLength * Mathf.Cos(poiRadians);
            var poiY = handY + poiLength * Mathf.Sin(poiRadians);

            return new Vector3(poiX, poiY);
        }

        public override List<Vector3> UpdateTransform(Transform transform)
        {
            var handRadians = 2 * Mathf.PI * ((Time.time+patternPhase) / period + armPhase);
            if (direction == Direction.Clockwise)
            {
                handRadians *= -1;
            }
            var handX = armLength * Mathf.Cos(handRadians);
            var handY = armLength * Mathf.Sin(handRadians);

            Vector3 handPosition = transform.parent.position + new Vector3(handX, handY);

            var poiRadians = 2 * Mathf.PI * ((Time.time+patternPhase) / (period / count) + poiPhase);
            if (direction == Direction.Clockwise && spin == Spin.InSpin || direction == Direction.Anticlockwise && spin == Spin.AntiSpin)
            {
                poiRadians *= -1;
            }
            var poiX = handX + poiLength * Mathf.Cos(poiRadians);
            var poiY = handY + poiLength * Mathf.Sin(poiRadians);

            transform.localPosition = new Vector3(poiX, poiY);

            transform.LookAt(handPosition);

            return new List<Vector3>() {
                transform.position,
                handPosition,
                handPosition,
                handPosition,
            };
        }
    }
}