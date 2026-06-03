using System.Windows;
using System.Windows.Interop;
using static TopBarPoC.NativeMethods;

namespace TopBarPoC;

public partial class HoverPreviewWindow : Window
{
    private IntPtr _thumbnailHandle = IntPtr.Zero;

    public HoverPreviewWindow() => InitializeComponent();

    // EnsureHandle() creates the HWND before Show() so the thumbnail is live
    // when the window first becomes visible — avoids a dark-window flash.
    internal void ShowForHwnd(IntPtr sourceHwnd, string title, double screenLeft, double screenTop, double dpiScale)
    {
        ReleaseThumbnail();
        TitleText.Text = title;
        Left = screenLeft;
        Top  = screenTop;

        var helper = new WindowInteropHelper(this);
        helper.EnsureHandle();

        if (DwmRegisterThumbnail(helper.Handle, sourceHwnd, out _thumbnailHandle) != 0)
        {
            // Elevated target or cloaked — show dark preview with title only.
            _thumbnailHandle = IntPtr.Zero;
            Show();
            return;
        }

        // rcDestination is in physical pixels (client coordinates).
        // Width/Height are DIP design values; multiply by dpiScale for physical size.
        int thumbW = (int)Math.Round(Width         * dpiScale);
        int thumbH = (int)Math.Round((Height - 22) * dpiScale);
        var props = new DWM_THUMBNAIL_PROPERTIES
        {
            dwFlags              = DWM_TNP_RECTDESTINATION | DWM_TNP_OPACITY | DWM_TNP_VISIBLE | DWM_TNP_SOURCECLIENTAREAONLY,
            rcDestination        = new RECT { Left = 0, Top = 0, Right = thumbW, Bottom = thumbH },
            opacity              = 255,
            fVisible             = 1,
            fSourceClientAreaOnly = 1,
        };
        DwmUpdateThumbnailProperties(_thumbnailHandle, ref props);
        Show();
    }

    internal void HidePreview()
    {
        ReleaseThumbnail();
        Hide();
    }

    protected override void OnClosed(EventArgs e)
    {
        ReleaseThumbnail();
        base.OnClosed(e);
    }

    private void ReleaseThumbnail()
    {
        if (_thumbnailHandle == IntPtr.Zero) return;
        DwmUnregisterThumbnail(_thumbnailHandle);
        _thumbnailHandle = IntPtr.Zero;
    }
}
