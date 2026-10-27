using Robust.Shared.Prototypes;
using Content.Shared.Imperial.ImperialStore;
using Content.Shared.Mind;
using Content.Shared.Roles;
using Content.Shared.Roles.Jobs;

namespace Content.Server.Imperial.ImperialStore;

/// <summary>
/// Allows a store entry to be filtered out based on the user's job.
/// Supports both blacklists and whitelists
/// </summary>
public sealed partial class ImperialBuyerJobCondition : ImperialListingCondition
{
    /// <summary>
    /// A whitelist of jobs prototypes that can purchase this listing. Only one needs to be found.
    /// </summary>
    [DataField("whitelist")]
    public HashSet<ProtoId<JobPrototype>>? Whitelist;

    /// <summary>
    /// A blacklist of job prototypes that can purchase this listing. Only one needs to be found.
    /// </summary>
    [DataField("blacklist")]
    public HashSet<ProtoId<JobPrototype>>? Blacklist;

    public override bool Condition(ImperialListingConditionArgs args)
    {
        var ent = args.EntityManager;
        var minds = ent.System<SharedMindSystem>();

        // this is for things like surplus crate
        if (!minds.TryGetMind(args.Buyer, out var mindId, out _))
            return true;

        var jobs = ent.System<SharedJobSystem>();
        jobs.MindTryGetJob(mindId, out var job);

        if (Blacklist != null)
        {
            if (job is not null && Blacklist.Contains(job.ID))
                return false;
        }

        if (Whitelist != null)
        {
            if (job == null || !Whitelist.Contains(job.ID))
                return false;
        }

        return true;
    }
}
