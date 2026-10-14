using Content.Shared.Chemistry;

namespace Content.Server.Imperial.Medieval.Alchemy;

[RegisterComponent]
public sealed partial class MedievalAlchemyComponent : Component
{
    public const string BottleContainerId = "alchemyBottles";
    public const string WaterSolution = "alchemyWater";
    public const string NutrimentSolution = "alchemyNutriment";
    public const int MaxBottles = 9;


    [ViewVariables]
    public EntityUid?[] BottleSlots = new EntityUid?[MaxBottles];

    [ViewVariables]
    public ChemMasterMode Mode = ChemMasterMode.Transfer;

    [ViewVariables]
    public ChemMasterSortingType SortingType = ChemMasterSortingType.None;
}
