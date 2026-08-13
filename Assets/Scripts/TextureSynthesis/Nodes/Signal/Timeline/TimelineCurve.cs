using System;
using System.Collections.Generic;
using UnityEngine;

namespace SecretFire.TextureSynth.Timeline
{
    [Serializable]
    public class CurveKey
    {
        public float time;
        public float value;
        public float inTangent;
        public float outTangent;
        // Broken handles: in/out tangents move independently, giving a sharp corner at the key
        public bool broken;

        public CurveKey() { }

        public CurveKey(float time, float value, float inTangent, float outTangent)
        {
            this.time = time;
            this.value = value;
            this.inTangent = inTangent;
            this.outTangent = outTangent;
        }
    }

    [Serializable]
    public class TimelineCurve
    {
        public const float MinKeySpacing = 0.001f;

        // Invariant: sorted ascending by time, neighbors >= MinKeySpacing apart.
        public List<CurveKey> keys = new List<CurveKey>();

        public TimelineCurve() { }

        public int KeyCount
        {
            get { return keys != null ? keys.Count : 0; }
        }

        public CurveKey GetKey(int index)
        {
            if (keys == null || index < 0 || index >= keys.Count)
            {
                return null;
            }
            return keys[index];
        }

        // Empty curve evaluates to 1 (identity for modulation). Zero allocation.
        public float Evaluate(float time)
        {
            if (keys == null)
            {
                return 1f;
            }
            int count = keys.Count;
            if (count == 0 || float.IsNaN(time))
            {
                return 1f;
            }
            CurveKey first = keys[0];
            if (first == null)
            {
                return 1f;
            }
            if (count == 1 || time <= first.time)
            {
                return first.value;
            }
            CurveKey last = keys[count - 1];
            if (last == null)
            {
                return 1f;
            }
            if (time >= last.time)
            {
                return last.value;
            }
            // time is strictly inside (first.time, last.time); find the containing segment.
            for (int i = 0; i < count - 1; i++)
            {
                CurveKey k0 = keys[i];
                CurveKey k1 = keys[i + 1];
                if (k0 == null || k1 == null)
                {
                    return 1f;
                }
                if (time > k1.time)
                {
                    continue;
                }
                float dt = k1.time - k0.time;
                if (dt <= 0f)
                {
                    return k1.value;
                }
                // Cubic Hermite, identical to Unity Keyframe math.
                float m0 = k0.outTangent * dt;
                float m1 = k1.inTangent * dt;
                float u = (time - k0.time) / dt;
                float u2 = u * u;
                float u3 = u2 * u;
                float h00 = 2f * u3 - 3f * u2 + 1f;
                float h10 = u3 - 2f * u2 + u;
                float h01 = -2f * u3 + 3f * u2;
                float h11 = u3 - u2;
                return h00 * k0.value + h10 * m0 + h01 * k1.value + h11 * m1;
            }
            return last.value;
        }

        // If a key already exists within MinKeySpacing of 'time', updates that key's value
        // (tangents preserved) and returns its index. Otherwise inserts sorted with linked
        // Catmull-Rom tangents computed from the neighbors (neighbors are not modified).
        public int AddKey(float time, float value)
        {
            if (keys == null)
            {
                keys = new List<CurveKey>();
            }
            time = Finite(time, 0f);
            value = float.IsNaN(value) ? 1f : Finite(value, 0f);

            int count = keys.Count;
            for (int i = 0; i < count; i++)
            {
                CurveKey existing = keys[i];
                if (existing == null)
                {
                    continue;
                }
                float dist = existing.time - time;
                if (dist < 0f)
                {
                    dist = -dist;
                }
                if (dist < MinKeySpacing)
                {
                    existing.value = value;
                    return i;
                }
            }

            int index = count;
            for (int i = 0; i < count; i++)
            {
                CurveKey k = keys[i];
                if (k != null && k.time > time)
                {
                    index = i;
                    break;
                }
            }

            float slope = 0f;
            CurveKey prev = index > 0 ? keys[index - 1] : null;
            CurveKey next = index < count ? keys[index] : null;
            if (prev != null && next != null)
            {
                float denom = next.time - prev.time;
                if (denom != 0f)
                {
                    slope = (next.value - prev.value) / denom;
                }
            }

            keys.Insert(index, new CurveKey(time, value, slope, slope));
            return index;
        }

        public void RemoveKey(int index)
        {
            if (keys == null || index < 0 || index >= keys.Count)
            {
                return;
            }
            keys.RemoveAt(index);
        }

        // Clamps newTime between the neighboring keys (± MinKeySpacing) so key order never
        // changes; returns the key's (possibly new) index. Zero allocation.
        public int MoveKey(int index, float newTime, float newValue)
        {
            if (keys == null || index < 0 || index >= keys.Count)
            {
                return index;
            }
            CurveKey key = keys[index];
            if (key == null)
            {
                return index;
            }
            if (float.IsNaN(newTime) || float.IsInfinity(newTime))
            {
                newTime = key.time;
            }
            if (float.IsNaN(newValue) || float.IsInfinity(newValue))
            {
                newValue = key.value;
            }
            if (index > 0)
            {
                CurveKey prev = keys[index - 1];
                if (prev != null && newTime < prev.time + MinKeySpacing)
                {
                    newTime = prev.time + MinKeySpacing;
                }
            }
            if (index < keys.Count - 1)
            {
                CurveKey next = keys[index + 1];
                if (next != null && newTime > next.time - MinKeySpacing)
                {
                    newTime = next.time - MinKeySpacing;
                }
            }
            key.time = newTime;
            key.value = newValue;
            return index;
        }

        // Linked tangent handles (v1): both fields track one slope.
        public void SetLinkedTangent(int index, float slope)
        {
            if (keys == null || index < 0 || index >= keys.Count)
            {
                return;
            }
            CurveKey key = keys[index];
            if (key == null)
            {
                return;
            }
            slope = Finite(slope, 0f);
            key.inTangent = slope;
            key.outTangent = slope;
            key.broken = false;
        }

        // Sets one side's tangent independently and marks the key's handles broken
        public void SetBrokenTangent(int index, float slope, bool inSide)
        {
            if (keys == null || index < 0 || index >= keys.Count)
            {
                return;
            }
            CurveKey key = keys[index];
            if (key == null)
            {
                return;
            }
            slope = Finite(slope, 0f);
            if (inSide)
            {
                key.inTangent = slope;
            }
            else
            {
                key.outTangent = slope;
            }
            key.broken = true;
        }

        // Stretches the curve along the time axis (duration rescale). Tangents are slopes
        // (value per second), so they scale inversely to preserve the curve's shape.
        public void ScaleTimes(float factor)
        {
            if (keys == null || float.IsNaN(factor) || float.IsInfinity(factor) || factor <= 0f)
            {
                return;
            }
            float inverse = 1f / factor;
            for (int i = 0; i < keys.Count; i++)
            {
                CurveKey k = keys[i];
                if (k == null)
                {
                    continue;
                }
                k.time *= factor;
                k.inTangent *= inverse;
                k.outTangent *= inverse;
            }
        }

        // Re-links broken handles to a single slope (the average of the two sides)
        public void RejoinTangents(int index)
        {
            CurveKey key = GetKey(index);
            if (key == null)
            {
                return;
            }
            float slope = (key.inTangent + key.outTangent) * 0.5f;
            key.inTangent = slope;
            key.outTangent = slope;
            key.broken = false;
        }

        public static TimelineCurve DefaultFlat(float duration)
        {
            if (float.IsNaN(duration) || float.IsInfinity(duration))
            {
                duration = MinKeySpacing;
            }
            float end = Mathf.Max(duration, MinKeySpacing);
            TimelineCurve curve = new TimelineCurve();
            curve.keys.Add(new CurveKey(0f, 1f, 0f, 0f));
            curve.keys.Add(new CurveKey(end, 1f, 0f, 0f));
            return curve;
        }

        // Restores the invariant after deserialization or external mutation: null list becomes
        // an empty list, null entries dropped, non-finite fields sanitized (value defaults to 1
        // when NaN), keys stably sorted by time, and later keys closer than MinKeySpacing to the
        // previous kept key removed.
        public void EnsureValid()
        {
            if (keys == null)
            {
                keys = new List<CurveKey>();
                return;
            }

            for (int i = keys.Count - 1; i >= 0; i--)
            {
                if (keys[i] == null)
                {
                    keys.RemoveAt(i);
                }
            }

            for (int i = 0; i < keys.Count; i++)
            {
                CurveKey k = keys[i];
                k.time = Finite(k.time, 0f);
                k.value = float.IsNaN(k.value) ? 1f : Finite(k.value, 0f);
                k.inTangent = Finite(k.inTangent, 0f);
                k.outTangent = Finite(k.outTangent, 0f);
            }

            // Stable insertion sort (keeps original order of equal-time keys so the earliest
            // one survives deduplication).
            for (int i = 1; i < keys.Count; i++)
            {
                CurveKey k = keys[i];
                int j = i - 1;
                while (j >= 0 && keys[j].time > k.time)
                {
                    keys[j + 1] = keys[j];
                    j--;
                }
                keys[j + 1] = k;
            }

            int lastKept = 0;
            for (int i = 1; i < keys.Count; )
            {
                // Dedupe threshold is half the spacing MoveKey enforces: keys legitimately placed
                // exactly MinKeySpacing apart must survive float rounding at large times.
                if (keys[i].time - keys[lastKept].time < MinKeySpacing * 0.5f)
                {
                    keys.RemoveAt(i);
                }
                else
                {
                    lastKept = i;
                    i++;
                }
            }
        }

        static float Finite(float v, float fallback)
        {
            return (float.IsNaN(v) || float.IsInfinity(v)) ? fallback : v;
        }
    }
}
