using Content.Server.GameTicking.Events;
using Content.Shared.Imperial.Medieval.Chemistry;
using Content.Shared.Imperial.Medieval.ChemistryRandomization;
using Robust.Server.Player;
using Robust.Shared.Random;
using Content.Shared.Random.Helpers;
using System.Linq;
using Robust.Shared.Utility;
using Content.Shared.Chemistry.Reagent;
using Robust.Shared.Prototypes;
using Content.Shared.Chemistry.Components.SolutionManager;
using Content.Shared.Examine;
using Content.Server.MedievalPotionChecker.Components;
using Robust.Shared.Containers;
using Content.Shared.Chemistry.Components;
using Content.Shared.Interaction.Events;

namespace Content.Server.Imperial.Medieval.ChemistryRandomization;

public sealed partial class ChemistryRandomizationSystem : EntitySystem
{
    [Dependency] private readonly SharedChemistryRandomizationSystem _chemRandom = default!;
    [Dependency] private readonly IRobustRandom _random = default!;
    [Dependency] private readonly IPlayerManager _player = default!;
    [Dependency] private readonly IPrototypeManager _prototype = default!;
    [Dependency] private readonly MetaDataSystem _meta = default!;
    [Dependency] private readonly IEntitySystemManager _ent = default!;
    [Dependency] private readonly SharedContainerSystem _containers = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<RoundStartingEvent>(OnRoundStarting);
        SubscribeNetworkEvent<RequestChemistryRandomizationSeedMessage>(OnRequestSeed);
        SubscribeLocalEvent<MedievalRandomChemistryRecipeComponent, MapInitEvent>(RecipeInit);
        SubscribeLocalEvent<MedievalRandomChemistryRecipeComponent, UseInHandEvent>(RevealRecipe);
        SubscribeLocalEvent<SolutionContainerManagerComponent, ExaminedEvent>(OnExamine);
    }
    public void OnExamine(EntityUid uid, SolutionContainerManagerComponent component, ExaminedEvent args)
    {
        if (!HasComp<MedievalPotionCheckerComponent>(args.Examiner))
            return;
        if (!TryComp<ContainerManagerComponent>(uid, out var contman))
            return;

        if (component.Containers == null)
            return;

        var str = $"{Loc.GetString("imperial-medieval-chemistry-examine")}{Environment.NewLine}";
        var addedsomething = false;
        var addedeffects = new List<string>();
        foreach (var key in component.Containers)
        {
            if (!_containers.TryGetContainer(uid, $"solution@{key}", out var container, contman))
                continue;
            var solution = EnsureComp<SolutionComponent>(((ContainerSlot)container).ContainedEntity!.Value).Solution;
            foreach (var reagent in solution.Contents)
            {
                var proto = _prototype.Index<ReagentPrototype>(reagent.Reagent.Prototype);
                if (proto.ShowInBook)
                    continue;

                if (proto.Metabolisms == null)
                    continue;

                foreach (var (_, effectentry) in proto.Metabolisms)
                {
                    var entry = effectentry.MakeGuideEntry(_prototype, _ent);
                    var effects = string.Empty;
                    foreach (var effect in entry.EffectDescriptions)
                    {
                        if (addedeffects.Contains(effect))
                            continue;
                        effects = $"{effects}{Environment.NewLine}- {effect}";
                        addedeffects.Add(effect);
                    }
                    str = $"{str}{effects}{Environment.NewLine}";
                    addedsomething = true;
                }
            }
        }
        if (!addedsomething)
            return;
        args.PushMarkup(str);
    }

    private void RecipeInit(EntityUid uid, MedievalRandomChemistryRecipeComponent component, MapInitEvent args)
    {
        if (component.Weights.Count == 0)
            return;

        var availableGroups = new Dictionary<string, float>();

        foreach (var (group, weight) in component.Weights)
        {
            if (weight > 0 && _chemRandom.GetReagentsFromGroup(group).Count > 0)
                availableGroups.Add(group, weight);
        }
        if (availableGroups.Count == 0)
            return;

        var type = _random.Pick(availableGroups);
        component.Reagent = _random.Pick(_chemRandom.GetReagentsFromGroup(type));

        if (component.Revealed)
            UpdateRecipeDescription(uid, component.Reagent);
    }


    private void RevealRecipe(EntityUid uid, MedievalRandomChemistryRecipeComponent component, UseInHandEvent args)
    {
        if (args.Handled || component.Revealed)
            return;

        if (component.Reagent == null)
            return;

        if (!UpdateRecipeDescription(uid, component.Reagent))
            return;

        component.Revealed = true;
        Dirty(uid, component);
        args.Handled = true;
    }

    private bool UpdateRecipeDescription(EntityUid uid, ReagentPrototype reagent)
    {
        var recipes = SharedChemistryRandomizationSystem.GetReactionsOrNull(reagent);

        if (recipes == null || recipes.Count == 0)
            return false;

        var lines = new List<string>();

        foreach (var recipe in recipes)
        {
            lines.Add(Loc.GetString("imperial-medieval-recipewritten"));

            foreach (var reactant in recipe.Reactants)
            {
                var proto = _prototype.Index<ReagentPrototype>(reactant.Key);
                lines.Add($"- {proto.LocalizedName}");
            }

            lines.Add(string.Empty);
        }


        _meta.SetEntityDescription(uid, string.Join(Environment.NewLine, lines).TrimEnd());
        _meta.SetEntityName(uid, $"recipe: {reagent.LocalizedName}");

        return true;

    }

    private void OnRoundStarting(RoundStartingEvent args)
    {
        _chemRandom.Seed = _random.Next();

        var ev = new SetChemistryRandomizationSeedMessage(_chemRandom.Seed);
        RaiseNetworkEvent(ev);

        _chemRandom.GeneratePotions();
    }

    private void OnRequestSeed(RequestChemistryRandomizationSeedMessage args)
    {
        if (args.Seed == _chemRandom.Seed)
            return;
        if (!_player.TryGetSessionByUsername(args.Username, out var session))
            return;

        var ev = new SetChemistryRandomizationSeedMessage(_chemRandom.Seed);
        RaiseNetworkEvent(ev, session);
    }
}
