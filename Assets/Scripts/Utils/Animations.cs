using UnityEngine;
using UnityEngine.UI;
using System.Collections;
using System;

namespace Lightsale.Animation
{
    //A frame delegate is run once per frame, with a value of i between 0 - 1.
    public delegate void FrameDelegate(float i);
    public class Animations
    {
        // Constrained from 0-1, returns a quickly-then-slowly decreasing value
        public static float Cubic(float k)
        {
            return 1f + ((k -= 1f) * k * k);
        }

        public static FrameDelegate LocalPositionLerp(Transform obj, Vector3 newPosition)
        {
            Vector3 startPosition = obj.localPosition;
            return (i) => { obj.localPosition = Vector3.Lerp(startPosition, newPosition, i); };
        }

        public static FrameDelegate LocalQuatLerp(Transform obj, Quaternion newRotation)
        {
            Quaternion startingRotation = obj.localRotation;
            return (i) => { obj.localRotation = Quaternion.Slerp(startingRotation, newRotation, i); };
        }

        public static FrameDelegate ScaleLerp(Transform obj, Vector3 newScale)
        {
            Vector3 startScale = obj.localScale;
            return (i) => { obj.localScale = Vector3.Lerp(startScale, newScale, i); };
        }

        public static FrameDelegate ScaleLerp(Transform obj, float newScale)
        {
            return ScaleLerp(obj, newScale * Vector3.one);
        }

        public static FrameDelegate ScaleFactorLerp(Transform obj, float scaleFactor)
        {
            return ScaleLerp(obj, scaleFactor * obj.localScale);
        }

        public static FrameDelegate PositionLerp(Transform obj, Vector3 newPosition)
        {
            Vector3 startPosition = obj.position;
            return (i) => { obj.position = Vector3.Lerp(startPosition, newPosition, i); };
        }

        public static FrameDelegate PointRotateLerp(Transform obj, Vector3 pivotPoint, Vector3 axis, float degrees)
        {
            return (i) => { obj.RotateAround(pivotPoint, axis, Mathf.LerpAngle(0, degrees, i)); };
        }

        public static FrameDelegate QuatLerp(Transform obj, Quaternion newRotation)
        {
            Quaternion startingRotation = obj.rotation;
            return (i) => { obj.rotation = Quaternion.Slerp(startingRotation, newRotation, i); };
        }

        public static FrameDelegate ColorLerp(SpriteRenderer sprite, Color newColor)
        {
            Color startingColor = sprite.color;
            return (i) => { sprite.color = Color.Lerp(startingColor, newColor, i); };
        }

        public static FrameDelegate ColorLerp(Image img, Color newColor)
        {
            Color startingColor = img.color;
            return (i) => { img.color = Color.Lerp(startingColor, newColor, i); };
        }

        public static FrameDelegate ColorLerp(Material material, Color newColor)
        {
            Color startingColor = material.GetColor("_EmissionColor");
            return (i) => { material.SetColor("_EmissionColor", Color.Lerp(startingColor, newColor, i)); };
        }

        public static FrameDelegate ColorLerp(Text text, Color newColor)
        {
            Color startingColor = text.color;
            return (i) => { text.color = Color.Lerp(startingColor, newColor, i); };
        }

        public static FrameDelegate CanvasGroupAlphaLerp(CanvasGroup cg, float newAlpha)
        {
            float startingAlpha = cg.alpha;
            return (i) => { cg.alpha = Mathf.Lerp(startingAlpha, newAlpha, i); };
        }

        public static IEnumerator SineOscillator(float period, params FrameDelegate[] anims)
        {
            while (true)
            {
                float i = 0.5f * Mathf.Sin(Time.time * 2 * Mathf.PI / period) + 0.5f;
                foreach (FrameDelegate anim in anims)
                {
                    anim(i);
                }
                yield return null;
            }
        }

        public static IEnumerator LinearTimedAnimator(float duration, params FrameDelegate[] anims)
        {
            for (float time = 0; time < duration; time += Time.deltaTime)
            {
                foreach (FrameDelegate anim in anims)
                {
                    anim(time / duration);
                }
                yield return null;
            }
            foreach (FrameDelegate anim in anims)
            {
                anim(1);
            }
        }

        public static IEnumerator CubicTimedAnimator(float duration, params FrameDelegate[] anims)
        {
            for (float time = 0; time < duration; time += Time.deltaTime)
            {
                foreach (FrameDelegate anim in anims)
                {
                    anim(Cubic(time / duration));
                }
                yield return null;
            }
            foreach (FrameDelegate anim in anims)
            {
                anim(1);
            }
        }

        public static FrameDelegate Reversed(FrameDelegate frameDelegate)
        {
            return (i) => { frameDelegate(1 - i); };
        }
    }

}