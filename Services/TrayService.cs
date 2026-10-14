using System.Reflection;
using System.Runtime.InteropServices;

namespace KeyboardController.Services;

public class TrayService
{
    // --- Win32 API ---
    [DllImport("user32.dll")]
    private static extern bool OpenClipboard(IntPtr hWndNewOwner);

    [DllImport("user32.dll")]
    private static extern bool CloseClipboard();

    [DllImport("user32.dll")]
    private static extern bool EmptyClipboard();

    [DllImport("user32.dll")]
    private static extern IntPtr SetClipboardData(uint uFormat, IntPtr hMem);

    [DllImport("kernel32.dll")]
    private static extern IntPtr GlobalAlloc(uint uFlags, UIntPtr dwBytes);

    [DllImport("kernel32.dll")]
    private static extern IntPtr GlobalLock(IntPtr hMem);

    [DllImport("kernel32.dll")]
    private static extern bool GlobalUnlock(IntPtr hMem);

    private const uint CF_UNICODETEXT = 13;
    private const uint GMEM_MOVEABLE = 0x0002;


    private NotifyIcon? _notifyIcon;
    private string _publicUrl = string.Empty;

    public void Initialize(string initialUrl)
    {
        _publicUrl = initialUrl;

        var trayThread = new Thread(() =>
        {
            _notifyIcon = new NotifyIcon
            {
                Icon = LoadAppIcon(),
                Text = "Remote Keyboard Controller",
                Visible = true
            };

            var contextMenu = new ContextMenuStrip();

            var copyLinkItem = new ToolStripMenuItem("Copy link", null, (_, _) => CopyLinkToClipboard());
            var exitItem = new ToolStripMenuItem("Exit", null, (_, _) => ExitApplication());

            contextMenu.Items.Add(copyLinkItem);
            contextMenu.Items.Add(new ToolStripSeparator());
            contextMenu.Items.Add(exitItem);

            _notifyIcon.ContextMenuStrip = contextMenu;

            Application.Run();
        });

        trayThread.SetApartmentState(ApartmentState.STA);
        trayThread.IsBackground = true;
        trayThread.Start();
    }

    private static Icon LoadAppIcon()
    {
        try
        {
            var assembly = Assembly.GetExecutingAssembly();
            var resourceName = assembly.GetManifestResourceNames()
                .FirstOrDefault(n => n.EndsWith(".ico", StringComparison.OrdinalIgnoreCase));

            if (resourceName != null)
            {
                using var stream = assembly.GetManifestResourceStream(resourceName);
                if (stream != null)
                {
                    return new Icon(stream);
                }
            }

            var exeIcon = Icon.ExtractAssociatedIcon(Application.ExecutablePath);
            if (exeIcon != null)
            {
                return exeIcon;
            }
        }
        catch{}

        return SystemIcons.Application;
    }
    private void CopyLinkToClipboard()
    {
        if (string.IsNullOrEmpty(_publicUrl)) return;

        if (SetWin32ClipboardText(_publicUrl))
        {
            _notifyIcon?.ShowBalloonTip(2000, "Link copied", _publicUrl, ToolTipIcon.Info);
        }
    }

    private static bool SetWin32ClipboardText(string text)
    {
        if (!OpenClipboard(IntPtr.Zero)) return false;

        try
        {
            EmptyClipboard();
            int bytes = (text.Length + 1) * 2;
            IntPtr hGlobal = GlobalAlloc(GMEM_MOVEABLE, (UIntPtr)bytes);
            if (hGlobal == IntPtr.Zero) return false;

            IntPtr target = GlobalLock(hGlobal);
            if (target == IntPtr.Zero) return false;

            try
            {
                Marshal.Copy(text.ToCharArray(), 0, target, text.Length);
                Marshal.WriteInt16(target, text.Length * 2, 0);
            }
            finally
            {
                GlobalUnlock(hGlobal);
            }

            return SetClipboardData(CF_UNICODETEXT, hGlobal) != IntPtr.Zero;
        }
        finally
        {
            CloseClipboard();
        }
    }

    private void ExitApplication()
    {
        if (_notifyIcon != null)
        {
            _notifyIcon.Visible = false;
            _notifyIcon.Dispose();
        }

        Environment.Exit(0);
    }
}