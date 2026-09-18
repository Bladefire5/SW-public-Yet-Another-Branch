using System.Numerics;
using Content.Shared.Imperial.Medieval.Calendar;
using Robust.Client.Graphics;
using Robust.Client.UserInterface.Controls;
using Robust.Shared.IoC;
using Robust.Shared.Localization;
using Robust.Shared.Maths;
using Robust.Shared.Prototypes;

namespace Content.Client.Imperial.Medieval.Calendar.Board.Elements;

public sealed class CalendarDayEntry : PanelContainer
{
    public CalendarDayEntry(int dayNumber, string eventId, bool isCurrentDay, string locKey = "calendar-board-day")
    {
        var prototypeManager = IoCManager.Resolve<IPrototypeManager>();

        MinSize = new Vector2(250, 200);
        MaxSize = new Vector2(250, 200);

        var vBox = new BoxContainer
        {
            Orientation = BoxContainer.LayoutOrientation.Vertical,
            HorizontalExpand = true,
            VerticalExpand = true,
            Margin = new Thickness(5)
        };

        var dayLabel = new Label
        {
            Text = Loc.GetString(locKey, ("day", dayNumber)),
            HorizontalAlignment = HAlignment.Center,
            FontColorOverride = Color.DarkGray
        };
        vBox.AddChild(dayLabel);

        if (prototypeManager.TryIndex<CalendarEventPrototype>(eventId, out var proto))
        {
            var nameLabel = new Label
            {
                Text = Loc.GetString(proto.Name),
                HorizontalAlignment = HAlignment.Center,
                FontColorOverride = isCurrentDay ? Color.Gold : Color.White
            };
            vBox.AddChild(nameLabel);

            var scrollContainer = new ScrollContainer
            {
                HorizontalExpand = true,
                VerticalExpand = true,
                HScrollEnabled = false,
                VScrollEnabled = true,
                Margin = new Thickness(0, 5, 0, 0)
            };

            var textLabel = new RichTextLabel
            {
                HorizontalExpand = true
            };

            textLabel.SetMessage(Loc.GetString(proto.Text));

            scrollContainer.AddChild(textLabel);
            vBox.AddChild(scrollContainer);
        }

        AddChild(vBox);

        PanelOverride = new StyleBoxFlat
        {
            BackgroundColor = isCurrentDay ? Color.FromHex("#334433") : Color.FromHex("#222222"),
            BorderColor = isCurrentDay ? Color.LimeGreen : Color.DarkGray,
            BorderThickness = new Thickness(1)
        };
    }
}
