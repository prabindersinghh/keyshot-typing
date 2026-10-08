using System.Drawing;
using System.Windows.Forms;
using Application = System.Windows.Application;

namespace KeyShot.App;

/// <summary>Notification-area icon with Enable / Disable / Settings / Exit.</summary>
internal sealed class TrayIcon : IDisposable
{
    private readonly NotifyIcon _icon;
    private readonly Icon _onIcon;
    private readonly Icon _offIcon;
    private readonly ToolStripMenuItem _enable;
    private readonly ToolStripMenuItem _disable;

    public TrayIcon(Action enable, Action disable, Action settings, Action exit)
    {
        _onIcon = LoadIcon("keyshot.ico");
        _offIcon = LoadIcon("keyshot-off.ico");

        _enable = new ToolStripMenuItem("Enable", null, (_, _) => enable());
        _disable = new ToolStripMenuItem("Disable", null, (_, _) => disable());
        var menu = new ContextMenuStrip();
        menu.Items.Add(_enable);
        menu.Items.Add(_disable);
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add(new ToolStripMenuItem("Settings", null, (_, _) => settings()) { Font = new Font(menu.Font, System.Drawing.FontStyle.Bold) });
        menu.Items.Add(new ToolStripMenuItem("Exit", null, (_, _) => exit()));

        _icon = new NotifyIcon
        {
            Icon = _onIcon,
            Text = "KeyShot",
            ContextMenuStrip = menu,
            Visible = true,
        };
        _icon.MouseClick += (_, e) => { if (e.Button == MouseButtons.Left) settings(); };
    }

    public void SetState(bool enabled, string hotkey)
    {
        _icon.Icon = enabled ? _onIcon : _offIcon;
        _icon.Text = $"KeyShot — {(enabled ? "ACTIVE" : "INACTIVE")} ({hotkey})";
        _enable.Checked = enabled;
        _enable.Enabled = !enabled;
        _disable.Checked = !enabled;
        _disable.Enabled = enabled;
    }

    public void Notify(string title, string text) =>
        _icon.ShowBalloonTip(2500, title, text, ToolTipIcon.None);

    public void Dispose()
    {
        _icon.Visible = false;
        _icon.Dispose();
        _onIcon.Dispose();
        _offIcon.Dispose();
    }

    private static Icon LoadIcon(string name)
    {
        var stream = Application.GetResourceStream(new Uri($"pack://application:,,,/Assets/{name}"))!.Stream;
        using (stream) return new Icon(stream, SystemInformation.SmallIconSize);
    }
}
