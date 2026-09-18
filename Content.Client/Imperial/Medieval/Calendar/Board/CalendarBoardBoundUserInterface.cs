using Content.Shared.Imperial.Medieval.Calendar;
using Robust.Client.GameObjects;

namespace Content.Client.Imperial.Medieval.Calendar.Board;

public sealed class CalendarBoardBoundUserInterface : BoundUserInterface
{
    [ViewVariables]
    private CalendarBoardWindow? _window;

    public CalendarBoardBoundUserInterface(EntityUid owner, Enum uiKey) : base(owner, uiKey)
    {
    }

    protected override void Open()
    {
        base.Open();

        _window = new CalendarBoardWindow();
        _window.Owner = Owner;
        _window.OnClose += Close;

        _window.OnCreateAnnouncement += (title, desc, author) =>
        {
            SendMessage(new CalendarBoardCreateAnnouncementMessage(title, desc, author));
        };

        _window.OnDeleteAnnouncement += id =>
        {
            SendMessage(new CalendarBoardDeleteAnnouncementMessage(id));
        };

        _window.OpenCentered();

        if (State is CalendarBoardBoundUserInterfaceState boardState)
            _window.Populate(boardState);
    }

    protected override void UpdateState(BoundUserInterfaceState state)
    {
        base.UpdateState(state);

        if (state is not CalendarBoardBoundUserInterfaceState boardState)
            return;

        _window?.Populate(boardState);
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
        if (!disposing)
            return;

        _window?.Close();
    }
}
