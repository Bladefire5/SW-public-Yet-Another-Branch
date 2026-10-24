// spellward start - limited uses for temporary conjured ice daggers.
namespace Content.Shared.Imperial.Medieval.Magic;

[RegisterComponent]
public sealed partial class SummonedIceDaggerComponent : Component
{
    /// <summary>
    /// Number of successful melee attacks before the summoned dagger shatters.
    /// Throwing it destroys it on impact or landing regardless of this count.
    /// </summary>
    [DataField]
    public int HitsRemaining = 5;
}
// spellward end
