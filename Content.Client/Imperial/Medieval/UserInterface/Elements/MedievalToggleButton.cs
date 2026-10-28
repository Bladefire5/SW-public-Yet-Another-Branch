using Robust.Client.Graphics;
using Robust.Client.ResourceManagement;
using Robust.Client.UserInterface.Controls;

namespace Content.Client.Imperial.Medieval.UserInterface.Elements;

/// <summary>
///     Medieval item slot using the existing inventory artwork, with distinct hover and selected states.
/// </summary>
[Virtual]
public class MedievalToggleButton : Button
{
    private readonly StyleBoxTexture _normalBox;
    private readonly StyleBoxTexture _hoverBox;
    private readonly StyleBoxTexture _pressedBox;
    private readonly StyleBoxTexture _disabledBox;

    public MedievalToggleButton()
    {
        var texture = IoCManager.Resolve<IResourceCache>()
            .GetResource<TextureResource>("/Textures/Interface/Medieval/SlotBackground.png").Texture;
        _normalBox = Box(texture, Color.White);
        _hoverBox = Box(texture, Color.FromHex("#fff1c8"));
        _pressedBox = Box(texture, MedievalUiStyle.GoldBright);
        _disabledBox = Box(texture, Color.FromHex("#635749"));
        UpdateStyle();
    }

    protected override void DrawModeChanged()
    {
        base.DrawModeChanged();

        // can fire from the base constructor, before the label exists
        if (Label == null || _normalBox == null)
            return;

        UpdateStyle();
    }

    private void UpdateStyle()
    {
        (StyleBoxOverride, Label.FontColorOverride) = DrawMode switch
        {
            DrawModeEnum.Pressed => (_pressedBox, MedievalUiStyle.GoldBright),
            DrawModeEnum.Hover => (_hoverBox, Color.White),
            DrawModeEnum.Disabled => (_disabledBox, MedievalUiStyle.TextDisabled),
            _ => (_normalBox, MedievalUiStyle.Text),
        };
    }

    private static StyleBoxTexture Box(Texture texture, Color modulate)
    {
        var box = new StyleBoxTexture
        {
            Texture = texture,
            Modulate = modulate,
        };
        box.SetPatchMargin(StyleBox.Margin.All, 3);
        box.SetContentMarginOverride(StyleBox.Margin.All, 3);
        return box;
    }
}
