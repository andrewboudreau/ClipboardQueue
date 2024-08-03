using System.Runtime.InteropServices;
using System.Diagnostics;

namespace ClipboardQueue;

public delegate IntPtr LowLevelKeyboardProc(int nCode, IntPtr wParam, IntPtr lParam);

public partial class MainForm : Form
{
    private readonly Queue<string> clipboardQueue = new();
    private const int WM_CLIPBOARDUPDATE = 0x031D;
    private bool isListening = false;
    private LowLevelKeyboardProc _proc;
    private IntPtr _hookID = IntPtr.Zero;

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
    private const int VK_V = 0x56;
    private const int VK_P = 0x50;

    public MainForm()
    {
        InitializeComponent();
        AttachClipboardListener();
        UpdateStatusLabel();
        _proc = HookCallback;
        _hookID = SetHook(_proc);
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

    private void AttachClipboardListener()
    {
        if (!isListening)
        {
            AddClipboardFormatListener(this.Handle);
            isListening = true;
            toggleListenerMenuItem.Text = "Detach Listener";
            UpdateStatusLabel();
        }
    }

    private void DetachClipboardListener()
    {
        if (isListening)
        {
            RemoveClipboardFormatListener(this.Handle);
            isListening = false;
            toggleListenerMenuItem.Text = "Attach Listener";
            UpdateStatusLabel();
        }
    }

    private void ToggleClipboardListener(object sender, EventArgs e)
    {
        if (isListening)
        {
            DetachClipboardListener();
        }
        else
        {
            AttachClipboardListener();
        }
    }

    private void OnClipboardChanged()
    {
        if (Clipboard.ContainsText())
        {
            string text = Clipboard.GetText();
            clipboardQueue.Enqueue(text);
            UpdateStatusLabel();
        }
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

    protected override void OnFormClosing(FormClosingEventArgs e)
    {
        DetachClipboardListener();
        base.OnFormClosing(e);
    }

    private IntPtr SetHook(LowLevelKeyboardProc proc)
    {
        using (Process curProcess = Process.GetCurrentProcess())
        using (ProcessModule curModule = curProcess.MainModule)
        {
            return SetWindowsHookEx(WH_KEYBOARD_LL, proc, GetModuleHandle(curModule.ModuleName), 0);
        }
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
                    case VK_P:
                        PrintQueue();
                        break;
                }
            }
        }
        return CallNextHookEx(_hookID, nCode, wParam, lParam);
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
    }

    private void OnPasteDetected()
    {
        if (clipboardQueue.Count > 0)
        {
            string text = clipboardQueue.Dequeue();
            Clipboard.SetText(text);
            UpdateStatusLabel();
        }
    }

    protected override void OnFormClosing(FormClosingEventArgs e)
    {
        UnhookWindowsHookEx(_hookID);
        DetachClipboardListener();
        base.OnFormClosing(e);
    }

    private void PrintQueue()
    {
        string queueContents = string.Join(Environment.NewLine, clipboardQueue);
        MessageBox.Show($"Current Queue Contents:{Environment.NewLine}{queueContents}", "Queue Contents");
    }
}
