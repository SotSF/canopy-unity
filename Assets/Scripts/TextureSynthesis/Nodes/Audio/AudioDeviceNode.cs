using System;
using System.Linq;
using Lasp;
using NodeEditorFramework;
using NodeEditorFramework.Utilities;
using SecretFire.TextureSynth;
using UnityEngine;

/// <summary>
/// Runtime-selectable audio input device (microphones, line-ins, loopback devices...).
/// Enumerates available inputs through LASP's AudioSystem at runtime — no edit-time
/// Inspector binding — with a rescan button for hot-plugged hardware. The selected
/// device streams through a LaspDeviceCapture (AudioInputManager-owned), emitting
/// spectrumData/sampleRate on the same [0,1] dB-normalized scale as
/// SystemAudioSpectrum and AudioTrack, so MelFilterbank chains are interchangeable.
/// The device ID is saved with the canvas and rebinds on load; a missing device
/// drops back to the picker with a warning instead of failing silently.
/// </summary>
[Node(false, "Audio/AudioDevice")]
public class AudioDeviceNode : TickingNode
{
    public override string GetID => "AudioDeviceNode";
    public override string Title { get { return "AudioDevice"; } }

    private Vector2 _DefaultSize = new Vector2(250, 200);
    public override Vector2 DefaultSize => _DefaultSize;
    public override Vector2 MinSize => new Vector2(250, 120);
    public override bool AutoLayout => true;

    [ValueConnectionKnob("spectrumData", Direction.Out, typeof(float[]), NodeSide.Right)]
    public ValueConnectionKnob spectrumDataKnob;

    [ValueConnectionKnob("sampleRate", Direction.Out, typeof(float), NodeSide.Right)]
    public ValueConnectionKnob sampleRateKnob;

    [ValueConnectionKnob("attackTau", Direction.In, typeof(float), NodeSide.Left)]
    public ValueConnectionKnob attackTauKnob;

    [ValueConnectionKnob("releaseTau", Direction.In, typeof(float), NodeSide.Left)]
    public ValueConnectionKnob releaseTauKnob;

    public string deviceId = "";
    public string deviceName = "";
    public bool deviceBound = false;
    public float attackTau = 0.04f;
    public float releaseTau = 0.25f;
    public float floorDb = -60f;

    [NonSerialized] private LaspDeviceCapture capture;
    [NonSerialized] private string[] listNames;
    [NonSerialized] private string[] listIds;
    [NonSerialized] private int selectedIdx;
    // GUI latches intent; the device list and bindings only change in DoCalc's
    // once-per-frame Update tick so GUI structure never mutates mid-pass
    [NonSerialized] private bool pendingRescan;
    [NonSerialized] private bool pendingBind;
    [NonSerialized] private bool pendingUnbind;
    [NonSerialized] private int lastPulseFrame = -1;

    public override void DoInit()
    {
        if (deviceBound && Application.isPlaying)
        {
            BindDevice();
        }
    }

    protected override void OnDelete()
    {
        if (Application.isPlaying && AudioInputManager.Instance != null)
        {
            AudioInputManager.Instance.ReleaseCapture(this);
        }
    }

    void BindDevice()
    {
        capture = AudioInputManager.Instance != null
            ? AudioInputManager.Instance.CreateCapture(this, deviceId)
            : null;
        if (capture == null)
        {
            Debug.LogWarning($"AudioDevice: couldn't bind '{deviceName}' ({deviceId}); returning to picker.");
            deviceBound = false;
            return;
        }
        deviceId = capture.DeviceId;
        deviceName = capture.DeviceName;
        deviceBound = true;
    }

    void UnbindDevice()
    {
        if (AudioInputManager.Instance != null)
        {
            AudioInputManager.Instance.ReleaseCapture(this);
        }
        capture = null;
        deviceBound = false;
        deviceId = "";
        deviceName = "";
    }

    void RefreshDeviceList()
    {
        AudioSystem.RequestDeviceScan();
        var devices = AudioSystem.InputDevices.ToArray();
        listNames = devices.Select(d => $"{d.Name} ({d.ChannelCount}ch {d.SampleRate}Hz)").ToArray();
        listIds = devices.Select(d => d.ID).ToArray();
    }

    public override void NodeGUI()
    {
        GUILayout.BeginVertical();
        if (!deviceBound)
        {
            DrawPicker();
        }
        else
        {
            DrawCapture();
        }
        GUILayout.EndVertical();

        if (GUI.changed)
            NodeEditor.curNodeCanvas.OnNodeChange(this);
    }

    void DrawPicker()
    {
        if (!Application.isPlaying)
        {
            GUILayout.Label("Enter play mode to pick an input device");
            return;
        }
        GUILayout.BeginHorizontal();
        GUILayout.Label("Input device:");
        GUILayout.FlexibleSpace();
        if (GUILayout.Button("Rescan", GUILayout.Width(56)))
        {
            pendingRescan = true;
        }
        GUILayout.EndHorizontal();

        if (listNames == null || listNames.Length == 0)
        {
            GUILayout.Label(listNames == null ? "scanning..." : "no input devices found");
        }
        else
        {
            selectedIdx = Mathf.Clamp(selectedIdx, 0, listNames.Length - 1);
            selectedIdx = GUILayout.SelectionGrid(selectedIdx, listNames, 1);
            GUILayout.Space(4);
            if (GUILayout.Button("Capture"))
            {
                pendingBind = true;
            }
        }
    }

    void DrawCapture()
    {
        // Fixed width so the label region is stable as status text changes
        GUILayout.Label(deviceName, GUILayout.Width(220));
        bool lost = capture != null && capture.DeviceLost;
        GUILayout.BeginHorizontal();
        GUILayout.Label(capture == null ? "no capture"
            : lost ? "⚠ device lost — reconnect or Stop"
            : $"{capture.SampleRate} Hz, {capture.ChannelCount} ch", GUILayout.Width(170));
        if (GUILayout.Button("Stop", GUILayout.Width(44)))
        {
            pendingUnbind = true;
        }
        GUILayout.EndHorizontal();

        spectrumDataKnob.DisplayLayout();
        sampleRateKnob.DisplayLayout();
        FloatKnobOrSlider(ref attackTau, 0f, 1f, attackTauKnob);
        FloatKnobOrSlider(ref releaseTau, 0f, 2f, releaseTauKnob);
        GUILayout.BeginHorizontal();
        GUILayout.Label("Flr", GUILayout.Width(28));
        floorDb = RTEditorGUI.Slider(floorDb, -90f, -20f);
        GUILayout.EndHorizontal();
    }

    public override bool DoCalc()
    {
        // List/binding changes happen once per frame in the Update-phase tick, never
        // during a GUI pass (OnNodeChange can re-run Calculate mid-OnGUI)
        if (Time.frameCount != lastPulseFrame && Application.isPlaying)
        {
            lastPulseFrame = Time.frameCount;

            if (!deviceBound && listNames == null)
            {
                RefreshDeviceList(); // first idle frame populates the picker
            }
            if (pendingRescan)
            {
                RefreshDeviceList();
            }
            if (pendingBind && listIds != null && selectedIdx >= 0 && selectedIdx < listIds.Length)
            {
                deviceId = listIds[selectedIdx];
                deviceName = listNames[selectedIdx];
                BindDevice();
            }
            if (pendingUnbind)
            {
                UnbindDevice();
            }
            pendingRescan = pendingBind = pendingUnbind = false;
        }

        if (capture == null || !capture.IsRunning) return true;
        capture.AttackTau = attackTauKnob.connected() ? attackTauKnob.GetValue<float>() : attackTau;
        capture.ReleaseTau = releaseTauKnob.connected() ? releaseTauKnob.GetValue<float>() : releaseTau;
        capture.FloorDb = floorDb;
        spectrumDataKnob.SetValue(capture.Spectrum);
        sampleRateKnob.SetValue((float)capture.SampleRate);
        return true;
    }
}
