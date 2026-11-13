using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;

namespace WinToolbar.AppBar;

/// <summary>
/// Mendaftarkan jendela sebagai AppBar di tepi atas monitor utama, sehingga shell
/// mengurangi area kerja dan jendela lain tidak menutupi bar.
/// </summary>
internal sealed class AppBarManager : IDisposable
{
    private const int ABM_NEW = 0, ABM_REMOVE = 1, ABM_QUERYPOS = 2, ABM_SETPOS = 3,
        ABM_ACTIVATE = 6, ABM_WINDOWPOSCHANGED = 9;
    private const int ABN_POSCHANGED = 1, ABN_FULLSCREENAPP = 2;
    private const int ABE_TOP = 1;

    private const int WM_ACTIVATE = 0x0006, WM_WINDOWPOSCHANGED = 0x0047,
        WM_DISPLAYCHANGE = 0x007E, WM_DPICHANGED = 0x02E0;

    private const int GWL_EXSTYLE = -20;
    private const int WS_EX_TOOLWINDOW = 0x00000080;
    private const uint SWP_NOSIZE = 0x0001, SWP_NOMOVE = 0x0002, SWP_NOACTIVATE = 0x0010;
    private static readonly IntPtr HWND_BOTTOM = new(1);
    private static readonly IntPtr HWND_TOPMOST = new(-1);

    private readonly Window _window;
    private readonly double _heightDip;
    private readonly int _callbackMessage;
    private IntPtr _hwnd;
    private HwndSource? _source;
    private bool _registered;
    private bool _positioning;

    public AppBarManager(Window window, double heightDip)
    {
        _window = window;
        _heightDip = heightDip;
        _callbackMessage = RegisterWindowMessage("WinToolbar.AppBarCallback");
    }

    /// <summary>Panggil setelah handle jendela tersedia (OnSourceInitialized).</summary>
    public void Register()
    {
        _hwnd = new WindowInteropHelper(_window).Handle;
        _source = HwndSource.FromHwnd(_hwnd);
        _source?.AddHook(WndProc);

        // Tool window: tidak muncul di Alt+Tab maupun taskbar.
        var exStyle = GetWindowLongPtr(_hwnd, GWL_EXSTYLE).ToInt64();
        SetWindowLongPtr(_hwnd, GWL_EXSTYLE, new IntPtr(exStyle | WS_EX_TOOLWINDOW));

        var data = NewData();
        data.uCallbackMessage = _callbackMessage;
        SHAppBarMessage(ABM_NEW, ref data);
        _registered = true;

        SetPosition();
    }

    private void SetPosition()
    {
        if (!_registered || _positioning) return;
        _positioning = true;
        try
        {
            var dpi = GetDpiForWindow(_hwnd);
            var height = (int)Math.Round(_heightDip * (dpi == 0 ? 96 : dpi) / 96.0);

            var data = NewData();
            data.uEdge = ABE_TOP;
            data.rc = new RECT { left = 0, top = 0, right = GetSystemMetrics(0 /* SM_CXSCREEN */), bottom = height };

            // Shell boleh menggeser posisi (misalnya ada appbar lain di tepi yang sama).
            SHAppBarMessage(ABM_QUERYPOS, ref data);
            data.rc.bottom = data.rc.top + height;
            SHAppBarMessage(ABM_SETPOS, ref data);

            SetWindowPos(_hwnd, IntPtr.Zero, data.rc.left, data.rc.top,
                data.rc.right - data.rc.left, data.rc.bottom - data.rc.top, SWP_NOACTIVATE | 0x0004 /* SWP_NOZORDER */);
        }
        finally
        {
            _positioning = false;
        }
    }

    private IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        if (msg == _callbackMessage)
        {
            switch (wParam.ToInt32())
            {
                case ABN_POSCHANGED:
                    _window.Dispatcher.BeginInvoke(SetPosition);
                    break;
                case ABN_FULLSCREENAPP:
                    // Beri jalan untuk aplikasi fullscreen (video, game), lalu kembali ke atas.
                    var fullscreen = lParam != IntPtr.Zero;
                    _window.Topmost = !fullscreen;
                    SetWindowPos(_hwnd, fullscreen ? HWND_BOTTOM : HWND_TOPMOST, 0, 0, 0, 0,
                        SWP_NOMOVE | SWP_NOSIZE | SWP_NOACTIVATE);
                    break;
            }
            handled = true;
            return IntPtr.Zero;
        }

        switch (msg)
        {
            case WM_ACTIVATE when _registered:
                var activate = NewData();
                SHAppBarMessage(ABM_ACTIVATE, ref activate);
                break;
            case WM_WINDOWPOSCHANGED when _registered:
                var changed = NewData();
                SHAppBarMessage(ABM_WINDOWPOSCHANGED, ref changed);
                break;
            case WM_DISPLAYCHANGE:
            case WM_DPICHANGED:
                _window.Dispatcher.BeginInvoke(SetPosition);
                break;
        }
        return IntPtr.Zero;
    }

    /// <summary>Wajib dipanggil saat keluar; tanpa ini area kerja tetap terpotong.</summary>
    public void Dispose()
    {
        if (_registered)
        {
            var data = NewData();
            SHAppBarMessage(ABM_REMOVE, ref data);
            _registered = false;
        }
        _source?.RemoveHook(WndProc);
        _source = null;
    }

    private APPBARDATA NewData() => new()
    {
        cbSize = Marshal.SizeOf<APPBARDATA>(),
        hWnd = _hwnd,
    };

    [StructLayout(LayoutKind.Sequential)]
    private struct RECT
    {
        public int left, top, right, bottom;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct APPBARDATA
    {
        public int cbSize;
        public IntPtr hWnd;
        public int uCallbackMessage;
        public int uEdge;
        public RECT rc;
        public IntPtr lParam;
    }

    [DllImport("shell32.dll")]
    private static extern IntPtr SHAppBarMessage(int dwMessage, ref APPBARDATA pData);

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern int RegisterWindowMessage(string msg);

    [DllImport("user32.dll")]
    private static extern bool SetWindowPos(IntPtr hWnd, IntPtr hWndInsertAfter, int x, int y, int cx, int cy, uint flags);

    [DllImport("user32.dll")]
    private static extern int GetSystemMetrics(int index);

    [DllImport("user32.dll")]
    private static extern uint GetDpiForWindow(IntPtr hWnd);

    [DllImport("user32.dll", EntryPoint = "GetWindowLongPtrW")]
    private static extern IntPtr GetWindowLongPtr(IntPtr hWnd, int index);

    [DllImport("user32.dll", EntryPoint = "SetWindowLongPtrW")]
    private static extern IntPtr SetWindowLongPtr(IntPtr hWnd, int index, IntPtr value);
}
