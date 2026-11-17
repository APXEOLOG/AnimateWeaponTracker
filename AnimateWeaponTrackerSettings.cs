using ExileCore.Shared.Attributes;
using ExileCore.Shared.Interfaces;
using ExileCore.Shared.Nodes;

namespace AnimateWeaponTracker;

public class AnimateWeaponTrackerSettings : ISettings
{
    public ToggleNode Enable { get; set; } = new(true);

    [Menu("Panel X (% of window width)")]
    public RangeNode<int> PositionX { get; set; } = new(12, 0, 100);

    [Menu("Panel Y (% of window height)")]
    public RangeNode<int> PositionY { get; set; } = new(40, 0, 100);

    [Menu("Bar width (pixels)")]
    public RangeNode<int> BarWidth { get; set; } = new(220, 100, 600);

    [Menu("Expiring soon below (seconds)")]
    public RangeNode<int> WarningSeconds { get; set; } = new(10, 1, 60);

    [Menu("Middle band up to (% of duration)")]
    public RangeNode<int> MiddleBandPercent { get; set; } = new(50, 10, 90);

    [Menu("Hide when no weapons are out")]
    public ToggleNode HideWhenEmpty { get; set; } = new(true);

    [Menu("Debug window")]
    public ToggleNode ShowDebug { get; set; } = new(false);
}
