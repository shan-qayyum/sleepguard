// Windows calls used outside the input hooks: idle time, locking, the media play/pause key, display off,
// and the "is sound playing" meter.

using System;
using System.Runtime.InteropServices;

namespace SleepGuard
{
    public static class Native
    {
        [StructLayout(LayoutKind.Sequential)] struct LastInputInfo { public uint Size; public uint Time; }
        [DllImport("user32.dll")] static extern bool GetLastInputInfo(ref LastInputInfo info);
        [DllImport("user32.dll")] public static extern bool LockWorkStation();
        [DllImport("user32.dll")] static extern bool PostMessage(IntPtr hwnd, uint msg, IntPtr wParam, IntPtr lParam);
        [DllImport("user32.dll", SetLastError = true)] static extern uint SendInput(uint count, Input[] inputs, int size);

        [StructLayout(LayoutKind.Sequential)]
        struct Input { public int Type; public InputUnion U; }
        [StructLayout(LayoutKind.Explicit)]
        struct InputUnion { [FieldOffset(0)] public MouseInput Mouse; [FieldOffset(0)] public KeyboardInput Keyboard; }
        [StructLayout(LayoutKind.Sequential)]
        struct MouseInput { public int Dx, Dy; public uint MouseData, Flags, Time; public IntPtr ExtraInfo; }
        [StructLayout(LayoutKind.Sequential)]
        struct KeyboardInput { public ushort Vk, Scan; public uint Flags, Time; public IntPtr ExtraInfo; }

        // Key presses sent by SleepGuard itself carry this tag, so its own input block lets them through.
        public static readonly IntPtr OwnInputTag = (IntPtr)0x53475244;   // "SGRD"

        // Milliseconds-based tick of the last keyboard or mouse input, or 0 if Windows does not say.
        public static uint LastInputTick()
        {
            var info = new LastInputInfo { Size = (uint)Marshal.SizeOf(typeof(LastInputInfo)) };
            return GetLastInputInfo(ref info) ? info.Time : 0;
        }

        // Presses the media Play/Pause key, which browsers and media players obey.
        public static bool PressPlayPause()
        {
            const ushort VK_MEDIA_PLAY_PAUSE = 0xB3;
            const uint KEYEVENTF_KEYUP = 2;
            var down = new Input { Type = 1 };
            down.U.Keyboard = new KeyboardInput { Vk = VK_MEDIA_PLAY_PAUSE, ExtraInfo = OwnInputTag };
            var up = new Input { Type = 1 };
            up.U.Keyboard = new KeyboardInput { Vk = VK_MEDIA_PLAY_PAUSE, Flags = KEYEVENTF_KEYUP, ExtraInfo = OwnInputTag };
            return SendInput(2, new[] { down, up }, Marshal.SizeOf(typeof(Input))) == 2;
        }

        public static void TurnDisplayOff()
        {
            PostMessage((IntPtr)0xFFFF /* HWND_BROADCAST */, 0x0112 /* WM_SYSCOMMAND */, (IntPtr)0xF170 /* SC_MONITORPOWER */, (IntPtr)2);
        }
    }

    // Peak level of everything playing on the default speakers, via Windows Core Audio.
    public sealed class AudioMeter
    {
        [ComImport, Guid("BCDE0395-E52F-467C-8E3D-C4579291692E")] class MMDeviceEnumeratorCom { }

        [ComImport, Guid("A95664D2-9614-4F35-A746-DE8DB63617E6"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
        interface IMMDeviceEnumerator
        {
            [PreserveSig] int EnumAudioEndpoints(int dataFlow, int stateMask, out IntPtr devices);
            [PreserveSig] int GetDefaultAudioEndpoint(int dataFlow, int role, out IMMDevice device);
        }

        [ComImport, Guid("D666063F-1587-4E43-81F1-B948E807363F"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
        interface IMMDevice
        {
            [PreserveSig] int Activate(ref Guid iid, int clsCtx, IntPtr activationParams, [MarshalAs(UnmanagedType.IUnknown)] out object iface);
        }

        [ComImport, Guid("C02216F6-8C67-4B5B-9D00-D008E73E0064"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
        interface IAudioMeterInformation
        {
            [PreserveSig] int GetPeakValue(out float peak);
        }

        IAudioMeterInformation meter;
        DateTime nextRetry = DateTime.MinValue;

        // 0..1, or -1 if the level cannot be read (no speakers, audio service stopped).
        public float Peak()
        {
            if (meter == null && DateTime.Now >= nextRetry) Open();
            if (meter == null) return -1;
            float peak;
            if (meter.GetPeakValue(out peak) == 0) return peak;
            meter = null;                         // device changed (e.g. headphones unplugged): reopen next time
            return -1;
        }

        void Open()
        {
            nextRetry = DateTime.Now.AddSeconds(10);
            try
            {
                var enumerator = (IMMDeviceEnumerator)new MMDeviceEnumeratorCom();
                IMMDevice device;
                if (enumerator.GetDefaultAudioEndpoint(0 /* render */, 1 /* multimedia */, out device) != 0) return;
                Guid iid = typeof(IAudioMeterInformation).GUID;
                object o;
                if (device.Activate(ref iid, 23 /* CLSCTX_ALL */, IntPtr.Zero, out o) != 0) return;
                meter = (IAudioMeterInformation)o;
            }
            catch (COMException) { }
            catch (InvalidCastException) { }
        }
    }
}
