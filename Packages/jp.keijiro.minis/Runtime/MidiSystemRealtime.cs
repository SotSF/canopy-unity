using System;

namespace Minis
{
    //
    // Port-level MIDI system-realtime events (timing clock and transport).
    //
    // These messages carry no channel, so they can't be routed through a
    // per-channel MidiDevice instance; instead they're surfaced as static
    // events carrying the originating port name. Events fire on the main
    // thread (MidiPort's queue is pumped from the player loop / editor
    // update), in message order.
    //
    // The double passed with onClock is an accumulated per-port timestamp in
    // seconds built from RtMidi's inter-message delta stamps. It has sub-frame
    // precision (driver-side timing), so tick-to-tick deltas are suitable for
    // BPM estimation; its absolute value is meaningless.
    //
    public static class MidiSystemRealtime
    {
        // 0xF8 Timing Clock (24 pulses per quarter note)
        public static event Action<string, double> onClock;

        // 0xFA Start
        public static event Action<string> onStart;

        // 0xFB Continue
        public static event Action<string> onContinue;

        // 0xFC Stop
        public static event Action<string> onStop;

        internal static void InvokeClock(string portName, double portTime)
          => onClock?.Invoke(portName, portTime);

        internal static void InvokeStart(string portName)
          => onStart?.Invoke(portName);

        internal static void InvokeContinue(string portName)
          => onContinue?.Invoke(portName);

        internal static void InvokeStop(string portName)
          => onStop?.Invoke(portName);

#if UNITY_EDITOR
        // With Fast Enter Play Mode (Domain Reload disabled), static event
        // backing fields persist across play sessions; clear them so stale
        // handlers from a previous session can't linger. SubsystemRegistration
        // runs before any subscriber's OnAwake.
        [UnityEngine.RuntimeInitializeOnLoadMethod(UnityEngine.RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStaticState()
        {
            onClock = null;
            onStart = null;
            onContinue = null;
            onStop = null;
        }
#endif
    }
}
