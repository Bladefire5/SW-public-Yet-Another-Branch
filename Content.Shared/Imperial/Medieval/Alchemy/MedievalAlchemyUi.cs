
using Content.Shared.Chemistry.Reagent;
using Content.Shared.FixedPoint;
using Robust.Shared.Serialization;
using Content.Shared.Chemistry;

namespace Content.Shared.Imperial.Medieval.Alchemy;

[Serializable, NetSerializable]
public enum MedievalAlchemyUiKey : byte
{
    Key
}

[Serializable, NetSerializable]
public sealed class MedievalAlchemyBottleInfo
{
    public readonly int SlotIndex;

    public readonly NetEntity Entity;

    public readonly string Name;

    public readonly FixedPoint2 Volume;
    public readonly FixedPoint2 Capacity;

    public readonly ReagentQuantity[] Reagents;

    public MedievalAlchemyBottleInfo(
        int slotIndex,
        NetEntity entity,
        string name,
        FixedPoint2 volume,
        FixedPoint2 capacity,
        ReagentQuantity[] reagents)
    {
        SlotIndex = slotIndex;
        Entity = entity;
        Name = name;
        Volume = volume;
        Capacity = capacity;
        Reagents = reagents;
    }

    public MedievalAlchemyBottleInfo(
        NetEntity entity,
        string name,
        FixedPoint2 volume,
        FixedPoint2 capacity)
        : this(
            -1,
            entity,
            name,
            volume,
            capacity,
            Array.Empty<ReagentQuantity>())
    {
    }
}

[Serializable, NetSerializable]
public sealed class MedievalAlchemyBolusBottleInfo
{
    public readonly NetEntity Entity;
    public readonly string Name;
    public readonly int Count;
    public readonly int Capacity;

    public MedievalAlchemyBolusBottleInfo(
        NetEntity entity,
        string name,
        int count,
        int capacity)
    {
        Entity = entity;
        Name = name;
        Count = count;
        Capacity = capacity;
    }
}

[Serializable, NetSerializable]
public sealed class MedievalAlchemyUiState : BoundUserInterfaceState
{
    public readonly MedievalAlchemyBottleInfo[] Bottles;
    public readonly int MaxBottles;
    public MedievalAlchemyBolusBottleInfo? BolusBottle { get; init; }
    public readonly FixedPoint2 InputVolume;
    public readonly FixedPoint2 InputCapacity;

    public readonly FixedPoint2 BufferVolume;
    public readonly FixedPoint2 BufferCapacity;

    public readonly FixedPoint2 WaterVolume;
    public readonly FixedPoint2 WaterCapacity;

    public readonly FixedPoint2 NutrimentVolume;
    public readonly FixedPoint2 NutrimentCapacity;

    public ChemMasterBoundUserInterfaceState? ChemMasterState { get; init; }

    public MedievalAlchemyUiState(
        MedievalAlchemyBottleInfo[] bottles,
        int maxBottles)
        : this(
            bottles,
            maxBottles,
            FixedPoint2.Zero,
            FixedPoint2.Zero,
            FixedPoint2.Zero,
            FixedPoint2.Zero,
            FixedPoint2.Zero,
            FixedPoint2.Zero)
    {
    }

    public MedievalAlchemyUiState(
        MedievalAlchemyBottleInfo[] bottles,
        int maxBottles,
        FixedPoint2 bufferVolume,
        FixedPoint2 bufferCapacity,
        FixedPoint2 waterVolume,
        FixedPoint2 waterCapacity,
        FixedPoint2 nutrimentVolume,
        FixedPoint2 nutrimentCapacity,
        FixedPoint2 inputVolume = default,
        FixedPoint2 inputCapacity = default)
    {
        Bottles = bottles;
        MaxBottles = maxBottles;
        BufferVolume = bufferVolume;
        BufferCapacity = bufferCapacity;
        InputVolume = inputVolume;
        InputCapacity = inputCapacity;
        WaterVolume = waterVolume;
        WaterCapacity = waterCapacity;
        NutrimentVolume = nutrimentVolume;
        NutrimentCapacity = nutrimentCapacity;
    }
}

[Serializable, NetSerializable]
public sealed class MedievalAlchemyEjectBottleMessage : BoundUserInterfaceMessage
{
    public readonly NetEntity Bottle;

    public MedievalAlchemyEjectBottleMessage(NetEntity bottle)
    {
        Bottle = bottle;
    }
}

[Serializable, NetSerializable]
public sealed class MedievalAlchemyFillBottlesMessage : BoundUserInterfaceMessage
{
    public readonly NetEntity[] Bottles;

    public readonly uint PotionDosage;

    public readonly uint WaterDosage;

    public readonly string Label;

    public MedievalAlchemyFillBottlesMessage(
        NetEntity[] bottles,
        uint potionDosage,
        uint waterDosage,
        string label)
    {
        Bottles = bottles;
        PotionDosage = potionDosage;
        WaterDosage = waterDosage;
        Label = label;
    }
}

[Serializable, NetSerializable]
public sealed class MedievalAlchemyCreateBolusesMessage : BoundUserInterfaceMessage
{
    public readonly uint Count;
    public readonly uint PotionDosage;
    public readonly uint Appearance;
    public readonly string Label;

    public MedievalAlchemyCreateBolusesMessage(
        uint count,
        uint potionDosage,
        uint appearance,
        string label)
    {
        Count = count;
        PotionDosage = potionDosage;
        Appearance = appearance;
        Label = label;
    }
}

[Serializable, NetSerializable]
public sealed class MedievalAlchemyFillNutrimentMessage : BoundUserInterfaceMessage
{
}


