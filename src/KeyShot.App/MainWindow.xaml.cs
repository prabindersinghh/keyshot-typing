using System.ComponentModel;
using System.Windows;
using System.Windows.Input;
using KeyShot.Core;

namespace KeyShot.App;

public partial class MainWindow : Window
{
    private readonly AppHost _host;
    private readonly MainViewModel _vm;

    public MainWindow(AppHost host)
    {
        _host = host;
        _vm = new MainViewModel(host);
        DataContext = _vm;
        InitializeComponent();
        // Never taller than the usable screen (small laptops, high scaling); content scrolls instead.
        MaxHeight = SystemParameters.WorkArea.Height;
    }

    private void Toggle_Click(object sender, RoutedEventArgs e) => _host.Toggle();

    private void Test_Click(object sender, RoutedEventArgs e) => _host.TestShot();

    private void Minimize_Click(object sender, RoutedEventArgs e) => WindowState = WindowState.Minimized;

    private void Close_Click(object sender, RoutedEventArgs e) => Close();

    protected override void OnClosing(CancelEventArgs e)
    {
        if (_vm.CapturingHotkey) StopCapture(null);
        if (_host.Settings.CloseToTray)
        {
            e.Cancel = true;
            Hide();
            _host.OnWindowClosedToTray();
            return;
        }
        base.OnClosing(e);
        _host.Exit();
    }

    private void ChangeHotkey_Click(object sender, RoutedEventArgs e)
    {
        if (_vm.CapturingHotkey)
        {
            StopCapture(null);
            return;
        }
        _host.BeginHotkeyCapture();
        _vm.CapturingHotkey = true;
    }

    protected override void OnPreviewKeyDown(KeyEventArgs e)
    {
        if (!_vm.CapturingHotkey)
        {
            base.OnPreviewKeyDown(e);
            return;
        }

        e.Handled = true;
        var key = e.Key == Key.System ? e.SystemKey : e.Key;
        if (key == Key.Escape)
        {
            StopCapture(null);
            return;
        }
        if (key is Key.LeftCtrl or Key.RightCtrl or Key.LeftShift or Key.RightShift or Key.LeftAlt or Key.RightAlt
            or Key.LWin or Key.RWin)
        {
            return; // wait for the non-modifier key
        }

        var mods = HotkeyModifiers.None;
        if (Keyboard.Modifiers.HasFlag(ModifierKeys.Control)) mods |= HotkeyModifiers.Ctrl;
        if (Keyboard.Modifiers.HasFlag(ModifierKeys.Shift)) mods |= HotkeyModifiers.Shift;
        if (Keyboard.Modifiers.HasFlag(ModifierKeys.Alt)) mods |= HotkeyModifiers.Alt;
        if (Keyboard.Modifiers.HasFlag(ModifierKeys.Windows)) mods |= HotkeyModifiers.Win;

        var gesture = new HotkeyGesture(mods, KeyInterop.VirtualKeyFromKey(key));
        // Round-trip through the parser so only gestures that can be saved are accepted.
        if (mods == HotkeyModifiers.None || !HotkeyGesture.TryParse(gesture.ToString(), out gesture)) return;
        StopCapture(gesture);
    }

    private void StopCapture(HotkeyGesture? gesture)
    {
        _vm.CapturingHotkey = false;
        _host.EndHotkeyCapture(gesture);
    }
}
