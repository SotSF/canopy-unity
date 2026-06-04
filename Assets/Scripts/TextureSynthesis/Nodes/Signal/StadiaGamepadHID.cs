using System.Runtime.InteropServices;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Layouts;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.InputSystem.Utilities;
#if UNITY_EDITOR
using UnityEditor;
#endif

// Custom InputSystem layout for the Google Stadia Controller (VID 0x18D1, PID 0x9400)
// in its native USB HID mode.
//
// Why this exists: in raw HID mode the Stadia controller reports as a GenericDesktop
// Gamepad (usage page 0x01, usage 0x05), but InputSystem's generic HID fallback only
// promotes it to a *Joystick*, never a Gamepad. As a result Gamepad.all is empty and
// GamepadFullControllerNode finds nothing. (On the other machine it worked because
// Steam Input was wrapping it as a virtual XInput pad -- but Steam only injects that
// into Steam-launched processes, not the Unity editor, so it's not a reliable fix.)
//
// This layout matches the device by VID/PID and re-types it as a Gamepad, mapping the
// HID report bytes onto the standard Gamepad controls that DoCalc() already reads.
//
// Byte layout (report id kept at offset 0 -- everything is shifted up one byte):
//   0  report id (0x03)
//   1  d-pad hat, low nibble (0..7 clockwise from up, 8 = neutral)
//   2  buttons: b7=btn15 b6=btn11 b5=btn12 b4=btn13 b3=btn19 b2=btn20 b1=btn17 b0=btn18
//   3  buttons: b6=trigger b5=btn2 b4=btn4 b3=btn5 b2=btn7 b1=btn8 b0=btn14
//   4  left stick X      5  left stick Y
//   6  right stick X (z) 7  right stick Y (rz)
//   8  left trigger  (analog)   <-- VERIFY: not surfaced by the HID fallback
//   9  right trigger (analog)   <-- VERIFY
//
// LOCKED (derived directly from the Input Debugger): d-pad, both sticks, registration.
// VERIFY (best-guess, confirm via the Input Debugger -- see notes at bottom of file):
// every face/shoulder/start/select/stick-click bit, and the two analog trigger bytes.

namespace SecretFire.TextureSynth.Input
{
    [StructLayout(LayoutKind.Explicit, Size = 11)]
    struct StadiaHIDInputReport : IInputStateTypeInfo
    {
        public FourCC format => new FourCC('H', 'I', 'D');

        [FieldOffset(0)] public byte reportId;

        // --- D-pad (LOCKED): 8-way hat in the low nibble of byte 1 ---
        // Because we extend Gamepad, dpad/up..left are ButtonControls by default (no
        // minValue field). Re-type each as DiscreteButton so the hat (0..7, 8=neutral)
        // decodes into directions -- same pattern as Unity's DualShock4 HID layout.
        [InputControl(name = "dpad", format = "BIT", layout = "Dpad", bit = 0, sizeInBits = 4, defaultState = 8)]
        [InputControl(name = "dpad/up",    format = "BIT", layout = "DiscreteButton", bit = 0, sizeInBits = 4,
            parameters = "minValue=7,maxValue=1,nullValue=8,wrapAtValue=7")]
        [InputControl(name = "dpad/right", format = "BIT", layout = "DiscreteButton", bit = 0, sizeInBits = 4,
            parameters = "minValue=1,maxValue=3")]
        [InputControl(name = "dpad/down",  format = "BIT", layout = "DiscreteButton", bit = 0, sizeInBits = 4,
            parameters = "minValue=3,maxValue=5")]
        [InputControl(name = "dpad/left",  format = "BIT", layout = "DiscreteButton", bit = 0, sizeInBits = 4,
            parameters = "minValue=5,maxValue=7")]
        [FieldOffset(1)] public byte hat;

        // --- Buttons in byte 2 (all verified) ---
        [InputControl(name = "start",           bit = 6, displayName = "Menu")]
        [InputControl(name = "select",          bit = 5, displayName = "Options")]
        [InputControl(name = "rightStickPress", bit = 7, displayName = "R3")]
        [InputControl(name = "capture",         bit = 0, layout = "Button", format = "BIT", displayName = "Capture")] // the ⃞ button
        // Bits 1/2/3/4 also carry Assistant / Stadia / digital L2-R2; unmapped (unused by the node).
        [FieldOffset(2)] public byte buttons1;

        // --- Buttons in byte 3 (verified empirically; A=bit6 best-guess, see notes) ---
        // Face buttons fall on bits 6/5/4/3 = A/B/X/Y; shoulders + L3 on bits 2/1/0.
        [InputControl(name = "buttonSouth",   bit = 6, displayName = "A")]   // best-guess (only unbound bit, no output)
        [InputControl(name = "buttonEast",    bit = 5, displayName = "B")]   // verified
        [InputControl(name = "buttonWest",    bit = 4, displayName = "X")]   // verified
        [InputControl(name = "buttonNorth",   bit = 3, displayName = "Y")]   // verified
        [InputControl(name = "leftShoulder",  bit = 2, displayName = "L1")]  // verified
        [InputControl(name = "rightShoulder", bit = 1, displayName = "R1")]  // verified
        [InputControl(name = "leftStickPress",bit = 0, displayName = "L3")]  // verified
        [FieldOffset(3)] public byte buttons2;

        // --- Sticks (LOCKED). HID byte axes are 0..255 with 0x80 center; Y is inverted. ---
        [InputControl(name = "leftStick", layout = "Stick", format = "VC2B")]
        [InputControl(name = "leftStick/x", offset = 0, format = "BYTE",
            parameters = "normalize,normalizeMin=0,normalizeMax=1,normalizeZero=0.5")]
        [InputControl(name = "leftStick/y", offset = 1, format = "BYTE",
            parameters = "normalize,normalizeMin=0,normalizeMax=1,normalizeZero=0.5,invert")]
        [FieldOffset(4)] public byte leftStickX;
        [FieldOffset(5)] public byte leftStickY;

        [InputControl(name = "rightStick", layout = "Stick", format = "VC2B")]
        [InputControl(name = "rightStick/x", offset = 0, format = "BYTE",
            parameters = "normalize,normalizeMin=0,normalizeMax=1,normalizeZero=0.5")]
        [InputControl(name = "rightStick/y", offset = 1, format = "BYTE",
            parameters = "normalize,normalizeMin=0,normalizeMax=1,normalizeZero=0.5,invert")]
        [FieldOffset(6)] public byte rightStickX;  // z
        [FieldOffset(7)] public byte rightStickY;  // rz

        // --- Analog triggers (VERIFY bytes 8/9 via Display Raw Memory) ---
        [InputControl(name = "leftTrigger",  format = "BYTE")]
        [FieldOffset(8)] public byte leftTrigger;
        [InputControl(name = "rightTrigger", format = "BYTE")]
        [FieldOffset(9)] public byte rightTrigger;

        // byte 10 unused so far
        [FieldOffset(10)] public byte unused;
    }

    [InputControlLayout(stateType = typeof(StadiaHIDInputReport), displayName = "Stadia Controller")]
#if UNITY_EDITOR
    [InitializeOnLoad]
#endif
    public class StadiaGamepadHID : Gamepad
    {
        // Editor: register so the device is recognized in edit mode too (the node enumerates
        // Gamepad.all from NodeGUI outside play mode), AND keep the binding alive across recompiles.
        //
        // A recompile is a domain reload. InputSystem's InitializeInEditor does a Reset() (clearing
        // layout registrations) and then defers restoring saved devices to the *first input update*.
        // That restore can happen before OR after our [InitializeOnLoad] runs (static-ctor ordering
        // across assemblies is undefined), so neither an immediate Register() nor a delayCall can
        // reliably win the race: if our layout isn't registered at restore time, the Stadia comes
        // back as the generic HID Joystick (keeping its old device name -- hence a device literally
        // named "StadiaGamepadHID" but typed Joystick), and a Register() that ran while no device
        // was live had nothing to promote.
        //
        // So instead of racing the restore, we re-run Register() whenever a device appears.
        // RegisterLayout is idempotent and re-fires RecreateDevicesUsingLayoutWithInferiorMatch,
        // which promotes any connected device that matches us better than its current layout --
        // so the Stadia gets re-promoted on whichever input update finally restores it.
#if UNITY_EDITOR
        static StadiaGamepadHID()
        {
            Register();
            EditorApplication.delayCall += Register;
            InputSystem.onDeviceChange += OnEditorDeviceChange;
        }

        static void OnEditorDeviceChange(InputDevice device, InputDeviceChange change)
        {
            // Ignore our own layout: RecreateDevice re-adds the device, which re-enters here, and
            // re-registering during a device-add notification risks reentrancy -- so skip + defer.
            if (device is StadiaGamepadHID) return;
            if (change == InputDeviceChange.Added || change == InputDeviceChange.Reconnected)
                EditorApplication.delayCall += Register;
        }
#endif

        // Player + play mode. Runs on EVERY play-mode enter, including under Fast Enter Play
        // Mode (domain reload disabled). This is the load type that matters there: when domain
        // reload is off, InputSystem rebuilds its device state on play-enter and the Stadia comes
        // back as the generic HID Joystick, while [InitializeOnLoad] (domain-load only) does NOT
        // re-run. So we MUST re-register here every time.
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void Init() => Register();

        // No local "already registered" guard on purpose: RegisterLayout is idempotent
        // (re-registering the type overwrites; AddMatcher dedups identical matchers), and we
        // *want* it to re-run each play-enter -- RegisterControlLayoutMatcher re-fires
        // RecreateDevicesUsingLayoutWithInferiorMatch, which re-promotes an already-connected
        // device from the generic Joystick layout to this one. A persistent static guard would
        // survive a no-domain-reload play-enter and suppress exactly that re-match.
        static void Register()
        {
            InputSystem.RegisterLayout<StadiaGamepadHID>(
                matches: new InputDeviceMatcher()
                    .WithInterface("HID")
                    .WithCapability("vendorId", 0x18D1)
                    .WithCapability("productId", 0x9400));
        }
    }
}
