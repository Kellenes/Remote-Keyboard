using System.Reflection;
using System.Runtime.InteropServices;
using System.ComponentModel;
using System.Diagnostics;
using System.Security;
using KeyboardController.Services;
using Microsoft.Win32;

namespace KeyboardController.UI;

public class TrayService
{
    // --- Win32 API ---
    [DllImport("user32.dll")]
    private static extern bool OpenClipboard(nint hWndNewOwner);

    [DllImport("user32.dll")]
    private static extern bool CloseClipboard();

    [DllImport("user32.dll")]
    private static extern bool EmptyClipboard();

    [DllImport("user32.dll")]
    private static extern nint SetClipboardData(uint uFormat, nint hMem);

    [DllImport("kernel32.dll")]
    private static extern nint GlobalAlloc(uint uFlags, nuint dwBytes);

    [DllImport("kernel32.dll")]
    private static extern nint GlobalLock(nint hMem);

    [DllImport("kernel32.dll")]
    private static extern bool GlobalUnlock(nint hMem);

    private const uint CF_UNICODETEXT = 13;
    private const uint GMEM_MOVEABLE = 0x0002;
    private const string EditorRegistryKey = @"Software\RemoteKeyboard\Editor";
    private const string EditorDownloadsUrl = "https://github.com/Kellenes/Remote-Keyboard-Grid-Builder/releases";


    private NotifyIcon? _notifyIcon;
    private LogForm? _logForm;
    private string _publicUrl = string.Empty;

    public void Initialize()
    {
        var trayThread = new Thread(() =>
        {
            _notifyIcon = new NotifyIcon
            {
                Icon = LoadAppIcon(),
                Text = "Remote Keyboard Controller",
                Visible = true
            };

            // The form MUST be created on this thread, because this thread runs the message loop
            _logForm = new LogForm();

            var contextMenu = new ContextMenuStrip();

            var showLogsItem = new ToolStripMenuItem("Show logs", null, (_, _) => ShowLogWindow());
            var copyLinkItem = new ToolStripMenuItem("Copy link", null, (_, _) => CopyLinkToClipboard());
            var openEditorItem = new ToolStripMenuItem("Open editor", null, (_, _) => OpenEditor());
            var exitItem = new ToolStripMenuItem("Exit", null, (_, _) => ExitApplication());

            contextMenu.Items.Add(showLogsItem);
            contextMenu.Items.Add(copyLinkItem);
            contextMenu.Items.Add(openEditorItem);
            contextMenu.Items.Add(new ToolStripSeparator());
            contextMenu.Items.Add(exitItem);

            _notifyIcon.ContextMenuStrip = contextMenu;

            // Double click on the tray icon also opens the logs
            _notifyIcon.DoubleClick += (_, _) => ShowLogWindow();

            // Open the window right at startup
            _logForm.Show();

            Application.Run();
        });

        trayThread.SetApartmentState(ApartmentState.STA);
        trayThread.IsBackground = true;
        trayThread.Start();
    }

    // Called later from Program.cs when the tunnel URL is ready
    public void SetUrl(string url)
    {
        _publicUrl = url;
    }

    private static void OpenEditor()
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(EditorRegistryKey);
            var editorPath = key?.GetValue("ExecutablePath") as string;

            if (editorPath != null && Path.IsPathFullyQualified(editorPath)
                && editorPath.EndsWith(".exe", StringComparison.OrdinalIgnoreCase)
                && File.Exists(editorPath))
            {
                using var editor = Process.Start(new ProcessStartInfo(editorPath)
                {
                    WorkingDirectory = Path.GetDirectoryName(editorPath)!,
                    UseShellExecute = false
                });
                return;
            }

            var answer = MessageBox.Show(
                "The editor could not be found. It may not have been downloaded, may not have been run yet, or may have been moved.\n\n"
                + "If you already have it, open it once from its current folder.\n\n"
                + "Open the download page?",
                "Remote Keyboard Editor", MessageBoxButtons.YesNo, MessageBoxIcon.Question,
                MessageBoxDefaultButton.Button2);

            if (answer == DialogResult.Yes)
            {
                using var browser = Process.Start(new ProcessStartInfo(EditorDownloadsUrl)
                {
                    UseShellExecute = true
                });
            }
        }
        catch (Exception ex) when (ex is Win32Exception or IOException or UnauthorizedAccessException or SecurityException)
        {
            AppLog.Write($"Could not open the editor or its download page: {ex.Message}");
            MessageBox.Show(ex.Message, "Remote Keyboard Editor", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    private void ShowLogWindow()
    {
        if (_logForm == null) return;

        if (!_logForm.Visible)
            _logForm.Show();

        // Restore if minimized and bring to front
        _logForm.WindowState = FormWindowState.Normal;
        _logForm.Activate();
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
        if (!OpenClipboard(nint.Zero)) return false;

        try
        {
            EmptyClipboard();
            int bytes = (text.Length + 1) * 2;
            nint hGlobal = GlobalAlloc(GMEM_MOVEABLE, (nuint)bytes);
            if (hGlobal == nint.Zero) return false;

            nint target = GlobalLock(hGlobal);
            if (target == nint.Zero) return false;

            try
            {
                Marshal.Copy(text.ToCharArray(), 0, target, text.Length);
                Marshal.WriteInt16(target, text.Length * 2, 0);
            }
            finally
            {
                GlobalUnlock(hGlobal);
            }

            return SetClipboardData(CF_UNICODETEXT, hGlobal) != nint.Zero;
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
