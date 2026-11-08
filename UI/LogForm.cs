using KeyboardController.Services;

namespace KeyboardController;

public class LogForm : Form
{
    private readonly TextBox _box = new()
    {
        Multiline = true,
        ReadOnly = true,
        ScrollBars = ScrollBars.Vertical,
        Dock = DockStyle.Fill,
        BackColor = Color.Black,
        ForeColor = Color.LightGray,
        Font = new Font("Consolas", 10),
        MaxLength = int.MaxValue // default limit is too small for long logs
    };

    public LogForm()
    {
        Text = "Remote Keyboard Controller - Logs";
        Size = new Size(700, 400);
        StartPosition = FormStartPosition.CenterScreen;
        Controls.Add(_box);

        AppLog.LineAdded += OnLineAdded;

        // Every time the window becomes visible, reload the whole history
        VisibleChanged += (_, _) =>
        {
            if (Visible) ReloadAll();
        };
    }

    private void ReloadAll()
    {
        var lines = AppLog.GetAll();
        _box.Text = string.Concat(lines.Select(l => l + Environment.NewLine));

        // Scroll to the bottom
        _box.SelectionStart = _box.TextLength;
        _box.ScrollToCaret();
    }

    private void OnLineAdded(string line)
    {
        // The window handle does not exist until the form was shown at least once
        if (!IsHandleCreated) return;

        // This event comes from a non-UI thread, so we must marshal to the UI thread
        BeginInvoke(() =>
        {
            // When hidden we skip appending, ReloadAll will load everything on next show
            if (Visible)
                _box.AppendText(line + Environment.NewLine);
        });
    }

    protected override void OnFormClosing(FormClosingEventArgs e)
    {
        // Closing with the X button only hides the window, the app keeps running
        if (e.CloseReason == CloseReason.UserClosing)
        {
            e.Cancel = true;
            Hide();
            return;
        }

        base.OnFormClosing(e);
    }
}