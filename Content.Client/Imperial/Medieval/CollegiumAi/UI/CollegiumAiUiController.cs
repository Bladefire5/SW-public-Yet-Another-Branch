using Content.Shared.Imperial.Medieval.CollegiumAi;
using Robust.Client.UserInterface.Controllers;

namespace Content.Client.Imperial.Medieval.CollegiumAi.UI;

public sealed class CollegiumAiUiController : UIController
{
    private CollegiumAiMageListMenu? _menu;

    /// <summary>
    /// Opens the jump list, or closes it if the watcher pressed the action again.
    /// </summary>
    public void ToggleMageList(List<CollegiumAiMageEntry> mages)
    {
        if (_menu != null)
        {
            _menu.Close();
            return;
        }

        _menu = UIManager.CreateWindow<CollegiumAiMageListMenu>();
        _menu.Populate(mages);

        _menu.OnClose += () => _menu = null;
        _menu.MageSelected += target =>
        {
            EntityManager.RaisePredictiveEvent(new CollegiumAiTeleportRequestEvent(target));
            _menu?.Close();
        };

        _menu.OpenCentered();
    }
}
