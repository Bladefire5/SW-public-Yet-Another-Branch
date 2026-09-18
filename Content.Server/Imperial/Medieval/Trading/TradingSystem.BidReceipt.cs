using System.Linq;
using Content.Shared.Examine;
using Content.Shared.FixedPoint;
using Content.Shared.Imperial.Medieval.Trading;
using Content.Shared.Interaction.Events;
using Content.Shared.Popups;
using Content.Shared.Stacks;
using Content.Shared.Store;
using Robust.Shared.Player;

namespace Content.Server.Imperial.Medieval.Trading;

public sealed partial class TradingSystem
{
    [Dependency] private readonly SharedAppearanceSystem _appearance = default!;

    private void InitializeBidReceipts()
    {
        SubscribeLocalEvent<TradingComponent, TradingCreateBidReceiptMessage>(OnCreateBidReceipt);
        SubscribeLocalEvent<BidReceiptComponent, UseInHandEvent>(OnBidReceiptUse);
        SubscribeLocalEvent<BidReceiptComponent, ExaminedEvent>(OnBidReceiptExamined);
        SubscribeLocalEvent<BidReceiptComponent, EntityTerminatingEvent>(OnBidReceiptTerminating);
    }

    private void OnCreateBidReceipt(
        Entity<TradingComponent> pit,
        ref TradingCreateBidReceiptMessage args)
    {
        if (!IsTradingPitOwner(args.Actor, pit.Comp) ||
            !TryGetMarket(out var market) ||
            !market.Comp.Offers.TryGetValue(args.OfferId, out var offer) ||
            offer.Pit != pit.Owner ||
            offer.Side != TradingOfferSide.Sell ||
            offer.ParticipantKind != TradingParticipantKind.Trader ||
            offer.Price <= 0)
        {
            return;
        }

        var remaining = offer.Price - GetPendingBidReceiptAmount(offer.Id);
        if (args.Amount <= 0 || args.Amount > remaining)
            return;

        var receiptUid = Spawn(pit.Comp.BidReceiptPrototype, Transform(args.Actor).Coordinates);
        if (!TryComp<BidReceiptComponent>(receiptUid, out var receipt))
        {
            QueueDel(receiptUid);
            return;
        }

        receipt.OfferId = offer.Id;
        receipt.Pit = pit.Owner;
        receipt.LotName = GetOfferItemName(offer);
        receipt.Amount = args.Amount;
        receipt.Currency = pit.Comp.Currency;
        receipt.Status = BidReceiptStatus.Pending;
        SetBidReceiptAppearance(receiptUid, receipt.Status);
        _delivery.Deliver(receiptUid, args.Actor);
        UpdatePitInterfaces(pit.Owner, pit.Comp);
    }

    private void OnBidReceiptUse(Entity<BidReceiptComponent> receipt, ref UseInHandEvent args)
    {
        if (args.Handled || receipt.Comp.Status == BidReceiptStatus.Pending)
            return;

        if (receipt.Comp.Status == BidReceiptStatus.Failed)
        {
            receipt.Comp.Status = BidReceiptStatus.Redeemed;
            args.Handled = true;
            QueueDel(receipt.Owner);
            return;
        }

        if (receipt.Comp.Status != BidReceiptStatus.Succeeded ||
            receipt.Comp.Amount <= 0 ||
            !_prototypeManager.TryIndex(receipt.Comp.Currency, out CurrencyPrototype? currency) ||
            currency.Cash == null ||
            !currency.CanWithdraw)
        {
            return;
        }

        receipt.Comp.Status = BidReceiptStatus.Redeemed;
        SetBidReceiptAppearance(receipt.Owner, receipt.Comp.Status);
        args.Handled = true;

        FixedPoint2 amountRemaining = receipt.Comp.Amount;
        var coordinates = Transform(args.User).Coordinates;
        foreach (var value in currency.Cash.Keys.OrderByDescending(value => value))
        {
            var amountToSpawn = (int) MathF.Floor((float) (amountRemaining / value));
            var entities = _stack.SpawnMultiple(currency.Cash[value], amountToSpawn, coordinates);
            foreach (var entity in entities)
            {
                _delivery.Deliver(entity, args.User);
            }

            amountRemaining -= value * amountToSpawn;
        }

        QueueDel(receipt.Owner);
    }

    private void OnBidReceiptExamined(Entity<BidReceiptComponent> receipt, ref ExaminedEvent args)
    {
        if (receipt.Comp.Status == BidReceiptStatus.Failed)
        {
            args.PushMarkup(Loc.GetString(
                "trading-bid-receipt-examine-failed",
                ("lot", receipt.Comp.LotName)));
            return;
        }

        if (receipt.Comp.Status is not (BidReceiptStatus.Pending or BidReceiptStatus.Succeeded))
            return;

        args.PushMarkup(Loc.GetString(
            "trading-bid-receipt-examine-lot",
            ("lot", receipt.Comp.LotName)));
        args.PushMarkup(Loc.GetString(
            "trading-bid-receipt-examine-share",
            ("amount", receipt.Comp.Amount)));
    }

    private void OnBidReceiptTerminating(
        Entity<BidReceiptComponent> receipt,
        ref EntityTerminatingEvent args)
    {
        if (receipt.Comp.Status != BidReceiptStatus.Pending)
            return;

        receipt.Comp.Status = BidReceiptStatus.Redeemed;
        if (TryComp<TradingComponent>(receipt.Comp.Pit, out var pit))
            UpdatePitInterfaces(receipt.Comp.Pit, pit);
    }

    private int CompleteBidReceipts(TradingMarketOffer offer)
    {
        if (offer.Side != TradingOfferSide.Sell)
            return 0;

        var total = 0L;
        var query = EntityQueryEnumerator<BidReceiptComponent>();
        while (query.MoveNext(out var uid, out var receipt))
        {
            if (receipt.OfferId != offer.Id ||
                receipt.Status != BidReceiptStatus.Pending ||
                TerminatingOrDeleted(uid) ||
                EntityManager.IsQueuedForDeletion(uid))
            {
                continue;
            }

            if (receipt.Amount <= 0 || receipt.Amount > offer.Price - total)
            {
                ChangeBidReceiptStatus(uid, receipt, BidReceiptStatus.Failed);
                continue;
            }

            ChangeBidReceiptStatus(uid, receipt, BidReceiptStatus.Succeeded);
            total += receipt.Amount;
        }

        return (int) total;
    }

    private void FailBidReceipts(TradingMarketOffer offer)
    {
        if (offer.Side != TradingOfferSide.Sell)
            return;

        var query = EntityQueryEnumerator<BidReceiptComponent>();
        while (query.MoveNext(out var uid, out var receipt))
        {
            if (receipt.OfferId != offer.Id ||
                receipt.Status != BidReceiptStatus.Pending ||
                TerminatingOrDeleted(uid) ||
                EntityManager.IsQueuedForDeletion(uid))
            {
                continue;
            }

            ChangeBidReceiptStatus(uid, receipt, BidReceiptStatus.Failed);
        }
    }

    private int GetPendingBidReceiptAmount(Guid offerId)
    {
        var total = 0L;
        var query = EntityQueryEnumerator<BidReceiptComponent>();
        while (query.MoveNext(out var uid, out var receipt))
        {
            if (receipt.OfferId != offerId ||
                receipt.Status != BidReceiptStatus.Pending ||
                receipt.Amount <= 0 ||
                TerminatingOrDeleted(uid) ||
                EntityManager.IsQueuedForDeletion(uid))
            {
                continue;
            }

            total += receipt.Amount;
        }

        return (int) Math.Min(int.MaxValue, total);
    }

    private string GetOfferItemName(TradingMarketOffer offer)
    {
        if (offer.Item is not { } item || !Exists(item))
            return offer.ListedItemName;

        var stackCount = TryComp<StackComponent>(item, out var stack) ? stack.Count : (int?) null;
        return FormatStackName(MetaData(item).EntityName, stackCount);
    }

    private void SetBidReceiptAppearance(EntityUid uid, BidReceiptStatus status)
    {
        _appearance.SetData(uid, BidReceiptVisuals.Status, status);
    }

    private void ChangeBidReceiptStatus(
        EntityUid uid,
        BidReceiptComponent receipt,
        BidReceiptStatus status)
    {
        if (receipt.Status == status)
            return;

        receipt.Status = status;
        SetBidReceiptAppearance(uid, status);

        if (status is not (BidReceiptStatus.Succeeded or BidReceiptStatus.Failed))
            return;

        if (TryComp<TradingComponent>(receipt.Pit, out var trading))
        {
            var sound = status == BidReceiptStatus.Succeeded
                ? trading.BuySuccessSound
                : trading.BidReceiptFailureSound;
            _audio.PlayPvs(sound, uid);
        }

        if (!TryGetBidReceiptHolder(uid, out var holder))
            return;

        var popup = status == BidReceiptStatus.Succeeded
            ? "trading-bid-receipt-succeeded-popup"
            : "trading-bid-receipt-failed-popup";
        var popupType = status == BidReceiptStatus.Succeeded
            ? PopupType.Medium
            : PopupType.MediumCaution;
        _popup.PopupEntity(
            Loc.GetString(popup, ("lot", receipt.LotName)),
            holder,
            holder,
            popupType);
    }

    private bool TryGetBidReceiptHolder(EntityUid uid, out EntityUid holder)
    {
        var current = uid;
        while (_containers.TryGetContainingContainer(current, out var container))
        {
            current = container.Owner;
            if (!HasComp<ActorComponent>(current))
                continue;

            holder = current;
            return true;
        }

        holder = default;
        return false;
    }

    private void UpdatePitInterfaces(EntityUid pitUid, TradingComponent pit)
    {
        foreach (var user in _ui.GetActors(pitUid, TradingUiKey.Key))
        {
            UpdateUserInterface(user, pitUid, pit);
        }
    }
}
