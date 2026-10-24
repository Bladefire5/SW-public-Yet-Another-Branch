using Robust.Shared.GameStates;

namespace Content.Shared.Imperial.Medieval.Poise.LightAttack;

/// <summary>
/// Replicates a light attack's visible phase before it starts, so receiving
/// clients can hide the input delay and seek to the correct animation frame.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState, AutoGenerateComponentPause]
public sealed partial class LightAttackWindupVisualComponent : Component
{
    [DataField, AutoNetworkedField, AutoPausedField]
    public TimeSpan Starts;

    [DataField, AutoNetworkedField, AutoPausedField]
    public TimeSpan Ends;

    /// <summary>
    /// An unset, not-yet-started or finished effect must remain hidden. A late
    /// packet uses elapsed progress instead of restarting the entire animation.
    /// </summary>
    public bool TryGetProgress(TimeSpan now, out float progress)
    {
        progress = 0f;
        if (Ends <= Starts || now < Starts || now >= Ends)
            return false;

        progress = (float) ((now - Starts).TotalSeconds / (Ends - Starts).TotalSeconds);
        return true;
    }
}
