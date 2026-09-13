using Content.Shared.Damage.Systems;
using Content.Shared.Imperial.Medieval.CombatArts.Movement;

namespace Content.Shared.Imperial.Medieval.CombatArts.Physical;

public sealed partial class MedievalPhysicalSystem : EntitySystem
{
    [Dependency]
    private readonly SharedStaminaSystem _staminaSystem = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<FullMovementEvent>
        (OnFullMovement, before: [typeof(FullMovementSystem)]);
    }
    /// <summary>
    /// Attempts to pay the stamina cost of a physical skill.
    /// </summary>
    public bool TryUseSkill(EntityUid performer, EntityUid action)
    {
        if (!TryComp<MedievalPhysicalComponent>(action, out var physical))
            return true;

        if (physical.StaminaCost <= 0f)
            return true;

        return _staminaSystem.TryTakeStamina(
            performer,
            physical.StaminaCost,
            ignoreResist: physical.IgnoreStaminaResistance);
    }

    private void OnFullMovement(FullMovementEvent args)
    {
        if (args.Handled)
            return;

        // Not a physical skill, so this system doesn't care.
        if (!HasComp<MedievalPhysicalComponent>(args.Action.Owner))
            return;

        if (TryUseSkill(args.Performer, args.Action.Owner))
            return;

        // Payment failed.
        // Marking it handled prevents FullMovementSystem from performing it.
        args.Cancelled = true;
    }
}