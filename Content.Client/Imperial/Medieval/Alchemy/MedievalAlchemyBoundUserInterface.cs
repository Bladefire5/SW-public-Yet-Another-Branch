using Content.Shared.Imperial.Medieval.Alchemy;
using JetBrains.Annotations;
using Robust.Client.GameObjects;
using Robust.Client.UserInterface;
using Content.Shared.Chemistry;
using Content.Shared.Containers.ItemSlots;
using Content.Shared.FixedPoint;

namespace Content.Client.Imperial.Medieval.Alchemy;

[UsedImplicitly]
public sealed class MedievalAlchemyBoundUserInterface : BoundUserInterface
{
    private MedievalAlchemyWindow? _window;

    public MedievalAlchemyBoundUserInterface(
        EntityUid owner,
        Enum uiKey) : base(owner, uiKey)
    {
    }

    protected override void Open()
    {
        base.Open();

        _window = this.CreateWindow<MedievalAlchemyWindow>();

        _window.Title = EntMan.GetComponent<MetaDataComponent>(Owner).EntityName;

        _window.InputEjectButton.OnPressed += _ => SendMessage(new ItemSlotButtonPressedEvent(SharedChemMaster.InputSlotName));

        _window.BufferTransferButton.OnPressed += _ => SendMessage(new ChemMasterSetModeMessage(ChemMasterMode.Transfer));

        _window.BufferDiscardButton.OnPressed += _ => SendMessage(new ChemMasterSetModeMessage(ChemMasterMode.Discard));

        _window.BufferSortButton.OnPressed += _ => SendMessage(new ChemMasterSortingTypeCycleMessage());

        _window.OnReagentButtonPressed += (_, button) => SendMessage(new ChemMasterReagentAmountButtonMessage(button.Id, button.Amount, button.IsBuffer));

        _window.OnAlchemyBottleEjectPressed += OnBottleEjectPressed;

        _window.OnBolusBottleEjectPressed += () =>
        {
            SendMessage(new ItemSlotButtonPressedEvent(SharedChemMaster.OutputSlotName));
        };

        _window.FillBottlesButton.OnPressed += _ =>
        {
            if (_window == null)
                return;

            var bottles = _window.GetSelectedAlchemyBottleEntities();

            if (bottles.Length == 0)
                return;

            if (!_window.TryGetBottleDosages(out var potionCents, out var waterCents))
                return;

            if (potionCents <= 0)
                return;

            if (potionCents + waterCents > 20000)
                return;

            var potionDosage = (uint)potionCents;
            var waterDosage = (uint)waterCents;

            var label = _window.BottleLabelLine.Text;

            SendMessage(new MedievalAlchemyFillBottlesMessage(bottles, potionDosage, waterDosage, label));
        };

        _window.FillNutrimentButton.OnPressed += _ =>
        {
            SendMessage(new MedievalAlchemyFillNutrimentMessage());
        };

        _window.CreateBolusButton.OnPressed += _ =>
        {
            if (_window == null)
                return;

            var count = _window.BolusCount.Value;

            if (count < 1 || count > 10)
                return;

            if (!_window.TryGetBolusDosage(out var potionCents))
                return;

            if (potionCents <= 0 || potionCents > 2000)
                return;

            var appearance = _window.SelectedBolusAppearance;

            if (appearance > 4)
                return;

            var label = _window.BolusBottleLabelLine.Text;

            SendMessage(new MedievalAlchemyCreateBolusesMessage((uint)count, (uint)potionCents, appearance, label));
        };
    }

    private void OnBottleEjectPressed(NetEntity bottle)
    {
        SendMessage(new MedievalAlchemyEjectBottleMessage(bottle));
    }

    protected override void UpdateState(BoundUserInterfaceState state)
    {
        base.UpdateState(state);

        if (state is not MedievalAlchemyUiState alchemyState || _window == null)
        {
            return;
        }
        if (alchemyState.ChemMasterState is { } chemMasterState)
        {
            _window.UpdateState(chemMasterState);
        }

        _window.UpdateAlchemyBottles(alchemyState);

        _window.FillNutrimentButton.Disabled = alchemyState.BufferVolume <= FixedPoint2.Zero || alchemyState.NutrimentVolume >= alchemyState.NutrimentCapacity;

        var entityName = EntMan.GetComponent<MetaDataComponent>(Owner).EntityName;

        _window.Title = $"{entityName} ({alchemyState.Bottles.Length}/{alchemyState.MaxBottles})";
    }
}