// spellward start - server-authoritative ice dagger lifespan and impact shatter.
using Content.Shared.Imperial.Medieval.Magic;
using Content.Shared.Throwing;
using Content.Shared.Weapons.Melee.Events;

namespace Content.Server.Imperial.Medieval.Magic;

public sealed class SummonedIceDaggerSystem : EntitySystem
{
    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<SummonedIceDaggerComponent, MeleeHitEvent>(OnMeleeHit);
        SubscribeLocalEvent<SummonedIceDaggerComponent, ThrowDoHitEvent>(OnThrownHit);
        SubscribeLocalEvent<SummonedIceDaggerComponent, LandEvent>(OnLand);
    }

    private void OnMeleeHit(EntityUid uid, SummonedIceDaggerComponent component, MeleeHitEvent args)
    {
        // A swing that misses, including one with no targets, does not spend a use.
        // A multi-target swing still counts as one hit.
        if (Deleted(uid) || !args.IsHit || args.HitEntities.Count == 0)
            return;

        component.HitsRemaining--;
        if (component.HitsRemaining <= 0)
            QueueDel(uid);
    }

    private void OnThrownHit(EntityUid uid, SummonedIceDaggerComponent component, ref ThrowDoHitEvent args)
    {
        // Queue deletion instead of immediate deletion so other impact handlers
        // can still apply DamageOtherOnHit during this same event dispatch.
        if (!Deleted(uid))
            QueueDel(uid);
    }

    private void OnLand(EntityUid uid, SummonedIceDaggerComponent component, ref LandEvent args)
    {
        // Landing without hitting an entity is still the end of this conjured weapon.
        if (!Deleted(uid))
            QueueDel(uid);
    }
}
// spellward end
