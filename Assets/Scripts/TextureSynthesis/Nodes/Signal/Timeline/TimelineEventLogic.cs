using System.Collections.Generic;
using UnityEngine;

namespace SecretFire.TextureSynth.Timeline
{
    /// <summary>
    /// Pure event-firing window logic for the Timeline node: given a playhead advance,
    /// which event times were crossed this frame? Stateless; all methods append indices
    /// to caller-provided lists and allocate nothing (safe to call every frame).
    /// </summary>
    public static class TimelineEventLogic
    {
        /// <summary>
        /// Appends to <paramref name="results"/> the indices i where
        /// from &lt; times[i] &lt;= to (exclusive of 'from', inclusive of 'to').
        /// No sorting requirement on <paramref name="times"/>; duplicate times each
        /// fire their own index. No-op when to &lt;= from or either list is null.
        /// </summary>
        public static void FiredInRange(float from, float to, IList<float> times, List<int> results)
        {
            if (times == null || results == null)
            {
                return;
            }
            if (to <= from)
            {
                return;
            }
            for (int i = 0; i < times.Count; i++)
            {
                float t = times[i];
                if (from < t && t <= to)
                {
                    results.Add(i);
                }
            }
        }

        /// <summary>
        /// Advances the playhead by <paramref name="dt"/> seconds within [0, duration],
        /// appending the indices of crossed events to <paramref name="fired"/> (caller
        /// clears the list; this only appends). Returns the new playhead.
        ///
        /// Semantics:
        /// - duration &lt;= 0 (or NaN) is degenerate: returns 0 with no fires,
        ///   wrapped = false, reachedEnd = true.
        /// - NaN dt is treated as 0; negative dt is clamped to 0.
        /// - Normal advance fires (playhead, playhead + dt] — exclusive of the current
        ///   playhead, so an event landed on exactly last frame never re-fires.
        /// - Reaching the end fires the tail segment (playhead, duration]; an event at
        ///   exactly 'duration' fires. Without loop the playhead clamps to duration and
        ///   reachedEnd is set.
        /// - With loop the playhead wraps: overshoot = (raw - duration) % duration,
        ///   which discards any full extra laps when dt exceeds the duration —
        ///   intermediate laps fire nothing, so each event index is appended AT MOST
        ///   ONCE per Advance call. The head segment [0, overshoot] then fires,
        ///   inclusive of an event at exactly 0.
        /// - Never throws.
        /// </summary>
        public static float Advance(float playhead, float dt, float duration, bool loop,
            IList<float> times, List<int> fired, out bool wrapped, out bool reachedEnd)
        {
            wrapped = false;
            reachedEnd = false;

            // Degenerate duration (<= 0, or NaN): nothing can play.
            if (!(duration > 0f))
            {
                reachedEnd = true;
                return 0f;
            }

            if (float.IsNaN(dt) || dt < 0f)
            {
                dt = 0f;
            }

            float raw = playhead + dt;
            if (raw < duration)
            {
                FiredInRange(playhead, raw, times, fired);
                return raw;
            }

            // Reached (or passed) the end this frame: fire the tail segment (playhead, duration].
            int tailStart = fired != null ? fired.Count : 0;
            FiredInRange(playhead, duration, times, fired);

            if (!loop)
            {
                reachedEnd = true;
                return duration;
            }

            wrapped = true;
            // Overshoot past the end, with full extra laps discarded: when dt spans more
            // than one whole loop, the intermediate laps fire nothing extra.
            float over = (raw - duration) % duration;

            // Fire the head segment [0, over], inclusive of an event at exactly 0.
            int headStart = fired != null ? fired.Count : 0;
            FiredInRange(-1e-9f, over, times, fired);

            // Enforce the at-most-once-per-call guarantee: when dt > duration the head
            // segment can overlap the tail segment's event set; drop head-segment indices
            // that already fired above. (List.RemoveAt on List<int> allocates nothing.)
            if (fired != null)
            {
                for (int i = fired.Count - 1; i >= headStart; i--)
                {
                    for (int j = tailStart; j < headStart; j++)
                    {
                        if (fired[j] == fired[i])
                        {
                            fired.RemoveAt(i);
                            break;
                        }
                    }
                }
            }
            return over;
        }

        /// <summary>
        /// Appends to <paramref name="results"/> the indices i where
        /// |times[i] - t| &lt;= epsilon. A negative epsilon matches nothing.
        /// No-op when either list is null.
        /// </summary>
        public static void FiredAt(float t, IList<float> times, List<int> results, float epsilon)
        {
            if (times == null || results == null)
            {
                return;
            }
            for (int i = 0; i < times.Count; i++)
            {
                if (Mathf.Abs(times[i] - t) <= epsilon)
                {
                    results.Add(i);
                }
            }
        }
    }
}
