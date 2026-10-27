using System.Collections.Generic;
using System.Numerics;
using Robust.Shared.Prototypes;

namespace Content.Server.MagicBarrier.Components
{
    public enum MagicBarrierRiftState : byte
    {
        Ready,
        Active,
        Resetting,
        Completed,
    }

    [RegisterComponent]
    public sealed partial class MagicBarrierRiftComponent : Component
    {
        [DataField]
        public string Element = "earth";

        [DataField]
        public bool GuardiansSpawned;

        [DataField]
        public List<EntityUid> Guardians = new();

        public int RemainingGuardians;

        public MagicBarrierRiftState State;

        [DataField()]
        public List<EntProtoId> GuardianEntities = new()
        {
            "MedievalMobSkeletMeat",
            "MedievalMobSkeletMeat",
            "MedievalMobSkeletMeat",
            "MedievalMobSkeletMeat",
        };

        [DataField]
        public List<Vector2> GuardianOffsets = new()
        {
            new(1f, 1f),
            new(-1f, 1f),
            new(1f, -1f),
            new(-1f, -1f),
        };

        [DataField]
        public EntityUid? Spawner;

        public bool DestroyedLegitimately;
    }
}
