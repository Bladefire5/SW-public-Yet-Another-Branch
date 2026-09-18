using Content.Client.Imperial.Medieval.CollegiumAi.UI;
using Content.Shared.Imperial.Medieval.CollegiumAi;
using Robust.Client.UserInterface;

namespace Content.Client.Imperial.Medieval.CollegiumAi;

/// <summary>
/// Client half of the Collegium watcher. Everything it does resolves on the server; the client only has to put the
/// jump list on screen when the server hands one over.
/// </summary>
public sealed class CollegiumAiSystem : EntitySystem
{
    [Dependency] private readonly IUserInterfaceManager _ui = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeNetworkEvent<CollegiumAiMageListEvent>(OnMageList);
    }

    private void OnMageList(CollegiumAiMageListEvent ev)
    {
        _ui.GetUIController<CollegiumAiUiController>().ToggleMageList(ev.Mages);
    }
}
