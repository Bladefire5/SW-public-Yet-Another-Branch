using Robust.Client.Animations;
using Robust.Client.Graphics;
using Robust.Client.ResourceManagement;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;
using Robust.Shared.Animations;
using Robust.Shared.Utility;

namespace Content.Client.Imperial.Medieval.Calendar;

// Наследуемся напрямую от LayoutContainer
public sealed class CalendarNotificationView : LayoutContainer
{
    [Dependency] private readonly IResourceCache _resCache = default!;
    private readonly CalendarNotificationUiController _controller = default!;
    private TextureRect _icon = null!;
    private Animation _animation = null!;

    private const string AnimationName = "fade";

    private readonly Color _invisibleColor = new(1f, 1f, 1f, 0f);
    private readonly Color _visibleColor = new(1f, 1f, 1f, 1f);

    public CalendarNotificationView()
    {
        IoCManager.InjectDependencies(this);

        _controller = UserInterfaceManager.GetUIController<CalendarNotificationUiController>();

        LayoutContainer.SetAnchorPreset(this, LayoutContainer.LayoutPreset.Wide);

        InitializeUI();
        CreateAnimation();
        _controller.BroadcastReceived += OnBroadcastReceived;
    }

    private void CreateAnimation()
    {
        _animation = new Animation
        {
            Length = TimeSpan.FromSeconds(24.5f),
            AnimationTracks =
            {
                new AnimationTrackControlProperty
                {
                    Property = nameof(Modulate),
                    InterpolationMode = AnimationInterpolationMode.Linear,
                    KeyFrames =
                    {
                        new AnimationTrackProperty.KeyFrame(_invisibleColor, 0f),
                        new AnimationTrackProperty.KeyFrame(_visibleColor, 3.0f),
                        new AnimationTrackProperty.KeyFrame(_visibleColor, 17.5f),
                        new AnimationTrackProperty.KeyFrame(_invisibleColor, 3.5f),
                    },
                },
            },
        };
    }

    private void InitializeUI()
    {
        _icon = new TextureRect
        {
            Visible = false,
            Modulate = _invisibleColor
        };

        AddChild(_icon);

        LayoutContainer.SetAnchorPreset(_icon, LayoutContainer.LayoutPreset.BottomRight);
        LayoutContainer.SetGrowHorizontal(_icon, LayoutContainer.GrowDirection.Begin);
        LayoutContainer.SetGrowVertical(_icon, LayoutContainer.GrowDirection.Begin);

        LayoutContainer.SetMarginRight(_icon, -30f);
        LayoutContainer.SetMarginBottom(_icon, -30f);
    }

    private void OnBroadcastReceived(string texturePath, string message)
    {
        if (_icon.HasRunningAnimation(AnimationName))
        {
            _icon.Modulate = _invisibleColor;
            _icon.StopAnimation(AnimationName);
        }

        if (!string.IsNullOrEmpty(texturePath) &&
            _resCache.TryGetResource<TextureResource>(new ResPath(texturePath), out var textureResource))
        {
            _icon.Texture = textureResource.Texture;
            _icon.Visible = true;
            _icon.PlayAnimation(_animation, AnimationName);
        }
        else
        {
            _icon.Visible = false;
        }

        var formatted = new FormattedMessage();
        formatted.AddMarkupOrThrow(message);
    }

    protected override void Deparented()
    {
        base.Deparented();
        _controller.BroadcastReceived -= OnBroadcastReceived;
    }
}
