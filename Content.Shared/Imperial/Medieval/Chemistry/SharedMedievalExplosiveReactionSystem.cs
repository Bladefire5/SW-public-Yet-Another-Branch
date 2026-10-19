using Content.Shared.Body.Components;
using Content.Shared.Body.Organ;
using Content.Shared.Chemistry;
using Content.Shared.Chemistry.Components;
using Content.Shared.Chemistry.Components.SolutionManager;
using Content.Shared.Chemistry.EntitySystems;
using Content.Shared.Chemistry.Reaction;

namespace Content.Shared.Imperial.Medieval.Chemistry;

/// <summary>
/// Keeps medieval explosive reactions out of machine buffers and internal body solutions.
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
        if (args.Cancelled || args.Reaction.ReactionCooldownGroup != CooldownGroup)
            return;

        if (IsProtectedSolution(ent.Owner, ent.Comp.Solution.Name))
            args.Cancelled = true;
    }

    private void OnContainerAttempt(Entity<SolutionContainerManagerComponent> ent,
        ref SolutionRelayEvent<ReactionAttemptEvent> args)
    {
        if (args.Event.Cancelled || args.Event.Reaction.ReactionCooldownGroup != CooldownGroup)
            return;

        if (IsProtectedSolution(ent.Owner, args.Name))
            args.Event.Cancelled = true;
    }

    private bool IsProtectedSolution(EntityUid owner, string? name)
    {
        // Check the actual solution owner, never the player's carried containers.
        // Organ owners also cover lungs and other internal metabolic solutions.
        return name == SharedChemMaster.BufferSolutionName ||
               HasComp<BodyComponent>(owner) || HasComp<BloodstreamComponent>(owner) ||
               HasComp<OrganComponent>(owner) || HasComp<StomachComponent>(owner);
    }
}
