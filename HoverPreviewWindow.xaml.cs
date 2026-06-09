using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;
using System.Windows.Media.Animation;
using static TopBarPoC.NativeMethods;

namespace TopBarPoC;

public partial class HoverPreviewWindow : Window
{
    internal const double CardWidthDip = 280.0;
    internal const double CardHeightDip = 185.0;
    internal const double CardGapDip = 10.0;
    private const double TitleHeightDip = 22.0;

    private readonly List<IntPtr> _thumbnailHandles = [];
    private IReadOnlyList<PreviewCardVm> _cards = [];
    private int _visibilityGeneration;
    private bool _isFadingOut;

    public HoverPreviewWindow()
    {
        InitializeComponent();
        MouseEnter += (_, _) => CancelFadeOut();
        SourceInitialized += (_, _) =>
        {
            var hwnd = new WindowInteropHelper(this).Handle;
            ApplyNoActivateStyle(hwnd);
            HwndSource.FromHwnd(hwnd)?.AddHook(WndProc);
        };
    }

    internal void ShowForHwnd(IntPtr sourceHwnd, string title, double screenLeft, double screenTop, double dpiScale)
        => ShowForCards(
            [
                new PreviewCardVm
                {
                    Hwnd = sourceHwnd,
                    Title = title,
                    Activate = _ => { },
                }
            ],
            screenLeft,
            screenTop,
            dpiScale);

    internal void ShowForCards(IReadOnlyList<PreviewCardVm> cards, double screenLeft, double screenTop, double dpiScale)
    {
        bool wasHidden = !IsVisible;
        _visibilityGeneration++;
        _isFadingOut = false;
        BeginAnimation(OpacityProperty, null);
        if (!wasHidden)
            Opacity = 1.0;
        ReleaseThumbnails();
        _cards = cards;
        CardsHost.ItemsSource = _cards;
        Width = PreviewWidthForCardCount(_cards.Count);
        Height = CardHeightDip;
        Left = screenLeft;
        Top = screenTop;

        var helper = new WindowInteropHelper(this);
        helper.EnsureHandle();
        ApplyNoActivateStyle(helper.Handle);

        for (int i = 0; i < _cards.Count; i++)
        {
            var card = _cards[i];
            card.ThumbnailAvailable = false;
            if (DwmRegisterThumbnail(helper.Handle, card.Hwnd, out var thumbnailHandle) != 0)
                continue;

            int left = (int)Math.Round(i * (CardWidthDip + CardGapDip) * dpiScale);
            int right = (int)Math.Round((i * (CardWidthDip + CardGapDip) + CardWidthDip) * dpiScale);
            int bottom = (int)Math.Round((CardHeightDip - TitleHeightDip) * dpiScale);
            var destination = CreateThumbnailDestination(thumbnailHandle, left, 0, right - left, bottom);
            var props = new DWM_THUMBNAIL_PROPERTIES
            {
                dwFlags = DWM_TNP_RECTDESTINATION | DWM_TNP_OPACITY | DWM_TNP_VISIBLE | DWM_TNP_SOURCECLIENTAREAONLY,
                rcDestination = destination,
                opacity = 255,
                fVisible = 1,
                fSourceClientAreaOnly = 1,
            };
            DwmUpdateThumbnailProperties(thumbnailHandle, ref props);
            _thumbnailHandles.Add(thumbnailHandle);
            card.ThumbnailAvailable = true;
        }

        if (wasHidden)
            Opacity = 0.0;
        Show();
        if (wasHidden)
            BeginAnimation(OpacityProperty, new DoubleAnimation(0.0, 1.0,
                new Duration(TimeSpan.FromMilliseconds(150)))
                { EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut } });
    }

    internal void HidePreview()
    {
        _visibilityGeneration++;
        _isFadingOut = false;
        BeginAnimation(OpacityProperty, null); // cancel any in-progress fade
        Opacity = 1.0;                          // reset for next show cycle
        ReleaseThumbnails();
        Hide();
    }

    internal void FadeOutPreview()
    {
        if (!IsVisible) return;

        int generation = ++_visibilityGeneration;
        _isFadingOut = true;
        double startOpacity = Opacity;
        BeginAnimation(OpacityProperty, null);
        Opacity = startOpacity;

        var animation = new DoubleAnimation(startOpacity, 0.0,
            new Duration(TimeSpan.FromMilliseconds(150)))
            { EasingFunction = new CubicEase { EasingMode = EasingMode.EaseIn } };
        animation.Completed += (_, _) =>
        {
            if (generation == _visibilityGeneration && _isFadingOut)
                HidePreview();
        };
        BeginAnimation(OpacityProperty, animation);
    }

    private void CancelFadeOut()
    {
        if (!_isFadingOut) return;

        _visibilityGeneration++;
        _isFadingOut = false;
        BeginAnimation(OpacityProperty, null);
        Opacity = 1.0;
    }

    protected override void OnClosed(EventArgs e)
    {
        _visibilityGeneration++;
        _isFadingOut = false;
        BeginAnimation(OpacityProperty, null);
        ReleaseThumbnails();
        base.OnClosed(e);
    }

    private void PreviewCard_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not Button { DataContext: PreviewCardVm card }) return;
        card.Activate(card.Hwnd);
    }

    private void PreviewCardClose_Click(object sender, RoutedEventArgs e)
    {
        e.Handled = true; // prevent event from bubbling to the outer card Button
        if (sender is not Button { DataContext: PreviewCardVm card }) return;
        card.Close(card.Hwnd);
    }

    private static void ApplyNoActivateStyle(IntPtr hwnd)
    {
        if (hwnd == IntPtr.Zero) return;
        var exStyle = GetWindowLongPtrSafe(hwnd, GWL_EXSTYLE);
        SetWindowLongPtrSafe(hwnd, GWL_EXSTYLE, (IntPtr)(exStyle.ToInt64() | WS_EX_NOACTIVATE));
    }

    private static IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        if (msg == WM_MOUSEACTIVATE)
        {
            handled = true;
            return (IntPtr)MA_NOACTIVATE;
        }

        return IntPtr.Zero;
    }

    private static RECT CreateThumbnailDestination(IntPtr thumbnailHandle, int areaLeft, int areaTop, int areaWidth, int areaHeight)
    {
        if (DwmQueryThumbnailSourceSize(thumbnailHandle, out var sourceSize) != 0 ||
            sourceSize.Cx <= 0 ||
            sourceSize.Cy <= 0 ||
            areaWidth <= 0 ||
            areaHeight <= 0)
        {
            return new RECT
            {
                Left = areaLeft,
                Top = areaTop,
                Right = areaLeft + areaWidth,
                Bottom = areaTop + areaHeight,
            };
        }

        double sourceAspect = (double)sourceSize.Cx / sourceSize.Cy;
        double areaAspect = (double)areaWidth / areaHeight;

        int width;
        int height;
        if (sourceAspect >= areaAspect)
        {
            width = areaWidth;
            height = Math.Max(1, (int)Math.Round(areaWidth / sourceAspect));
        }
        else
        {
            height = areaHeight;
            width = Math.Max(1, (int)Math.Round(areaHeight * sourceAspect));
        }

        int left = areaLeft + (areaWidth - width) / 2;
        int top = areaTop + (areaHeight - height) / 2;
        return new RECT
        {
            Left = left,
            Top = top,
            Right = left + width,
            Bottom = top + height,
        };
    }

    internal static double PreviewWidthForCardCount(int cardCount)
        => cardCount <= 0
            ? 0.0
            : CardWidthDip * cardCount + CardGapDip * (cardCount - 1);

    private void ReleaseThumbnails()
    {
        foreach (var thumbnailHandle in _thumbnailHandles)
            DwmUnregisterThumbnail(thumbnailHandle);
        _thumbnailHandles.Clear();
    }
}
