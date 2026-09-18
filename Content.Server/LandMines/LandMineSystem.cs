using Content.Shared.Armable;
using Content.Shared.Item.ItemToggle.Components;
using Content.Shared.LandMines;
using Content.Shared.Popups;
using Content.Shared.StepTrigger.Systems;
using Content.Shared.Trigger.Systems;
using Robust.Shared.Audio.Systems;
using Content.Server.Myrmex.Components;// imperial medieval
using Content.Shared.Imperial.Medieval.Myrmex; // imperial medieval - larvacomponent
namespace Content.Server.LandMines;

public sealed class LandMineSystem : EntitySystem
{
    [Dependency] private readonly SharedAudioSystem _audioSystem = default!;
    [Dependency] private readonly SharedPopupSystem _popupSystem = default!;
    [Dependency] private readonly TriggerSystem _trigger = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<LandMineComponent, StepTriggeredOnEvent>(HandleStepOnTriggered);
        SubscribeLocalEvent<LandMineComponent, StepTriggeredOffEvent>(HandleStepOffTriggered);
        SubscribeLocalEvent<LandMineComponent, StepTriggerAttemptEvent>(HandleStepTriggerAttempt);
    }

    /// <summary>
    /// Warns the player when stepped on.
    /// </summary>
    private void HandleStepOnTriggered(EntityUid uid, LandMineComponent component, ref StepTriggeredOnEvent args)
    {
        if (HasComp<MyrmexComponent>(args.Tripper) || HasComp<LarvaComponent>(args.Tripper)) return; // imperial medieval
        if (!string.IsNullOrEmpty(component.TriggerText))
          {
              _popupSystem.PopupCoordinates(
                  Loc.GetString(component.TriggerText, ("mine", uid)),
                  Transform(uid).Coordinates,
                  args.Tripper,
                  PopupType.LargeCaution);
          }
        _audioSystem.PlayPvs(component.Sound, uid);
    }

    /// <summary>
    /// Sends a trigger when stepped off.
    /// </summary>
    private void HandleStepOffTriggered(EntityUid uid, LandMineComponent component, ref StepTriggeredOffEvent args)
    {
        if (HasComp<MyrmexComponent>(args.Tripper) || HasComp<LarvaComponent>(args.Tripper)) return; // imperial medieval - safety net
        // TODO: Adjust to the new trigger system
        _trigger.Trigger(uid, args.Tripper, TriggerSystem.DefaultTriggerKey);
    }

    /// <summary>
    /// Presumes that the landmine isn't armable and should be treated as always armed.
    /// If Armable and ItemToggle is present the event will continue only if the mine is activated.
    /// </summary>
    private void HandleStepTriggerAttempt(EntityUid uid, LandMineComponent component, ref StepTriggerAttemptEvent args)
    {
        // imperial medieval - myrmex and their larvae never set off their own traps
        if (HasComp<MyrmexComponent>(args.Tripper) || HasComp<LarvaComponent>(args.Tripper))
        {
           args.Cancelled = true;
           return;
        }

        args.Continue = true;

        if (HasComp<ArmableComponent>(uid) && TryComp<ItemToggleComponent>(uid, out var itemToggle))
           args.Continue = itemToggle.Activated;
    }
}
