using Content.Server.Chat.Managers;
using Content.Server.Players.RateLimiting;
using Content.Shared.Imperial.Medieval.CCVar;
using Content.Shared.Players.RateLimiting;
using Robust.Shared.Player;

namespace Content.Server.Imperial.Medieval.UserInterface;

public sealed class MedievalUserInterfaceRateLimitSystem : EntitySystem
{
    [Dependency] private readonly PlayerRateLimitManager _rateLimit = default!;
    [Dependency] private readonly IChatManager _chatManager = default!;

    public void Register<TComponent>(
        string key,
        EntityEventRefHandler<TComponent, BoundUserInterfaceMessageAttempt>? validate = null)
        where TComponent : IComponent
    {
        _rateLimit.Register(key, new RateLimitRegistration(
            MedievalCCVars.UiRateLimitPeriod,
            MedievalCCVars.UiRateLimitCount,
            null,
            MedievalCCVars.UiRateLimitAnnounceAdminsDelay,
            player => _chatManager.SendAdminAlert(
                Loc.GetString("medieval-ui-rate-limit-admin-announcement", ("player", player.Name), ("key", key)))));

        SubscribeLocalEvent<TComponent, BoundUserInterfaceMessageAttempt>(
            (Entity<TComponent> ent, ref BoundUserInterfaceMessageAttempt args) =>
            {
                if (args.Cancelled || args.Message is CloseBoundInterfaceMessage)
                    return;

                validate?.Invoke(ent, ref args);
                OnUiMessageAttempt(key, args);
            });
    }

    private void OnUiMessageAttempt(string key, BoundUserInterfaceMessageAttempt args)
    {
        if (args.Cancelled)
            return;

        if (!TryComp<ActorComponent>(args.Actor, out var actor) ||
            _rateLimit.CountAction(actor.PlayerSession, key) != RateLimitStatus.Allowed)
        {
            args.Cancel();
        }
    }
}
