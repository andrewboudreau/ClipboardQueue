using System.Runtime.InteropServices;
using System.Text;

namespace ClipboardQueue;

public partial class MainForm : Form
{
    private Queue<string> clipboardQueue = new Queue<string>();
    private const int WM_CLIPBOARDUPDATE = 0x031D;
    private bool isListening = false;

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool AddClipboardFormatListener(IntPtr hwnd);

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool RemoveClipboardFormatListener(IntPtr hwnd);

    public MainForm()
    {
        InitializeComponent();
        this.KeyPreview = true;
        this.KeyDown += MainForm_KeyDown;
        UpdateStatusLabel();
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
            toggleListenerMenuItem.Text = "Detach Clipboard Queue Listener";
            UpdateStatusLabel();
        }
    }

    private void DetachClipboardListener()
    {
        if (isListening)
        {
            RemoveClipboardFormatListener(this.Handle);
            isListening = false;
            toggleListenerMenuItem.Text = "Attach Clipboard Queue Listener";
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
    }

    protected override void OnFormClosing(FormClosingEventArgs e)
    {
        DetachClipboardListener();
        base.OnFormClosing(e);
    }

    private void MainForm_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Control)
        {
            switch (char.ToLower((char)e.KeyCode))
            {
                case 'c':
                    OnCopyDetected();
                    break;
                case 'v':
                    OnPasteDetected();
                    break;
                case 'p':
                    PrintQueue();
                    break;
            }
        }
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

    private void PrintQueue()
    {
        string queueContents = string.Join(Environment.NewLine, clipboardQueue);
        MessageBox.Show($"Current Queue Contents:{Environment.NewLine}{queueContents}", "Queue Contents");
    }
}
