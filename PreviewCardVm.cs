namespace TopBarPoC;

internal sealed class PreviewCardVm
{
    public required IntPtr Hwnd { get; init; }
    public required string Title { get; init; }
    public required Action<IntPtr> Activate { get; init; }
    public Action<IntPtr> Close { get; init; } = _ => { };
    public int OverflowCount { get; init; }
    public bool ThumbnailAvailable { get; set; }
}
