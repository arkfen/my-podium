namespace Podium.Shared.Models;

public class EventDependencies
{
    public int PredictionCount { get; set; }
    public bool HasResult { get; set; }
    public bool HasDependencies => PredictionCount > 0 || HasResult;
}
