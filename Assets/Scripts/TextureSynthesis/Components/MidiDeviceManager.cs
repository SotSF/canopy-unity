using System;
using System.Collections.Generic;
using System.Linq;
using Minis;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Layouts;

/// <summary>
/// Singleton manager for MIDI device detection and event routing.
/// Persists across canvas loads to maintain MIDI device state.
/// </summary>
public class MidiDeviceManager : Singleton<MidiDeviceManager>
{
    public delegate void MidiControlChangeHandler(MidiValueControl cc, float value);
    public delegate void MidiNoteOnHandler(MidiNoteControl note, float velocity);
    public delegate void MidiNoteOffHandler(MidiNoteControl note);
    public delegate void MidiBindCompleteHandler(MidiDevice device, int channel, int controlID);
    public delegate void MidiNoteBindCompleteHandler(MidiDevice device, int channel, int noteNumber);
    // portTime: accumulated per-port timestamp (seconds) with driver-side precision;
    // tick-to-tick deltas are BPM-grade, the absolute value is meaningless.
    public delegate void MidiClockTickHandler(string portName, double portTime);
    public delegate void MidiClockTransportHandler(string portName);
    // Clock messages carry no channel, so a clock bind resolves to a port, not a device.
    public delegate void MidiClockBindCompleteHandler(string portName);

    public enum BindingType
    {
        ControlChange,
        Note,
        Clock
    }

    private List<MidiDevice> midiDevices = new List<MidiDevice>();

    // Registered control change handlers: key = nodeInstanceId_channel_cc{controlId}
    private Dictionary<string, MidiControlChangeHandler> controlChangeHandlers = new Dictionary<string, MidiControlChangeHandler>();

    // Registered note handlers: key = nodeInstanceId_channel_note{noteNumber}
    private Dictionary<string, MidiNoteOnHandler> noteOnHandlers = new Dictionary<string, MidiNoteOnHandler>();
    private Dictionary<string, MidiNoteOffHandler> noteOffHandlers = new Dictionary<string, MidiNoteOffHandler>();

    // Registered clock handlers: key = nodeInstanceId_clockport{portName}. Clock/transport
    // messages are port-wide (no channel), so routing is keyed on the RtMidi port name
    // rather than a device channel.
    private Dictionary<string, MidiClockTickHandler> clockTickHandlers = new Dictionary<string, MidiClockTickHandler>();
    private Dictionary<string, MidiClockTransportHandler> clockStartHandlers = new Dictionary<string, MidiClockTransportHandler>();
    private Dictionary<string, MidiClockTransportHandler> clockContinueHandlers = new Dictionary<string, MidiClockTransportHandler>();
    private Dictionary<string, MidiClockTransportHandler> clockStopHandlers = new Dictionary<string, MidiClockTransportHandler>();

    // --- Oddball support ---
    // The Oddball (https://playoddball.com) is a BLE-MIDI motion controller that streams
    // CCs continuously. It must NOT participate in the normal "use control to bind" flow
    // (it would clobber every bind) and its messages are routed only to nodes that
    // explicitly subscribe to it, keyed by nodeInstanceId.
    //
    // Cross-platform identification: a Minis MidiDevice's description.product is
    // "{portName} Channel {N}", so the underlying MIDI port name is embedded there.
    //   - macOS (direct BLE MIDI): port shows up as the device name (e.g. "ODD", "F62B...")
    //   - Windows (loopMIDI + a BLE-MIDI bridge like MIDIberry): name the loopMIDI port so
    //     it contains one of these tokens (e.g. "oddball_loopmidi").
    // Matching is a case-insensitive substring test against any configured identifier.
    public static readonly string[] DefaultOddballIdentifiers = { "ODD", "F62B", "oddball" };
    private readonly List<string> oddballIdentifiers = new List<string>(DefaultOddballIdentifiers);

    // Device-level Oddball subscribers: key = nodeInstanceId. These receive every CC / note
    // event originating from any device identified as an Oddball, regardless of channel.
    private Dictionary<string, MidiControlChangeHandler> oddballControlHandlers = new Dictionary<string, MidiControlChangeHandler>();
    private Dictionary<string, MidiNoteOnHandler> oddballNoteOnHandlers = new Dictionary<string, MidiNoteOnHandler>();
    private Dictionary<string, MidiNoteOffHandler> oddballNoteOffHandlers = new Dictionary<string, MidiNoteOffHandler>();

    // Active binding state
    private bool isBinding = false;
    private BindingType currentBindingType = BindingType.ControlChange;
    private string bindingNodeId = null;
    private MidiBindCompleteHandler controlBindingCompleteCallback = null;
    private MidiNoteBindCompleteHandler noteBindingCompleteCallback = null;
    private MidiClockBindCompleteHandler clockBindingCompleteCallback = null;

    protected override void OnAwake()
    {
        // Initialize MIDI device list
        var match = new InputDeviceMatcher().WithInterface("Minis");
        foreach (InputDevice device in InputSystem.devices)
        {
            if (match.MatchPercentage(device.description) > 0)
            {
                var midiDevice = device as MidiDevice;
                if (midiDevice != null && !midiDevices.Contains(midiDevice))
                {
                    midiDevices.Add(midiDevice);
                    // Attach central handlers so devices that already exist at startup
                    // (e.g. an Oddball connected before play) get routed, not just ones
                    // that fire an Added event later.
                    AttachDeviceHandlers(midiDevice);
                    Debug.Log($"[MidiDeviceManager] Found MIDI device on channel {midiDevice.channel} (product '{midiDevice.description.product}'){(IsOddballDevice(midiDevice) ? " [Oddball]" : "")}");
                }
            }
        }

        // Register for device changes
        InputSystem.onDeviceChange += OnDeviceChange;

        // Clock/transport events are port-level static events (no per-device attach).
        MidiSystemRealtime.onClock += OnClockMessage;
        MidiSystemRealtime.onStart += OnClockStart;
        MidiSystemRealtime.onContinue += OnClockContinue;
        MidiSystemRealtime.onStop += OnClockStop;
    }

    /// <summary>
    /// Attach the manager's central routing handlers to a device. Safe to pair with
    /// DetachDeviceHandlers; never attach twice for the same device.
    /// </summary>
    private void AttachDeviceHandlers(MidiDevice device)
    {
        device.onWillControlChange += OnControlChange;
        device.onWillNoteOn += OnNoteOn;
        device.onWillNoteOff += OnNoteOff;
    }

    private void DetachDeviceHandlers(MidiDevice device)
    {
        device.onWillControlChange -= OnControlChange;
        device.onWillNoteOn -= OnNoteOn;
        device.onWillNoteOff -= OnNoteOff;
    }

    private void OnDestroy()
    {
        // Clean up all event handlers
        foreach (var device in midiDevices)
        {
            if (device != null)
            {
                DetachDeviceHandlers(device);
            }
        }

        InputSystem.onDeviceChange -= OnDeviceChange;

        MidiSystemRealtime.onClock -= OnClockMessage;
        MidiSystemRealtime.onStart -= OnClockStart;
        MidiSystemRealtime.onContinue -= OnClockContinue;
        MidiSystemRealtime.onStop -= OnClockStop;

        controlChangeHandlers.Clear();
        noteOnHandlers.Clear();
        noteOffHandlers.Clear();
        clockTickHandlers.Clear();
        clockStartHandlers.Clear();
        clockContinueHandlers.Clear();
        clockStopHandlers.Clear();
        oddballControlHandlers.Clear();
        oddballNoteOnHandlers.Clear();
        oddballNoteOffHandlers.Clear();
    }

    private void OnDeviceChange(InputDevice device, InputDeviceChange change)
    {
        var midiDevice = device as MidiDevice;
        if (midiDevice == null) return;

        if (change == InputDeviceChange.Added)
        {
            if (!midiDevices.Contains(midiDevice))
            {
                midiDevices.Add(midiDevice);
                Debug.Log($"[MidiDeviceManager] MIDI device added on channel {midiDevice.channel} (product '{midiDevice.description.product}'){(IsOddballDevice(midiDevice) ? " [Oddball]" : "")}");

                // Attach our central handlers
                AttachDeviceHandlers(midiDevice);

                // If we're in binding mode, attach binding handlers
                if (isBinding)
                {
                    if (currentBindingType == BindingType.ControlChange)
                    {
                        midiDevice.onWillControlChange += OnBindControlChange;
                    }
                    else
                    {
                        midiDevice.onWillNoteOn += OnBindNoteOn;
                    }
                }
            }
        }
        else if (change == InputDeviceChange.Removed)
        {
            if (midiDevices.Contains(midiDevice))
            {
                DetachDeviceHandlers(midiDevice);
                midiDevice.onWillControlChange -= OnBindControlChange;
                midiDevice.onWillNoteOn -= OnBindNoteOn;
                midiDevices.Remove(midiDevice);
                Debug.Log($"[MidiDeviceManager] MIDI device removed on channel {midiDevice.channel}");
            }
        }
    }

    private void OnControlChange(MidiValueControl cc, float value)
    {
        var device = cc.device as MidiDevice;
        if (device == null) return;

        // Oddball messages are isolated: they only reach explicit Oddball subscribers and
        // never the normal channel/cc routing (so the Oddball can't clobber bound nodes).
        if (IsOddballDevice(device))
        {
            foreach (var kvp in oddballControlHandlers.ToList())
            {
                try
                {
                    kvp.Value?.Invoke(cc, value);
                }
                catch (Exception e)
                {
                    Debug.LogError($"[MidiDeviceManager] Error invoking Oddball control handler: {e.Message}");
                }
            }
            return;
        }

        // Route to all registered handlers for this channel/control combination
        var handlersToNotify = controlChangeHandlers
            .Where(kvp => kvp.Key.Contains($"_ch{device.channel}_cc{cc.controlNumber}"))
            .ToList();

        foreach (var kvp in handlersToNotify)
        {
            try
            {
                kvp.Value?.Invoke(cc, value);
            }
            catch (Exception e)
            {
                Debug.LogError($"[MidiDeviceManager] Error invoking control handler: {e.Message}");
            }
        }
    }

    private void OnNoteOn(MidiNoteControl note, float velocity)
    {
        var device = note.device as MidiDevice;
        if (device == null) return;

        if (IsOddballDevice(device))
        {
            foreach (var kvp in oddballNoteOnHandlers.ToList())
            {
                try
                {
                    kvp.Value?.Invoke(note, velocity);
                }
                catch (Exception e)
                {
                    Debug.LogError($"[MidiDeviceManager] Error invoking Oddball note on handler: {e.Message}");
                }
            }
            return;
        }

        // Route to all registered handlers for this channel/note combination
        var handlersToNotify = noteOnHandlers
            .Where(kvp => kvp.Key.Contains($"_ch{device.channel}_note{note.noteNumber}"))
            .ToList();

        foreach (var kvp in handlersToNotify)
        {
            try
            {
                kvp.Value?.Invoke(note, velocity);
            }
            catch (Exception e)
            {
                Debug.LogError($"[MidiDeviceManager] Error invoking note on handler: {e.Message}");
            }
        }
    }

    private void OnNoteOff(MidiNoteControl note)
    {
        var device = note.device as MidiDevice;
        if (device == null) return;

        if (IsOddballDevice(device))
        {
            foreach (var kvp in oddballNoteOffHandlers.ToList())
            {
                try
                {
                    kvp.Value?.Invoke(note);
                }
                catch (Exception e)
                {
                    Debug.LogError($"[MidiDeviceManager] Error invoking Oddball note off handler: {e.Message}");
                }
            }
            return;
        }

        // Route to all registered handlers for this channel/note combination
        var handlersToNotify = noteOffHandlers
            .Where(kvp => kvp.Key.Contains($"_ch{device.channel}_note{note.noteNumber}"))
            .ToList();

        foreach (var kvp in handlersToNotify)
        {
            try
            {
                kvp.Value?.Invoke(note);
            }
            catch (Exception e)
            {
                Debug.LogError($"[MidiDeviceManager] Error invoking note off handler: {e.Message}");
            }
        }
    }

    private void OnClockMessage(string portName, double portTime)
    {
        // Clock binding completes on the first tick heard from any port.
        if (isBinding && currentBindingType == BindingType.Clock && clockBindingCompleteCallback != null)
        {
            isBinding = false;
            var callback = clockBindingCompleteCallback;
            clockBindingCompleteCallback = null;
            bindingNodeId = null;

            try
            {
                callback.Invoke(portName);
            }
            catch (Exception e)
            {
                Debug.LogError($"[MidiDeviceManager] Error in clock binding callback: {e.Message}");
            }
            return;
        }

        var handlersToNotify = clockTickHandlers
            .Where(kvp => kvp.Key.EndsWith($"_clockport{portName}"))
            .ToList();

        foreach (var kvp in handlersToNotify)
        {
            try
            {
                kvp.Value?.Invoke(portName, portTime);
            }
            catch (Exception e)
            {
                Debug.LogError($"[MidiDeviceManager] Error invoking clock tick handler: {e.Message}");
            }
        }
    }

    private void OnClockStart(string portName) => RouteTransport(clockStartHandlers, portName, "start");
    private void OnClockContinue(string portName) => RouteTransport(clockContinueHandlers, portName, "continue");
    private void OnClockStop(string portName) => RouteTransport(clockStopHandlers, portName, "stop");

    private void RouteTransport(Dictionary<string, MidiClockTransportHandler> handlers, string portName, string kind)
    {
        var handlersToNotify = handlers
            .Where(kvp => kvp.Key.EndsWith($"_clockport{portName}"))
            .ToList();

        foreach (var kvp in handlersToNotify)
        {
            try
            {
                kvp.Value?.Invoke(portName);
            }
            catch (Exception e)
            {
                Debug.LogError($"[MidiDeviceManager] Error invoking clock {kind} handler: {e.Message}");
            }
        }
    }

    private void OnBindControlChange(MidiValueControl cc, float value)
    {
        if (!isBinding || currentBindingType != BindingType.ControlChange || controlBindingCompleteCallback == null) return;

        var device = cc.device as MidiDevice;
        if (device == null) return;

        // Ignore the Oddball's constant CC stream during binding, otherwise it would
        // instantly hijack every bind attempt before the user can touch their controller.
        if (IsOddballDevice(device)) return;

        // Stop binding mode
        isBinding = false;
        foreach (var midiDevice in midiDevices)
        {
            midiDevice.onWillControlChange -= OnBindControlChange;
        }

        // Invoke callback
        try
        {
            controlBindingCompleteCallback.Invoke(device, device.channel, cc.controlNumber);
        }
        catch (Exception e)
        {
            Debug.LogError($"[MidiDeviceManager] Error in control binding callback: {e.Message}");
        }

        bindingNodeId = null;
        controlBindingCompleteCallback = null;
    }

    private void OnBindNoteOn(MidiNoteControl note, float velocity)
    {
        if (!isBinding || currentBindingType != BindingType.Note || noteBindingCompleteCallback == null) return;

        var device = note.device as MidiDevice;
        if (device == null) return;

        // Ignore Oddball motion notes (Tap/Shake/Twist) during binding so they don't
        // hijack a user's note-bind attempt.
        if (IsOddballDevice(device)) return;

        // Stop binding mode
        isBinding = false;
        foreach (var midiDevice in midiDevices)
        {
            midiDevice.onWillNoteOn -= OnBindNoteOn;
        }

        // Invoke callback
        try
        {
            noteBindingCompleteCallback.Invoke(device, device.channel, note.noteNumber);
        }
        catch (Exception e)
        {
            Debug.LogError($"[MidiDeviceManager] Error in note binding callback: {e.Message}");
        }

        bindingNodeId = null;
        noteBindingCompleteCallback = null;
    }

    /// <summary>
    /// Register a handler for MIDI control changes
    /// </summary>
    public void RegisterControlHandler(string nodeInstanceId, int channel, int controlID, MidiControlChangeHandler handler)
    {
        string key = $"{nodeInstanceId}_ch{channel}_cc{controlID}";

        if (controlChangeHandlers.ContainsKey(key))
        {
            Debug.LogWarning($"[MidiDeviceManager] Handler already registered for {key}, replacing");
        }

        controlChangeHandlers[key] = handler;

        // Note: Central handler is already attached in OnDeviceChange
        Debug.Log($"[MidiDeviceManager] Registered control handler for {key}");
    }

    /// <summary>
    /// Register handlers for MIDI note events
    /// </summary>
    public void RegisterNoteHandlers(string nodeInstanceId, int channel, int noteNumber,
        MidiNoteOnHandler onNoteOn = null, MidiNoteOffHandler onNoteOff = null)
    {
        string key = $"{nodeInstanceId}_ch{channel}_note{noteNumber}";

        if (onNoteOn != null)
        {
            if (noteOnHandlers.ContainsKey(key))
            {
                Debug.LogWarning($"[MidiDeviceManager] Note On handler already registered for {key}, replacing");
            }
            noteOnHandlers[key] = onNoteOn;
        }

        if (onNoteOff != null)
        {
            if (noteOffHandlers.ContainsKey(key))
            {
                Debug.LogWarning($"[MidiDeviceManager] Note Off handler already registered for {key}, replacing");
            }
            noteOffHandlers[key] = onNoteOff;
        }

        // Note: Central handlers are already attached in OnDeviceChange
        Debug.Log($"[MidiDeviceManager] Registered note handlers for {key}");
    }

    /// <summary>
    /// Register handlers for MIDI clock/transport events from a specific port.
    /// Clock is port-wide (no channel), so the key is the RtMidi port name captured
    /// at bind time.
    /// </summary>
    public void RegisterClockHandlers(string nodeInstanceId, string portName,
        MidiClockTickHandler onTick = null,
        MidiClockTransportHandler onStart = null,
        MidiClockTransportHandler onContinue = null,
        MidiClockTransportHandler onStop = null)
    {
        string key = $"{nodeInstanceId}_clockport{portName}";

        if (onTick != null)
        {
            if (clockTickHandlers.ContainsKey(key))
            {
                Debug.LogWarning($"[MidiDeviceManager] Clock tick handler already registered for {key}, replacing");
            }
            clockTickHandlers[key] = onTick;
        }
        if (onStart != null) clockStartHandlers[key] = onStart;
        if (onContinue != null) clockContinueHandlers[key] = onContinue;
        if (onStop != null) clockStopHandlers[key] = onStop;

        Debug.Log($"[MidiDeviceManager] Registered clock handlers for {key}");
    }

    /// <summary>
    /// Unregister clock/transport handlers
    /// </summary>
    public void UnregisterClockHandlers(string nodeInstanceId, string portName)
    {
        string key = $"{nodeInstanceId}_clockport{portName}";

        // Non-short-circuiting so every dictionary is swept
        if (clockTickHandlers.Remove(key)
            | clockStartHandlers.Remove(key)
            | clockContinueHandlers.Remove(key)
            | clockStopHandlers.Remove(key))
        {
            Debug.Log($"[MidiDeviceManager] Unregistered clock handlers for {key}");
        }
    }

    /// <summary>
    /// Unregister all handlers for a specific node
    /// </summary>
    public void UnregisterNode(string nodeInstanceId)
    {
        // Node types are transiently instantiated and destroyed during NodeTypes.FetchNodeTypes()
        // before DoInit runs, so nodeInstanceId can be null/empty here. Bail out rather than
        // letting Dictionary.Remove(null) throw.
        if (string.IsNullOrEmpty(nodeInstanceId)) return;

        var controlKeysToRemove = controlChangeHandlers.Keys
            .Where(k => k.StartsWith(nodeInstanceId + "_"))
            .ToList();

        foreach (var key in controlKeysToRemove)
        {
            controlChangeHandlers.Remove(key);
            Debug.Log($"[MidiDeviceManager] Unregistered control handler for {key}");
        }

        var noteOnKeysToRemove = noteOnHandlers.Keys
            .Where(k => k.StartsWith(nodeInstanceId + "_"))
            .ToList();

        foreach (var key in noteOnKeysToRemove)
        {
            noteOnHandlers.Remove(key);
            Debug.Log($"[MidiDeviceManager] Unregistered note on handler for {key}");
        }

        var noteOffKeysToRemove = noteOffHandlers.Keys
            .Where(k => k.StartsWith(nodeInstanceId + "_"))
            .ToList();

        foreach (var key in noteOffKeysToRemove)
        {
            noteOffHandlers.Remove(key);
            Debug.Log($"[MidiDeviceManager] Unregistered note off handler for {key}");
        }

        var clockKeysToRemove = clockTickHandlers.Keys
            .Concat(clockStartHandlers.Keys)
            .Concat(clockContinueHandlers.Keys)
            .Concat(clockStopHandlers.Keys)
            .Where(k => k.StartsWith(nodeInstanceId + "_"))
            .Distinct()
            .ToList();

        foreach (var key in clockKeysToRemove)
        {
            clockTickHandlers.Remove(key);
            clockStartHandlers.Remove(key);
            clockContinueHandlers.Remove(key);
            clockStopHandlers.Remove(key);
            Debug.Log($"[MidiDeviceManager] Unregistered clock handlers for {key}");
        }

        // Oddball subscribers are keyed directly by nodeInstanceId.
        if (oddballControlHandlers.Remove(nodeInstanceId)
            | oddballNoteOnHandlers.Remove(nodeInstanceId)
            | oddballNoteOffHandlers.Remove(nodeInstanceId))
        {
            Debug.Log($"[MidiDeviceManager] Unregistered Oddball handlers for {nodeInstanceId}");
        }
    }

    /// <summary>
    /// Unregister a specific control handler
    /// </summary>
    public void UnregisterControlHandler(string nodeInstanceId, int channel, int controlID)
    {
        string key = $"{nodeInstanceId}_ch{channel}_cc{controlID}";
        if (controlChangeHandlers.Remove(key))
        {
            Debug.Log($"[MidiDeviceManager] Unregistered control handler for {key}");
        }
    }

    /// <summary>
    /// Unregister note handlers
    /// </summary>
    public void UnregisterNoteHandlers(string nodeInstanceId, int channel, int noteNumber)
    {
        string key = $"{nodeInstanceId}_ch{channel}_note{noteNumber}";

        bool removedOn = noteOnHandlers.Remove(key);
        bool removedOff = noteOffHandlers.Remove(key);

        if (removedOn || removedOff)
        {
            Debug.Log($"[MidiDeviceManager] Unregistered note handlers for {key}");
        }
    }

    /// <summary>
    /// Begin binding mode for MIDI control changes - waits for next control change
    /// </summary>
    public void BeginControlBinding(string nodeInstanceId, MidiBindCompleteHandler onComplete)
    {
        if (isBinding)
        {
            Debug.LogWarning("[MidiDeviceManager] Already in binding mode");
            return;
        }

        isBinding = true;
        currentBindingType = BindingType.ControlChange;
        bindingNodeId = nodeInstanceId;
        controlBindingCompleteCallback = onComplete;

        // Attach binding handler to all devices
        foreach (var device in midiDevices)
        {
            device.onWillControlChange += OnBindControlChange;
        }

        Debug.Log("[MidiDeviceManager] Control binding mode started");
    }

    /// <summary>
    /// Begin binding mode for MIDI notes - waits for next note on
    /// </summary>
    public void BeginNoteBinding(string nodeInstanceId, MidiNoteBindCompleteHandler onComplete)
    {
        if (isBinding)
        {
            Debug.LogWarning("[MidiDeviceManager] Already in binding mode");
            return;
        }

        isBinding = true;
        currentBindingType = BindingType.Note;
        bindingNodeId = nodeInstanceId;
        noteBindingCompleteCallback = onComplete;

        // Attach binding handler to all devices
        foreach (var device in midiDevices)
        {
            device.onWillNoteOn += OnBindNoteOn;
        }

        Debug.Log("[MidiDeviceManager] Note binding mode started");
    }

    /// <summary>
    /// Begin binding mode for MIDI clock - completes on the first clock tick heard
    /// from any port. No per-device attach needed: clock events arrive through the
    /// always-subscribed MidiSystemRealtime static events.
    /// </summary>
    public void BeginClockBinding(string nodeInstanceId, MidiClockBindCompleteHandler onComplete)
    {
        if (isBinding)
        {
            Debug.LogWarning("[MidiDeviceManager] Already in binding mode");
            return;
        }

        isBinding = true;
        currentBindingType = BindingType.Clock;
        bindingNodeId = nodeInstanceId;
        clockBindingCompleteCallback = onComplete;

        Debug.Log("[MidiDeviceManager] Clock binding mode started");
    }

    /// <summary>
    /// Cancel binding mode
    /// </summary>
    public void CancelBinding()
    {
        if (!isBinding) return;

        foreach (var device in midiDevices)
        {
            device.onWillControlChange -= OnBindControlChange;
            device.onWillNoteOn -= OnBindNoteOn;
        }

        isBinding = false;
        bindingNodeId = null;
        controlBindingCompleteCallback = null;
        noteBindingCompleteCallback = null;
        clockBindingCompleteCallback = null;

        Debug.Log("[MidiDeviceManager] Binding mode cancelled");
    }

    /// <summary>
    /// Get list of available MIDI devices
    /// </summary>
    public List<MidiDevice> GetDevices()
    {
        return new List<MidiDevice>(midiDevices);
    }

    #region Device layouts

    // Physical-layout descriptions per known controller, matched the same way as the
    // Oddball: case-insensitive substring test against description.product, which Minis
    // fills as "{portName} Channel {N}".
    public static readonly string[] MidiMixIdentifiers = { "MIDI Mix", "MIDIMIX" };

    private AkaiMidiMixLayout midiMixLayout;

    public bool IsMidiMixDevice(MidiDevice device)
    {
        return device != null && ProductMatches(device.description.product, MidiMixIdentifiers);
    }

    /// <summary>
    /// Layout for a bound control. Prefers the product string captured at bind time
    /// (exact even with multiple devices per channel); falls back to whatever device
    /// currently claims the channel (pre-existing binds that didn't save a product).
    /// Returns null when the device has no known layout (Oddball, synthetic ports...).
    /// </summary>
    public AkaiMidiMixLayout GetLayoutFor(string deviceProduct, int channel)
    {
        if (!string.IsNullOrEmpty(deviceProduct))
        {
            return ProductMatches(deviceProduct, MidiMixIdentifiers) ? MidiMixLayout : null;
        }
        var device = GetDeviceByChannel(channel);
        return IsMidiMixDevice(device) ? MidiMixLayout : null;
    }

    private AkaiMidiMixLayout MidiMixLayout
    {
        get
        {
            if (midiMixLayout == null) midiMixLayout = new AkaiMidiMixLayout();
            return midiMixLayout;
        }
    }

    private static bool ProductMatches(string product, string[] identifiers)
    {
        if (string.IsNullOrEmpty(product)) return false;
        foreach (var id in identifiers)
        {
            if (!string.IsNullOrEmpty(id) &&
                product.IndexOf(id, StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return true;
            }
        }
        return false;
    }

    #endregion

    #region Oddball support

    /// <summary>
    /// True if the device's MIDI port name matches any configured Oddball identifier
    /// (case-insensitive substring match against description.product).
    /// </summary>
    public bool IsOddballDevice(MidiDevice device)
    {
        if (device == null) return false;
        var product = device.description.product;
        if (string.IsNullOrEmpty(product)) return false;
        foreach (var id in oddballIdentifiers)
        {
            if (!string.IsNullOrEmpty(id) &&
                product.IndexOf(id, StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return true;
            }
        }
        return false;
    }

    /// <summary>
    /// Add a custom identifier token used to recognize an Oddball port (e.g. the name of
    /// your loopMIDI port on Windows). Matching is case-insensitive substring.
    /// </summary>
    public void AddOddballIdentifier(string identifier)
    {
        if (string.IsNullOrEmpty(identifier)) return;
        if (!oddballIdentifiers.Contains(identifier))
        {
            oddballIdentifiers.Add(identifier);
        }
    }

    /// <summary>
    /// Any currently-connected device that identifies as an Oddball.
    /// </summary>
    public IEnumerable<MidiDevice> GetOddballDevices()
    {
        return midiDevices.Where(IsOddballDevice);
    }

    /// <summary>
    /// True if at least one Oddball is currently connected.
    /// </summary>
    public bool IsOddballConnected()
    {
        return midiDevices.Any(IsOddballDevice);
    }

    /// <summary>
    /// Subscribe a node to every CC / note event coming from any Oddball device, regardless
    /// of channel. Oddball events are routed only to these subscribers and never participate
    /// in normal channel/cc routing or "use control to bind". Re-registering with the same
    /// nodeInstanceId replaces the previous handlers. Unregister via <see cref="UnregisterNode"/>.
    /// </summary>
    public void RegisterOddballHandlers(string nodeInstanceId,
        MidiControlChangeHandler onControlChange = null,
        MidiNoteOnHandler onNoteOn = null,
        MidiNoteOffHandler onNoteOff = null)
    {
        if (onControlChange != null) oddballControlHandlers[nodeInstanceId] = onControlChange;
        else oddballControlHandlers.Remove(nodeInstanceId);

        if (onNoteOn != null) oddballNoteOnHandlers[nodeInstanceId] = onNoteOn;
        else oddballNoteOnHandlers.Remove(nodeInstanceId);

        if (onNoteOff != null) oddballNoteOffHandlers[nodeInstanceId] = onNoteOff;
        else oddballNoteOffHandlers.Remove(nodeInstanceId);

        Debug.Log($"[MidiDeviceManager] Registered Oddball handlers for {nodeInstanceId}");
    }

    #endregion

    /// <summary>
    /// Get device by channel number
    /// </summary>
    public MidiDevice GetDeviceByChannel(int channel)
    {
        return midiDevices.FirstOrDefault(d => d.channel == channel);
    }

    /// <summary>
    /// Check if currently in binding mode
    /// </summary>
    public bool IsBinding()
    {
        return isBinding;
    }

    /// <summary>
    /// Get current binding type
    /// </summary>
    public BindingType GetBindingType()
    {
        return currentBindingType;
    }
}
