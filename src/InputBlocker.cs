// Swallows all keyboard input and all mouse clicks/scrolling system-wide while active, using low-level hooks.
// Mouse movement still passes, so the pointer moves but cannot click anything. Every swallowed key press is
// reported through KeyPressed (on the hook thread) so the app can read a PIN.
//
// The hooks live on their own thread that does nothing else. Windows lets input through, and can silently
// remove the hook, when a hook thread is too slow to answer, so it must never share a thread with UI work
// such as the deliberately slow PIN check.
//
// Limits (by Windows design): Ctrl+Alt+Del and Win+L cannot be blocked. Both lead only to password-protected
// screens (lock, sign out, switch user), so they cannot be used to get past the guard.

using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Threading;
using System.Windows.Forms;

namespace SleepGuard
{
    public sealed class InputBlocker : IDisposable
    {
        delegate IntPtr HookProc(int code, IntPtr wParam, IntPtr lParam);

        [DllImport("user32.dll", SetLastError = true)] static extern IntPtr SetWindowsHookEx(int id, HookProc proc, IntPtr module, uint threadId);
        [DllImport("user32.dll")] static extern bool UnhookWindowsHookEx(IntPtr hook);
        [DllImport("user32.dll")] static extern IntPtr CallNextHookEx(IntPtr hook, int code, IntPtr wParam, IntPtr lParam);
        [DllImport("kernel32.dll", CharSet = CharSet.Unicode)] static extern IntPtr GetModuleHandle(string name);
        [DllImport("kernel32.dll")] static extern uint GetCurrentThreadId();
        [DllImport("user32.dll")] static extern int GetMessage(out Msg msg, IntPtr hwnd, uint min, uint max);
        [DllImport("user32.dll")] static extern bool PostThreadMessage(uint threadId, uint msg, IntPtr wParam, IntPtr lParam);

        [StructLayout(LayoutKind.Sequential)]
        struct Msg { public IntPtr Hwnd; public uint Message; public IntPtr WParam, LParam; public uint Time; public int X, Y; }

        [StructLayout(LayoutKind.Sequential)]
        struct KeyInfo { public uint VkCode, ScanCode, Flags, Time; public IntPtr Extra; }

        const int WH_KEYBOARD_LL = 13, WH_MOUSE_LL = 14;
        const int WM_KEYDOWN = 0x0100, WM_SYSKEYDOWN = 0x0104, WM_MOUSEMOVE = 0x0200;
        const uint WM_QUIT = 0x0012;

        readonly HookProc keyboardProc, mouseProc;     // kept in fields so the GC cannot collect them while hooked
        IntPtr keyboardHook, mouseHook;
        Thread thread;
        uint threadId;

        // Raised on the hook thread for every key press while blocking. Handlers must return quickly.
        public event Action<Keys> KeyPressed;

        public bool Active { get { return thread != null; } }

        public InputBlocker()
        {
            keyboardProc = OnKeyboard;
            mouseProc = OnMouse;
        }

        // False if Windows refused the hooks.
        public bool Start()
        {
            if (Active) return true;
            bool ok = false;
            using (var ready = new ManualResetEvent(false))
            {
                var t = new Thread(delegate()
                {
                    threadId = GetCurrentThreadId();
                    IntPtr module = GetModuleHandle(Process.GetCurrentProcess().MainModule.ModuleName);
                    keyboardHook = SetWindowsHookEx(WH_KEYBOARD_LL, keyboardProc, module, 0);
                    mouseHook = SetWindowsHookEx(WH_MOUSE_LL, mouseProc, module, 0);
                    ok = keyboardHook != IntPtr.Zero && mouseHook != IntPtr.Zero;
                    ready.Set();
                    if (ok)
                    {
                        Msg msg;
                        while (GetMessage(out msg, IntPtr.Zero, 0, 0) > 0) { }   // hooks are called from this loop
                    }
                    if (keyboardHook != IntPtr.Zero) { UnhookWindowsHookEx(keyboardHook); keyboardHook = IntPtr.Zero; }
                    if (mouseHook != IntPtr.Zero) { UnhookWindowsHookEx(mouseHook); mouseHook = IntPtr.Zero; }
                });
                t.IsBackground = true;
                t.Priority = ThreadPriority.Highest;
                t.Name = "SleepGuard input block";
                t.Start();
                ready.WaitOne(5000);
                if (!ok) { t.Join(1000); return false; }
                thread = t;
            }
            return true;
        }

        // Windows silently drops a low-level hook whose callback was ever too slow. Re-attaching from time to time
        // while blocking means such a drop cannot leave the laptop unprotected for long.
        public bool Refresh()
        {
            if (!Active) return false;
            Stop();
            return Start();
        }

        public void Stop()
        {
            Thread t = thread;
            if (t == null) return;
            PostThreadMessage(threadId, WM_QUIT, IntPtr.Zero, IntPtr.Zero);
            t.Join(2000);
            thread = null;
        }

        IntPtr OnKeyboard(int code, IntPtr wParam, IntPtr lParam)
        {
            if (code < 0) return CallNextHookEx(keyboardHook, code, wParam, lParam);
            var info = (KeyInfo)Marshal.PtrToStructure(lParam, typeof(KeyInfo));
            if (info.Extra == Native.OwnInputTag)                 // SleepGuard's own keys (e.g. the sleep timer's pause)
                return CallNextHookEx(keyboardHook, code, wParam, lParam);
            int msg = wParam.ToInt32();
            if (msg == WM_KEYDOWN || msg == WM_SYSKEYDOWN)
            {
                Action<Keys> handler = KeyPressed;
                if (handler != null) handler((Keys)info.VkCode);
            }
            return (IntPtr)1;                                // swallow every other key, up and down
        }

        IntPtr OnMouse(int code, IntPtr wParam, IntPtr lParam)
        {
            if (code >= 0 && wParam.ToInt32() != WM_MOUSEMOVE) return (IntPtr)1;   // swallow clicks and wheel
            return CallNextHookEx(mouseHook, code, wParam, lParam);
        }

        public void Dispose() { Stop(); }
    }
}
