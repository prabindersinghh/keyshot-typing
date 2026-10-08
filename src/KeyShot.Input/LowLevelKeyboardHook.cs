using System.ComponentModel;
using System.Runtime.InteropServices;
using KeyShot.Core;
using static KeyShot.Input.NativeMethods;

namespace KeyShot.Input;

/// <summary>
/// Global WH_KEYBOARD_LL hook running on a dedicated high-priority thread.
/// It only observes: every event is passed on unchanged via CallNextHookEx,
/// and nothing is ever injected back into the input stream.
/// </summary>
public sealed class LowLevelKeyboardHook : IDisposable
{
    private readonly Action<KeyInput> _handler;
    private readonly LowLevelKeyboardProc _proc; // rooted so the GC never collects the callback
    private Thread? _thread;
    private uint _threadId;
    private IntPtr _hook;

    public LowLevelKeyboardHook(Action<KeyInput> handler)
    {
        _handler = handler;
        _proc = HookCallback;
    }

    public bool IsRunning => _hook != IntPtr.Zero;

    /// <summary>Raised on the hook thread if the handler throws. The key still passes through.</summary>
    public event Action<Exception>? HandlerFailed;

    public void Start()
    {
        if (_thread != null) return;

        using var ready = new ManualResetEventSlim();
        Exception? startError = null;

        _thread = new Thread(() =>
        {
            _threadId = GetCurrentThreadId();
            _hook = SetWindowsHookEx(WH_KEYBOARD_LL, _proc, GetModuleHandle(null), 0);
            if (_hook == IntPtr.Zero)
                startError = new Win32Exception(Marshal.GetLastWin32Error(), "SetWindowsHookEx failed");
            ready.Set();
            if (_hook == IntPtr.Zero) return;

            // LL hooks are called on the installing thread, which must pump messages.
            while (GetMessage(out _, IntPtr.Zero, 0, 0) > 0) { }

            UnhookWindowsHookEx(_hook);
            _hook = IntPtr.Zero;
        })
        {
            Name = "KeyShot keyboard hook",
            IsBackground = true,
            Priority = ThreadPriority.Highest,
        };
        _thread.Start();
        ready.Wait();

        if (startError != null)
        {
            _thread = null;
            throw startError;
        }
    }

    public void Stop()
    {
        var thread = _thread;
        if (thread == null) return;
        PostThreadMessage(_threadId, WM_QUIT, IntPtr.Zero, IntPtr.Zero);
        thread.Join(TimeSpan.FromSeconds(2));
        _thread = null;
    }

    public void Dispose() => Stop();

    private unsafe IntPtr HookCallback(int nCode, IntPtr wParam, IntPtr lParam)
    {
        if (nCode >= 0)
        {
            try
            {
                int msg = (int)wParam;
                bool down = msg is WM_KEYDOWN or WM_SYSKEYDOWN;
                if (down || msg is WM_KEYUP or WM_SYSKEYUP)
                {
                    var data = (KBDLLHOOKSTRUCT*)lParam; // direct read: no marshalling allocation
                    bool injected = (data->flags & (LLKHF_INJECTED | LLKHF_LOWER_IL_INJECTED)) != 0;
                    _handler(new KeyInput((int)data->vkCode, down, injected, down && ShortcutModifierHeld()));
                }
            }
            catch (Exception ex)
            {
                try { HandlerFailed?.Invoke(ex); } catch { /* never let anything escape the hook */ }
            }
        }

        // Always forward: KeyShot never swallows, delays or alters input.
        return CallNextHookEx(IntPtr.Zero, nCode, wParam, lParam);
    }

    private static bool IsDown(int vk) => (GetAsyncKeyState(vk) & 0x8000) != 0;

    /// <summary>Ctrl, Alt or Win held, treating Ctrl+Alt together as AltGr (typing, not a shortcut).</summary>
    private static bool ShortcutModifierHeld()
    {
        bool ctrl = IsDown(VirtualKeys.Control);
        bool alt = IsDown(VirtualKeys.Menu);
        bool win = IsDown(VirtualKeys.LWin) || IsDown(VirtualKeys.RWin);
        return win || (ctrl ^ alt);
    }
}
