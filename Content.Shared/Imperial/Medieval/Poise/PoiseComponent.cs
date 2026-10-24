using Robust.Shared.GameStates;
using Robust.Shared.Serialization;


namespace Content.Shared.Imperial.Medieval.Poise;


[RegisterComponent, NetworkedComponent, AutoGenerateComponentState, AutoGenerateComponentPause]
public sealed partial class PoiseComponent : Component
{
    // spellward start - opt-in NPC rules; player prototypes keep their existing behavior.
    [DataField, AutoNetworkedField]
    public bool Npc;

    // Separate from ordinary health resistance so explosion protection can be tuned independently.
    [DataField]
    public float ExplosionDamageReduction;
    // spellward end

    // Текущий накопленный урон по стойке.
    [DataField, AutoNetworkedField]
    public int PoiseDamage;

    // Количество урона по стойке, необходимое для её пробития.
    [DataField, AutoNetworkedField]
    public int PoiseThreshold = 50;

    // Дополнительная стойка от экипированной брони.
    [DataField, AutoNetworkedField]
    public int ArmorPoiseIncrease;

    // Количество урона по стойке, восстанавливаемое за один тик восстановления.
    [DataField, AutoNetworkedField]
    public int PoiseRecoveryPerTick = 5;

    // Доля стойки, восстанавливаемая после выхода из состояния пробитой стойки.
    [DataField, AutoNetworkedField]
    public float PoiseBreakRecoveryFraction = 0.5f;

    // Снижение входящего урона по здоровью, пока стойка активна.
    [DataField, AutoNetworkedField]
    public float HealthDamageReduction = 0.4f;

    [DataField, AutoNetworkedField]
    public float HealthDamageReductionCap = 0.8f;

    [DataField, AutoNetworkedField]
    public float UnarmoredHealthDamageReduction = 0.1f;

    // spellward start - optional health vulnerability while poise is broken.
    [DataField, AutoNetworkedField]
    public float BrokenHealthDamageMultiplier = 1f;
    // spellward end

    [DataField, AutoNetworkedField]
    public float PoiseMultiplier = 1.0f;

    [DataField, AutoNetworkedField]
    public bool HasPoiseArmor;

    // Указывает, пробита ли стойка в данный момент.
    [DataField, AutoNetworkedField]
    public bool PoiseBroken;

    // Задержка после получения урона по стойке перед началом обычного восстановления.
    [DataField]
    public TimeSpan PoiseRecoveryDelay = TimeSpan.FromSeconds(5f);

    // Интервал между тиками обычного восстановления стойки.
    [DataField]
    public TimeSpan PoiseRecoveryInterval = TimeSpan.FromSeconds(2f);

    // Время, в течение которого стойка остаётся пробитой.
    [DataField]
    public TimeSpan PoiseBreakDuration = TimeSpan.FromSeconds(10f);

    [AutoPausedField]
    public TimeSpan PoiseRecoveryStarts;
    [AutoPausedField]
    public TimeSpan NextPoiseRecoveryTick;

    [AutoPausedField, AutoNetworkedField]
    public TimeSpan PoiseBreakEnds;

    public bool ProtectBreakingHit;

}

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class PoiseArmorComponent : Component
{
    [DataField, AutoNetworkedField]
    public PoiseArmorClass ArmorClass = PoiseArmorClass.Light;

    [DataField, AutoNetworkedField]
    public float? MultiplierOverride;

    [DataField, AutoNetworkedField]
    public float? PoiseIncreaseOverride;
}

[Serializable, NetSerializable]
public enum PoiseArmorClass : byte
{
    Light,
    Medium,
    Heavy
}
