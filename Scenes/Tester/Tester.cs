using System;
using System.Threading.Tasks;
using Godot;

public partial class Tester : Node3D
{
    private EventBus Bus;

    [Export]
    private Control ListUI;

    public partial class TestItem : Node3D
    {
        private string name;

        public TestItem(string _name)
        {
            name = _name;
        }

        public override void _Ready()
        {
            var label = new RichTextLabel();

            label.BbcodeEnabled = true;
            label.Text = "";
        }
    }

    private async Task<bool> ExpectObjectInteractionAcknowledged(
        string verb,
        string _object,
        string target
    )
    {
        var success = ToSignal(Bus, EventBus.SignalName.ObjectInteractionAcknowledged);

        var source = new TaskCompletionSource<bool>();
        var timer = GetTree().CreateTimer(3);

        void OnObjectInteractionAcknowledged()
        {
            source.TrySetResult(true);
        }

        void OnTimeout()
        {
            source.TrySetResult(false);
        }

        timer.Timeout += OnTimeout;
        Bus.ObjectInteractionAcknowledged += (string ackVerb, string ackObject, string ackTarget) =>
        {
            if (ackVerb == verb && _object == ackObject && target == ackTarget)
            {
                OnObjectInteractionAcknowledged();
            }
        };

        return await source.Task;
    }

    // Called when the node enters the scene tree for the first time.
    public override async void _Ready()
    {
        Bus = EventBus.Get();

        await ToSignal(GetTree().CreateTimer(5), "timeout");

        GD.Print("Starting test");
    }

    // Called every frame. 'delta' is the elapsed time since the previous frame.
    public override void _Process(double delta) { }
}
