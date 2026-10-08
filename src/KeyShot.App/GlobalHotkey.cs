using System.Runtime.InteropServices;
using System.Windows.Interop;
using KeyShot.Core;

namespace KeyShot.App;

/// <summary>System-wide hotkey via RegisterHotKey on a message-only window.</summary>
internal sealed class GlobalHotkey : IDisposable
{
    private const int WM_HOTKEY = 0x0312;
    private const uint MOD_NOREPEAT = 0x4000;
    private const int HotkeyId = 0x4B53; // "KS"

    private readonly HwndSource _source;
    private bool _registered;

    public GlobalHotkey()
    {
        _source = new HwndSource(new HwndSourceParameters("KeyShot.Hotkey")
        {
            ParentWindow = new IntPtr(-3), // HWND_MESSAGE: invisible, never activated
            WindowStyle = 0,
        });
        _source.AddHook(WndProc);
    }

    public event Action? Pressed;

    public HotkeyGesture? Current { get; private set; }

    public bool Register(HotkeyGesture gesture)
    {
        Unregister();
        _registered = RegisterHotKey(_source.Handle, HotkeyId, (uint)gesture.Modifiers | MOD_NOREPEAT, (uint)gesture.VirtualKey);
        Current = _registered ? gesture : null;
        return _registered;
    }

    public void Unregister()
    {
        if (!_registered) return;
        UnregisterHotKey(_source.Handle, HotkeyId);
        _registered = false;
        Current = null;
    }

    public void Dispose()
    {
        Unregister();
        _source.RemoveHook(WndProc);
        _source.Dispose();
    }

    private IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        if (msg == WM_HOTKEY && wParam.ToInt32() == HotkeyId)
        {
            handled = true;
            Pressed?.Invoke();
        }
        return IntPtr.Zero;
    }

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool RegisterHotKey(IntPtr hWnd, int id, uint fsModifiers, uint vk);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool UnregisterHotKey(IntPtr hWnd, int id);
}
