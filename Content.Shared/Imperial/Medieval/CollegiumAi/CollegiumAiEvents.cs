using Content.Shared.Actions;
using Robust.Shared.Serialization;

namespace Content.Shared.Imperial.Medieval.CollegiumAi;

/// <summary>
/// Opens the list of mages the watcher may jump to.
/// </summary>
public sealed partial class CollegiumAiTeleportActionEvent : InstantActionEvent;

/// <summary>
/// Strips the spells a mage has learned. Only valid on a mage of the watcher's own faction.
/// </summary>
public sealed partial class CollegiumAiStripMagicActionEvent : EntityTargetActionEvent;

/// <summary>
/// Speaks privately into one mage's head.
/// </summary>
public sealed partial class CollegiumAiWhisperActionEvent : EntityTargetActionEvent;

/// <summary>
/// Hands back the spells the Collegium took off a mage.
/// </summary>
public sealed partial class CollegiumAiRestoreMagicActionEvent : EntityTargetActionEvent;

/// <summary>
/// Sends the watcher back to its statue.
/// </summary>
public sealed partial class CollegiumAiReturnActionEvent : InstantActionEvent;

/// <summary>
/// Sends the watcher to the barrier the Collegium maintains.
/// </summary>
public sealed partial class CollegiumAiBarrierActionEvent : InstantActionEvent;

/// <summary>
/// One mage as shown in the watcher's jump list.
/// </summary>
[Serializable, NetSerializable]
public sealed class CollegiumAiMageEntry
{
    public NetEntity Entity;
    public string Name = string.Empty;
    public string Job = string.Empty;
    public bool Alive;
}

/// <summary>
/// Server -> client. The mages the watcher may jump to right now.
/// Sent on demand because most of them are well outside the watcher's PVS.
/// </summary>
[Serializable, NetSerializable]
public sealed class CollegiumAiMageListEvent : EntityEventArgs
{
    public List<CollegiumAiMageEntry> Mages = new();
}

/// <summary>
/// Client -> server. The watcher picked a mage to jump to.
/// </summary>
[Serializable, NetSerializable]
public sealed class CollegiumAiTeleportRequestEvent : EntityEventArgs
{
    public NetEntity Target;

    public CollegiumAiTeleportRequestEvent(NetEntity target)
    {
        Target = target;
    }
}
