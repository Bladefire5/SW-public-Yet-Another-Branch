using Robust.Shared.Prototypes;

namespace Content.Server.CustomDoorKey.Components
{
    [RegisterComponent]
    public sealed partial class DoorHackableComponent : Component
    {
        [DataField]
        public int MinNumber = -5;

        [DataField]
        public int MaxNumber = 5;

        [DataField]
        public int NumberCount = 5;

        [DataField]
        public int LockPickProgress = 0;

        [DataField]
        public int[] Numbers = new int[100];

        [DataField(), ViewVariables(VVAccess.ReadOnly)]
        public EntProtoId EffectSoundOnNewLock = "/Audio/Imperial/Medieval/new_lock.ogg";
    }
}
