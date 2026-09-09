using Content.Shared.Damage;
using Content.Shared.Imperial.Medieval.Magic.Mana;
using Content.Shared.Inventory;
using Content.Shared.Mobs.Components;
using Content.Shared.Mobs;
using Robust.Shared.Timing;

namespace Content.Server.Imperial.Medieval.ArmorIntegrity;

public sealed class MedievalWearerHealingSystem : EntitySystem
{
    [Dependency] private readonly DamageableSystem _damageable = default!;
    [Dependency] private readonly IGameTiming _timing = default!;

    private TimeSpan _nextHeal;

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        if (_timing.CurTime < _nextHeal)
            return;

        _nextHeal = _timing.CurTime + TimeSpan.FromSeconds(1);
        var query = EntityQueryEnumerator<InventoryComponent, MobStateComponent, DamageableComponent>();
        while (query.MoveNext(out var uid, out var inventory, out var mob, out var damageable))
        {
            if (mob.CurrentState == MobState.Dead)
                continue;

            // Magical equipment only heals wearers above one quarter of their maximum mana.
            if (!TryComp<ManaComponent>(uid, out var mana) ||
                mana.MaxMana <= 0f || mana.Mana <= mana.MaxMana * 0.25f)
                continue;

            var slots = new InventorySystem.InventorySlotEnumerator(inventory, SlotFlags.HEAD | SlotFlags.OUTERCLOTHING);
            while (slots.MoveNext(out var slot))
            {
                if (slot.ContainedEntity is not { } item ||
                    !TryComp<MedievalWearerHealingComponent>(item, out var healing))
                    continue;

                _damageable.TryChangeDamage(uid, healing.Damage, true, false, damageable);
            }
        }
    }
}
