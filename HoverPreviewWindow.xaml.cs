using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;
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

    public HoverPreviewWindow() => InitializeComponent();

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
        ReleaseThumbnails();
        _cards = cards;
        CardsHost.ItemsSource = _cards;
        Width = PreviewWidthForCardCount(_cards.Count);
        Height = CardHeightDip;
        Left = screenLeft;
        Top = screenTop;

        var helper = new WindowInteropHelper(this);
        helper.EnsureHandle();

        for (int i = 0; i < _cards.Count; i++)
        {
            var card = _cards[i];
            card.ThumbnailAvailable = false;
            if (DwmRegisterThumbnail(helper.Handle, card.Hwnd, out var thumbnailHandle) != 0)
                continue;

            int left = (int)Math.Round(i * (CardWidthDip + CardGapDip) * dpiScale);
            int right = (int)Math.Round((i * (CardWidthDip + CardGapDip) + CardWidthDip) * dpiScale);
            int bottom = (int)Math.Round((CardHeightDip - TitleHeightDip) * dpiScale);
            var props = new DWM_THUMBNAIL_PROPERTIES
            {
                dwFlags = DWM_TNP_RECTDESTINATION | DWM_TNP_OPACITY | DWM_TNP_VISIBLE | DWM_TNP_SOURCECLIENTAREAONLY,
                rcDestination = new RECT { Left = left, Top = 0, Right = right, Bottom = bottom },
                opacity = 255,
                fVisible = 1,
                fSourceClientAreaOnly = 1,
            };
            DwmUpdateThumbnailProperties(thumbnailHandle, ref props);
            _thumbnailHandles.Add(thumbnailHandle);
            card.ThumbnailAvailable = true;
        }

        Show();
    }

    internal void HidePreview()
    {
        ReleaseThumbnails();
        Hide();
    }

    protected override void OnClosed(EventArgs e)
    {
        ReleaseThumbnails();
        base.OnClosed(e);
    }

    private void PreviewCard_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not Button { DataContext: PreviewCardVm card }) return;
        card.Activate(card.Hwnd);
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
