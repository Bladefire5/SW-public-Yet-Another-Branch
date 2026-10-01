using Robust.Shared.GameStates;

namespace Content.Shared.Imperial.Medieval.Poise;


[RegisterComponent, NetworkedComponent, AutoGenerateComponentState, AutoGenerateComponentPause]
public sealed partial class PoiseComponent : Component
{

    // Текущий накопленный урон по стойке.
    [DataField, AutoNetworkedField]
    public int PoiseDamage;

    // Количество урона по стойке, необходимое для её пробития.
    [DataField, AutoNetworkedField]
    public int PoiseThreshold = 50;

    // Количество урона по стойке, восстанавливаемое за один тик восстановления.
    [DataField, AutoNetworkedField]
    public int PoiseRecoveryPerTick = 5;

    // Доля стойки, восстанавливаемая после выхода из состояния пробитой стойки.
    [DataField, AutoNetworkedField]
    public float PoiseBreakRecoveryFraction = 0.5f;

    // Снижение входящего урона по здоровью, пока стойка активна.
    [DataField, AutoNetworkedField]
    public float HealthDamageReduction = 0.5f;

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

}
