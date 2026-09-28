namespace PingArmor.UI.ViewModels;

/// <summary>One row in the "planned changes" list on the overview page.</summary>
public sealed class PlannedChangeViewModel
{
    public PlannedChangeViewModel(string transition, string reason)
    {
        Transition = transition;
        Reason = reason;
    }

    /// <summary>Readable metric transition, e.g. "Wi-Fi  10 → 5".</summary>
    public string Transition { get; }

    public string Reason { get; }
}