using System;
using Godot;

public partial class ToastNotifications : Node
{
    [Export]
    private Label StatusView;

    private EventBus bus;

    Tween tween;

    public override void _Ready()
    {
        StatusView = GetTree().CurrentScene.GetNode<Label>("CenterContainer/VBoxContainer/Label");
        bus = EventBus.Get();

        bus.ToastNotification += (message) =>
        {
            StatusView.Text = message;

            if (tween != null)
            {
                tween.Stop();
            }

            tween = CreateTween();

            StatusView.Modulate = new Color(1, 1, 1, 1);
            tween.TweenProperty(StatusView, "modulate", new Color(1, 1, 1, 0), 1);
            tween.Play();
        };

        GD.Print("Toast Notifications initialized");
    }
}
