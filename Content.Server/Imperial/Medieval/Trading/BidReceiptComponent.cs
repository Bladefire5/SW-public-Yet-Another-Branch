using Content.Shared.Imperial.Medieval.Trading;
using Content.Shared.Store;
using Robust.Shared.Prototypes;

namespace Content.Server.Imperial.Medieval.Trading;

[RegisterComponent]
public sealed partial class BidReceiptComponent : Component
{
    public Guid OfferId;

    public EntityUid Pit;

    public string LotName = string.Empty;

    public int Amount;

    public ProtoId<CurrencyPrototype> Currency = "Revent";

    public BidReceiptStatus Status;
}
