using System;
using System.Numerics;
using Content.Shared.Database;
using Content.Shared.EntityEffects;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;
using Robust.Shared.Maths;
using Robust.Shared.Network;
using Robust.Shared.Prototypes;

namespace Content.Shared.Imperial.Medieval.Chemistry;

public enum MedievalGroundFireTier : byte
{
    T1 = 1,
    T3 = 3,
}

/// <summary>
/// Places the fixed alchemical fire pattern around the reaction's tile.
/// The conventional explosion is a separate reaction effect.
/// </summary>
[DataDefinition]
public sealed partial class MedievalGroundFireReactionEffect : EntityEffect
{
    private static readonly EntProtoId T1Prototype = "MedievalAlchemyGroundFireT1"; //spellward
    private static readonly EntProtoId T3Prototype = "MedievalAlchemyGroundFireT3"; //spellward

    private static readonly Vector2i[] T1Offsets =
    {
        new(0, 1),
        new(-1, 0), new(0, 0), new(1, 0),
        new(0, -1),
    };

    private static readonly Vector2i[] T3Offsets =
    {
        new(-1, 1), new(0, 1), new(1, 1),
        new(-2, 0), new(-1, 0), new(0, 0), new(1, 0), new(2, 0),
        new(-1, -1), new(0, -1), new(1, -1),
    };

    [DataField("fireTier", required: true)]
    public MedievalGroundFireTier FireTier;

    public override bool ShouldLog => true;
    public override LogImpact LogImpact => LogImpact.High;

    protected override string? ReagentEffectGuidebookText(IPrototypeManager prototype,
        IEntitySystemManager entSys)
    {
        var pattern = GetPattern();
        return Loc.GetString("reagent-effect-guidebook-create-entity-reaction-effect",
            ("chance", Probability),
            ("entname", prototype.Index<EntityPrototype>(pattern.Prototype).Name),
            ("amount", pattern.Offsets.Length));
    }

    public override void Effect(EntityEffectBaseArgs args)
    {
        if (!IoCManager.Resolve<INetManager>().IsServer)
            return;

        var transform = args.EntityManager.System<SharedTransformSystem>();
        if (!transform.TryGetMapOrGridCoordinates(args.TargetEntity, out var coordinates))
            return;

        var origin = coordinates.Value;
        var tileSize = 1f;
        if (args.EntityManager.TryGetComponent<MapGridComponent>(origin.EntityId, out var grid))
        {
            var map = args.EntityManager.System<SharedMapSystem>();
            var tile = map.CoordinatesToTile(origin.EntityId, grid, origin);
            origin = map.ToCenterCoordinates(origin.EntityId, tile, grid);
            tileSize = grid.TileSize;
        }
        else
        {
            origin = new EntityCoordinates(origin.EntityId,
                new Vector2(MathF.Floor(origin.Position.X) + 0.5f,
                    MathF.Floor(origin.Position.Y) + 0.5f));
        }

        var pattern = GetPattern();
        foreach (var offset in pattern.Offsets)
        {
            var position = origin.Position + new Vector2(offset.X, offset.Y) * tileSize;
            args.EntityManager.SpawnAttachedTo(pattern.Prototype,
                new EntityCoordinates(origin.EntityId, position));
        }
    }

    private (EntProtoId Prototype, Vector2i[] Offsets) GetPattern() //spellward
    {
        return FireTier switch
        {
            MedievalGroundFireTier.T1 => (T1Prototype, T1Offsets),
            MedievalGroundFireTier.T3 => (T3Prototype, T3Offsets),
            _ => throw new ArgumentOutOfRangeException(nameof(FireTier)),
        };
    }
}
