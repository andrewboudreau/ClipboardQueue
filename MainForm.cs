using System.Runtime.InteropServices;
using System.Text;

namespace ClipboardQueue;

public partial class MainForm : Form
{
    private Queue<string> clipboardQueue = new Queue<string>();
    private const int WM_CLIPBOARDUPDATE = 0x031D;
    private IntPtr nextClipboardViewer;

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool AddClipboardFormatListener(IntPtr hwnd);

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool RemoveClipboardFormatListener(IntPtr hwnd);

    public MainForm()
    {
        InitializeComponent();
        AddClipboardFormatListener(this.Handle);
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
        toolStripStatusLabel1.Text = $"Items in queue: {clipboardQueue.Count}";
    }

    protected override void OnFormClosing(FormClosingEventArgs e)
    {
        RemoveClipboardFormatListener(this.Handle);
        base.OnFormClosing(e);
    }

    // This method should be called when a paste operation is detected
    private void OnPasteDetected()
    {
        if (clipboardQueue.Count > 0)
        {
            clipboardQueue.Dequeue();
            UpdateStatusLabel();
        }
    }
}
