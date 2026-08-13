using System.Collections.Generic;
using NUnit.Framework;
using SecretFire.TextureSynth.Timeline;

namespace SecretFire.TextureSynth.Timeline.Tests
{
    public class TimelineViewStateTests
    {
        const float Delta = 1e-4f;

        static TimelineViewState MakeView(float start, float end)
        {
            TimelineViewState view = new TimelineViewState();
            view.viewStart = start;
            view.viewEnd = end;
            return view;
        }

        // ---------------------------------------------------------------- construction

        [Test]
        public void Constructor_Default_ShowsSixtySeconds()
        {
            TimelineViewState view = new TimelineViewState();
            Assert.AreEqual(0f, view.viewStart, Delta);
            Assert.AreEqual(60f, view.viewEnd, Delta);
            Assert.AreEqual(60f, view.Span, Delta);
        }

        // ---------------------------------------------------------------- time <-> pixel

        [Test]
        public void TimeToPixel_LinearMapping_MapsEndpointsAndMidpoint()
        {
            TimelineViewState view = MakeView(0f, 60f);
            Assert.AreEqual(0f, view.TimeToPixel(0f, 600f), Delta);
            Assert.AreEqual(300f, view.TimeToPixel(30f, 600f), Delta);
            Assert.AreEqual(600f, view.TimeToPixel(60f, 600f), Delta);
        }

        [Test]
        public void TimeToPixel_TimeOutsideView_ReturnsValueOutsideRect()
        {
            TimelineViewState view = MakeView(10f, 20f);
            Assert.AreEqual(-100f, view.TimeToPixel(0f, 100f), Delta);
            Assert.AreEqual(150f, view.TimeToPixel(25f, 100f), Delta);
        }

        [Test]
        public void TimeToPixel_PixelToTime_RoundTripsArbitraryTimes()
        {
            TimelineViewState view = MakeView(12.5f, 73.2f);
            float width = 437.5f;
            float[] samples = { 12.5f, 20f, 42.42f, 73.2f, 5f, 100f };
            for (int i = 0; i < samples.Length; i++)
            {
                float pixel = view.TimeToPixel(samples[i], width);
                Assert.AreEqual(samples[i], view.PixelToTime(pixel, width), 1e-3f,
                    "round-trip failed for t=" + samples[i]);
            }
        }

        [Test]
        public void PixelToTime_TimeToPixel_RoundTripsArbitraryPixels()
        {
            TimelineViewState view = MakeView(3f, 9f);
            float width = 320f;
            float[] pixels = { 0f, 1f, 160f, 319f, 320f, -40f, 500f };
            for (int i = 0; i < pixels.Length; i++)
            {
                float t = view.PixelToTime(pixels[i], width);
                Assert.AreEqual(pixels[i], view.TimeToPixel(t, width), 1e-3f,
                    "round-trip failed for px=" + pixels[i]);
            }
        }

        [Test]
        public void TimeToPixel_NaNTime_TreatedAsViewStart()
        {
            TimelineViewState view = MakeView(10f, 20f);
            Assert.AreEqual(0f, view.TimeToPixel(float.NaN, 100f), Delta);
        }

        [Test]
        public void TimeToPixel_ZeroOrNegativeWidth_ReturnsZero()
        {
            TimelineViewState view = MakeView(10f, 20f);
            Assert.AreEqual(0f, view.TimeToPixel(15f, 0f), Delta);
            Assert.AreEqual(0f, view.TimeToPixel(15f, -50f), Delta);
        }

        [Test]
        public void PixelToTime_NaNPixel_ReturnsViewStart()
        {
            TimelineViewState view = MakeView(10f, 20f);
            Assert.AreEqual(10f, view.PixelToTime(float.NaN, 100f), Delta);
        }

        // ---------------------------------------------------------------- zoom

        [Test]
        public void ZoomAround_ZoomIn_PivotStaysAtSamePixel()
        {
            TimelineViewState view = MakeView(10f, 30f);
            float width = 500f;
            float pivot = 22f;
            float pixelBefore = view.TimeToPixel(pivot, width);

            view.ZoomAround(pivot, 0.5f, 60f);

            float pixelAfter = view.TimeToPixel(pivot, width);
            Assert.AreEqual(pixelBefore, pixelAfter, 1e-3f);
            Assert.AreEqual(10f, view.Span, Delta);
        }

        [Test]
        public void ZoomAround_ZoomOut_PivotStaysAtSamePixel()
        {
            TimelineViewState view = MakeView(10f, 30f);
            float width = 500f;
            float pivot = 20f;
            float pixelBefore = view.TimeToPixel(pivot, width);

            view.ZoomAround(pivot, 1.5f, 60f);

            float pixelAfter = view.TimeToPixel(pivot, width);
            Assert.AreEqual(pixelBefore, pixelAfter, 1e-3f);
            Assert.AreEqual(30f, view.Span, Delta);
        }

        [Test]
        public void ZoomAround_ExtremeZoomIn_ClampsSpanToMinSpan()
        {
            TimelineViewState view = MakeView(10f, 10.1f);
            view.ZoomAround(10.05f, 0.001f, 60f);
            Assert.AreEqual(TimelineViewState.MinSpan, view.Span, 1e-5f);
        }

        [Test]
        public void ZoomAround_ExtremeZoomOut_ClampsToFullDuration()
        {
            TimelineViewState view = MakeView(10f, 30f);
            view.ZoomAround(20f, 100f, 60f);
            Assert.AreEqual(0f, view.viewStart, Delta);
            Assert.AreEqual(60f, view.viewEnd, Delta);
        }

        [Test]
        public void ZoomAround_ZoomOutNearEdge_WindowStaysInsideRange()
        {
            TimelineViewState view = MakeView(0f, 10f);
            view.ZoomAround(1f, 2f, 60f);
            Assert.AreEqual(20f, view.Span, Delta);
            Assert.IsTrue(view.viewStart >= 0f);
            Assert.IsTrue(view.viewEnd <= 60f);
        }

        [Test]
        public void ZoomAround_NaNPivot_TreatedAsViewStart()
        {
            TimelineViewState view = MakeView(10f, 30f);
            view.ZoomAround(float.NaN, 0.5f, 60f);
            // pivot = viewStart -> frac = 0 -> viewStart unchanged
            Assert.AreEqual(10f, view.viewStart, Delta);
            Assert.AreEqual(10f, view.Span, Delta);
        }

        [Test]
        public void ZoomAround_NaNOrNonPositiveFactor_LeavesSpanUnchanged()
        {
            TimelineViewState view = MakeView(10f, 30f);
            view.ZoomAround(20f, float.NaN, 60f);
            Assert.AreEqual(20f, view.Span, Delta);
            view.ZoomAround(20f, -2f, 60f);
            Assert.AreEqual(20f, view.Span, Delta);
        }

        // ---------------------------------------------------------------- pan

        [Test]
        public void Pan_DragRight_MovesViewEarlier()
        {
            TimelineViewState view = MakeView(10f, 30f);
            view.Pan(50f, 200f, 60f);
            // timeDelta = -50 * 20 / 200 = -5
            Assert.AreEqual(5f, view.viewStart, Delta);
            Assert.AreEqual(25f, view.viewEnd, Delta);
        }

        [Test]
        public void Pan_DragLeft_MovesViewLater()
        {
            TimelineViewState view = MakeView(10f, 30f);
            view.Pan(-50f, 200f, 60f);
            Assert.AreEqual(15f, view.viewStart, Delta);
            Assert.AreEqual(35f, view.viewEnd, Delta);
        }

        [Test]
        public void Pan_PastLeftEdge_ClampsToZeroPreservingSpan()
        {
            TimelineViewState view = MakeView(2f, 22f);
            view.Pan(500f, 200f, 60f);
            Assert.AreEqual(0f, view.viewStart, Delta);
            Assert.AreEqual(20f, view.viewEnd, Delta);
            Assert.AreEqual(20f, view.Span, Delta);
        }

        [Test]
        public void Pan_PastRightEdge_ClampsToDurationPreservingSpan()
        {
            TimelineViewState view = MakeView(30f, 50f);
            view.Pan(-500f, 200f, 60f);
            Assert.AreEqual(40f, view.viewStart, Delta);
            Assert.AreEqual(60f, view.viewEnd, Delta);
            Assert.AreEqual(20f, view.Span, Delta);
        }

        [Test]
        public void Pan_ZeroRectWidth_DoesNotThrowAndKeepsInvariant()
        {
            TimelineViewState view = MakeView(10f, 30f);
            view.Pan(1f, 0f, 60f);
            Assert.IsTrue(view.viewStart >= 0f);
            Assert.IsTrue(view.viewEnd <= 60f);
            Assert.AreEqual(20f, view.Span, Delta);
        }

        [Test]
        public void Pan_NaNDelta_LeavesViewUnchanged()
        {
            TimelineViewState view = MakeView(10f, 30f);
            view.Pan(float.NaN, 200f, 60f);
            Assert.AreEqual(10f, view.viewStart, Delta);
            Assert.AreEqual(30f, view.viewEnd, Delta);
        }

        // ---------------------------------------------------------------- reset / clamp

        [Test]
        public void Reset_NormalDuration_ShowsFullRange()
        {
            TimelineViewState view = MakeView(10f, 20f);
            view.Reset(120f);
            Assert.AreEqual(0f, view.viewStart, Delta);
            Assert.AreEqual(120f, view.viewEnd, Delta);
        }

        [Test]
        public void Reset_TinyDuration_UsesMinSpan()
        {
            TimelineViewState view = MakeView(10f, 20f);
            view.Reset(0.001f);
            Assert.AreEqual(0f, view.viewStart, Delta);
            Assert.AreEqual(TimelineViewState.MinSpan, view.viewEnd, 1e-6f);
        }

        [Test]
        public void ClampTo_NegativeViewStart_ShiftsRightPreservingSpan()
        {
            TimelineViewState view = MakeView(-5f, 15f);
            view.ClampTo(60f);
            Assert.AreEqual(0f, view.viewStart, Delta);
            Assert.AreEqual(20f, view.viewEnd, Delta);
        }

        [Test]
        public void ClampTo_ViewEndPastDuration_ShiftsLeftPreservingSpan()
        {
            TimelineViewState view = MakeView(50f, 70f);
            view.ClampTo(60f);
            Assert.AreEqual(40f, view.viewStart, Delta);
            Assert.AreEqual(60f, view.viewEnd, Delta);
        }

        [Test]
        public void ClampTo_WindowInsideRange_Unchanged()
        {
            TimelineViewState view = MakeView(10f, 30f);
            view.ClampTo(60f);
            Assert.AreEqual(10f, view.viewStart, Delta);
            Assert.AreEqual(30f, view.viewEnd, Delta);
        }

        [Test]
        public void ClampTo_SpanLargerThanDuration_Resets()
        {
            TimelineViewState view = MakeView(0f, 120f);
            view.ClampTo(60f);
            Assert.AreEqual(0f, view.viewStart, Delta);
            Assert.AreEqual(60f, view.viewEnd, Delta);
        }

        [Test]
        public void ClampTo_InvertedWindow_Resets()
        {
            TimelineViewState view = MakeView(30f, 10f);
            view.ClampTo(60f);
            Assert.AreEqual(0f, view.viewStart, Delta);
            Assert.AreEqual(60f, view.viewEnd, Delta);
        }

        [Test]
        public void ClampTo_NaNWindow_Resets()
        {
            TimelineViewState view = MakeView(float.NaN, 10f);
            view.ClampTo(60f);
            Assert.AreEqual(0f, view.viewStart, Delta);
            Assert.AreEqual(60f, view.viewEnd, Delta);
        }

        [Test]
        public void ClampTo_SpanEqualsDuration_ShiftsWithoutReset()
        {
            TimelineViewState view = MakeView(-10f, 50f);
            view.ClampTo(60f);
            Assert.AreEqual(0f, view.viewStart, Delta);
            Assert.AreEqual(60f, view.viewEnd, Delta);
        }

        // ---------------------------------------------------------------- tick step

        [Test]
        public void SelectTickStep_TenPixelsPerSecond_PicksFiveSeconds()
        {
            // pixels/sec = 600/60 = 10; smallest step with >= 50px is 5s.
            Assert.AreEqual(5f, TimelineViewState.SelectTickStep(60f, 600f, 50f), Delta);
        }

        [Test]
        public void SelectTickStep_ZoomedIn_PicksSubSecondStep()
        {
            // pixels/sec = 800/1 = 800; 0.1s ticks are 80px apart >= 50.
            Assert.AreEqual(0.1f, TimelineViewState.SelectTickStep(1f, 800f, 50f), Delta);
        }

        [Test]
        public void SelectTickStep_HourSpan_PicksTenMinutes()
        {
            // pixels/sec = 400/3600; need step >= 540s -> 600s.
            Assert.AreEqual(600f, TimelineViewState.SelectTickStep(3600f, 400f, 60f), Delta);
        }

        [Test]
        public void SelectTickStep_ImpossiblyLargeSpan_FallsBackToLargestStep()
        {
            Assert.AreEqual(3600f, TimelineViewState.SelectTickStep(1000000f, 100f, 50f), Delta);
        }

        [Test]
        public void SelectTickStep_DegenerateInputs_ReturnOne()
        {
            Assert.AreEqual(1f, TimelineViewState.SelectTickStep(0f, 600f, 50f), Delta);
            Assert.AreEqual(1f, TimelineViewState.SelectTickStep(-5f, 600f, 50f), Delta);
            Assert.AreEqual(1f, TimelineViewState.SelectTickStep(60f, 0f, 50f), Delta);
            Assert.AreEqual(1f, TimelineViewState.SelectTickStep(60f, -1f, 50f), Delta);
            Assert.AreEqual(1f, TimelineViewState.SelectTickStep(float.NaN, 600f, 50f), Delta);
        }

        // ---------------------------------------------------------------- marker positions

        [Test]
        public void MarkerPositions_AllInView_MapsLinearly()
        {
            List<float> times = new List<float> { 0f, 2.5f, 5f, 10f };
            float[] results = new float[4];
            TimelineViewState.MarkerPositions(times, 0f, 10f, 100f, 20f, results);
            Assert.AreEqual(0f, results[0], Delta);
            Assert.AreEqual(25f, results[1], Delta);
            Assert.AreEqual(50f, results[2], Delta);
            Assert.AreEqual(100f, results[3], Delta);
        }

        [Test]
        public void MarkerPositions_BoundaryTimes_TreatedAsInView()
        {
            List<float> times = new List<float> { 10f, 20f };
            float[] results = new float[2];
            TimelineViewState.MarkerPositions(times, 10f, 20f, 100f, 20f, results);
            Assert.AreEqual(0f, results[0], Delta);
            Assert.AreEqual(100f, results[1], Delta);
        }

        [Test]
        public void MarkerPositions_LeftOverflow_StacksFromLeftEdgeInTimeOrder()
        {
            // All before viewStart=10; ascending time order is 1 (idx 1), 3 (idx 2), 5 (idx 0).
            List<float> times = new List<float> { 5f, 1f, 3f };
            float[] results = new float[3];
            TimelineViewState.MarkerPositions(times, 10f, 20f, 100f, 20f, results);
            Assert.AreEqual(40f, results[0], Delta); // t=5 -> j=2
            Assert.AreEqual(0f, results[1], Delta);  // t=1 -> j=0
            Assert.AreEqual(20f, results[2], Delta); // t=3 -> j=1
        }

        [Test]
        public void MarkerPositions_RightOverflow_StacksFromRightEdgeInDescendingTimeOrder()
        {
            // All after viewEnd=10; descending time order is 30 (idx 1), 15 (idx 2), 12 (idx 0).
            List<float> times = new List<float> { 12f, 30f, 15f };
            float[] results = new float[3];
            TimelineViewState.MarkerPositions(times, 0f, 10f, 100f, 20f, results);
            Assert.AreEqual(60f, results[0], Delta);  // t=12 -> j=2
            Assert.AreEqual(100f, results[1], Delta); // t=30 -> j=0
            Assert.AreEqual(80f, results[2], Delta);  // t=15 -> j=1
        }

        [Test]
        public void MarkerPositions_MixedOverflowAndInView_AllGroupsPlaced()
        {
            // View [10,20], width 200, spacing 10.
            List<float> times = new List<float> { 5f, 15f, 25f, 12f, 2f, 22f };
            float[] results = new float[6];
            TimelineViewState.MarkerPositions(times, 10f, 20f, 200f, 10f, results);
            Assert.AreEqual(10f, results[0], Delta);  // left, t=5 -> j=1
            Assert.AreEqual(100f, results[1], Delta); // in view
            Assert.AreEqual(200f, results[2], Delta); // right, t=25 -> j=0
            Assert.AreEqual(40f, results[3], Delta);  // in view
            Assert.AreEqual(0f, results[4], Delta);   // left, t=2 -> j=0
            Assert.AreEqual(190f, results[5], Delta); // right, t=22 -> j=1
        }

        [Test]
        public void MarkerPositions_EqualOverflowTimes_StableByIndex()
        {
            // Three identical left-overflow times keep index order: 0, s, 2s.
            List<float> leftTimes = new List<float> { 3f, 3f, 3f };
            float[] leftResults = new float[3];
            TimelineViewState.MarkerPositions(leftTimes, 10f, 20f, 100f, 20f, leftResults);
            Assert.AreEqual(0f, leftResults[0], Delta);
            Assert.AreEqual(20f, leftResults[1], Delta);
            Assert.AreEqual(40f, leftResults[2], Delta);

            // Two identical right-overflow times: earlier index sits at the edge.
            List<float> rightTimes = new List<float> { 30f, 30f };
            float[] rightResults = new float[2];
            TimelineViewState.MarkerPositions(rightTimes, 0f, 10f, 100f, 20f, rightResults);
            Assert.AreEqual(100f, rightResults[0], Delta);
            Assert.AreEqual(80f, rightResults[1], Delta);
        }

        [Test]
        public void MarkerPositions_ResultsArrayLargerThanTimes_ExtraEntriesUntouched()
        {
            List<float> times = new List<float> { 5f, 15f, 25f };
            float[] results = { -999f, -999f, -999f, -999f, -999f, -999f };
            TimelineViewState.MarkerPositions(times, 10f, 20f, 100f, 20f, results);
            Assert.AreEqual(0f, results[0], Delta);   // left overflow
            Assert.AreEqual(50f, results[1], Delta);  // in view
            Assert.AreEqual(100f, results[2], Delta); // right overflow
            Assert.AreEqual(-999f, results[3], Delta);
            Assert.AreEqual(-999f, results[4], Delta);
            Assert.AreEqual(-999f, results[5], Delta);
        }

        [Test]
        public void MarkerPositions_EmptyTimes_DoesNothing()
        {
            float[] results = { -1f };
            TimelineViewState.MarkerPositions(new List<float>(), 0f, 10f, 100f, 20f, results);
            Assert.AreEqual(-1f, results[0], Delta);
        }

        [Test]
        public void MarkerPositions_NullArguments_DoNotThrow()
        {
            TimelineViewState.MarkerPositions(null, 0f, 10f, 100f, 20f, new float[1]);
            TimelineViewState.MarkerPositions(new List<float> { 1f }, 0f, 10f, 100f, 20f, null);
        }

        [Test]
        public void MarkerPositions_NaNTime_TreatedAsViewStart()
        {
            List<float> times = new List<float> { float.NaN };
            float[] results = new float[1];
            TimelineViewState.MarkerPositions(times, 10f, 20f, 100f, 20f, results);
            Assert.AreEqual(0f, results[0], Delta);
        }
    }
}
