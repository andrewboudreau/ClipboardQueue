using System.Runtime.InteropServices;
using System.Diagnostics;
using System.Text;

namespace ClipboardQueue;

public enum WindowState
{
    Normal,
    Minimized,
    Maximized
}

public delegate IntPtr LowLevelKeyboardProc(int nCode, IntPtr wParam, IntPtr lParam);

public partial class MainForm : Form
{
    private readonly Queue<string> clipboardQueue = new();
    private readonly LowLevelKeyboardProc _proc;
    private readonly List<char> operationHistory = new();

    private const int WM_CLIPBOARDUPDATE = 0x031D;
    private bool isListening = false;
    private IntPtr _hookId = IntPtr.Zero;

    private const char CUT_ICON = '✂';
    private const char COPY_ICON = 'C';//'📋';
    private const char PASTE_ICON = 'P';//'📄';

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool AddClipboardFormatListener(IntPtr hwnd);

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool RemoveClipboardFormatListener(IntPtr hwnd);

    [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
    private static extern IntPtr SetWindowsHookEx(int idHook, LowLevelKeyboardProc lpfn, IntPtr hMod, uint dwThreadId);

    [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool UnhookWindowsHookEx(IntPtr hhk);

    [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
    private static extern IntPtr CallNextHookEx(IntPtr hhk, int nCode, IntPtr wParam, IntPtr lParam);

    [DllImport("kernel32.dll", CharSet = CharSet.Auto, SetLastError = true)]
    private static extern IntPtr GetModuleHandle(string lpModuleName);

    private const int WH_KEYBOARD_LL = 13;
    private const int WM_KEYDOWN = 0x0100;
    private const int VK_CONTROL = 0x11;
    private const int VK_C = 0x43;
    private const int VK_P = 0x50;
    private const int VK_V = 0x56;
    private const int VK_X = 0x58;

    public MainForm()
    {
        InitializeComponent();
        UpdateStatusLabel();
        _proc = HookCallback;

        // Set up the notify icon
        notifyIcon1.Icon = SystemIcons.Application; // Use the default application icon
        notifyIcon1.Visible = true;

        // Set up the notify icon context menu
        ContextMenuStrip contextMenu = new();
        contextMenu.Items.Add("Show", null, ShowForm);
        contextMenu.Items.Add("Exit", null, ExitApplication);
        notifyIcon1.ContextMenuStrip = contextMenu;

        // Hide the form on startup
        this.WindowState = FormWindowState.Minimized;
        this.ShowInTaskbar = false;

        // Attach listeners
        ToggleListeners(true);
    }

    private void ShowForm(object? sender, EventArgs e)
    {
        Show();
        this.WindowState = FormWindowState.Normal;
    }

    private void ExitApplication(object? sender, EventArgs e)
    {
        Application.Exit();
    }

    private void NotifyIcon1_MouseClick(object sender, MouseEventArgs e)
    {
        if (e.Button == MouseButtons.Left)
        {
            ShowForm(sender, e);
        }
    }

    protected override void OnResize(EventArgs e)
    {
        if (WindowState == FormWindowState.Minimized)
        {
            Hide();
        }

        base.OnResize(e);
    }

    protected override void WndProc(ref Message m)
    {
        switch (m.Msg)
        {
            case WM_CLIPBOARDUPDATE:
                OnClipboardChanged();
                break;
        }
        base.WndProc(ref m);
    }


    private void ToggleListeners(bool attach)
    {
        if (attach)
        {
            AddClipboardFormatListener(this.Handle);
            _hookId = SetWindowsHookEx(WH_KEYBOARD_LL, _proc, GetModuleHandle(Process.GetCurrentProcess().MainModule.ModuleName), 0);
            isListening = true;
            toggleListenerMenuItem.Text = "Detach Listeners";
        }
        else
        {
            RemoveClipboardFormatListener(this.Handle);
            UnhookWindowsHookEx(_hookId);
            isListening = false;
            toggleListenerMenuItem.Text = "Attach Listeners";
        }
        UpdateStatusLabel();
    }

    private void ToggleClipboardListener(object sender, EventArgs e)
    {
        ToggleListeners(!isListening);
    }

    private void OnClipboardChanged()
    {
        if (Clipboard.ContainsText())
        {
            string text = Clipboard.GetText();
            clipboardQueue.Enqueue(text);
            UpdateStatusLabel();
            AddToHistory(COPY_ICON);
        }
    }

    private void AddToHistory(char operation)
    {
        operationHistory.Insert(0, operation);
        if (operationHistory.Count > 20)
        {
            operationHistory.RemoveAt(20);
        }
        UpdateHistoryLabel();
    }

    private void UpdateHistoryLabel()
    {
        StringBuilder history = new StringBuilder();
        foreach (char op in operationHistory)
        {
            history.Append(op);
        }
        historyStatusLabel.Text = history.ToString();
    }

    private void UpdateStatusLabel()
    {
        toolStripStatusLabel1.Text = $"Items in queue: {clipboardQueue.Count} | Listener: {(isListening ? "Active" : "Inactive")}";
        UpdateQueueListBox();
    }

    private void UpdateQueueListBox()
    {
        queueListBox.BeginUpdate();
        queueListBox.Items.Clear();
        foreach (var item in clipboardQueue.Reverse())
        {
            queueListBox.Items.Add(TruncateString(item, 100));
        }
        queueListBox.EndUpdate();
    }

    private static string TruncateString(string str, int maxLength)
    {
        string result = str.Replace("\r\n", "");
        if (string.IsNullOrEmpty(result)) return result;
        return result.Length <= maxLength ? result : string.Concat(result.AsSpan(0, maxLength - 3), "...");
    }


    private IntPtr HookCallback(int nCode, IntPtr wParam, IntPtr lParam)
    {
        if (nCode >= 0 && wParam == (IntPtr)WM_KEYDOWN)
        {
            int vkCode = Marshal.ReadInt32(lParam);
            if (IsKeyPressed(VK_CONTROL))
            {
                switch (vkCode)
                {
                    case VK_C:
                        OnCopyDetected();
                        break;
                    case VK_V:
                        OnPasteDetected();
                        break;
                    case VK_X:
                        OnCutDetected();
                        break;
                    case VK_P:
                        PrintQueue();
                        break;
                }
            }
        }
        return CallNextHookEx(_hookId, nCode, wParam, lParam);
    }

    [DllImport("user32.dll")]
    private static extern short GetAsyncKeyState(int vKey);

    private static bool IsKeyPressed(int vKey)
    {
        return (GetAsyncKeyState(vKey) & 0x8000) != 0;
    }

    private void OnCopyDetected()
    {
        // The actual copying is handled by OnClipboardChanged
        // This method can be used for additional actions if needed
        AddToHistory(COPY_ICON);
    }

    private void OnPasteDetected()
    {
        if (clipboardQueue.Count > 0)
        {
            string text = clipboardQueue.Dequeue();
            Clipboard.SetText(text);
            UpdateStatusLabel();
            AddToHistory(PASTE_ICON);
        }
    }

    private void OnCutDetected()
    {
        AddToHistory(CUT_ICON);
    }

    protected override void OnFormClosing(FormClosingEventArgs e)
    {
        ToggleListeners(false);
        base.OnFormClosing(e);
    }

    private void PrintQueue()
    {
        string queueContents = string.Join(Environment.NewLine, clipboardQueue);
        MessageBox.Show($"Current Queue Contents:{Environment.NewLine}{queueContents}", "Queue Contents");
    }
}
