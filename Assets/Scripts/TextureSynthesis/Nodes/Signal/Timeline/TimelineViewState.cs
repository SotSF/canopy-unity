using System;
using System.Collections.Generic;
using UnityEngine;

namespace SecretFire.TextureSynth.Timeline
{
    /// <summary>
    /// Pure view math for the timeline editor: the visible window in seconds, time-to-pixel
    /// mapping for a rect width, zoom-around-point, pan, clamp rules, ruler tick step selection
    /// and event-marker overflow stacking. Plain serializable data — public fields only, no
    /// UnityEngine.Object references. All methods are guard-heavy and never throw.
    /// </summary>
    [Serializable]
    public class TimelineViewState
    {
        public const float MinSpan = 0.05f;

        // Visible window in seconds. Invariant: 0 <= viewStart < viewEnd.
        public float viewStart;
        public float viewEnd;

        public TimelineViewState()
        {
            viewStart = 0f;
            viewEnd = 60f;
        }

        public float Span => viewEnd - viewStart;

        // Returns the current span, substituting MinSpan if the stored window is degenerate
        // (inverted, zero or NaN) so mapping math never divides by zero.
        float SafeSpan()
        {
            float span = viewEnd - viewStart;
            if (float.IsNaN(span) || span <= 0f) span = MinSpan;
            return span;
        }

        /// <summary>Linear time -> pixel map; may return values outside [0, rectWidth].</summary>
        public float TimeToPixel(float time, float rectWidth)
        {
            if (float.IsNaN(time)) time = viewStart;
            if (float.IsNaN(rectWidth) || rectWidth <= 0f) return 0f;
            return (time - viewStart) / SafeSpan() * rectWidth;
        }

        /// <summary>Linear pixel -> time map; inverse of TimeToPixel for positive rect widths.</summary>
        public float PixelToTime(float pixel, float rectWidth)
        {
            if (float.IsNaN(pixel)) return viewStart;
            if (float.IsNaN(rectWidth) || rectWidth <= 0f) rectWidth = 1f;
            return viewStart + pixel / rectWidth * SafeSpan();
        }

        /// <summary>
        /// Zooms the window by zoomFactor (&lt;1 zooms in) keeping pivotTime at the same
        /// fractional position within the window, then clamps into [0, max(duration, MinSpan)].
        /// </summary>
        public void ZoomAround(float pivotTime, float zoomFactor, float duration)
        {
            if (float.IsNaN(pivotTime)) pivotTime = viewStart;
            if (float.IsNaN(zoomFactor) || zoomFactor <= 0f) zoomFactor = 1f;
            if (float.IsNaN(duration)) duration = 0f;

            float span = viewEnd - viewStart;
            if (float.IsNaN(span) || span <= 0f)
            {
                Reset(duration);
                span = viewEnd - viewStart;
            }

            float total = Mathf.Max(duration, MinSpan);
            float newSpan = Mathf.Clamp(span * zoomFactor, MinSpan, total);
            float frac = Mathf.Clamp01((pivotTime - viewStart) / span);
            viewStart = pivotTime - frac * newSpan;
            viewEnd = viewStart + newSpan;
            ClampTo(duration);
        }

        /// <summary>
        /// Pans by a pixel delta: dragging right (positive delta) moves content right, i.e. the
        /// view window moves earlier in time. Span is preserved; window clamps to [0, duration].
        /// </summary>
        public void Pan(float deltaPixels, float rectWidth, float duration)
        {
            if (float.IsNaN(deltaPixels)) deltaPixels = 0f;
            float width = (float.IsNaN(rectWidth) || rectWidth < 1f) ? 1f : rectWidth;

            float span = viewEnd - viewStart;
            if (float.IsNaN(span) || span <= 0f)
            {
                Reset(duration);
                return;
            }

            float timeDelta = -deltaPixels * span / width;
            viewStart += timeDelta;
            viewEnd += timeDelta;
            ClampTo(duration);
        }

        /// <summary>Resets the window to show [0, max(duration, MinSpan)].</summary>
        public void Reset(float duration)
        {
            if (float.IsNaN(duration)) duration = 0f;
            viewStart = 0f;
            viewEnd = Mathf.Max(duration, MinSpan);
        }

        /// <summary>
        /// Shifts (never resizes, except by resetting a degenerate/oversized window) the window
        /// so it lies within [0, max(duration, MinSpan)].
        /// </summary>
        public void ClampTo(float duration)
        {
            if (float.IsNaN(duration)) duration = 0f;
            float total = Mathf.Max(duration, MinSpan);
            float span = viewEnd - viewStart;
            if (float.IsNaN(span) || span <= 0f || span > total)
            {
                Reset(duration);
                return;
            }
            if (viewStart < 0f)
            {
                viewEnd -= viewStart;
                viewStart = 0f;
            }
            if (viewEnd > total)
            {
                viewStart -= viewEnd - total;
                viewEnd = total;
            }
        }

        static readonly float[] tickSteps =
        {
            0.1f, 0.25f, 0.5f, 1f, 2f, 5f, 10f, 15f, 30f, 60f, 120f, 300f, 600f, 1800f, 3600f
        };

        /// <summary>
        /// Picks the smallest ruler tick step (seconds) that renders at least minPixelsPerTick
        /// pixels apart; falls back to the largest step. Degenerate inputs return 1.
        /// </summary>
        public static float SelectTickStep(float span, float rectWidth, float minPixelsPerTick)
        {
            if (float.IsNaN(span) || span <= 0f) return 1f;
            if (float.IsNaN(rectWidth) || rectWidth <= 0f) return 1f;
            if (float.IsNaN(minPixelsPerTick)) return 1f;

            float pixelsPerSecond = rectWidth / span;
            for (int i = 0; i < tickSteps.Length; i++)
            {
                if (tickSteps[i] * pixelsPerSecond >= minPixelsPerTick) return tickSteps[i];
            }
            return tickSteps[tickSteps.Length - 1];
        }

        /// <summary>
        /// Computes marker x-positions for event times against a view window. In-view times map
        /// linearly into [0, rectWidth]. Times left of the window stack from the left edge at
        /// minSpacing intervals in ascending time order (stable by index for equal times); times
        /// right of the window stack from the right edge in descending time order (stable by
        /// index). Fills only the first times.Count entries of results (which must be at least
        /// that long; extra entries are untouched).
        /// </summary>
        public static void MarkerPositions(IList<float> times, float viewStart, float viewEnd,
            float rectWidth, float minSpacing, float[] results)
        {
            if (times == null || results == null) return;
            int count = Mathf.Min(times.Count, results.Length);
            if (count <= 0) return;

            if (float.IsNaN(viewStart)) viewStart = 0f;
            if (float.IsNaN(rectWidth) || rectWidth < 0f) rectWidth = 0f;
            if (float.IsNaN(minSpacing) || minSpacing < 0f) minSpacing = 0f;
            float span = viewEnd - viewStart;
            if (float.IsNaN(span) || span <= 0f)
            {
                span = MinSpan;
                viewEnd = viewStart + span;
            }

            // Classify each marker; in-view markers map directly. Overflow indices are gathered
            // for edge stacking. Called only when the view or events change, so the small temp
            // arrays here are acceptable.
            int[] leftIndices = new int[count];
            int[] rightIndices = new int[count];
            int leftCount = 0;
            int rightCount = 0;
            for (int i = 0; i < count; i++)
            {
                float t = times[i];
                if (float.IsNaN(t)) t = viewStart;
                if (t < viewStart)
                {
                    leftIndices[leftCount++] = i;
                }
                else if (t > viewEnd)
                {
                    rightIndices[rightCount++] = i;
                }
                else
                {
                    results[i] = (t - viewStart) / span * rectWidth;
                }
            }

            // Left overflow: ascending by time (stable by index), j-th sits at j * minSpacing.
            InsertionSortByTime(leftIndices, leftCount, times, ascending: true);
            for (int j = 0; j < leftCount; j++)
            {
                results[leftIndices[j]] = j * minSpacing;
            }

            // Right overflow: descending by time (stable by index), j-th sits at
            // rectWidth - j * minSpacing.
            InsertionSortByTime(rightIndices, rightCount, times, ascending: false);
            for (int j = 0; j < rightCount; j++)
            {
                results[rightIndices[j]] = rectWidth - j * minSpacing;
            }
        }

        // Stable insertion sort of the first `count` entries of `indices` by times[index].
        // Equal times keep their original (index) order because equal keys never shift.
        static void InsertionSortByTime(int[] indices, int count, IList<float> times, bool ascending)
        {
            for (int i = 1; i < count; i++)
            {
                int cur = indices[i];
                float curTime = times[cur];
                int k = i - 1;
                if (ascending)
                {
                    while (k >= 0 && times[indices[k]] > curTime)
                    {
                        indices[k + 1] = indices[k];
                        k--;
                    }
                }
                else
                {
                    while (k >= 0 && times[indices[k]] < curTime)
                    {
                        indices[k + 1] = indices[k];
                        k--;
                    }
                }
                indices[k + 1] = cur;
            }
        }
    }
}
