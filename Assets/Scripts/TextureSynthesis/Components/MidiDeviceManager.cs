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

    public enum BindingType
    {
        ControlChange,
        Note
    }

    private List<MidiDevice> midiDevices = new List<MidiDevice>();

    // Registered control change handlers: key = nodeInstanceId_channel_cc{controlId}
    private Dictionary<string, MidiControlChangeHandler> controlChangeHandlers = new Dictionary<string, MidiControlChangeHandler>();

    // Registered note handlers: key = nodeInstanceId_channel_note{noteNumber}
    private Dictionary<string, MidiNoteOnHandler> noteOnHandlers = new Dictionary<string, MidiNoteOnHandler>();
    private Dictionary<string, MidiNoteOffHandler> noteOffHandlers = new Dictionary<string, MidiNoteOffHandler>();

    // Active binding state
    private bool isBinding = false;
    private BindingType currentBindingType = BindingType.ControlChange;
    private string bindingNodeId = null;
    private MidiBindCompleteHandler controlBindingCompleteCallback = null;
    private MidiNoteBindCompleteHandler noteBindingCompleteCallback = null;

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
                    Debug.Log($"[MidiDeviceManager] Found MIDI device on channel {midiDevice.channel}");
                }
            }
        }

        // Register for device changes
        InputSystem.onDeviceChange += OnDeviceChange;
    }

    private void OnDestroy()
    {
        // Clean up all event handlers
        foreach (var device in midiDevices)
        {
            if (device != null)
            {
                device.onWillControlChange -= OnControlChange;
                device.onWillNoteOn -= OnNoteOn;
                device.onWillNoteOff -= OnNoteOff;
            }
        }

        InputSystem.onDeviceChange -= OnDeviceChange;
        controlChangeHandlers.Clear();
        noteOnHandlers.Clear();
        noteOffHandlers.Clear();
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
                Debug.Log($"[MidiDeviceManager] MIDI device added on channel {midiDevice.channel}");

                // Attach our central handlers
                midiDevice.onWillControlChange += OnControlChange;
                midiDevice.onWillNoteOn += OnNoteOn;
                midiDevice.onWillNoteOff += OnNoteOff;

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
                midiDevice.onWillControlChange -= OnControlChange;
                midiDevice.onWillControlChange -= OnBindControlChange;
                midiDevice.onWillNoteOn -= OnNoteOn;
                midiDevice.onWillNoteOn -= OnBindNoteOn;
                midiDevice.onWillNoteOff -= OnNoteOff;
                midiDevices.Remove(midiDevice);
                Debug.Log($"[MidiDeviceManager] MIDI device removed on channel {midiDevice.channel}");
            }
        }
    }

    private void OnControlChange(MidiValueControl cc, float value)
    {
        var device = cc.device as MidiDevice;
        if (device == null) return;

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

    private void OnBindControlChange(MidiValueControl cc, float value)
    {
        if (!isBinding || currentBindingType != BindingType.ControlChange || controlBindingCompleteCallback == null) return;

        var device = cc.device as MidiDevice;
        if (device == null) return;

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
    /// Unregister all handlers for a specific node
    /// </summary>
    public void UnregisterNode(string nodeInstanceId)
    {
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

        Debug.Log("[MidiDeviceManager] Binding mode cancelled");
    }

    /// <summary>
    /// Get list of available MIDI devices
    /// </summary>
    public List<MidiDevice> GetDevices()
    {
        return new List<MidiDevice>(midiDevices);
    }

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
