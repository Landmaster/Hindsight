using BaseLib.Config;

namespace Hindsight.HindsightCode.Config;

internal class HindsightModConfig : SimpleModConfig
{
    [ConfigSlider(1, 20)]
    [ConfigHoverTip]
    public static int RunsSavedToHindsight { get; set; } = 2;
    
    public static bool SaveHindsightedRuns { get; set; } = false;
}