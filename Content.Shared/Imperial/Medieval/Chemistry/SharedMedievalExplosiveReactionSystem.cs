using Content.Shared.Chemistry;
using Content.Shared.Chemistry.Components;
using Content.Shared.Chemistry.Components.SolutionManager;
using Content.Shared.Chemistry.EntitySystems;
using Content.Shared.Chemistry.Reaction;

namespace Content.Shared.Imperial.Medieval.Chemistry;

/// <summary>
/// Keeps medieval explosive reactions out of machine buffers.
/// All other solutions use the existing chemistry eligibility rules.
/// </summary>
public sealed class SharedMedievalExplosiveReactionSystem : EntitySystem
{
    public const string CooldownGroup = "MedievalExplosive";

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<SolutionComponent, ReactionAttemptEvent>(OnSolutionAttempt);
        SubscribeLocalEvent<SolutionContainerManagerComponent, SolutionRelayEvent<ReactionAttemptEvent>>(OnContainerAttempt);
    }

    private void OnSolutionAttempt(Entity<SolutionComponent> ent, ref ReactionAttemptEvent args)
    {
        if (args.Cancelled || args.Reaction.ReactionCooldownGroup != CooldownGroup ||
            ent.Comp.Solution.Name != SharedChemMaster.BufferSolutionName)
            return;

        args.Cancelled = true;
    }

    private void OnContainerAttempt(Entity<SolutionContainerManagerComponent> ent,
        ref SolutionRelayEvent<ReactionAttemptEvent> args)
    {
        if (args.Event.Cancelled || args.Event.Reaction.ReactionCooldownGroup != CooldownGroup ||
            args.Name != SharedChemMaster.BufferSolutionName)
            return;

        args.Event.Cancelled = true;
    }
}
