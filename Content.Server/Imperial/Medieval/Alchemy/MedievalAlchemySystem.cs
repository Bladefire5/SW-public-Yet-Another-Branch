using System.Collections.Generic;
using Content.Shared.Chemistry.Components.SolutionManager;
using Content.Shared.Chemistry;
using Content.Shared.Chemistry.Components;
using Content.Shared.Chemistry.EntitySystems;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Imperial.Medieval.Alchemy;
using Content.Shared.Interaction;
using Content.Shared.Tag;
using Robust.Server.Containers;
using Robust.Server.GameObjects;
using Robust.Shared.Containers;
using Robust.Shared.Prototypes;
using Content.Shared.FixedPoint;
using Content.Shared.Labels.EntitySystems;
using Content.Shared.Containers.ItemSlots;
using Content.Server.Storage.EntitySystems;
using Content.Shared.Storage;
using Content.Shared.Chemistry.Reagent;

namespace Content.Server.Imperial.Medieval.Alchemy;

public sealed class MedievalAlchemySystem : EntitySystem
{
    [Dependency] private readonly ContainerSystem _containers = default!;
    [Dependency] private readonly SharedSolutionContainerSystem _solutions = default!;
    [Dependency] private readonly SharedHandsSystem _hands = default!;
    [Dependency] private readonly UserInterfaceSystem _ui = default!;
    [Dependency] private readonly TagSystem _tags = default!;
    [Dependency] private readonly TransformSystem _transform = default!;
    [Dependency] private readonly LabelSystem _labelSystem = default!;
    [Dependency] private readonly ItemSlotsSystem _itemSlotsSystem = default!;
    [Dependency] private readonly StorageSystem _storageSystem = default!;

    private static readonly ProtoId<TagPrototype> BottleTag = "Bottle";

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<MedievalAlchemyComponent, ComponentInit>(OnComponentInit);
        SubscribeLocalEvent<MedievalAlchemyComponent, InteractUsingEvent>(OnInteractUsing);
        SubscribeLocalEvent<MedievalAlchemyComponent, BoundUIOpenedEvent>(OnUiOpened);
        SubscribeLocalEvent<MedievalAlchemyComponent, EntInsertedIntoContainerMessage>(OnContainerInserted);
        SubscribeLocalEvent<MedievalAlchemyComponent, EntRemovedFromContainerMessage>(OnContainerRemoved);
        SubscribeLocalEvent<MedievalAlchemyComponent, SolutionContainerChangedEvent>(OnSolutionChanged);
        SubscribeLocalEvent<MedievalAlchemyTrackedContainerComponent, SolutionContainerChangedEvent>(OnTrackedSolutionChanged);
        SubscribeLocalEvent<MedievalAlchemyComponent, MedievalAlchemyEjectBottleMessage>(OnEjectBottle);
        SubscribeLocalEvent<MedievalAlchemyComponent, MedievalAlchemyFillBottlesMessage>(OnFillBottles);
        SubscribeLocalEvent<MedievalAlchemyComponent, MedievalAlchemyFillNutrimentMessage>(OnFillNutriment);
        SubscribeLocalEvent<MedievalAlchemyComponent, ChemMasterSetModeMessage>(OnSetMode);
        SubscribeLocalEvent<MedievalAlchemyComponent, ChemMasterSortingTypeCycleMessage>(OnCycleSortingType);
        SubscribeLocalEvent<MedievalAlchemyComponent, ChemMasterReagentAmountButtonMessage>(OnReagentAmountButton);
        SubscribeLocalEvent<MedievalAlchemyComponent, MedievalAlchemyCreateBolusesMessage>(OnCreateBoluses);
    }

    private void OnComponentInit(
        Entity<MedievalAlchemyComponent> ent,
        ref ComponentInit args)
    {
        _containers.EnsureContainer<Container>(
            ent.Owner,
            MedievalAlchemyComponent.BottleContainerId);
    }

    private Container? GetBottleContainer(EntityUid uid)
    {
        if (!_containers.TryGetContainer(
                uid,
                MedievalAlchemyComponent.BottleContainerId,
                out var container))
        {
            return null;
        }

        return container as Container;
    }

    private void OnInteractUsing(
        Entity<MedievalAlchemyComponent> ent,
        ref InteractUsingEvent args)
    {
        if (args.Handled)
            return;

        if (TryInsertBottle(ent.Owner, args.User, args.Used))
            args.Handled = true;
    }

    private bool TryInsertBottle(
        EntityUid table,
        EntityUid user,
        EntityUid bottle)
    {
        if (!_tags.HasTag(bottle, BottleTag))
            return false;

        if (!_solutions.TryGetSolution(
                bottle,
                SharedChemMaster.BottleSolutionName,
                out _,
                out _))
        {
            return false;
        }

        var container = GetBottleContainer(table);

        if (container == null)
            return false;

        if (container.ContainedEntities.Count >=
            MedievalAlchemyComponent.MaxBottles)
        {
            return false;
        }

        if (!_containers.CanInsert(bottle, container))
            return false;

        if (!_hands.TryDrop(user, bottle))
            return false;

        if (!_containers.Insert(bottle, container))
        {
            _hands.TryPickupAnyHand(user, bottle);
            return false;
        }

        RefreshUi(table);
        return true;
    }

    private void OnEjectBottle(Entity<MedievalAlchemyComponent> ent, ref MedievalAlchemyEjectBottleMessage args)
    {
        var container = GetBottleContainer(ent.Owner);

        if (container == null)
            return;

        var bottle = GetEntity(args.Bottle);

        if (!container.Contains(bottle))
            return;

        if (!_hands.TryPickupAnyHand(args.Actor, bottle))
        {
            if (!_containers.Remove(bottle, container))
                return;

            _transform.AttachToGridOrMap(bottle);
        }

        RefreshUi(ent.Owner);
    }

    private void OnCreateBoluses(Entity<MedievalAlchemyComponent> ent, ref MedievalAlchemyCreateBolusesMessage msg)
    {
        if (msg.Count == 0 || msg.Count > 10 || msg.PotionDosage == 0 || msg.PotionDosage > 1000 || msg.Appearance > 4)
            return;

        if (msg.Label == null || msg.Label.Length > SharedChemMaster.LabelMaxLength)
            return;

        var output = _itemSlotsSystem.GetItemOrNull(ent.Owner, SharedChemMaster.OutputSlotName);

        if (output == null || !TryComp(output.Value, out StorageComponent? storage))
            return;

        var availableSlots = storage.Grid.GetArea() - _storageSystem.GetCumulativeItemAreas((output.Value, storage));

        if (availableSlots < (int)msg.Count)
            return;

        if (!_solutions.TryGetSolution(ent.Owner, SharedChemMaster.BufferSolutionName, out var bufferEntity, out var bufferSolution))
            return;

        if (!_solutions.TryGetSolution(ent.Owner, MedievalAlchemyComponent.NutrimentSolution, out var nutrimentEntity, out var nutrimentSolution))
            return;

        var chemicalPerBolus = FixedPoint2.FromCents((int)msg.PotionDosage);
        var nutrimentPerBolus = chemicalPerBolus;
        var totalPerBolus = chemicalPerBolus + nutrimentPerBolus;
        var totalRequired = FixedPoint2.FromCents((int)msg.PotionDosage * (int)msg.Count);


        var nutrimentId = new ReagentId("Nutriment", null);
        var chemicalSource = new Solution();

        foreach (var reagent in bufferSolution.Contents)
        {
            if (reagent.Reagent.Equals(nutrimentId))
                continue;

            chemicalSource.AddReagent(reagent.Reagent, reagent.Quantity);
        }

        if (!nutrimentSolution.TryGetReagentQuantity(nutrimentId, out var availableNutriment))
            return;

        if (chemicalSource.Volume < totalRequired || availableNutriment < totalRequired)
            return;

        var created = new List<EntityUid>();

        for (var i = 0; i < msg.Count; i++)
        {
            var bolus = Spawn("MedievalBolus", Transform(output.Value).Coordinates);

            if (!_solutions.TryGetSolution(bolus, SharedChemMaster.PillSolutionName, out _, out var bolusSolution) || bolusSolution.AvailableVolume < totalPerBolus ||
               !_storageSystem.Insert(output.Value, bolus, out _, user: msg.Actor, storageComp: storage))
            {
                QueueDel(bolus);

                foreach (var item in created)
                    QueueDel(item);

                return;
            }

            created.Add(bolus);
        }

        var chemicals = chemicalSource.SplitSolution(totalRequired);

        foreach (var reagent in chemicals.Contents)
        {
            bufferSolution.RemoveReagent(reagent.Reagent, reagent.Quantity, preserveOrder: true);
        }

        var nutriment = _solutions.SplitSolution(nutrimentEntity.Value, totalRequired);

        var chemicalBackup = new Solution();
        var nutrimentBackup = new Solution();

        foreach (var reagent in chemicals.Contents)
            chemicalBackup.AddReagent(reagent.Reagent, reagent.Quantity);

        foreach (var reagent in nutriment.Contents)
            nutrimentBackup.AddReagent(reagent.Reagent, reagent.Quantity);

        foreach (var bolus in created)
        {
            var dose = chemicals.SplitSolution(chemicalPerBolus);
            var nutrientDose = nutriment.SplitSolution(nutrimentPerBolus);

            foreach (var reagent in nutrientDose.Contents)
                dose.AddReagent(reagent.Reagent, reagent.Quantity);

            if (!_solutions.TryGetSolution(bolus, SharedChemMaster.PillSolutionName, out var bolusSolutionEntity, out _) ||
                !_solutions.TryAddSolution(bolusSolutionEntity.Value, dose))
            {
                foreach (var item in created)
                    QueueDel(item);

                foreach (var reagent in chemicalBackup.Contents)
                    bufferSolution.AddReagent(reagent.Reagent, reagent.Quantity);

                foreach (var reagent in nutrimentBackup.Contents)
                    nutrimentSolution.AddReagent(reagent.Reagent, reagent.Quantity);

                _solutions.UpdateChemicals(bufferEntity.Value, needsReactionsProcessing: false);

                _solutions.UpdateChemicals(nutrimentEntity.Value, needsReactionsProcessing: false);

                RefreshUi(ent.Owner);
                return;
            }

            var pill = EnsureComp<PillComponent>(bolus);
            pill.PillType = msg.Appearance;
            Dirty(bolus, pill);
        }

        _solutions.UpdateChemicals(bufferEntity.Value, needsReactionsProcessing: false);

        if (!string.IsNullOrWhiteSpace(msg.Label)) _labelSystem.Label(output.Value, msg.Label);

        RefreshUi(ent.Owner);
    }

    private void OnFillBottles(Entity<MedievalAlchemyComponent> ent, ref MedievalAlchemyFillBottlesMessage msg)
    {
        var container = GetBottleContainer(ent.Owner);

        if (container == null)
            return;

        if (msg.Bottles == null || msg.Bottles.Length == 0 || msg.Bottles.Length > MedievalAlchemyComponent.MaxBottles)
            return;

        // Dosages are transmitted in hundredths of a unit.
        // 1 = 0.01u, 125 = 1.25u, 500 = 5.00u.
        const uint maxDosageCents = 20000;

        if (msg.PotionDosage == 0 || msg.PotionDosage > maxDosageCents || msg.WaterDosage > maxDosageCents)
            return;

        var totalDosageCents = msg.PotionDosage + msg.WaterDosage;

        if (totalDosageCents > maxDosageCents)
            return;

        if (msg.Label == null || msg.Label.Length > SharedChemMaster.LabelMaxLength)
            return;

        var requestedVolume = FixedPoint2.FromCents((int)totalDosageCents);

        var validatedBottles = new HashSet<EntityUid>();

        foreach (var netBottle in msg.Bottles)
        {
            var bottle = GetEntity(netBottle);

            if (!validatedBottles.Add(bottle))
                return;

            if (!container.Contains(bottle))
                return;

            if (!_solutions.TryGetSolution(bottle, SharedChemMaster.BottleSolutionName, out _, out var solution))
                return;

            if (solution.AvailableVolume < requestedVolume)
                return;
        }

        var inputBeaker = _itemSlotsSystem.GetItemOrNull(ent.Owner, SharedChemMaster.InputSlotName);

        if (inputBeaker == null)
            return;

        if (!_solutions.TryGetFitsInDispenser(inputBeaker.Value, out var inputSolutionEntity, out var inputSolution))
            return;


        if (!_solutions.TryGetSolution(ent.Owner, MedievalAlchemyComponent.WaterSolution, out var waterEntity, out var waterSolution))
            return;

        var bottleCount = validatedBottles.Count;


        var potionPerBottle = FixedPoint2.FromCents((int)msg.PotionDosage);
        var waterPerBottle = FixedPoint2.FromCents((int)msg.WaterDosage);

        var totalPotionRequired = FixedPoint2.FromCents((int)msg.PotionDosage * bottleCount);
        var totalWaterRequired = FixedPoint2.FromCents((int)msg.WaterDosage * bottleCount);

        if (inputSolution.Volume < totalPotionRequired || waterSolution.Volume < totalWaterRequired)
            return;

        foreach (var bottle in validatedBottles)
        {
            if (!_solutions.TryGetSolution(bottle, SharedChemMaster.BottleSolutionName, out var bottleSolutionEntity, out _))
                return;

            // Assemble the complete dose before adding it.
            // This avoids a separate potion and water addition
            // triggering intermediate reactions inside the bottle.
            var combinedDose = new Solution();

            Solution? potionPortion = null;
            Solution? waterPortion = null;

            if (potionPerBottle > 0)
            {
                potionPortion = _solutions.SplitSolution(inputSolutionEntity.Value, potionPerBottle);

                foreach (var reagent in potionPortion.Contents)
                {
                    combinedDose.AddReagent(reagent.Reagent, reagent.Quantity);
                }
            }
            if (waterPerBottle > 0)
            {
                waterPortion = _solutions.SplitSolution(waterEntity.Value, waterPerBottle);

                foreach (var reagent in waterPortion.Contents)
                {
                    combinedDose.AddReagent(reagent.Reagent, reagent.Quantity);
                }
            }
            if (!_solutions.TryAddSolution(bottleSolutionEntity.Value, combinedDose))
            {
                // Restore this bottle's withdrawn ingredients
                // if the insertion unexpectedly fails.
                if (potionPortion != null)
                    _solutions.TryAddSolution(inputSolutionEntity.Value, potionPortion);

                if (waterPortion != null)
                    _solutions.TryAddSolution(waterEntity.Value, waterPortion);

                RefreshUi(ent.Owner);
                return;
            }
            if (!string.IsNullOrWhiteSpace(msg.Label))
            {
                _labelSystem.Label(bottle, msg.Label);
            }

        }

        RefreshUi(ent.Owner);
    }

    private void OnFillNutriment(Entity<MedievalAlchemyComponent> ent, ref MedievalAlchemyFillNutrimentMessage msg)
    {
        if (!_solutions.TryGetSolution(ent.Owner, SharedChemMaster.BufferSolutionName, out var bufferEntity, out var bufferSolution))
            return;

        if (!_solutions.TryGetSolution(ent.Owner, MedievalAlchemyComponent.NutrimentSolution, out var nutrimentEntity, out var nutrimentSolution))
            return;

        var nutriment = new ReagentId("Nutriment", null);

        if (!bufferSolution.TryGetReagentQuantity(nutriment, out var available))
            return;

        var amount = FixedPoint2.Min(available, nutrimentSolution.AvailableVolume);

        if (amount <= FixedPoint2.Zero)
            return;

        var removed = bufferSolution.RemoveReagent(nutriment, amount, preserveOrder: true);

        if (removed <= FixedPoint2.Zero)
            return;

        _solutions.TryAddReagent(nutrimentEntity.Value, nutriment, removed, out var accepted);

        if (accepted < removed) bufferSolution.AddReagent(nutriment, removed - accepted);

        _solutions.UpdateChemicals(bufferEntity.Value, needsReactionsProcessing: false);

        RefreshUi(ent.Owner);
    }

    private void OnUiOpened(
        Entity<MedievalAlchemyComponent> ent,
        ref BoundUIOpenedEvent args)
    {
        RefreshUi(ent.Owner);
    }

    private void OnContainerInserted(Entity<MedievalAlchemyComponent> ent, ref EntInsertedIntoContainerMessage args)
    {
        var containerId = args.Container.ID;

        if (containerId == SharedChemMaster.OutputSlotName)
        {
            RefreshUi(ent.Owner);
            return;
        }

        if (containerId != SharedChemMaster.InputSlotName && containerId != MedievalAlchemyComponent.BottleContainerId)
            return;

        var tracked = EnsureComp<MedievalAlchemyTrackedContainerComponent>(args.Entity);

        tracked.Table = ent.Owner;

        RefreshUi(ent.Owner);
    }

    private void OnContainerRemoved(Entity<MedievalAlchemyComponent> ent, ref EntRemovedFromContainerMessage args)
    {
        var containerId = args.Container.ID;

        if (containerId == SharedChemMaster.OutputSlotName)
        {
            RefreshUi(ent.Owner);
            return;
        }

        if (containerId != SharedChemMaster.InputSlotName && containerId != MedievalAlchemyComponent.BottleContainerId)
            return;

        RemComp<MedievalAlchemyTrackedContainerComponent>(args.Entity);

        RefreshUi(ent.Owner);
    }

    private void OnSolutionChanged(Entity<MedievalAlchemyComponent> ent, ref SolutionContainerChangedEvent args)
    {
        RefreshUi(ent.Owner);
    }


    private void OnTrackedSolutionChanged(Entity<MedievalAlchemyTrackedContainerComponent> ent, ref SolutionContainerChangedEvent args)
    {
        var table = ent.Comp.Table;

        if (!HasComp<MedievalAlchemyComponent>(table))
            return;

        if (_itemSlotsSystem.GetItemOrNull(table, SharedChemMaster.InputSlotName) == ent.Owner)
        {
            if (TryComp(ent.Owner, out FitsInDispenserComponent? fits) && args.SolutionId == fits.Solution)
            {
                RefreshUi(table);
            }
            return;
        }

        if (args.SolutionId != SharedChemMaster.BottleSolutionName)
            return;

        var container = GetBottleContainer(table);

        if (container == null || !container.Contains(ent.Owner))
            return;

        RefreshUi(table);
    }

    private void OnSetMode(Entity<MedievalAlchemyComponent> ent, ref ChemMasterSetModeMessage args)
    {
        if (!Enum.IsDefined(args.ChemMasterMode))
            return;

        ent.Comp.Mode = args.ChemMasterMode;

        RefreshUi(ent.Owner);
    }

    private void OnCycleSortingType(Entity<MedievalAlchemyComponent> ent, ref ChemMasterSortingTypeCycleMessage args)
    {
        ent.Comp.SortingType++;

        if (ent.Comp.SortingType > ChemMasterSortingType.Latest)
            ent.Comp.SortingType = ChemMasterSortingType.None;

        RefreshUi(ent.Owner);
    }


    private void OnReagentAmountButton(Entity<MedievalAlchemyComponent> ent, ref ChemMasterReagentAmountButtonMessage args)
    {
        if (!Enum.IsDefined(args.Amount))
            return;

        if (ent.Comp.Mode == ChemMasterMode.Discard)
        {
            DiscardAlchemyReagents(ent.Owner, args);
            return;
        }

        if (ent.Comp.Mode != ChemMasterMode.Transfer)
            return;

        var inputBeaker = _itemSlotsSystem.GetItemOrNull(ent.Owner, SharedChemMaster.InputSlotName);

        if (inputBeaker == null)
            return;

        if (!_solutions.TryGetFitsInDispenser(inputBeaker.Value, out var inputSolutionEntity, out var inputSolution))
            return;

        if (!_solutions.TryGetSolution(ent.Owner, SharedChemMaster.BufferSolutionName, out var bufferEntity, out var bufferSolution))
            return;

        var amount = args.Amount.GetFixedPoint();

        if (args.FromBuffer)
        {
            if (!bufferSolution.TryGetReagentQuantity(args.ReagentId, out var available))
                return;

            amount = FixedPoint2.Min(amount, FixedPoint2.Min(available, inputSolution.AvailableVolume));

            if (amount <= FixedPoint2.Zero)
                return;

            var removed = bufferSolution.RemoveReagent(args.ReagentId, amount, preserveOrder: true);

            if (removed <= FixedPoint2.Zero)
                return;

            _solutions.TryAddReagent(inputSolutionEntity.Value, args.ReagentId, removed, out var accepted);

            if (accepted < removed)
            {
                bufferSolution.AddReagent(args.ReagentId, removed - accepted);
            }

            _solutions.UpdateChemicals(bufferEntity.Value, needsReactionsProcessing: false);
        }
        else
        {
            if (!inputSolution.TryGetReagentQuantity(args.ReagentId, out var available))
                return;

            amount = FixedPoint2.Min(amount, available);

            if (amount <= FixedPoint2.Zero)
                return;

            var removed = _solutions.RemoveReagent(inputSolutionEntity.Value, args.ReagentId, amount);

            if (removed <= FixedPoint2.Zero)
                return;

            var requiredVolume = bufferSolution.Volume + removed;

            if (bufferSolution.MaxVolume < requiredVolume)
                bufferSolution.MaxVolume = requiredVolume;

            bufferSolution.AddReagent(args.ReagentId, removed);

            _solutions.UpdateChemicals(bufferEntity.Value, needsReactionsProcessing: false);
        }

        RefreshUi(ent.Owner);
    }

    private void DiscardAlchemyReagents(EntityUid table, ChemMasterReagentAmountButtonMessage args)
    {
        var amount = args.Amount.GetFixedPoint();

        if (args.FromBuffer)
        {
            if (!_solutions.TryGetSolution(table, SharedChemMaster.BufferSolutionName, out var bufferEntity, out var bufferSolution))
                return;

            var removed = bufferSolution.RemoveReagent(args.ReagentId, amount, preserveOrder: true);

            if (removed <= FixedPoint2.Zero)
                return;

            // Update solution state without triggering reactions in storage.
            _solutions.UpdateChemicals(bufferEntity.Value, needsReactionsProcessing: false);
        }
        else
        {
            var inputBeaker = _itemSlotsSystem.GetItemOrNull(table, SharedChemMaster.InputSlotName);

            if (inputBeaker == null)
                return;

            if (!_solutions.TryGetFitsInDispenser(inputBeaker.Value, out var inputSolutionEntity, out _))
                return;

            var removed = _solutions.RemoveReagent(inputSolutionEntity.Value, args.ReagentId, amount);

            if (removed <= FixedPoint2.Zero)
                return;
        }

        RefreshUi(table);
    }

    private static void SynchronizeBottleSlots(MedievalAlchemyComponent component, Container container)
    {
        var slots = component.BottleSlots;

        for (var i = 0; i < MedievalAlchemyComponent.MaxBottles; i++)
        {
            var bottle = slots[i];

            if (bottle != null && !container.Contains(bottle.Value))
                slots[i] = null;
        }

        foreach (var bottle in container.ContainedEntities)
        {
            var alreadyAssigned = false;

            for (var i = 0; i < MedievalAlchemyComponent.MaxBottles; i++)
            {
                if (slots[i] != bottle)
                    continue;

                alreadyAssigned = true;
                break;
            }

            if (alreadyAssigned)
                continue;

            for (var i = 0; i < MedievalAlchemyComponent.MaxBottles; i++)
            {
                if (slots[i] != null)
                    continue;

                slots[i] = bottle;
                break;
            }
        }
    }


    private void RefreshUi(EntityUid table)
    {
        if (!TryComp<MedievalAlchemyComponent>(table, out var component))
        {
            return;
        }

        var container = GetBottleContainer(table);

        if (container == null)
            return;

        SynchronizeBottleSlots(component, container);

        var bottles = new List<MedievalAlchemyBottleInfo>();

        for (var slotIndex = 0; slotIndex < MedievalAlchemyComponent.MaxBottles; slotIndex++)
        {
            var bottle = component.BottleSlots[slotIndex];

            if (bottle == null)
                continue;

            if (!_solutions.TryGetSolution(bottle.Value, SharedChemMaster.BottleSolutionName, out _, out var solution))
            {
                continue;
            }

            var reagents = solution.Contents.ToArray();

            bottles.Add(new MedievalAlchemyBottleInfo(slotIndex, GetNetEntity(bottle.Value), Name(bottle.Value), solution.Volume, solution.MaxVolume, reagents));
        }

        MedievalAlchemyBolusBottleInfo? bolusBottleInfo = null;

        var output = _itemSlotsSystem.GetItemOrNull(table, SharedChemMaster.OutputSlotName);

        if (output != null && MetaData(output.Value).EntityPrototype?.ID == "MedievalBolusBottle" && TryComp<StorageComponent>(output.Value, out var storage))
        {
            bolusBottleInfo = new MedievalAlchemyBolusBottleInfo(GetNetEntity(output.Value), Name(output.Value), storage.StoredItems.Count, storage.Grid.GetArea());
        }

        var bufferVolume = FixedPoint2.Zero;
        var bufferCapacity = FixedPoint2.Zero;

        if (_solutions.TryGetSolution(table, SharedChemMaster.BufferSolutionName, out _, out var bufferSolution))
        {
            bufferVolume = bufferSolution.Volume;
            bufferCapacity = bufferSolution.MaxVolume;
        }

        var waterVolume = FixedPoint2.Zero;
        var waterCapacity = FixedPoint2.Zero;

        if (_solutions.TryGetSolution(table, MedievalAlchemyComponent.WaterSolution, out _, out var waterSolution))
        {
            waterVolume = waterSolution.Volume;
            waterCapacity = waterSolution.MaxVolume;
        }

        var nutrimentVolume = FixedPoint2.Zero;
        var nutrimentCapacity = FixedPoint2.Zero;

        if (_solutions.TryGetSolution(table, MedievalAlchemyComponent.NutrimentSolution, out _, out var nutrimentSolution))
        {
            nutrimentVolume = nutrimentSolution.Volume;
            nutrimentCapacity = nutrimentSolution.MaxVolume;
        }

        var inputVolume = FixedPoint2.Zero;
        var inputCapacity = FixedPoint2.Zero;

        ContainerInfo? inputContainerInfo = null;

        var inputBeaker = _itemSlotsSystem.GetItemOrNull(table, SharedChemMaster.InputSlotName);

        if (inputBeaker != null && _solutions.TryGetFitsInDispenser(inputBeaker.Value, out _, out var inputSolution))
        {
            inputVolume = inputSolution.Volume;
            inputCapacity = inputSolution.MaxVolume;

            inputContainerInfo = new ContainerInfo(Name(inputBeaker.Value), inputSolution.Volume, inputSolution.MaxVolume)
            {
                Reagents = inputSolution.Contents
            };
        }

        ChemMasterBoundUserInterfaceState? chemMasterState = null;

        if (_solutions.TryGetSolution(table, SharedChemMaster.BufferSolutionName, out _, out var chemBufferSolution))
        {
            chemMasterState = new ChemMasterBoundUserInterfaceState(component.Mode, component.SortingType, inputContainerInfo, null, chemBufferSolution.Contents, chemBufferSolution.Volume, 0, 0, false);
        }

        var state = new MedievalAlchemyUiState(
            bottles.ToArray(),
            MedievalAlchemyComponent.MaxBottles,
            bufferVolume,
            bufferCapacity,
            waterVolume,
            waterCapacity,
            nutrimentVolume,
            nutrimentCapacity,
            inputVolume,
            inputCapacity)
        {
            ChemMasterState = chemMasterState,
            BolusBottle = bolusBottleInfo
        };

        _ui.SetUiState(table, MedievalAlchemyUiKey.Key, state);

    }
}
