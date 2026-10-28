using Content.Shared.Imperial.Medieval.Poise.LightAttack;
using Robust.Client.GameObjects;
using Robust.Shared.Timing;

namespace Content.Client.Imperial.Medieval.Poise.LightAttack;

/// <summary>
/// Presents the same scheduled wind-up on the attacker and on observing clients.
/// Gameplay remains in the shared wind-up system and is validated by the server.
/// </summary>
public sealed class LightAttackWindupVisualSystem : EntitySystem
{
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly SpriteSystem _sprite = default!;

    public override void Initialize()
    {
        base.Initialize();
        UpdatesBefore.Add(typeof(SpriteSystem));
    }

    public override void FrameUpdate(float frameTime)
    {
        base.FrameUpdate(frameTime);
        var now = _timing.CurTime;
        var query = EntityQueryEnumerator<LightAttackWindupVisualComponent, SpriteComponent>();
        while (query.MoveNext(out var uid, out var visual, out var sprite))
        {
            if (Paused(uid))
                continue;

            var visible = visual.TryGetProgress(now, out var progress);
            _sprite.SetVisible((uid, sprite), visible);

            if (!_sprite.TryGetLayer((uid, sprite), 0, out var layer, false) ||
                layer.ActualRsi is not { } rsi || !rsi.TryGetState(layer.State, out var state))
            {
                continue;
            }

            _sprite.LayerSetAutoAnimated(layer, false);
            if (!visible)
                continue;

            var animationDuration = 0f;
            for (var i = 0; i < state.DelayCount; i++)
                animationDuration += state.GetDelay(i);

            // Play the existing first three charge frames over the visible phase.
            // Stay below the RSI loop boundary even if float rounding reaches 1.
            var animationTime = MathF.Min(animationDuration * progress, MathF.Max(0f, animationDuration - 0.000001f));
            _sprite.LayerSetAnimationTime(layer, animationTime);
        }
    }
}
