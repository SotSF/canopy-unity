using UnityEngine.InputSystem.Layouts;
using UnityEngine.InputSystem;
using RtMidiDll = RtMidi.Unmanaged;

namespace Minis
{
    //
    // MIDI port class that manages an RtMidi input object and MIDI device
    // objects bound with each MIDI channel found in the port.
    //
    unsafe sealed class MidiPort : System.IDisposable
    {
        #region Internal objects and methods

        RtMidiDll.Wrapper* _rtmidi;
        string _portName;
        double _portTime;
        MidiDevice [] _channels = new MidiDevice[16];

        // Get a device object bound with a specified channel.
        // Create a new device if it doesn't exist.
        MidiDevice GetChannelDevice(int channel)
        {
            if (_channels[channel] == null)
            {
                var desc = new InputDeviceDescription {
                    interfaceName = "Minis",
                    deviceClass = "MIDI",
                    product = _portName + " Channel " + channel,
                    capabilities = "{\"channel\":" + channel + "}"
                };
                _channels[channel] = (MidiDevice)InputSystem.AddDevice(desc);
            }
            return _channels[channel];
        }

        #endregion

        #region Public methods

        public MidiPort(int portNumber, string portName)
        {
            _portName = portName;

            _rtmidi = RtMidiDll.InCreateDefault();

            if (_rtmidi == null || !_rtmidi->ok)
            {
                UnityEngine.Debug.LogWarning("Failed to create an RtMidi device object.");
                return;
            }

            RtMidiDll.OpenPort(_rtmidi, (uint)portNumber, "RtMidi Input");

            // RtMidi ignores timing messages by default; accept them so MIDI
            // clock (0xF8) and friends reach MidiSystemRealtime. Sysex and
            // active sensing stay ignored.
            RtMidiDll.InIgnoreTypes(_rtmidi, true, false, true);
        }

        ~MidiPort()
        {
            if (_rtmidi == null || !_rtmidi->ok) return;
            RtMidiDll.InFree(_rtmidi);
        }

        public void Dispose()
        {
            if (_rtmidi == null || !_rtmidi->ok) return;

            RtMidiDll.InFree(_rtmidi);
            _rtmidi = null;

            foreach (var dev in _channels)
                if (dev != null) InputSystem.RemoveDevice(dev);

            System.GC.SuppressFinalize(this);
        }

        public void ProcessMessageQueue()
        {
            if (_rtmidi == null || !_rtmidi->ok) return;

            const int bufferSize = 32;
            var message = stackalloc byte [bufferSize];

            while (true)
            {
                var size = (ulong)bufferSize;

                var stamp = RtMidiDll.InGetMessage(_rtmidi, message, ref size);
                if (size == 0) break; // Queue drained

                // Accumulated per-port message time from RtMidi's
                // inter-message delta stamps: driver-side precision, unlike
                // the frame-quantized time at which we poll the queue.
                _portTime += stamp;

                if (size == 1)
                {
                    // System-realtime messages are port-wide (no channel).
                    switch (message[0])
                    {
                        case 0xf8: MidiSystemRealtime.InvokeClock(_portName, _portTime); break;
                        case 0xfa: MidiSystemRealtime.InvokeStart(_portName); break;
                        case 0xfb: MidiSystemRealtime.InvokeContinue(_portName); break;
                        case 0xfc: MidiSystemRealtime.InvokeStop(_portName); break;
                    }
                    continue;
                }

                if (size != 3) continue; // 2-byte messages (MTC quarter-frame, program change...)

                var status = message[0] >> 4;
                var channel = message[0] & 0xf;
                var data1 = message[1];
                var data2 = message[2];

                if (status == 0xf) continue; // 3-byte system common (song position)

                if (data1 > 0x7f || data2 > 0x7f) continue; // Invalid data

                var noteOff = (status == 8) || (status == 9 && data2 == 0);

                if (status == 9 && !noteOff)
                    GetChannelDevice(channel).ProcessNoteOn(data1, data2);
                else if (noteOff)
                    GetChannelDevice(channel).ProcessNoteOff(data1);
                else if (status == 0xb)
                    GetChannelDevice(channel).ProcessControlChange(data1, data2);
            }
        }

        #endregion
    }
}
