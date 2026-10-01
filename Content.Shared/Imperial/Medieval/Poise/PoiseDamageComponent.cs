using Robust.Shared.GameStates;

namespace Content.Shared.Imperial.Medieval.Poise;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class PoiseDamageComponent : Component
{
    [DataField, AutoNetworkedField]
    public int PoiseDamage = 10;

    [DataField, AutoNetworkedField]
    public float HeavyPoiseMultiplier = 2f;

    [DataField, AutoNetworkedField]
    public TimeSpan PoiseRecoveryDelayAfterAttack = TimeSpan.FromSeconds(5);
}
