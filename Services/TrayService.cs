using System.Drawing;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace KeyboardController.Services;

public class TrayService
{
    // --- Win32 API ---

    [DllImport("kernel32.dll")]
    private static extern IntPtr GetConsoleWindow();

    [DllImport("user32.dll", EntryPoint = "GetWindowLongPtr")]
    private static extern IntPtr GetWindowLongPtr64(IntPtr hWnd, int nIndex);

    [DllImport("user32.dll", EntryPoint = "GetWindowLong")]
    private static extern IntPtr GetWindowLongPtr32(IntPtr hWnd, int nIndex);

    private static IntPtr GetWindowLongPtr(IntPtr hWnd, int nIndex) =>
        IntPtr.Size == 8 ? GetWindowLongPtr64(hWnd, nIndex) : GetWindowLongPtr32(hWnd, nIndex);

    [DllImport("user32.dll", EntryPoint = "SetWindowLongPtr")]
    private static extern IntPtr SetWindowLongPtr64(IntPtr hWnd, int nIndex, IntPtr dwNewLong);

    [DllImport("user32.dll", EntryPoint = "SetWindowLong")]
    private static extern IntPtr SetWindowLongPtr32(IntPtr hWnd, int nIndex, IntPtr dwNewLong);

    private static IntPtr SetWindowLongPtr(IntPtr hWnd, int nIndex, IntPtr dwNewLong) =>
        IntPtr.Size == 8 ? SetWindowLongPtr64(hWnd, nIndex, dwNewLong) : SetWindowLongPtr32(hWnd, nIndex, dwNewLong);

    [DllImport("user32.dll")]
    private static extern bool SetWindowPos(IntPtr hWnd, IntPtr hWndInsertAfter, int X, int Y, int cx, int cy, uint uFlags);

    // --- Win32 API для прямого доступа к Буферу обмена (без COM/Ole32) ---
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

    // --- Константы окна ---
    private const int GWL_EXSTYLE = -20;
    private const long WS_EX_TOOLWINDOW = 0x00000080L;
    private const long WS_EX_APPWINDOW = 0x00040000L;

    private const uint SWP_NOSIZE = 0x0001;
    private const uint SWP_NOMOVE = 0x0002;
    private const uint SWP_NOZORDER = 0x0004;
    private const uint SWP_FRAMECHANGED = 0x0020;

    private const int SW_HIDE = 0;
    private const int SW_SHOW = 5;

    private NotifyIcon? _notifyIcon;
    private IntPtr _consoleHandle;
    private bool _isConsoleVisible = true;
    private string _publicUrl = string.Empty;

    public void Initialize(string initialUrl)
    {
        _publicUrl = initialUrl;
        _consoleHandle = GetConsoleWindow();

        RemoveFromTaskbar();

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
            _notifyIcon.ShowBalloonTip(3000, "Remote Controller", "Application started and minimized to tray.", ToolTipIcon.Info);

            Application.Run();
        });

        trayThread.SetApartmentState(ApartmentState.STA);
        trayThread.IsBackground = true;
        trayThread.Start();
    }

    /// <summary>
    /// Надежная загрузка иконки: сначала из ресурсов, затем из самого .exe файла
    /// </summary>
    private static Icon LoadAppIcon()
    {
        try
        {
            var assembly = Assembly.GetExecutingAssembly();
            // Ищем любой вшитый .ico ресурс, независимо от названия проекта
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

            // Резервный способ: берем иконку напрямую из заголовка скомпилированного .exe
            var exeIcon = Icon.ExtractAssociatedIcon(Application.ExecutablePath);
            if (exeIcon != null)
            {
                return exeIcon;
            }
        }
        catch
        {
            // Игнорируем и переходим к стандартной
        }

        return SystemIcons.Application;
    }

    /// <summary>
    /// Копирование текста напрямую через Win32 API (не падает при тримминге!)
    /// </summary>
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
                // Нулевой терминатор в конце строки
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

    private void RemoveFromTaskbar()
    {
        if (_consoleHandle == IntPtr.Zero) return;

        long exStyle = GetWindowLongPtr(_consoleHandle, GWL_EXSTYLE).ToInt64();
        exStyle &= ~WS_EX_APPWINDOW;
        exStyle |= WS_EX_TOOLWINDOW;

        SetWindowLongPtr(_consoleHandle, GWL_EXSTYLE, new IntPtr(exStyle));
        SetWindowPos(_consoleHandle, IntPtr.Zero, 0, 0, 0, 0,
            SWP_NOMOVE | SWP_NOSIZE | SWP_NOZORDER | SWP_FRAMECHANGED);
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