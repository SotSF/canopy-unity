using System.Collections.Generic;
using NUnit.Framework;
using SecretFire.TextureSynth.Timeline;
using UnityEngine;

namespace SecretFire.TextureSynth.Timeline.Tests
{
    public class TimelineCurveTests
    {
        const float Eps = 1e-6f;

        static TimelineCurve MakeCurve(params CurveKey[] curveKeys)
        {
            TimelineCurve curve = new TimelineCurve();
            curve.keys = new List<CurveKey>(curveKeys);
            return curve;
        }

        // ---------- Evaluate ----------

        [Test]
        public void Evaluate_EmptyCurve_ReturnsOne()
        {
            TimelineCurve curve = new TimelineCurve();
            Assert.AreEqual(1f, curve.Evaluate(0f));
            Assert.AreEqual(1f, curve.Evaluate(-3f));
            Assert.AreEqual(1f, curve.Evaluate(42f));
        }

        [Test]
        public void Evaluate_NullKeysList_ReturnsOne()
        {
            TimelineCurve curve = new TimelineCurve();
            curve.keys = null;
            Assert.AreEqual(1f, curve.Evaluate(0.5f));
        }

        [Test]
        public void Evaluate_NaNTime_ReturnsOne()
        {
            TimelineCurve curve = MakeCurve(new CurveKey(0f, 0.25f, 0f, 0f), new CurveKey(1f, 0.75f, 0f, 0f));
            Assert.AreEqual(1f, curve.Evaluate(float.NaN));
        }

        [Test]
        public void Evaluate_SingleKey_ReturnsItsValueEverywhere()
        {
            TimelineCurve curve = MakeCurve(new CurveKey(2f, 0.7f, 5f, -5f));
            Assert.AreEqual(0.7f, curve.Evaluate(-100f), Eps);
            Assert.AreEqual(0.7f, curve.Evaluate(2f), Eps);
            Assert.AreEqual(0.7f, curve.Evaluate(100f), Eps);
        }

        [Test]
        public void Evaluate_BeforeFirstKey_ReturnsFirstValue()
        {
            TimelineCurve curve = MakeCurve(new CurveKey(1f, 0.3f, 9f, 9f), new CurveKey(2f, 0.9f, 0f, 0f));
            Assert.AreEqual(0.3f, curve.Evaluate(-5f), Eps);
            Assert.AreEqual(0.3f, curve.Evaluate(1f), Eps);
            Assert.AreEqual(0.3f, curve.Evaluate(float.NegativeInfinity), Eps);
        }

        [Test]
        public void Evaluate_AfterLastKey_ReturnsLastValue()
        {
            TimelineCurve curve = MakeCurve(new CurveKey(1f, 0.3f, 0f, 0f), new CurveKey(2f, 0.9f, 9f, 9f));
            Assert.AreEqual(0.9f, curve.Evaluate(2f), Eps);
            Assert.AreEqual(0.9f, curve.Evaluate(50f), Eps);
            Assert.AreEqual(0.9f, curve.Evaluate(float.PositiveInfinity), Eps);
        }

        [Test]
        public void Evaluate_SCurveMidpoint_ReturnsHalfExactly()
        {
            // Keys (0,0,0,0),(1,1,0,0): flat tangents, symmetric smoothstep.
            TimelineCurve curve = MakeCurve(new CurveKey(0f, 0f, 0f, 0f), new CurveKey(1f, 1f, 0f, 0f));
            Assert.AreEqual(0.5f, curve.Evaluate(0.5f));
        }

        [Test]
        public void Evaluate_SCurveQuarterPoint_MatchesHermiteBasis()
        {
            // At u=0.25 with zero tangents: result = h01 = -2u^3 + 3u^2 = 0.15625.
            TimelineCurve curve = MakeCurve(new CurveKey(0f, 0f, 0f, 0f), new CurveKey(1f, 1f, 0f, 0f));
            Assert.AreEqual(0.15625f, curve.Evaluate(0.25f), Eps);
        }

        [Test]
        public void Evaluate_NonzeroOutTangent_MatchesHandComputedHermite()
        {
            // Keys (0,0,in 0,out 2),(1,1,in 0,out 0): dt=1, m0=2, m1=0.
            // u=0.25: h10 = u^3 - 2u^2 + u = 0.140625; h01 = 0.15625.
            // result = h10*m0 + h01*1 = 0.28125 + 0.15625 = 0.4375.
            TimelineCurve curve = MakeCurve(new CurveKey(0f, 0f, 0f, 2f), new CurveKey(1f, 1f, 0f, 0f));
            Assert.AreEqual(0.4375f, curve.Evaluate(0.25f), Eps);
        }

        [Test]
        public void Evaluate_TangentScaledBySegmentLength_MatchesHandComputedHermite()
        {
            // Keys (0,0,in 0,out 1),(2,0,in 0,out 0): dt=2, m0=1*2=2, m1=0.
            // time=1 -> u=0.5: h10 = 0.125 - 0.5 + 0.5 = 0.125; result = 0.125*2 = 0.25.
            TimelineCurve curve = MakeCurve(new CurveKey(0f, 0f, 0f, 1f), new CurveKey(2f, 0f, 0f, 0f));
            Assert.AreEqual(0.25f, curve.Evaluate(1f), Eps);
        }

        [Test]
        public void Evaluate_ThreeKeys_SecondSegmentInterpolates()
        {
            // Segment (1,1)-(2,3) flat tangents at u=0.5: 0.5*1 + 0.5*3 = 2.
            TimelineCurve curve = MakeCurve(
                new CurveKey(0f, 0f, 0f, 0f),
                new CurveKey(1f, 1f, 0f, 0f),
                new CurveKey(2f, 3f, 0f, 0f));
            Assert.AreEqual(2f, curve.Evaluate(1.5f), Eps);
        }

        // ---------- DefaultFlat ----------

        [Test]
        public void DefaultFlat_AnyTime_EvaluatesToOne()
        {
            TimelineCurve curve = TimelineCurve.DefaultFlat(60f);
            Assert.AreEqual(2, curve.KeyCount);
            Assert.AreEqual(0f, curve.GetKey(0).time, Eps);
            Assert.AreEqual(60f, curve.GetKey(1).time, Eps);
            Assert.AreEqual(1f, curve.Evaluate(-10f), Eps);
            Assert.AreEqual(1f, curve.Evaluate(0f), Eps);
            Assert.AreEqual(1f, curve.Evaluate(13.7f), Eps);
            Assert.AreEqual(1f, curve.Evaluate(60f), Eps);
            Assert.AreEqual(1f, curve.Evaluate(1000f), Eps);
        }

        [Test]
        public void DefaultFlat_ZeroDuration_SecondKeyAtMinSpacing()
        {
            TimelineCurve curve = TimelineCurve.DefaultFlat(0f);
            Assert.AreEqual(2, curve.KeyCount);
            Assert.AreEqual(TimelineCurve.MinKeySpacing, curve.GetKey(1).time, Eps);
            Assert.AreEqual(1f, curve.Evaluate(0.0005f), Eps);
        }

        // ---------- GetKey / KeyCount ----------

        [Test]
        public void GetKey_OutOfRange_ReturnsNull()
        {
            TimelineCurve curve = TimelineCurve.DefaultFlat(1f);
            Assert.IsNull(curve.GetKey(-1));
            Assert.IsNull(curve.GetKey(2));
        }

        [Test]
        public void KeyCount_NullKeysList_ReturnsZero()
        {
            TimelineCurve curve = new TimelineCurve();
            curve.keys = null;
            Assert.AreEqual(0, curve.KeyCount);
        }

        // ---------- AddKey ----------

        [Test]
        public void AddKey_EmptyCurve_InsertsAtZeroWithFlatTangents()
        {
            TimelineCurve curve = new TimelineCurve();
            int index = curve.AddKey(5f, 2f);
            Assert.AreEqual(0, index);
            Assert.AreEqual(1, curve.KeyCount);
            Assert.AreEqual(5f, curve.GetKey(0).time, Eps);
            Assert.AreEqual(2f, curve.GetKey(0).value, Eps);
            Assert.AreEqual(0f, curve.GetKey(0).inTangent, Eps);
            Assert.AreEqual(0f, curve.GetKey(0).outTangent, Eps);
        }

        [Test]
        public void AddKey_BeforeExistingKeys_InsertsSortedAtFront()
        {
            TimelineCurve curve = new TimelineCurve();
            curve.AddKey(5f, 1f);
            int index = curve.AddKey(1f, 0f);
            Assert.AreEqual(0, index);
            Assert.AreEqual(2, curve.KeyCount);
            Assert.AreEqual(1f, curve.GetKey(0).time, Eps);
            Assert.AreEqual(5f, curve.GetKey(1).time, Eps);
            // Single neighbor -> slope 0.
            Assert.AreEqual(0f, curve.GetKey(0).inTangent, Eps);
            Assert.AreEqual(0f, curve.GetKey(0).outTangent, Eps);
        }

        [Test]
        public void AddKey_BetweenNeighbors_ComputesCatmullRomTangent()
        {
            // Neighbors (0,0) and (2,4): slope = (4-0)/(2-0) = 2, linked in/out.
            TimelineCurve curve = MakeCurve(new CurveKey(0f, 0f, 0f, 0f), new CurveKey(2f, 4f, 0f, 0f));
            int index = curve.AddKey(1f, 3f);
            Assert.AreEqual(1, index);
            Assert.AreEqual(3, curve.KeyCount);
            CurveKey added = curve.GetKey(1);
            Assert.AreEqual(1f, added.time, Eps);
            Assert.AreEqual(3f, added.value, Eps);
            Assert.AreEqual(2f, added.inTangent, Eps);
            Assert.AreEqual(2f, added.outTangent, Eps);
            // Neighbors untouched.
            Assert.AreEqual(0f, curve.GetKey(0).outTangent, Eps);
            Assert.AreEqual(0f, curve.GetKey(2).inTangent, Eps);
        }

        [Test]
        public void AddKey_MaintainsSortedOrder()
        {
            TimelineCurve curve = new TimelineCurve();
            curve.AddKey(3f, 0f);
            curve.AddKey(1f, 0f);
            curve.AddKey(2f, 0f);
            curve.AddKey(0.5f, 0f);
            Assert.AreEqual(4, curve.KeyCount);
            for (int i = 1; i < curve.KeyCount; i++)
            {
                Assert.IsTrue(curve.GetKey(i - 1).time < curve.GetKey(i).time,
                    "keys not sorted at index " + i);
            }
        }

        [Test]
        public void AddKey_NearDuplicate_ReplacesValueKeepsTangentsAndTime()
        {
            TimelineCurve curve = MakeCurve(new CurveKey(1f, 2f, 0.5f, 0.5f));
            int index = curve.AddKey(1.0004f, 7f);
            Assert.AreEqual(0, index);
            Assert.AreEqual(1, curve.KeyCount);
            CurveKey key = curve.GetKey(0);
            Assert.AreEqual(1f, key.time, Eps);
            Assert.AreEqual(7f, key.value, Eps);
            Assert.AreEqual(0.5f, key.inTangent, Eps);
            Assert.AreEqual(0.5f, key.outTangent, Eps);
        }

        [Test]
        public void AddKey_JustBeyondMinSpacing_InsertsNewKey()
        {
            TimelineCurve curve = MakeCurve(new CurveKey(1f, 2f, 0f, 0f));
            int index = curve.AddKey(1.002f, 7f);
            Assert.AreEqual(1, index);
            Assert.AreEqual(2, curve.KeyCount);
        }

        // ---------- RemoveKey ----------

        [Test]
        public void RemoveKey_ValidIndex_RemovesKey()
        {
            TimelineCurve curve = TimelineCurve.DefaultFlat(10f);
            curve.RemoveKey(0);
            Assert.AreEqual(1, curve.KeyCount);
            Assert.AreEqual(10f, curve.GetKey(0).time, Eps);
        }

        [Test]
        public void RemoveKey_OutOfRange_DoesNotThrow()
        {
            TimelineCurve curve = TimelineCurve.DefaultFlat(10f);
            curve.RemoveKey(-1);
            curve.RemoveKey(2);
            curve.RemoveKey(9999);
            Assert.AreEqual(2, curve.KeyCount);

            TimelineCurve nullKeys = new TimelineCurve();
            nullKeys.keys = null;
            nullKeys.RemoveKey(0);
            Assert.AreEqual(0, nullKeys.KeyCount);
        }

        // ---------- MoveKey ----------

        [Test]
        public void MoveKey_WithinNeighbors_SetsTimeAndValue()
        {
            TimelineCurve curve = MakeCurve(
                new CurveKey(0f, 0f, 0f, 0f),
                new CurveKey(1f, 1f, 0f, 0f),
                new CurveKey(2f, 2f, 0f, 0f));
            int index = curve.MoveKey(1, 1.5f, 0.25f);
            Assert.AreEqual(1, index);
            Assert.AreEqual(1.5f, curve.GetKey(1).time, Eps);
            Assert.AreEqual(0.25f, curve.GetKey(1).value, Eps);
        }

        [Test]
        public void MoveKey_PastNextKey_ClampsToNextMinusSpacing()
        {
            TimelineCurve curve = MakeCurve(
                new CurveKey(0f, 0f, 0f, 0f),
                new CurveKey(1f, 1f, 0f, 0f),
                new CurveKey(2f, 2f, 0f, 0f));
            int index = curve.MoveKey(1, 50f, 1f);
            Assert.AreEqual(1, index);
            Assert.AreEqual(2f - TimelineCurve.MinKeySpacing, curve.GetKey(1).time, Eps);
            // Order preserved.
            Assert.IsTrue(curve.GetKey(0).time < curve.GetKey(1).time);
            Assert.IsTrue(curve.GetKey(1).time < curve.GetKey(2).time);
        }

        [Test]
        public void MoveKey_PastPrevKey_ClampsToPrevPlusSpacing()
        {
            TimelineCurve curve = MakeCurve(
                new CurveKey(0f, 0f, 0f, 0f),
                new CurveKey(1f, 1f, 0f, 0f),
                new CurveKey(2f, 2f, 0f, 0f));
            int index = curve.MoveKey(1, -50f, 1f);
            Assert.AreEqual(1, index);
            Assert.AreEqual(0f + TimelineCurve.MinKeySpacing, curve.GetKey(1).time, Eps);
            Assert.IsTrue(curve.GetKey(0).time < curve.GetKey(1).time);
            Assert.IsTrue(curve.GetKey(1).time < curve.GetKey(2).time);
        }

        [Test]
        public void MoveKey_FirstKey_NoLowerClamp()
        {
            TimelineCurve curve = MakeCurve(new CurveKey(0f, 0f, 0f, 0f), new CurveKey(1f, 1f, 0f, 0f));
            curve.MoveKey(0, -10f, 0.5f);
            Assert.AreEqual(-10f, curve.GetKey(0).time, Eps);
            Assert.AreEqual(0.5f, curve.GetKey(0).value, Eps);
        }

        [Test]
        public void MoveKey_LastKey_NoUpperClamp()
        {
            TimelineCurve curve = MakeCurve(new CurveKey(0f, 0f, 0f, 0f), new CurveKey(1f, 1f, 0f, 0f));
            curve.MoveKey(1, 500f, 1f);
            Assert.AreEqual(500f, curve.GetKey(1).time, Eps);
        }

        [Test]
        public void MoveKey_OutOfRange_DoesNotThrow()
        {
            TimelineCurve curve = TimelineCurve.DefaultFlat(10f);
            Assert.AreEqual(-1, curve.MoveKey(-1, 5f, 5f));
            Assert.AreEqual(7, curve.MoveKey(7, 5f, 5f));
            Assert.AreEqual(0f, curve.GetKey(0).time, Eps);
            Assert.AreEqual(10f, curve.GetKey(1).time, Eps);
        }

        // ---------- SetLinkedTangent ----------

        [Test]
        public void SetLinkedTangent_ValidIndex_SetsBothTangents()
        {
            TimelineCurve curve = TimelineCurve.DefaultFlat(10f);
            curve.SetLinkedTangent(0, 3.5f);
            Assert.AreEqual(3.5f, curve.GetKey(0).inTangent, Eps);
            Assert.AreEqual(3.5f, curve.GetKey(0).outTangent, Eps);
        }

        [Test]
        public void SetLinkedTangent_OutOfRange_DoesNotThrow()
        {
            TimelineCurve curve = TimelineCurve.DefaultFlat(10f);
            curve.SetLinkedTangent(-1, 1f);
            curve.SetLinkedTangent(5, 1f);
            Assert.AreEqual(0f, curve.GetKey(0).inTangent, Eps);
        }

        // ---------- EnsureValid ----------

        [Test]
        public void EnsureValid_NullKeysList_CreatesEmptyList()
        {
            TimelineCurve curve = new TimelineCurve();
            curve.keys = null;
            curve.EnsureValid();
            Assert.IsNotNull(curve.keys);
            Assert.AreEqual(0, curve.KeyCount);
            Assert.AreEqual(1f, curve.Evaluate(0f));
        }

        [Test]
        public void EnsureValid_UnsortedKeys_SortsByTime()
        {
            TimelineCurve curve = MakeCurve(
                new CurveKey(2f, 20f, 0f, 0f),
                new CurveKey(0f, 0f, 0f, 0f),
                new CurveKey(1f, 10f, 0f, 0f));
            curve.EnsureValid();
            Assert.AreEqual(3, curve.KeyCount);
            Assert.AreEqual(0f, curve.GetKey(0).time, Eps);
            Assert.AreEqual(1f, curve.GetKey(1).time, Eps);
            Assert.AreEqual(2f, curve.GetKey(2).time, Eps);
            Assert.AreEqual(10f, curve.GetKey(1).value, Eps);
        }

        [Test]
        public void EnsureValid_KeysCloserThanHalfMinSpacing_RemovesLaterDuplicates()
        {
            // Dedupe threshold is MinKeySpacing * 0.5: keys legitimately placed exactly
            // MinKeySpacing apart by MoveKey must survive float rounding on reload.
            TimelineCurve curve = MakeCurve(
                new CurveKey(0f, 1f, 0f, 0f),
                new CurveKey(0.0002f, 99f, 0f, 0f),
                new CurveKey(1f, 2f, 0f, 0f));
            curve.EnsureValid();
            Assert.AreEqual(2, curve.KeyCount);
            Assert.AreEqual(0f, curve.GetKey(0).time, Eps);
            Assert.AreEqual(1f, curve.GetKey(0).value, Eps); // earlier key kept
            Assert.AreEqual(1f, curve.GetKey(1).time, Eps);
        }

        [Test]
        public void EnsureValid_KeysAtMinSpacing_AreKept()
        {
            TimelineCurve curve = MakeCurve(
                new CurveKey(0f, 1f, 0f, 0f),
                new CurveKey(TimelineCurve.MinKeySpacing, 99f, 0f, 0f),
                new CurveKey(1f, 2f, 0f, 0f));
            curve.EnsureValid();
            Assert.AreEqual(3, curve.KeyCount);
        }

        [Test]
        public void EnsureValid_NaNValue_SanitizedToOne()
        {
            TimelineCurve curve = MakeCurve(new CurveKey(0f, float.NaN, 0f, 0f), new CurveKey(1f, 2f, 0f, 0f));
            curve.EnsureValid();
            Assert.AreEqual(1f, curve.GetKey(0).value, Eps);
        }

        [Test]
        public void EnsureValid_NaNAndInfiniteFields_SanitizedToZero()
        {
            TimelineCurve curve = MakeCurve(
                new CurveKey(float.NaN, 3f, float.NaN, float.PositiveInfinity),
                new CurveKey(1f, float.PositiveInfinity, float.NegativeInfinity, 0f));
            curve.EnsureValid();
            Assert.AreEqual(2, curve.KeyCount);
            Assert.AreEqual(0f, curve.GetKey(0).time, Eps);
            Assert.AreEqual(0f, curve.GetKey(0).inTangent, Eps);
            Assert.AreEqual(0f, curve.GetKey(0).outTangent, Eps);
            Assert.AreEqual(0f, curve.GetKey(1).value, Eps);
            Assert.AreEqual(0f, curve.GetKey(1).inTangent, Eps);
        }

        [Test]
        public void EnsureValid_CombinedDisorderAndDuplicates_RestoresInvariant()
        {
            TimelineCurve curve = MakeCurve(
                new CurveKey(2f, 5f, 0f, 0f),
                new CurveKey(0f, float.NaN, 0f, 0f),
                new CurveKey(0.0002f, 3f, 0f, 0f),
                new CurveKey(1f, 4f, float.NaN, 0f));
            curve.EnsureValid();
            Assert.AreEqual(3, curve.KeyCount);
            Assert.AreEqual(0f, curve.GetKey(0).time, Eps);
            Assert.AreEqual(1f, curve.GetKey(0).value, Eps);   // NaN value -> 1
            Assert.AreEqual(1f, curve.GetKey(1).time, Eps);
            Assert.AreEqual(0f, curve.GetKey(1).inTangent, Eps); // NaN tangent -> 0
            Assert.AreEqual(2f, curve.GetKey(2).time, Eps);
            for (int i = 1; i < curve.KeyCount; i++)
            {
                Assert.IsTrue(curve.GetKey(i).time - curve.GetKey(i - 1).time >= TimelineCurve.MinKeySpacing,
                    "spacing invariant violated at index " + i);
            }
        }

        // ---------- Serialization ----------

        [Test]
        public void JsonRoundTrip_PreservesAllKeyFields()
        {
            TimelineCurve curve = MakeCurve(
                new CurveKey(0f, 0.5f, -1.5f, 2.5f),
                new CurveKey(1.25f, -3f, 0.125f, 0.25f),
                new CurveKey(7f, 42f, 0f, -0.001f));
            string json = JsonUtility.ToJson(curve);
            TimelineCurve restored = JsonUtility.FromJson<TimelineCurve>(json);

            Assert.AreEqual(curve.KeyCount, restored.KeyCount);
            for (int i = 0; i < curve.KeyCount; i++)
            {
                CurveKey a = curve.GetKey(i);
                CurveKey b = restored.GetKey(i);
                Assert.AreEqual(a.time, b.time, Eps, "time mismatch at " + i);
                Assert.AreEqual(a.value, b.value, Eps, "value mismatch at " + i);
                Assert.AreEqual(a.inTangent, b.inTangent, Eps, "inTangent mismatch at " + i);
                Assert.AreEqual(a.outTangent, b.outTangent, Eps, "outTangent mismatch at " + i);
            }
        }

        [Test]
        public void JsonRoundTrip_RestoredCurve_EvaluatesIdentically()
        {
            TimelineCurve curve = MakeCurve(
                new CurveKey(0f, 0f, 0f, 2f),
                new CurveKey(1f, 1f, 0f, 0f),
                new CurveKey(2f, 0.5f, -1f, -1f));
            TimelineCurve restored = JsonUtility.FromJson<TimelineCurve>(JsonUtility.ToJson(curve));
            for (float t = -0.5f; t <= 2.5f; t += 0.05f)
            {
                Assert.AreEqual(curve.Evaluate(t), restored.Evaluate(t), Eps, "mismatch at t=" + t);
            }
        }

        [Test]
        public void ScaleTimes_HalvesKeyTimes_AndPreservesShape()
        {
            TimelineCurve original = MakeCurve(
                new CurveKey(0f, 0f, 0f, 2f),
                new CurveKey(4f, 1f, 0.5f, 0.5f),
                new CurveKey(10f, 0.25f, -1f, 0f));
            TimelineCurve scaled = JsonUtility.FromJson<TimelineCurve>(JsonUtility.ToJson(original));
            scaled.ScaleTimes(0.5f);
            Assert.AreEqual(2f, scaled.GetKey(1).time, Eps);
            Assert.AreEqual(5f, scaled.GetKey(2).time, Eps);
            Assert.AreEqual(1f, scaled.GetKey(1).inTangent, Eps); // slope doubles when time halves
            // shape preserved: scaled curve at t/2 equals original at t
            for (float t = 0f; t <= 10f; t += 0.25f)
            {
                Assert.AreEqual(original.Evaluate(t), scaled.Evaluate(t * 0.5f), 1e-3f, "mismatch at t=" + t);
            }
        }

        [Test]
        public void ScaleTimes_InvalidFactor_IsIgnored()
        {
            TimelineCurve curve = MakeCurve(new CurveKey(1f, 1f, 0f, 0f));
            curve.ScaleTimes(0f);
            curve.ScaleTimes(float.NaN);
            curve.ScaleTimes(-2f);
            Assert.AreEqual(1f, curve.GetKey(0).time, Eps);
        }
    }
}
