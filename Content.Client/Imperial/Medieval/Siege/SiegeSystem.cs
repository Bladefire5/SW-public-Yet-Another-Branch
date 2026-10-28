using Robust.Client.GameObjects;
using Content.Shared.Siege.Components;

namespace Content.Client.Imperial.Siege;

public sealed class SiegeSystem : EntitySystem
{
    [Dependency] private readonly SpriteSystem _sprite = default!;

    public override void Initialize()
    {
        SubscribeLocalEvent<SiegeWeaponComponent, AppearanceChangeEvent>(OnChangeAppearance);
    }

    public void OnChangeAppearance(EntityUid uid, SiegeWeaponComponent component, ref AppearanceChangeEvent args)
    {
        if (args.Sprite == null || string.IsNullOrEmpty(component.AnimationState))
            return;

        _sprite.LayerSetRsiState((uid, args.Sprite), CatapultVisualKey.Ready, component.AnimationState);
    }
}
