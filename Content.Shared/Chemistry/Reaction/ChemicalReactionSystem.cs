using System.Collections.Frozen;
using System.Linq;
using Content.Shared.Administration.Logs;
using Content.Shared.Chemistry.Components;
using Content.Shared.Chemistry.EntitySystems; //spellward
using Content.Shared.Chemistry.Reagent;
using Content.Shared.Database;
using Content.Shared.EntityEffects;
using Content.Shared.FixedPoint;
using Content.Shared.Imperial.Medieval.ChemistryRandomization;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Network;
using Robust.Shared.Prototypes;
using Robust.Shared.Timing; //spellward
using Robust.Shared.Utility;


namespace Content.Shared.Chemistry.Reaction
{
    public sealed class ChemicalReactionSystem : EntitySystem
    {
        /// <summary>
        /// Foam reaction protoId.
        /// </summary>
        public static readonly ProtoId<ReactionPrototype> FoamReaction = "Foam";

        /// <summary>
        ///     The maximum number of reactions that may occur when a solution is changed.
        /// </summary>
        private const int MaxReactionIterations = 20;

        [Dependency] private readonly INetManager _netMan = default!;
        [Dependency] private readonly IPrototypeManager _prototypeManager = default!;
        [Dependency] private readonly ISharedAdminLogManager _adminLogger = default!;
        [Dependency] private readonly SharedAudioSystem _audio = default!;
        [Dependency] private readonly SharedTransformSystem _transformSystem = default!;
        [Dependency] private readonly SharedChemistryRandomizationSystem _chemRandom = default!;
        [Dependency] private readonly IGameTiming _timing = default!; //spellward

        /// <summary>
        /// A cache of all reactions indexed by at most ONE of their required reactants.
        /// I.e., even if a reaction has more than one reagent, it will only ever appear once in this dictionary.
        /// </summary>
        private FrozenDictionary<string, List<ReactionPrototype>> _reactionsSingle = default!;

        /// <summary>
        ///     A cache of all reactions indexed by one of their required reactants.
        /// </summary>
        private FrozenDictionary<string, List<ReactionPrototype>> _reactions = default!;

        // spellward start
        // Requests made by effects while a controlled solution is already reacting.
        // These are drained by its outer loop, not stored as future charges.
        private readonly HashSet<EntityUid> _reentrantUpdates = new();

        // spellward end
        public override void Initialize()
        {
            base.Initialize();

            InitializeReactionCache();
            SubscribeLocalEvent<PrototypesReloadedEventArgs>(OnPrototypesReloaded);
            // spellward start
            SubscribeLocalEvent<ChemicalReactionCooldownComponent, EntityUnpausedEvent>(OnUnpaused);
        }

        public override void Update(float frameTime)
        {
            base.Update(frameTime);

            // Clients predict reactions when a solution changes. The server alone
            // advances idle solutions and publishes the resulting composition.
            if (!_netMan.IsServer)
                return;

            var now = _timing.CurTime;
            var ready = new List<EntityUid>();
            var query = EntityQueryEnumerator<ChemicalReactionCooldownComponent, SolutionComponent>();
            while (query.MoveNext(out var uid, out var cooldown, out _))
            {
                if (cooldown.Processing || cooldown.Cooldowns.Count == 0 || Paused(uid))
                    continue;

                if (cooldown.Cooldowns.Values.Any(entry => entry.NextActivation <= now))
                    ready.Add(uid);
            }

            // Effects can change or delete solutions. Do not run them while an
            // entity query is being enumerated, and resolve each solution again.
            foreach (var uid in ready)
            {
                if (Deleted(uid) || Terminating(uid) || Paused(uid) ||
                    !TryComp<ChemicalReactionCooldownComponent>(uid, out var cooldown) ||
                    !TryComp<SolutionComponent>(uid, out var solution) || cooldown.Processing)
                    continue;

                var expired = cooldown.Cooldowns
                    .Where(entry => entry.Value.NextActivation <= now)
                    .Select(entry => entry.Key)
                    .ToArray();
                if (expired.Length == 0)
                    continue;

                foreach (var key in expired)
                    cooldown.Cooldowns.Remove(key);

                Dirty(uid, cooldown);

                // This rechecks CanReact, temperature, inputs and reaction-attempt
                // gates, and uses the normal overflow/appearance/update events.
                EntityManager.System<SharedSolutionContainerSystem>().UpdateChemicals((uid, solution));
            }
        }

        private void OnUnpaused(Entity<ChemicalReactionCooldownComponent> ent, ref EntityUnpausedEvent args)
        {
            if (!_netMan.IsServer || ent.Comp.Cooldowns.Count == 0)
                return;

            foreach (var key in ent.Comp.Cooldowns.Keys.ToArray())
            {
                var next = ent.Comp.Cooldowns[key].NextActivation + args.PausedTime;
                ent.Comp.Cooldowns[key] = new ChemicalReactionCooldown(next);
            }

            Dirty(ent);
        }

        private static string CooldownKey(ReactionPrototype reaction)
        {
            // Keep explicit group names separate from individual reaction IDs.
            return reaction.ReactionCooldownGroup.Length == 0
                ? "reaction:" + reaction.ID
                : "group:" + reaction.ReactionCooldownGroup;
            // spellward end
        }

        /// <summary>
        ///     Handles building the reaction cache.
        /// </summary>
        private void InitializeReactionCache()
        {
            // Construct single-reaction dictionary.
            var dict = new Dictionary<string, List<ReactionPrototype>>();
            foreach (var reaction in _prototypeManager.EnumeratePrototypes<ReactionPrototype>())
            {
                // For this dictionary we only need to cache based on the first reagent.
                var reagent = reaction.Reactants.Keys.First();
                var list = dict.GetOrNew(reagent);
                list.Add(reaction);
            }
            _reactionsSingle = dict.ToFrozenDictionary();

            dict.Clear();
            foreach (var reaction in _prototypeManager.EnumeratePrototypes<ReactionPrototype>())
            {
                foreach (var reagent in reaction.Reactants.Keys)
                {
                    var list = dict.GetOrNew(reagent);
                    list.Add(reaction);
                }
            }
            _reactions = dict.ToFrozenDictionary();
        }

        /// <summary>
        ///     Updates the reaction cache when the prototypes are reloaded.
        /// </summary>
        /// <param name="eventArgs">The set of modified prototypes.</param>
        private void OnPrototypesReloaded(PrototypesReloadedEventArgs eventArgs)
        {
            if (eventArgs.WasModified<ReactionPrototype>())
                InitializeReactionCache();
        }

        /// <summary>
        ///     Checks if a solution can undergo a specified reaction.
        /// </summary>
        /// <param name="solution">The solution to check.</param>
        /// <param name="reaction">The reaction to check.</param>
        /// <param name="lowestUnitReactions">How many times this reaction can occur.</param>
        /// <returns></returns>
        private bool CanReact(Entity<SolutionComponent> soln, ReactionPrototype reaction, ReactionMixerComponent? mixerComponent, out FixedPoint2 lowestUnitReactions)
        {
            var solution = soln.Comp.Solution;

            lowestUnitReactions = FixedPoint2.MaxValue;
            // spellward start
            if (reaction.ReactionCooldown > TimeSpan.Zero &&
                TryComp<ChemicalReactionCooldownComponent>(soln.Owner, out var cooldown) &&
                cooldown.Cooldowns.TryGetValue(CooldownKey(reaction), out var deadline) &&
                _timing.CurTime < deadline.NextActivation)
            {
                lowestUnitReactions = FixedPoint2.Zero;
                return false;
            }

            // spellward end
            if (solution.Temperature < reaction.MinimumTemperature)
            {
                lowestUnitReactions = FixedPoint2.Zero;
                return false;
            }
            if (solution.Temperature > reaction.MaximumTemperature)
            {
                lowestUnitReactions = FixedPoint2.Zero;
                return false;
            }

            if ((mixerComponent == null && reaction.MixingCategories != null) ||
                mixerComponent != null && reaction.MixingCategories != null && reaction.MixingCategories.Except(mixerComponent.ReactionTypes).Any())
            {
                lowestUnitReactions = FixedPoint2.Zero;
                return false;
            }

            var attempt = new ReactionAttemptEvent(reaction, soln);
            RaiseLocalEvent(soln, ref attempt);
            if (attempt.Cancelled)
            {
                lowestUnitReactions = FixedPoint2.Zero;
                return false;
            }

            foreach (var reactantData in reaction.Reactants)
            {
                var reactantName = reactantData.Key;
                var reactantCoefficient = reactantData.Value.Amount;

                var reactantQuantity = solution.GetTotalPrototypeQuantity(reactantName);

                if (reactantQuantity <= FixedPoint2.Zero)
                    return false;

                if (reactantData.Value.Catalyst)
                {
                    // catalyst is not consumed, so will not limit the reaction. But it still needs to be present, and
                    // for quantized reactions we need to have a minimum amount

                    if (reactantQuantity == FixedPoint2.Zero || reaction.Quantized && reactantQuantity < reactantCoefficient)
                        return false;

                    continue;
                }

                var unitReactions = reactantQuantity / reactantCoefficient;

                if (unitReactions < lowestUnitReactions)
                {
                    lowestUnitReactions = unitReactions;
                }
                // spellward start
            }

            // Limit consumption before PerformReaction removes any reagents.
            // Amount-1 fractional recipes therefore consume min(input A, input B,
            // cap) of each input rather than requiring a full capped batch.
            if (reaction.MaxReactionUnits is { } maximum)
            {
                if (maximum <= FixedPoint2.Zero)
                {
                    lowestUnitReactions = FixedPoint2.Zero;
                    return false;
                }

                lowestUnitReactions = FixedPoint2.Min(lowestUnitReactions, maximum);
                // spellward end
            }

            if (reaction.Quantized)
                lowestUnitReactions = (int)lowestUnitReactions;

            return lowestUnitReactions > 0;
        }

        /// <summary>
        ///     Perform a reaction on a solution. This assumes all reaction criteria are met.
        ///     Removes the reactants from the solution, adds products, and returns a list of products.
        /// </summary>
        private List<string> PerformReaction(Entity<SolutionComponent> soln, ReactionPrototype reaction, FixedPoint2 unitReactions)
        {
            var (uid, comp) = soln;
            var solution = comp.Solution;

            var energy = reaction.ConserveEnergy ? solution.GetThermalEnergy(_prototypeManager) : 0;

            //Remove reactants
            foreach (var reactant in reaction.Reactants)
            {
                if (!reactant.Value.Catalyst)
                {
                    var amountToRemove = unitReactions * reactant.Value.Amount;
                    solution.RemoveReagent(reactant.Key, amountToRemove, ignoreReagentData: true);
                }
            }

            //Create products
            var products = new List<string>();
            foreach (var product in reaction.Products)
            {
                products.Add(product.Key);
                solution.AddReagent(product.Key, product.Value * unitReactions);
            }

            if (reaction.ConserveEnergy)
            {
                var newCap = solution.GetHeatCapacity(_prototypeManager);
                if (newCap > 0)
                    solution.Temperature = energy / newCap;
            }

            OnReaction(soln, reaction, null, unitReactions);

            return products;
        }

        private void OnReaction(Entity<SolutionComponent> soln, ReactionPrototype reaction, ReagentPrototype? reagent, FixedPoint2 unitReactions)
        {
            var args = new EntityEffectReagentArgs(soln, EntityManager, null, soln.Comp.Solution, unitReactions, reagent, null, 1f);

            var posFound = _transformSystem.TryGetMapOrGridCoordinates(soln, out var gridPos);

            _adminLogger.Add(LogType.ChemicalReaction, reaction.Impact,
                $"Chemical reaction {reaction.ID:reaction} occurred with strength {unitReactions:strength} on entity {ToPrettyString(soln):metabolizer} at Pos:{(posFound ? $"{gridPos:coordinates}" : "[Grid or Map not Found]")}");

            foreach (var effect in reaction.Effects)
            {
                if (!effect.ShouldApply(args))
                    continue;

                if (effect.ShouldLog)
                {
                    var entity = args.TargetEntity;
                    _adminLogger.Add(LogType.ReagentEffect, effect.LogImpact,
                        $"Reaction effect {effect.GetType().Name:effect} of reaction {reaction.ID:reaction} applied on entity {ToPrettyString(entity):entity} at Pos:{(posFound ? $"{gridPos:coordinates}" : "[Grid or Map not Found")}");
                }

                effect.Effect(args);
            }

            // Someday, some brave soul will thread through an optional actor
            // argument in from every call of OnReaction up, all just to pass
            // it to PlayPredicted. I am not that brave soul.
            if (_netMan.IsServer)
                _audio.PlayPvs(reaction.Sound, soln);
        }

        /// <summary>
        ///     Performs all chemical reactions that can be run on a solution.
        ///     Removes the reactants from the solution, then returns a solution with all products.
        ///     WARNING: Does not trigger reactions between solution and new products.
        /// </summary>
        // spellward start
        private bool ProcessReactions(Entity<SolutionComponent> soln, SortedSet<ReactionPrototype> reactions,
            ReactionMixerComponent? mixerComponent, ref ChemicalReactionCooldownComponent? cooldown)
        // spellward end
        {
            List<string>? products = null;

            // attempt to perform any applicable reaction
            foreach (var reaction in reactions)
            {
                // spellward start
                if (cooldown == null &&
                    (reaction.MaxReactionUnits.HasValue || reaction.ReactionCooldown > TimeSpan.Zero))
                {
                    cooldown = EnsureComp<ChemicalReactionCooldownComponent>(soln.Owner);
                    cooldown.Processing = true;
                }

                // spellward end
                if (!CanReact(soln, reaction, mixerComponent, out var unitReactions))
                {
                    continue;
                    // spellward start
                }

                if (reaction.ReactionCooldown > TimeSpan.Zero)
                {
                    // Lock the entire group before effects can reenter chemistry.
                    // Schedule from now rather than the old deadline: no catch-up
                    // burst after a lag spike or a long pause.
                    cooldown!.Cooldowns[CooldownKey(reaction)] =
                        new ChemicalReactionCooldown(_timing.CurTime + reaction.ReactionCooldown);
                    Dirty(soln.Owner, cooldown!);
                    // spellward end
                }

                products = PerformReaction(soln, reaction, unitReactions);
                break;
            }

            // did any reaction occur?
            if (products == null)
                return false;

            if (products.Count == 0)
                return true;

            // Add any reactions associated with the new products. This may re-add reactions that were already iterated
            // over previously. The new product may mean the reactions are applicable again and need to be processed.
            foreach (var product in products)
            {
                if (_reactions.TryGetValue(product, out var reactantReactions))
                    reactions.UnionWith(reactantReactions);
            }

            return true;
        }

        /// <summary>
        ///     Continually react a solution until no more reactions occur, with a volume constraint.
        /// </summary>
        public void FullyReactSolution(Entity<SolutionComponent> soln, ReactionMixerComponent? mixerComponent = null)
        {
            // spellward start
            TryComp<ChemicalReactionCooldownComponent>(soln.Owner, out var cooldown);
            if (cooldown != null)
            {
                if (cooldown.Processing)
                {
                    _reentrantUpdates.Add(soln.Owner);
                    return;
                }
                // spellward end

                cooldown.Processing = true; //spellward
            }

            try //spellward
            {
                // spellward start
                _chemRandom.FullyReactSolution(soln, mixerComponent);   // Imperial Medieval chemistry randomization

                // construct the initial set of reactions to check.
                SortedSet<ReactionPrototype> reactions = new();
                foreach (var reactant in soln.Comp.Solution.Contents)
                {
                    if (_reactionsSingle.TryGetValue(reactant.Reagent.Prototype, out var reactantReactions))
                        reactions.UnionWith(reactantReactions);
                }

                // Repeatedly attempt to perform reactions, ending when there are no more applicable reactions, or when we
                // exceed the iteration limit.
                for (var i = 0; i < MaxReactionIterations; i++)
                {
                    if (Deleted(soln.Owner) || Terminating(soln.Owner))
                        return;

                    if (_reentrantUpdates.Remove(soln.Owner))
                    {
                        // Effects may have added inputs that are not listed as
                        // reaction products. Preserve their requested update.
                        _chemRandom.FullyReactSolution(soln, mixerComponent);
                        foreach (var reactant in soln.Comp.Solution.Contents)
                        {
                            if (_reactionsSingle.TryGetValue(reactant.Reagent.Prototype, out var reactantReactions))
                                reactions.UnionWith(reactantReactions);
                        }
                    }

                    if (Deleted(soln.Owner) || Terminating(soln.Owner))
                        return;

                    if (!ProcessReactions(soln, reactions, mixerComponent, ref cooldown) &&
                        !_reentrantUpdates.Contains(soln.Owner))
                        return;
                }

                Log.Error($"{nameof(Solution)} {soln.Owner} could not finish reacting in under {MaxReactionIterations} loops.");
                // spellward end
            }
            // spellward start
            finally
            {
                _reentrantUpdates.Remove(soln.Owner);
                if (cooldown != null)
                    cooldown.Processing = false;
            }
            // spellward end
        }
    }

    /// <summary>
    ///     Raised directed at the owner of a solution to determine whether the reaction should be allowed to occur.
    /// </summary>
    /// <reamrks>
    ///     Some solution containers (e.g., bloodstream, smoke, foam) use this to block certain reactions from occurring.
    /// </reamrks>
    [ByRefEvent]
    public record struct ReactionAttemptEvent(ReactionData Reaction, Entity<SolutionComponent> Solution)    // Imperial Medieval - ReactionData
    {
        public readonly ReactionData Reaction = Reaction;   // Imperial Medieval - ReactionData
        public readonly Entity<SolutionComponent> Solution = Solution;
        public bool Cancelled = false;
    }
}
