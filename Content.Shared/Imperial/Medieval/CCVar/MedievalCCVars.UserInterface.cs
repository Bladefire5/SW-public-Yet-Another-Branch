using Robust.Shared.Configuration;

namespace Content.Shared.Imperial.Medieval.CCVar;

public sealed partial class MedievalCCVars
{
    public static readonly CVarDef<float> UiRateLimitPeriod =
        CVarDef.Create("medieval.ui_rate_limit_period", 2f, CVar.SERVER);

    public static readonly CVarDef<int> UiRateLimitCount =
        CVarDef.Create("medieval.ui_rate_limit_count", 6, CVar.SERVER);

    public static readonly CVarDef<int> UiRateLimitAnnounceAdminsDelay =
        CVarDef.Create("medieval.ui_rate_limit_announce_admins_delay", 30, CVar.SERVER);
}
