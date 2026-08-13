using System.Collections.Generic;
using NUnit.Framework;
using SecretFire.TextureSynth.Timeline;

namespace SecretFire.TextureSynth.Timeline.Tests
{
    public class TimelineEventLogicTests
    {
        const float Delta = 1e-5f;

        static List<float> Times(params float[] ts)
        {
            return new List<float>(ts);
        }

        // ---------------------------------------------------------------
        // FiredInRange
        // ---------------------------------------------------------------

        [Test]
        public void FiredInRange_NormalWindow_FiresStrictlyAfterFromUpToIncludingTo()
        {
            var times = Times(0.1f, 0.2f, 0.3f, 0.4f);
            var results = new List<int>();
            TimelineEventLogic.FiredInRange(0.1f, 0.3f, times, results);
            // 0.1 excluded (from exclusive), 0.3 included (to inclusive), 0.4 out of range.
            CollectionAssert.AreEqual(new[] { 1, 2 }, results);
        }

        [Test]
        public void FiredInRange_ToEqualsFrom_FiresNothing()
        {
            var times = Times(0.5f);
            var results = new List<int>();
            TimelineEventLogic.FiredInRange(0.5f, 0.5f, times, results);
            Assert.AreEqual(0, results.Count);
        }

        [Test]
        public void FiredInRange_ToLessThanFrom_FiresNothing()
        {
            var times = Times(0.5f);
            var results = new List<int>();
            TimelineEventLogic.FiredInRange(1.0f, 0.0f, times, results);
            Assert.AreEqual(0, results.Count);
        }

        [Test]
        public void FiredInRange_UnsortedTimes_FiresMatchingIndicesInListOrder()
        {
            var times = Times(0.9f, 0.2f, 0.5f, 0.1f, 0.4f);
            var results = new List<int>();
            TimelineEventLogic.FiredInRange(0.15f, 0.5f, times, results);
            CollectionAssert.AreEqual(new[] { 1, 2, 4 }, results);
        }

        [Test]
        public void FiredInRange_DuplicateTimes_BothFire()
        {
            var times = Times(0.5f, 0.5f);
            var results = new List<int>();
            TimelineEventLogic.FiredInRange(0.0f, 1.0f, times, results);
            CollectionAssert.AreEqual(new[] { 0, 1 }, results);
        }

        [Test]
        public void FiredInRange_ExistingResults_AppendsWithoutClearing()
        {
            var times = Times(0.5f);
            var results = new List<int> { 42 };
            TimelineEventLogic.FiredInRange(0.0f, 1.0f, times, results);
            CollectionAssert.AreEqual(new[] { 42, 0 }, results);
        }

        [Test]
        public void FiredInRange_EmptyTimes_FiresNothing()
        {
            var results = new List<int>();
            TimelineEventLogic.FiredInRange(0.0f, 1.0f, new List<float>(), results);
            Assert.AreEqual(0, results.Count);
        }

        // ---------------------------------------------------------------
        // Advance — normal (non-wrapping)
        // ---------------------------------------------------------------

        [Test]
        public void Advance_NormalAdvance_FiresEventsInWindowAndReturnsRaw()
        {
            var times = Times(0.25f, 0.75f);
            var fired = new List<int>();
            bool wrapped, reachedEnd;
            float result = TimelineEventLogic.Advance(0.0f, 0.5f, 10.0f, false,
                times, fired, out wrapped, out reachedEnd);
            Assert.AreEqual(0.5f, result, Delta);
            CollectionAssert.AreEqual(new[] { 0 }, fired);
            Assert.IsFalse(wrapped);
            Assert.IsFalse(reachedEnd);
        }

        [Test]
        public void Advance_EventExactlyAtPlayhead_DoesNotRefireOnNextAdvance()
        {
            var times = Times(0.5f);
            var fired = new List<int>();
            bool wrapped, reachedEnd;

            // First advance lands exactly on the event: it fires (to inclusive).
            float playhead = TimelineEventLogic.Advance(0.0f, 0.5f, 10.0f, false,
                times, fired, out wrapped, out reachedEnd);
            Assert.AreEqual(0.5f, playhead, Delta);
            CollectionAssert.AreEqual(new[] { 0 }, fired);

            // Next advance starts at the event's time: from is exclusive, no re-fire.
            fired.Clear();
            TimelineEventLogic.Advance(playhead, 0.5f, 10.0f, false,
                times, fired, out wrapped, out reachedEnd);
            Assert.AreEqual(0, fired.Count);
        }

        [Test]
        public void Advance_ZeroDt_FiresNothingAndReturnsPlayhead()
        {
            var times = Times(0.5f);
            var fired = new List<int>();
            bool wrapped, reachedEnd;
            float result = TimelineEventLogic.Advance(0.5f, 0.0f, 10.0f, true,
                times, fired, out wrapped, out reachedEnd);
            Assert.AreEqual(0.5f, result, Delta);
            Assert.AreEqual(0, fired.Count);
            Assert.IsFalse(wrapped);
            Assert.IsFalse(reachedEnd);
        }

        [Test]
        public void Advance_NegativeDt_TreatedAsZero()
        {
            var times = Times(0.25f);
            var fired = new List<int>();
            bool wrapped, reachedEnd;
            float result = TimelineEventLogic.Advance(0.5f, -1.0f, 10.0f, true,
                times, fired, out wrapped, out reachedEnd);
            Assert.AreEqual(0.5f, result, Delta);
            Assert.AreEqual(0, fired.Count);
            Assert.IsFalse(wrapped);
            Assert.IsFalse(reachedEnd);
        }

        [Test]
        public void Advance_NaNDt_TreatedAsZero()
        {
            var times = Times(0.25f);
            var fired = new List<int>();
            bool wrapped, reachedEnd;
            float result = TimelineEventLogic.Advance(0.5f, float.NaN, 10.0f, true,
                times, fired, out wrapped, out reachedEnd);
            Assert.AreEqual(0.5f, result, Delta);
            Assert.AreEqual(0, fired.Count);
            Assert.IsFalse(wrapped);
            Assert.IsFalse(reachedEnd);
        }

        // ---------------------------------------------------------------
        // Advance — end of timeline, no loop
        // ---------------------------------------------------------------

        [Test]
        public void Advance_EventAtExactlyDuration_FiresWhenReachingEnd()
        {
            var times = Times(1.0f);
            var fired = new List<int>();
            bool wrapped, reachedEnd;
            float result = TimelineEventLogic.Advance(0.9f, 0.2f, 1.0f, false,
                times, fired, out wrapped, out reachedEnd);
            Assert.AreEqual(1.0f, result, Delta);
            CollectionAssert.AreEqual(new[] { 0 }, fired);
            Assert.IsTrue(reachedEnd);
            Assert.IsFalse(wrapped);
        }

        [Test]
        public void Advance_NoLoopOvershoot_ClampsToDurationAndSetsReachedEnd()
        {
            var times = Times(0.95f, 0.2f);
            var fired = new List<int>();
            bool wrapped, reachedEnd;
            float result = TimelineEventLogic.Advance(0.9f, 5.0f, 1.0f, false,
                times, fired, out wrapped, out reachedEnd);
            Assert.AreEqual(1.0f, result, Delta);
            // Only the tail event fires; nothing wraps around without loop.
            CollectionAssert.AreEqual(new[] { 0 }, fired);
            Assert.IsTrue(reachedEnd);
            Assert.IsFalse(wrapped);
        }

        // ---------------------------------------------------------------
        // Advance — loop wrap
        // ---------------------------------------------------------------

        [Test]
        public void Advance_LoopWrap_FiresTailThenHeadIncludingEventAtExactlyZero()
        {
            var times = Times(0.95f, 0.0f, 0.05f);
            var fired = new List<int>();
            bool wrapped, reachedEnd;
            float result = TimelineEventLogic.Advance(0.9f, 0.2f, 1.0f, true,
                times, fired, out wrapped, out reachedEnd);
            Assert.AreEqual(0.1f, result, Delta);
            Assert.IsTrue(wrapped);
            Assert.IsFalse(reachedEnd);
            // Tail (0.9, 1.0] fires index 0; head [0, ~0.1] fires indices 1 (exactly 0.0) and 2.
            CollectionAssert.AreEquivalent(new[] { 0, 1, 2 }, fired);
        }

        [Test]
        public void Advance_LoopWrap_EventInBothSegments_FiresBoth()
        {
            // One event in the tail segment, one in the head segment: both fire in one call.
            var times = Times(0.98f, 0.02f);
            var fired = new List<int>();
            bool wrapped, reachedEnd;
            TimelineEventLogic.Advance(0.95f, 0.1f, 1.0f, true,
                times, fired, out wrapped, out reachedEnd);
            Assert.IsTrue(wrapped);
            CollectionAssert.AreEquivalent(new[] { 0, 1 }, fired);
        }

        [Test]
        public void Advance_DtSpanningMultipleLaps_DoesNotCrashAndFiresEachEventAtMostOnce()
        {
            // playhead 1 (== duration), dt 2.5, duration 1: raw = 3.5, full laps discarded,
            // over = 0.5. Tail (1, 1] is empty; head fires everything in [0, 0.5].
            var times = Times(0.25f, 0.75f);
            var fired = new List<int>();
            bool wrapped, reachedEnd;
            float result = TimelineEventLogic.Advance(1.0f, 2.5f, 1.0f, true,
                times, fired, out wrapped, out reachedEnd);
            Assert.AreEqual(0.5f, result, Delta);
            Assert.IsTrue(wrapped);
            Assert.IsFalse(reachedEnd);
            CollectionAssert.AreEqual(new[] { 0 }, fired);
            // Guarantee: each event index appears at most once per Advance call.
            var seen = new HashSet<int>();
            foreach (int idx in fired)
            {
                Assert.IsTrue(seen.Add(idx), "index " + idx + " fired more than once");
            }
        }

        [Test]
        public void Advance_DtLargerThanDuration_OverlappingSegments_EventFiresAtMostOnce()
        {
            // dt > duration makes the head segment [0, over] overlap the tail segment's
            // event set: event 0.5 is in both (0.3, 1.0] and [0, 0.8]. It must fire once.
            var times = Times(0.5f);
            var fired = new List<int>();
            bool wrapped, reachedEnd;
            float result = TimelineEventLogic.Advance(0.3f, 1.5f, 1.0f, true,
                times, fired, out wrapped, out reachedEnd);
            Assert.AreEqual(0.8f, result, Delta);
            Assert.IsTrue(wrapped);
            CollectionAssert.AreEqual(new[] { 0 }, fired);
        }

        [Test]
        public void Advance_RawExactlyDuration_Loop_WrapsToZeroAndFiresEndEvent()
        {
            var times = Times(1.0f, 0.0f);
            var fired = new List<int>();
            bool wrapped, reachedEnd;
            float result = TimelineEventLogic.Advance(0.5f, 0.5f, 1.0f, true,
                times, fired, out wrapped, out reachedEnd);
            // raw == duration -> wrap; over = 0; tail fires the event at exactly duration,
            // head [0, 0] fires the event at exactly 0.
            Assert.AreEqual(0.0f, result, Delta);
            Assert.IsTrue(wrapped);
            Assert.IsFalse(reachedEnd);
            CollectionAssert.AreEquivalent(new[] { 0, 1 }, fired);
        }

        // ---------------------------------------------------------------
        // Advance — degenerate inputs
        // ---------------------------------------------------------------

        [Test]
        public void Advance_ZeroDuration_ReturnsZeroWithReachedEndAndNoFires()
        {
            var times = Times(0.5f);
            var fired = new List<int>();
            bool wrapped, reachedEnd;
            float result = TimelineEventLogic.Advance(0.5f, 0.1f, 0.0f, true,
                times, fired, out wrapped, out reachedEnd);
            Assert.AreEqual(0.0f, result, Delta);
            Assert.AreEqual(0, fired.Count);
            Assert.IsFalse(wrapped);
            Assert.IsTrue(reachedEnd);
        }

        [Test]
        public void Advance_NegativeDuration_ReturnsZeroWithReachedEndAndNoFires()
        {
            var times = Times(0.5f);
            var fired = new List<int>();
            bool wrapped, reachedEnd;
            float result = TimelineEventLogic.Advance(0.5f, 0.1f, -3.0f, false,
                times, fired, out wrapped, out reachedEnd);
            Assert.AreEqual(0.0f, result, Delta);
            Assert.AreEqual(0, fired.Count);
            Assert.IsFalse(wrapped);
            Assert.IsTrue(reachedEnd);
        }

        [Test]
        public void Advance_EmptyTimes_AdvancesWithoutFiring()
        {
            var fired = new List<int>();
            bool wrapped, reachedEnd;
            float result = TimelineEventLogic.Advance(0.0f, 0.5f, 1.0f, true,
                new List<float>(), fired, out wrapped, out reachedEnd);
            Assert.AreEqual(0.5f, result, Delta);
            Assert.AreEqual(0, fired.Count);
        }

        [Test]
        public void Advance_UnsortedTimes_FiresCorrectIndices()
        {
            var times = Times(0.8f, 0.1f, 0.4f, 0.9f);
            var fired = new List<int>();
            bool wrapped, reachedEnd;
            TimelineEventLogic.Advance(0.2f, 0.3f, 1.0f, false,
                times, fired, out wrapped, out reachedEnd);
            // Window (0.2, 0.5]: only 0.4 (index 2).
            CollectionAssert.AreEqual(new[] { 2 }, fired);
        }

        [Test]
        public void Advance_DuplicateTimes_BothIndicesFire()
        {
            var times = Times(0.25f, 0.25f);
            var fired = new List<int>();
            bool wrapped, reachedEnd;
            TimelineEventLogic.Advance(0.0f, 0.5f, 1.0f, false,
                times, fired, out wrapped, out reachedEnd);
            CollectionAssert.AreEqual(new[] { 0, 1 }, fired);
        }

        [Test]
        public void Advance_AppendsToFiredWithoutClearing()
        {
            var times = Times(0.25f);
            var fired = new List<int> { 7 };
            bool wrapped, reachedEnd;
            TimelineEventLogic.Advance(0.0f, 0.5f, 1.0f, false,
                times, fired, out wrapped, out reachedEnd);
            CollectionAssert.AreEqual(new[] { 7, 0 }, fired);
        }

        // ---------------------------------------------------------------
        // FiredAt
        // ---------------------------------------------------------------

        [Test]
        public void FiredAt_WithinEpsilon_Fires()
        {
            var times = Times(0.5f, 0.6f);
            var results = new List<int>();
            TimelineEventLogic.FiredAt(0.505f, times, results, 0.01f);
            CollectionAssert.AreEqual(new[] { 0 }, results);
        }

        [Test]
        public void FiredAt_ExactlyEpsilonAway_Fires()
        {
            var times = Times(0.5f);
            var results = new List<int>();
            // |0.5 - 0.375| == 0.125 exactly (all binary-exact values).
            TimelineEventLogic.FiredAt(0.375f, times, results, 0.125f);
            CollectionAssert.AreEqual(new[] { 0 }, results);
        }

        [Test]
        public void FiredAt_OutsideEpsilon_DoesNotFire()
        {
            var times = Times(0.5f);
            var results = new List<int>();
            TimelineEventLogic.FiredAt(0.6f, times, results, 0.05f);
            Assert.AreEqual(0, results.Count);
        }

        [Test]
        public void FiredAt_ZeroEpsilon_ExactMatchOnly()
        {
            var times = Times(0.5f, 0.5000001f);
            var results = new List<int>();
            TimelineEventLogic.FiredAt(0.5f, times, results, 0.0f);
            CollectionAssert.AreEqual(new[] { 0 }, results);
        }

        [Test]
        public void FiredAt_NegativeEpsilon_FiresNothing()
        {
            var times = Times(0.5f);
            var results = new List<int>();
            TimelineEventLogic.FiredAt(0.5f, times, results, -0.01f);
            Assert.AreEqual(0, results.Count);
        }

        [Test]
        public void FiredAt_MultipleMatches_AllFire()
        {
            var times = Times(0.5f, 0.501f, 0.9f, 0.499f);
            var results = new List<int>();
            TimelineEventLogic.FiredAt(0.5f, times, results, 0.01f);
            CollectionAssert.AreEqual(new[] { 0, 1, 3 }, results);
        }
    }
}
