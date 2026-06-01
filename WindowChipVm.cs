using System.ComponentModel;
using System.Windows.Media;

namespace TopBarPoC;

internal readonly record struct WindowInfo(IntPtr Handle, string Title, bool IsMinimized);

internal sealed class WindowChipVm : INotifyPropertyChanged
{
    public required IntPtr       Handle { get; init; }
    public          ImageSource? Icon   { get; init; }

    private string _title = "";
    public required string Title
    {
        get => _title;
        set
        {
            if (_title == value) return;
            _title = value;
            PropertyChanged?.Invoke(this, _titlePcea);
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

    public event PropertyChangedEventHandler? PropertyChanged;
    private static readonly PropertyChangedEventArgs _titlePcea       = new(nameof(Title));
    private static readonly PropertyChangedEventArgs _isMinimizedPcea = new(nameof(IsMinimized));
    private static readonly PropertyChangedEventArgs _isActivePcea    = new(nameof(IsActive));
    private static readonly PropertyChangedEventArgs _widthPcea       = new(nameof(Width));
}
