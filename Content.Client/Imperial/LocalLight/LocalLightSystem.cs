using Robust.Client.GameObjects;
using Robust.Client.Player;
using Content.Shared.Imperial.LocalLight;
using Robust.Shared.Map;
using Robust.Shared.Player;

namespace Content.Client.Imperial.LocalLight;

public sealed class LocalLightSystem : EntitySystem
{
    [Dependency] private readonly IPlayerManager _playerMan = default!;
    [Dependency] private readonly PointLightSystem _lightSys = default!;

    private readonly Dictionary<EntityUid, EntityUid> _lightEntities = new();

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<LocalLightComponent, ComponentInit>(OnLightInit);
        SubscribeLocalEvent<LocalLightComponent, ComponentShutdown>(OnLightShutdown);
        SubscribeLocalEvent<LocalLightComponent, AfterAutoHandleStateEvent>(OnLightSync);
        SubscribeLocalEvent<LocalLightComponent, LocalPlayerAttachedEvent>(OnPlayerAttached);
        SubscribeLocalEvent<LocalLightComponent, LocalPlayerDetachedEvent>(OnPlayerDetached);
    }

    private void OnLightInit(Entity<LocalLightComponent> ent, ref ComponentInit args)
    {
        SetLightParameters(ent);
    }

    private void OnLightShutdown(Entity<LocalLightComponent> ent, ref ComponentShutdown args)
    {
        RemoveLight(ent);
    }

    private void OnLightSync(Entity<LocalLightComponent> ent, ref AfterAutoHandleStateEvent args)
    {
        SetLightParameters(ent);
    }

    private void OnPlayerAttached(Entity<LocalLightComponent> ent, ref LocalPlayerAttachedEvent args)
    {
        SetLightParameters(ent);
    }

    private void OnPlayerDetached(Entity<LocalLightComponent> ent, ref LocalPlayerDetachedEvent args)
    {
        RemoveLight(ent);
    }

    private void SetLightParameters(Entity<LocalLightComponent> ent)
    {
        if (TerminatingOrDeleted(ent) || !ent.Comp.Enabled || _playerMan.LocalEntity != ent.Owner)
        {
            RemoveLight(ent);
            return;
        }

        // Use an attached client entity, as upstream fire visuals do, so an owner's
        // server-controlled light can coexist with the local player's personal light.
        if (!_lightEntities.TryGetValue(ent, out var lightUid) ||
            !TryComp(lightUid, out PointLightComponent? clientLight) ||
            TerminatingOrDeleted(lightUid))
        {
            RemoveLight(ent);
            lightUid = Spawn(null, new EntityCoordinates(ent, default));
            clientLight = Factory.GetComponent<PointLightComponent>();
            // The engine registers networking ownership when the component is added.
            clientLight.NetSyncEnabled = false;
            AddComp(lightUid, clientLight);
            _lightEntities[ent] = lightUid;
        }

        clientLight.Offset = ent.Comp.Offset;
        _lightSys.SetEnabled(lightUid, true, clientLight);
        _lightSys.SetRadius(lightUid, ent.Comp.Radius, clientLight);
        _lightSys.SetEnergy(lightUid, ent.Comp.Energy, clientLight);
        _lightSys.SetSoftness(lightUid, ent.Comp.Softness, clientLight);
        _lightSys.SetColor(lightUid, ent.Comp.Color, clientLight);
        _lightSys.SetMask(ent.Comp.MaskPath, clientLight);
        _lightSys.SetCastShadows(lightUid, ent.Comp.CastShadows, clientLight);
    }

    private void RemoveLight(EntityUid owner)
    {
        if (_lightEntities.Remove(owner, out var light) && !TerminatingOrDeleted(light))
            Del(light);
    }
}
