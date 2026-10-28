using System.Linq;
using Content.Shared.Administration.Logs;
using Content.Shared.Chemistry.Components;
using Content.Shared.Chemistry.Reaction;
using Content.Shared.Chemistry.Reagent;
using Content.Shared.Database;
using Content.Shared.Destructible.Thresholds;
using Content.Shared.EntityEffects;
using Content.Shared.FixedPoint;
using Content.Shared.Nutrition;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Network;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;
using Robust.Shared.Utility;

namespace Content.Shared.Imperial.Medieval.ChemistryRandomization;

public sealed class SharedChemistryRandomizationSystem : EntitySystem
{
    [Dependency] private readonly IPrototypeManager _prototypeManager = default!;
    [Dependency] private readonly SharedAudioSystem _audio = default!;
    [Dependency] private readonly ISharedAdminLogManager _adminLogger = default!;
    [Dependency] private readonly SharedTransformSystem _transformSystem = default!;
    [Dependency] private readonly INetManager _net = default!;

    /// <summary>
    /// Хранит все реагенты в паре с одним из реактантов
    /// </summary>
    private readonly Dictionary<string, List<ReactionData>> _reactionsSingle = new();

    /// <summary>
    /// Хранит все реагенты в парах с каждым из реактантов
    /// </summary>
    private readonly Dictionary<string, List<ReactionData>> _reactions = new();

    /// <summary>
    /// Хранит все рандомно сгенерированные реагенты и инфу о них
    /// </summary>
    private readonly Dictionary<string, GeneratedReagentData> _reagentsData = new();
    /// <summary>
    /// Хранит инфу сгенерированных груп
    /// </summary>
    private readonly Dictionary<string, GeneratedGroupData> _groupData = new();

    /// <summary>
    /// Сид, для синхронизации клиента и сервера
    /// </summary>
    public int Seed = 0;

    public System.Random Random = new();

    #region Генерация
    /// <summary>
    /// Генерирует зелья заново на основе <see cref="Seed"/>
    /// </summary>
    public void GeneratePotions()
    {
        _reactions.Clear();
        _reactionsSingle.Clear();
        _reagentsData.Clear();
        _groupData.Clear();

        var prototypes = _prototypeManager.EnumeratePrototypes<ChemistryRandomizationGroupPrototype>().OrderBy(x => x.Priority);
        var offset = 0;
        foreach (var item in prototypes)
        {
            Random = new System.Random(Seed + offset);
            offset++;
            var groups = new List<Dictionary<string, ReactantPrototype>>();
            var grouprecipes = new List<List<Dictionary<string, ReactantPrototype>>>();
            var reagents = new List<Dictionary<string, ReactantPrototype>>();
            if (item.Reactants > 0)
            {
                var blacklist = new Dictionary<string, List<string>>();
                foreach (var reactant in item.Reagents)
                {
                    var result = new Dictionary<string, ReactantPrototype>() { { reactant, new() } };
                    var i = 0;
                    if (item.Reactants > 1)
                    {
                        foreach (var reactantother in item.Reagents)
                        {
                            if (reactantother == reactant)
                                continue;

                            if (blacklist.TryGetValue(reactant, out var bl) && bl.Contains(reactantother))
                                continue;

                            result.Add(reactantother, new());
                            blacklist.GetOrNew(reactantother).Add(reactant);
                            i++;
                            if (i == item.Reactants - 1)
                            {
                                i = 0;
                                reagents.Add(result);
                                result = new Dictionary<string, ReactantPrototype>() { { reactant, new() } };
                            }
                        }
                    }
                    else
                    {
                        reagents.Add(result);
                    }
                }
            }
            foreach (var group in item.UsedGroups)
            {
                if (!_groupData.TryGetValue(group, out var generated))
                {
                    Log.Error($"{item.ID} was generated before {group}. Try setting priority of {item.ID} higher.");
                    continue;
                }
                var result = new Dictionary<string, ReactantPrototype>();
                foreach (var reactant in generated.GeneratedPotions)
                {
                    result.Add(reactant.Key, new());
                }
                groups.Add(result);
            }
            foreach (var group in item.UsedGroupsRecipes)
            {
                if (!_groupData.TryGetValue(group, out var generated))
                {
                    Log.Error($"{item.ID} was generated before {group}. Try setting priority of {item.ID} higher.");
                    continue;
                }
                grouprecipes.Add(generated.RecipesLeft);
            }
            var recipes = new List<Dictionary<string, ReactantPrototype>>();
            var generating = true;
            while (generating == true)
            {
                var recipe = new Dictionary<string, ReactantPrototype>();
                if (item.Reactants > 0)
                {
                    if (reagents.Count <= 0)
                    {
                        generating = false;
                        break;
                    }
                    var reagentlist = Random.PickAndTake(reagents);
                    foreach (var reagent in reagentlist)
                    {
                        recipe.Add(reagent.Key, reagent.Value);
                    }
                }
                if (generating == false)
                    break;

                foreach (var potions in groups)
                {
                    if (potions.Count <= 0)
                    {
                        generating = false;
                        break;
                    }

                    var potion = Random.PickAndTake(potions);
                    recipe.Add(potion.Key, potion.Value);
                }
                if (generating == false)
                    break;

                foreach (var potionrecipes in grouprecipes)
                {
                    if (potionrecipes.Count <= 0)
                    {
                        generating = false;
                        break;
                    }

                    var potionrecipe = Random.PickAndTake(potionrecipes);
                    foreach (var reagent in potionrecipe)
                    {
                        recipe.Add(reagent.Key, reagent.Value);
                    }
                }
                if (generating == false)
                    break;

                recipes.Add(recipe);
            }
            GenerateFromGroup(item, ref recipes, ref offset);
        }
    }

    /// <summary>
    /// Создаёт зелья из указанной группы
    /// </summary>
    /// <param name="randomProtoId">Группа с требуемыми данными</param>
    /// <param name="offset">Смещение сида. Необходимо для того, чтобы не создавались одинаковые рецепты</param>
    private void GenerateFromGroup(ProtoId<ChemistryRandomizationGroupPrototype> randomProtoId, ref List<Dictionary<string, ReactantPrototype>> recipes, ref int offset)
    {
        var randomProto = _prototypeManager.Index(randomProtoId);

        // Validate every override, including potions not selected for this round.
        var productAmounts = new Dictionary<string, FixedPoint2>();
        foreach (var (potion, amount) in randomProto.ProductAmounts)
        {
            if (!randomProto.Potions.Contains(potion))
            {
                Log.Error($"Chemistry group {randomProto.ID}: productAmounts key '{potion}' is not in potions. Override ignored.");
                continue;
            }

            if (!_prototypeManager.TryIndex<ReagentPrototype>(potion, out _))
            {
                Log.Error($"Chemistry group {randomProto.ID}: productAmounts key '{potion}' is not a known reagent. Override ignored.");
                continue;
            }

            if (amount <= FixedPoint2.Zero || amount == FixedPoint2.MaxValue)
            {
                Log.Error($"Chemistry group {randomProto.ID}: productAmounts for '{potion}' must be positive and cannot be MaxValue (got {amount}). Using each recipe's reactants.Count instead.");
                continue;
            }

            productAmounts.Add(potion, amount);
        }

        var requiredReagents = ValidateRequiredReagents(randomProto);

        var generated = new GeneratedGroupData()
        {
            ID = randomProtoId.Id
        };
        //var names = randomProto.Names.Clone();    // Если хотите сделать полноценные названия
        var copyJustInCase = randomProto.Potions.Clone();
        for (var i = 0; i < randomProto.PotionCount; i++)
        {
            Random = new System.Random(Seed + offset);
            offset++;
            var item = Random.PickAndTake(copyJustInCase);
            var reactantList = requiredReagents.TryGetValue(item, out var required)
                ? GetRequiredReactants(recipes, randomProto.RecipeCount, randomProto.Reagents, required, randomProto.ID, item)
                : GetReactants(ref recipes, randomProto.RecipeCount);
            var reactions = new List<ReactionData>();
            foreach (var reactants in reactantList)
            {
                //var randomEffects = GetEffects(randomProto.EffectRandom);
                //var randomMixer = GetMixer(randomProto.MixerRandom);
                var (minTemp, maxTemp) = GetTemperatures(randomProto.MinTemperature, randomProto.MaxTemperature);

                var productAmount = productAmounts.TryGetValue(item, out var amount)
                    ? amount
                    : FixedPoint2.New(reactants.Count);

                var resultReaction = new ReactionData()
                {
                    Reactants = reactants,
                    Name = item,
                    //MixingCategories = randomMixer,
                    MinimumTemperature = minTemp,
                    MaximumTemperature = maxTemp,
                    Sound = Random.Pick(randomProto.Sounds),
                    Products = new() { { item, productAmount } }
                };
                if (_prototypeManager.TryIndex<ReactionPrototype>(item, out var reaction))
                {
                    resultReaction.Effects = reaction.Effects;
                }

                _reactionsSingle.GetOrNew(reactants.First().Key).Add(resultReaction);
                foreach (var reactant in reactants)
                {
                    _reactions.GetOrNew(reactant.Key).Add(resultReaction);
                }
                reactions.Add(resultReaction);
            }

            var reagentData = new GeneratedReagentData()
            {
                //Name = Loc.GetString(Random.PickAndTake(names)),  // Если хотите сделать полноценные названия
                Name = Random.Next(1000, 9999).ToString(),
                Description = Random.Pick(randomProto.Descriptions),
                Flavor = Random.Pick(randomProto.Flavors),
                Color = Random.Pick(randomProto.Colors),
                Reactions = reactions,
                Group = randomProtoId.Id
            };
            if (_net.IsServer)
            {
                Log.Info($"{item} is");
                foreach (var reaction in reactions)
                {
                    var str = "";
                    foreach (var reactant in reaction.Reactants)
                    {
                        str = $"{str}{reactant.Key}|";
                    }
                    Log.Info(str);
                }
            }
            generated.GeneratedPotions.Add(item, reagentData);
            _reagentsData.Add(item, reagentData);
        }
        generated.RecipesLeft = recipes;
        _groupData.Add(randomProtoId.Id, generated);
    }

    /// <summary>
    /// Возвращает рандомно выбранные реактанты и зелья из списков реагентов и групп зелий
    /// </summary>
    /// <param name="reactants">Список реактантов из основной группы</param>
    /// <param name="groups">Группы, из которых возьмётся случайное зелье</param>
    /// <param name="reactantsCount">Число реактантов, которое будет использовано</param>
    /// <returns></returns>
    private List<Dictionary<string, ReactantPrototype>> GetReactants(ref List<Dictionary<string, ReactantPrototype>> recipes, int recipesCount)
    {
        var result = new List<Dictionary<string, ReactantPrototype>>();
        for (var i = 0; i < recipesCount; i++)
        {
            result.Add(Random.PickAndTake(recipes));
        }
        return result;
    }

    private Dictionary<string, List<string>> ValidateRequiredReagents(ChemistryRandomizationGroupPrototype group)
    {
        var result = new Dictionary<string, List<string>>();
        if (group.RequiredReagents.Count == 0)
            return result;

        var randomizedPotions = _prototypeManager.EnumeratePrototypes<ChemistryRandomizationGroupPrototype>()
            .SelectMany(proto => proto.Potions).ToHashSet();
        foreach (var (potion, reagents) in group.RequiredReagents)
        {
            if (!group.Potions.Contains(potion) || !_prototypeManager.TryIndex<ReagentPrototype>(potion, out _))
                throw new System.InvalidOperationException($"Chemistry group {group.ID}: requiredReagents key '{potion}' must be a known potion in this group.");

            if (reagents == null || reagents.Distinct().Count() != reagents.Count)
                throw new System.InvalidOperationException($"Chemistry group {group.ID}: requiredReagents for '{potion}' must be a list without duplicates.");

            foreach (var reagent in reagents)
            {
                if (!group.Reagents.Contains(reagent) || randomizedPotions.Contains(reagent) ||
                    !_prototypeManager.TryIndex<ReagentPrototype>(reagent, out _))
                    throw new System.InvalidOperationException($"Chemistry group {group.ID}: required reagent '{reagent}' for '{potion}' must be a known raw reagent from this group's reagents.");
            }

            if (reagents.Count > 0)
                result.Add(potion, reagents);
        }

        return result;
    }

    /// <summary>
    /// Reserves existing raw slots, preserving potion precursors and reactant coefficients.
    /// Only the selected recipe is removed from the original pool.
    /// </summary>
    private List<Dictionary<string, ReactantPrototype>> GetRequiredReactants(
        List<Dictionary<string, ReactantPrototype>> recipes, int recipesCount,
        List<string> rawReagents, List<string> required, string group, string potion)
    {
        var result = new List<Dictionary<string, ReactantPrototype>>();
        for (var i = 0; i < recipesCount; i++)
        {
            var candidates = new List<(int Index, Dictionary<string, ReactantPrototype> Reactants)>();
            for (var index = 0; index < recipes.Count; index++)
            {
                var recipe = ReserveRequiredReagents(recipes[index], rawReagents, required);
                if (recipe == null || result.Any(previous => SameReactants(previous, recipe)) ||
                    candidates.Any(candidate => SameReactants(candidate.Reactants, recipe)))
                    continue;

                // A reserved recipe must not duplicate a reaction already assigned to another potion.
                if (_reactions.TryGetValue(recipe.First().Key, out var existing) &&
                    existing.Any(reaction => SameReactants(reaction.Reactants, recipe)))
                    continue;

                // If this exact recipe is already in the pool, consume that entry so it cannot
                // be assigned again later by the normal, unconstrained selection path.
                var originalIndex = recipes.FindIndex(original => SameReactants(original, recipe));
                candidates.Add(originalIndex >= 0
                    ? (originalIndex, recipes[originalIndex])
                    : (index, recipe));
            }

            if (candidates.Count == 0)
                throw new System.InvalidOperationException($"Chemistry group {group}: cannot generate {recipesCount} unique recipes for '{potion}' with its required raw reagents. Check the available raw slots and recipe pool.");

            var selected = Random.PickAndTake(candidates);
            recipes.RemoveAt(selected.Index);
            result.Add(selected.Reactants);
        }

        return result;
    }

    private static Dictionary<string, ReactantPrototype>? ReserveRequiredReagents(
        Dictionary<string, ReactantPrototype> recipe, List<string> rawReagents, List<string> required)
    {
        var missing = required.Where(reagent => !recipe.ContainsKey(reagent)).ToList();
        if (missing.Count == 0)
            return recipe;

        var replaceable = recipe.Keys.Where(reagent => rawReagents.Contains(reagent) && !required.Contains(reagent)).ToList();
        if (replaceable.Count < missing.Count)
            return null;

        var replacements = new Dictionary<string, string>();
        for (var i = 0; i < missing.Count; i++)
            replacements.Add(replaceable[replaceable.Count - 1 - i], missing[i]);

        var result = new Dictionary<string, ReactantPrototype>();
        foreach (var (reagent, reactant) in recipe)
            result.Add(replacements.TryGetValue(reagent, out var replacement) ? replacement : reagent, reactant);

        return result;
    }

    private static bool SameReactants(Dictionary<string, ReactantPrototype> first, Dictionary<string, ReactantPrototype> second)
    {
        if (first.Count != second.Count)
            return false;

        foreach (var (reagent, reactant) in first)
        {
            if (!second.TryGetValue(reagent, out var other) || reactant.Amount != other.Amount || reactant.Catalyst != other.Catalyst)
                return false;
        }

        return true;
    }

    /// <summary>
    /// Возвращает случайно выбранные эффекты для реакции
    /// </summary>
    /// <param name="effectRandom">Класс, хранящий инфу о доступных эффектах</param>
    /// <returns></returns>
    private List<EntityEffect> GetEffects(EntityEffectRandom effectRandom)
    {
        var result = new List<EntityEffect>();

        if (!Random.Prob(effectRandom.Probability))
            return result;

        var effects = effectRandom.Effects.ShallowClone();
        for (var i = 0; i < effectRandom.Count && i < effectRandom.Effects.Count; i++)
        {
            result.Add(Random.PickAndTake(effects));
        }

        return result;
    }

    /// <summary>
    /// Возвращает случайно выбранный миксер для реакции
    /// </summary>
    /// <param name="mixerRandom">Класс, хранящий инфу о доступных миксерах</param>
    /// <returns></returns>
    private List<ProtoId<MixingCategoryPrototype>> GetMixer(MixingCategoryRandom mixerRandom)
    {
        var result = new List<ProtoId<MixingCategoryPrototype>>();

        if (!Random.Prob(mixerRandom.Probability) || mixerRandom.Categories.Count <= 0)
            return result;

        result.Add(Random.Pick(mixerRandom.Categories));

        return result;
    }

    /// <summary>
    /// Возвращает минимальную и максимальную температуры для реакции
    /// </summary>
    /// <param name="min"></param>
    /// <param name="max"></param>
    /// <returns></returns>
    private (float, float) GetTemperatures(MinMax? min, MinMax? max)
    {
        (float, float) result = (0, float.PositiveInfinity);
        if (min.HasValue)
            result.Item1 = Random.Next(min.Value.Min, min.Value.Max);
        if (max.HasValue)
            result.Item2 = Random.Next(max.Value.Min, max.Value.Max);

        return result;
    }
    #endregion

    #region Статичные функции
    // Prototype properties and solution color helpers have no entity-system dependency.
    // Resolve the current game context so another server/client cannot replace its recipes.
    private static GeneratedReagentData? GetGeneratedData(ReagentPrototype proto)
    {
        if (!IoCManager.Resolve<IEntitySystemManager>().TryGetEntitySystem<SharedChemistryRandomizationSystem>(out var system))
            return null;

        return system._reagentsData.GetValueOrDefault(proto.ID);
    }

    /// <summary>
    /// Получает имя реагента с учётом рандомно сгенерированных
    /// </summary>
    /// <param name="proto"></param>
    /// <returns></returns>
    public static string GetName(ReagentPrototype proto)
    {
        if (GetGeneratedData(proto) is not { } value)
            return Robust.Shared.Localization.Loc.GetString(proto.Name);

        return Robust.Shared.Localization.Loc.GetString("spellward-random-potion-name", ("number", value.Name));
    }

    public static string GetStaticName(ReagentPrototype proto)
    {
        var staticName = Robust.Shared.Localization.Loc.GetString(proto.Name);

        if (GetGeneratedData(proto) is not { } value)
            return staticName;

        var generatedName = value.Name;

        if (generatedName.Length < 4)
            return staticName;

        var number = generatedName.Substring(generatedName.Length - 4);

        foreach (var digit in number)
        {
            if (digit < '0' || digit > '9')
                return staticName;
        }

        return $"{staticName} {number}";
    }

    /// <summary>
    /// Получает физическое описание реагента с учётом рандомно сгенерированных
    /// </summary>
    /// <param name="proto"></param>
    /// <returns></returns>
    public static string GetDescription(ReagentPrototype proto)
    {
        if (GetGeneratedData(proto) is not { } value)
            return Robust.Shared.Localization.Loc.GetString(proto.PhysicalDescription);

        return Robust.Shared.Localization.Loc.GetString(value.Description);
    }

    /// <summary>
    /// Получает вкус реагента с учётом рандомно сгенерированных
    /// </summary>
    /// <param name="proto"></param>
    /// <returns></returns>
    public static ProtoId<FlavorPrototype>? GetFlavor(ReagentPrototype proto)
    {
        if (GetGeneratedData(proto) is not { } value)
            return proto.Flavor;

        return value.Flavor;
    }

    /// <summary>
    /// Получает цвет реагента с учётом рандомно сгенерированных
    /// </summary>
    /// <param name="proto"></param>
    /// <returns></returns>
    public static Color GetColor(ReagentPrototype proto)
    {
        if (GetGeneratedData(proto) is not { } value)
            return proto.SubstanceColor;

        return value.Color;
    }

    /// <summary>
    /// Получает реакцию для получения реагента, если она была сгенерирована
    /// В ином случае возвращает null
    /// </summary>
    /// <param name="proto"></param>
    /// <returns></returns>
    public static List<ReactionData>? GetReactionsOrNull(ReagentPrototype proto)
    {
        if (GetGeneratedData(proto) is not { } value)
            return null;

        return value.Reactions;
    }
    #endregion

    #region Обычные функции
    /// <summary>
    /// Возвращает лист реагентов определенной групы
    /// </summary>
    public List<ReagentPrototype> GetReagentsFromGroup(string group)
    {
        var result = new List<ReagentPrototype>();
        foreach (var (reagent, _) in _reagentsData.Where(x => x.Value.Group == group).ToList())
        {
            if (!_prototypeManager.TryIndex<ReagentPrototype>(reagent, out var proto))
                continue;
            result.Add(proto);
        }
        return result;
    }
    #endregion

    #region Копипаста офф кода
    /// <summary>
    ///     Checks if a solution can undergo a specified reaction.
    /// </summary>
    /// <param name="solution">The solution to check.</param>
    /// <param name="reaction">The reaction to check.</param>
    /// <param name="lowestUnitReactions">How many times this reaction can occur.</param>
    /// <returns></returns>
    private bool CanReact(Entity<SolutionComponent> soln, ReactionData reaction, ReactionMixerComponent? mixerComponent, out FixedPoint2 lowestUnitReactions)
    {
        var solution = soln.Comp.Solution;

        lowestUnitReactions = FixedPoint2.MaxValue;
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
        }

        if (reaction.Quantized)
            lowestUnitReactions = (int)lowestUnitReactions;

        if (lowestUnitReactions <= FixedPoint2.Zero)
            return false;

        // FixedPoint2 stores hundredths. Do not consume inputs if a positive product
        // would truncate below the smallest representable output (0.01 units).
        foreach (var product in reaction.Products)
        {
            if (product.Value > FixedPoint2.Zero && (long)product.Value.Value * lowestUnitReactions.Value < 100)
                return false;
        }

        return true;
    }

    /// <summary>
    ///     Perform a reaction on a solution. This assumes all reaction criteria are met.
    ///     Removes the reactants from the solution, adds products, and returns a list of products.
    /// </summary>
    private List<string> PerformReaction(Entity<SolutionComponent> soln, ReactionData reaction, FixedPoint2 unitReactions)
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

    private void OnReaction(Entity<SolutionComponent> soln, ReactionData reaction, ReagentPrototype? reagent, FixedPoint2 unitReactions)
    {
        var args = new EntityEffectReagentArgs(soln, EntityManager, null, soln.Comp.Solution, unitReactions, reagent, null, 1f);

        var posFound = _transformSystem.TryGetMapOrGridCoordinates(soln, out var gridPos);

        _adminLogger.Add(LogType.ChemicalReaction, reaction.Impact,
            $"Chemical reaction occurred with strength {unitReactions:strength} on entity {ToPrettyString(soln):metabolizer} at Pos:{(posFound ? $"{gridPos:coordinates}" : "[Grid or Map not Found]")}");

        foreach (var effect in reaction.Effects)
        {
            if (!effect.ShouldApply(args))
                continue;

            if (effect.ShouldLog)
            {
                var entity = args.TargetEntity;
                _adminLogger.Add(LogType.ReagentEffect, effect.LogImpact,
                    $"Reaction effect {effect.GetType().Name:effect} of reaction applied on entity {ToPrettyString(entity):entity} at Pos:{(posFound ? $"{gridPos:coordinates}" : "[Grid or Map not Found")}");
            }

            effect.Effect(args);
        }

        _audio.PlayPvs(reaction.Sound, soln);
    }

    /// <summary>
    ///     Performs all chemical reactions that can be run on a solution.
    ///     Removes the reactants from the solution, then returns a solution with all products.
    ///     WARNING: Does not trigger reactions between solution and new products.
    /// </summary>
    private bool ProcessReactions(Entity<SolutionComponent> soln, SortedSet<ReactionData> reactions, ReactionMixerComponent? mixerComponent)
    {
        HashSet<ReactionData> toRemove = new();
        List<string>? products = null;

        // attempt to perform any applicable reaction
        foreach (var reaction in reactions)
        {
            if (!CanReact(soln, reaction, mixerComponent, out var unitReactions))
            {
                toRemove.Add(reaction);
                continue;
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
        // construct the initial set of reactions to check.
        SortedSet<ReactionData> reactions = new();
        foreach (var reactant in soln.Comp.Solution.Contents)
        {
            if (_reactionsSingle.TryGetValue(reactant.Reagent.Prototype, out var reactantReactions))
                reactions.UnionWith(reactantReactions);
        }

        // Repeatedly attempt to perform reactions, ending when there are no more applicable reactions, or when we
        // exceed the iteration limit.
        for (var i = 0; i < 20; i++)
        {
            if (!ProcessReactions(soln, reactions, mixerComponent))
                return;
        }

        Log.Error($"{nameof(Solution)} {soln.Owner} could not finish reacting in under 20 loops.");
    }
    #endregion
}

