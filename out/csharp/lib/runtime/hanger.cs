using GlpRuntime.Runtime;

namespace GlpRuntime.Runtime;

/// <summary>Hanger: ensures single reactivation when a goal suspended on multiple readers.</summary>
public class Hanger
{
    public GoalId GoalId { get; }
    public Pc Kappa { get; }     // restart at clause selection
    public bool Armed { get; set; } = true;     // true at creation; first wake sets to false

    public Hanger(GoalId goalId, Pc kappa, bool armed = true)
    {
        GoalId = goalId;
        Kappa  = kappa;
        Armed  = armed;
    }
}
