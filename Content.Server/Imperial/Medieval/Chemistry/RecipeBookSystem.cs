
using Content.Server.Popups;
using Content.Shared.Imperial.Medieval.Chemistry;
using Content.Shared.Interaction;
using Robust.Server.GameObjects;
using System.Linq;
using Content.Shared.Imperial.Medieval.ChemistryRandomization;
using Content.Shared.Random.Helpers;
using Robust.Shared.Random;

namespace Content.Server.Imperial.Medieval.Chemistry;

public sealed class RecipeBookSystem : EntitySystem
{
    private const string AntidoteT1 = "medievalantidote";

    [Dependency] private readonly PopupSystem _popup = default!;
    [Dependency] private readonly UserInterfaceSystem _ui = default!;
    [Dependency] private readonly IRobustRandom _random = default!;
    [Dependency] private readonly SharedChemistryRandomizationSystem _chemRandom = default!;

    public override void Initialize()
    {
        SubscribeLocalEvent<MedievalRecipeBookComponent, MapInitEvent>(OnMapInit);
        SubscribeLocalEvent<MedievalRecipeBookComponent, InteractUsingEvent>(PutRecipe);
    }

    private void OnMapInit(EntityUid uid, MedievalRecipeBookComponent component, MapInitEvent args)
    {
        // Every book knows the permanent T1 Antidote formula without a recipe page.
        if (!component.Recipes.Contains(AntidoteT1))
            component.Recipes.Add(AntidoteT1);

        UpdateUserInterface(uid, component);
    }

    public void PutRecipe(EntityUid uid, MedievalRecipeBookComponent component, InteractUsingEvent args)
    {
        if (args.Handled)
            return;

        if (!TryComp<MedievalRandomChemistryRecipeComponent>(args.Used, out var recipe))
            return;

        string reagentId;

        if (recipe.Revealed)
        {
            reagentId = recipe.Reagent.ID;

            if (component.Recipes.Contains(reagentId))
            {
                _popup.PopupEntity(Loc.GetString("imperial-medieval-recipe-already"), uid, args.User);
                return;
            }
        }
        else
        {
            var availableGroups = new Dictionary<string, float>();

            foreach (var (group, weight) in recipe.Weights)
            {
                if (weight > 0 && _chemRandom.GetReagentsFromGroup(group).Any(reagent => !component.Recipes.Contains(reagent.ID)))
                    availableGroups.Add(group, weight);
            }

            if (availableGroups.Count == 0)
            {
                _popup.PopupEntity(Loc.GetString("imperial-medieval-recipe-already"), uid, args.User);
                return;
            }

            var selectedGroup = _random.Pick(availableGroups);

            var availableRecipes = _chemRandom.GetReagentsFromGroup(selectedGroup)
            .Where(reagent => !component.Recipes.Contains(reagent.ID))
            .ToList();

            reagentId = _random.Pick(availableRecipes).ID;
        }

        component.Recipes.Add(reagentId);
        QueueDel(args.Used);
        args.Handled = true;

        _popup.PopupEntity(Loc.GetString("imperial-medieval-recipe-inserted"), uid, args.User);

        UpdateUserInterface(uid, component);
    }

    private void UpdateUserInterface(EntityUid uid, MedievalRecipeBookComponent component)
    {
        if (_ui.HasUi(uid, RecipeBookUi.Key))
        {
            _ui.SetUiState(uid, RecipeBookUi.Key, new PotionBookUserInterfaceState { Ids = component.Recipes });
        }
    }

}

