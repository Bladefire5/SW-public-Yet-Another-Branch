using Robust.Shared.Serialization;

namespace Content.Shared.Imperial.Medieval.Trading;

[Serializable, NetSerializable]
public enum BidReceiptStatus : byte
{
    Pending,
    Succeeded,
    Failed,
    Redeemed,
}

[Serializable, NetSerializable]
public enum BidReceiptVisuals : byte
{
    Status,
}
