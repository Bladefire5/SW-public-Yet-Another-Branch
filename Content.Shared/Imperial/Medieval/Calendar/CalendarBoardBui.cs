using System;
using System.Collections.Generic;
using Robust.Shared.Serialization;
using Content.Shared.Imperial.Medieval.Factions;

namespace Content.Shared.Imperial.Medieval.Calendar;

[Serializable, NetSerializable]
public enum CalendarBoardUiKey : byte
{
    Key
}

[Serializable, NetSerializable]
public sealed class CalendarBoardBoundUserInterfaceState : BoundUserInterfaceState
{
    public Dictionary<int, WantedData> Wanted;
    public List<string> DayDeck;
    public List<string> NightDeck;
    public int CurrentCycle;
    public List<AnnouncementData> Announcements;

    public CalendarBoardBoundUserInterfaceState(
        Dictionary<int, WantedData> wanted,
        List<string> dayDeck,
        List<string> nightDeck,
        int currentCycle,
        List<AnnouncementData> announcements)
    {
        Wanted = wanted;
        DayDeck = dayDeck;
        NightDeck = nightDeck;
        CurrentCycle = currentCycle;
        Announcements = announcements;
    }
}
