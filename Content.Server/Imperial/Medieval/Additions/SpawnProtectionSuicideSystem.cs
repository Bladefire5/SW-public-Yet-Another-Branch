using Content.Server.Chat;
using Content.Shared.Imperial.Medieval.Additions;
using Content.Shared.Interaction.Events;

namespace Content.Server.Imperial.Medieval.Additions;

/// <summary>
/// Ends spawn protection after a successful permanent suicide ghost request,
/// before the standard suicide system applies lethal damage.
/// </summary>
public sealed class SpawnProtectionSuicideSystem : EntitySystem
{
    [Dependency] private readonly ShieldOnStartupSystem _protection = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<ShieldOnStartupComponent, SuicideGhostEvent>(OnSuicideGhost,
            after: new[] { typeof(SuicideSystem) });
    }

    private void OnSuicideGhost(EntityUid uid, ShieldOnStartupComponent component, SuicideGhostEvent args)
    {
        if (args.Handled && !args.CanReturnToBody && args.Victim == uid)
            _protection.EndProtection(uid);
    }
}
