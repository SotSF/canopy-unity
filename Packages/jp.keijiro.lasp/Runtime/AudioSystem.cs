using System.Collections.Generic;
using System.Linq;
using UnityEngine.LowLevel;
using PInvokeCallbackAttribute = AOT.MonoPInvokeCallbackAttribute;

namespace Lasp
{
    //
    // Audio system class
    //
    // This class manages a global libsoundio context and a list of devices
    // found in the context. It's also in charge of invoking the Update
    // function of the device handle class using a Player Loop System.
    //
    public static class AudioSystem
    {
        #region Public members

        public static IEnumerable<DeviceDescriptor> InputDevices
          => InputDeviceList.
             Select(dev => new DeviceDescriptor { _handle = dev });

        public static DeviceDescriptor GetInputDevice(string id)
          => new DeviceDescriptor
             { _handle = InputDeviceList.FirstOrDefault(dev => dev.ID == id) };

        public static DeviceDescriptor DefaultDevice
          => new DeviceDescriptor{ _handle = InputDeviceList.FirstOrDefault() };

        public static InputStream GetInputStream(DeviceDescriptor desc)
          => InputStream.Create(desc._handle);

        public static InputStream GetDefaultInputStream()
          => InputStream.Create(InputDeviceList.FirstOrDefault());

        public static InputStream GetInputStream(string id)
          => GetInputStream(GetInputDevice(id));

        #endregion

        #region Fast Enter Play Mode reset

        // With Domain Reload disabled, static state persists across play-mode
        // sessions. OnExit is only wired to assembly-reload / editor-quit
        // events, so it never runs on play-mode exit when reload is off.
        // Reset all mutable static state to its original values on every play
        // entry. OnExit disposes the stale native context/device list, then we
        // restore the inline initial values.
        [UnityEngine.RuntimeInitializeOnLoadMethod(
            UnityEngine.RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStaticState()
        {
            OnExit();                              // dispose + null _context/_inputDeviceList
            _inputDeviceList = new InputDeviceList();
            _shouldScanDevices = true;
            // _context already nulled by OnExit; rebuilt lazily on next access.
        }

        #endregion

        #region Device list management

        static bool _shouldScanDevices = true;

        static InputDeviceList InputDeviceList => CheckAndGetInputDeviceList();
        static InputDeviceList _inputDeviceList = new InputDeviceList();

        static InputDeviceList CheckAndGetInputDeviceList()
        {
            Context.FlushEvents();
            if (_shouldScanDevices)
            {
                _inputDeviceList.ScanAvailable(Context);
                _shouldScanDevices = false;
            }
            return _inputDeviceList;
        }

        #endregion

        #region libsoundio context management

        static SoundIO.Context Context => GetContextWithLazyInitialization();
        static SoundIO.Context _context;

        static SoundIO.Context GetContextWithLazyInitialization()
        {
            if (_context == null)
            {
                // libsoundio context initialization
                _context = SoundIO.Context.Create();
                _context.OnDevicesChange = _onDevicesChangeDelegate;
                _context.Connect();
                _context.FlushEvents();

                // Install the Player Loop System.
                InsertPlayerLoopSystem();

                // Install the "on-exit" callback.
            #if UNITY_EDITOR
                UnityEditor.AssemblyReloadEvents.beforeAssemblyReload += OnExit;
                UnityEditor.EditorApplication.quitting += OnExit;
            #else
                UnityEngine.Application.quitting += OnExit;
            #endif
            }

            return _context;
        }

        static void OnExit()
        {
            _inputDeviceList?.Dispose();
            _inputDeviceList = null;

            _context?.Dispose();
            _context = null;
        }

        #endregion

        #region libsoundio context callback delegate

        static readonly SoundIO.Context.OnDevicesChangeDelegate _onDevicesChangeDelegate
          = new SoundIO.Context.OnDevicesChangeDelegate(OnDevicesChange);

        [PInvokeCallback(typeof(SoundIO.Context.OnDevicesChangeDelegate))]
        static void OnDevicesChange(System.IntPtr pointer)
          => _shouldScanDevices = true;

        #endregion

        #region Update method implementation

        static void Update()
        {
            Context.FlushEvents();
            _inputDeviceList.UpdateAll(UnityEngine.Time.deltaTime);
        }

        #endregion

        #region PlayerLoopSystem implementation

        static void InsertPlayerLoopSystem()
        {
            // Append a custom system to the Early Update phase.

            var customSystem = new PlayerLoopSystem()
            {
                type = typeof(AudioSystem),
                updateDelegate = () => AudioSystem.Update()
            };

            var playerLoop = PlayerLoop.GetCurrentPlayerLoop();

            for (var i = 0; i < playerLoop.subSystemList.Length; i++)
            {
                ref var phase = ref playerLoop.subSystemList[i];
                if (phase.type == typeof(UnityEngine.PlayerLoop.EarlyUpdate))
                {
                    phase.subSystemList = phase.subSystemList.
                        Concat(new[]{ customSystem }).ToArray();
                    break;
                }
            }

            PlayerLoop.SetPlayerLoop(playerLoop);
        }

        #endregion
    }
}
