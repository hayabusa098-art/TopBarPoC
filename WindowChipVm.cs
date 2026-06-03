using System.ComponentModel;
using System.Windows;
using System.Windows.Media;

namespace TopBarPoC;

internal readonly record struct WindowInfo(
    IntPtr Handle, string Title, bool IsMinimized, string AppKey, string AppLabel);

internal sealed class WindowChipVm : INotifyPropertyChanged
{
    public required IntPtr       Handle { get; init; }
    public required IntPtr[]     Handles { get; init; }
    public          ImageSource? Icon   { get; init; }
    public          bool         IsGrouped => Handles.Length > 1;
    public          string       ToolTipText => IsGrouped
        ? $"{Title}\nClick to choose window\nCtrl+Click to cycle\nDrag to reorder"
        : $"{Title} - Drag to reorder";

    private string _title = "";
    public required string Title
    {
        get => _title;
        set
        {
            if (_title == value) return;
            _title = value;
            PropertyChanged?.Invoke(this, _titlePcea);
            PropertyChanged?.Invoke(this, _toolTipTextPcea);
        }
    }

    private bool _isMinimized;
    public bool IsMinimized
    {
        get => _isMinimized;
        set
        {
            if (_isMinimized == value) return;
            _isMinimized = value;
            PropertyChanged?.Invoke(this, _isMinimizedPcea);
        }
    }

    private bool _isActive;
    public bool IsActive
    {
        get => _isActive;
        set
        {
            if (_isActive == value) return;
            _isActive = value;
            PropertyChanged?.Invoke(this, _isActivePcea);
        }
    }

    private double _width = 110.0;
    public double Width
    {
        get => _width;
        set
        {
            if (_width == value) return;
            _width = value;
            PropertyChanged?.Invoke(this, _widthPcea);
        }
    }

    private GridLength _chipGapWidth = new(3);
    public GridLength ChipGapWidth
    {
        get => _chipGapWidth;
        set
        {
            if (_chipGapWidth == value) return;
            _chipGapWidth = value;
            PropertyChanged?.Invoke(this, _chipGapWidthPcea);
        }
    }

    private Thickness _chipMargin = new(0, 0, 3, 0);
    public Thickness ChipMargin
    {
        get => _chipMargin;
        set
        {
            if (_chipMargin == value) return;
            _chipMargin = value;
            PropertyChanged?.Invoke(this, _chipMarginPcea);
        }
    }

    private Thickness _chipPadding = new(8, 0, 8, 0);
    public Thickness ChipPadding
    {
        get => _chipPadding;
        set
        {
            if (_chipPadding == value) return;
            _chipPadding = value;
            PropertyChanged?.Invoke(this, _chipPaddingPcea);
        }
    }

    public event PropertyChangedEventHandler? PropertyChanged;
    private static readonly PropertyChangedEventArgs _titlePcea        = new(nameof(Title));
    private static readonly PropertyChangedEventArgs _toolTipTextPcea  = new(nameof(ToolTipText));
    private static readonly PropertyChangedEventArgs _isMinimizedPcea  = new(nameof(IsMinimized));
    private static readonly PropertyChangedEventArgs _isActivePcea     = new(nameof(IsActive));
    private static readonly PropertyChangedEventArgs _widthPcea        = new(nameof(Width));
    private static readonly PropertyChangedEventArgs _chipGapWidthPcea  = new(nameof(ChipGapWidth));
    private static readonly PropertyChangedEventArgs _chipMarginPcea    = new(nameof(ChipMargin));
    private static readonly PropertyChangedEventArgs _chipPaddingPcea   = new(nameof(ChipPadding));
}
