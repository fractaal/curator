using System;
using Godot;

public partial class FearFactor : Node
{
    private EventBus Bus;

    public double OldFearFactorValue = 0;
    public double FearFactorValue = 0;

    public override void _Ready()
    {
        Bus = EventBus.Get();

        Bus.GhostAction += (string verb, string arguments) =>
        {
            if (verb == "appearasghost")
            {
                FearFactorValue += 5;
            }
            else if (verb == "chaseplayerasghost")
            {
                FearFactorValue += 30;
            }
            else if (verb == "speakasghost")
            {
                FearFactorValue += 5;
            }
            else if (verb == "emitsoundasghost")
            {
                FearFactorValue += 15;
            }
        };

        Bus.PlayerEffect += (string verb, string arguments) =>
        {
            if (verb == "pullplayertoghost")
            {
                FearFactorValue += 10;
            }
            else if (verb == "throwplayeraround")
            {
                FearFactorValue += 5;
            }
            else if (verb == "dimplayerflashlight")
            {
                FearFactorValue += 20;
            }
        };

        Bus.ObjectInteractionAcknowledged += (string verb, string objectType, string target) =>
        {
            if (objectType.Contains("light"))
            {
                if (verb == "explode")
                {
                    FearFactorValue += 10;
                }
                else if (verb == "turnoff")
                {
                    FearFactorValue += 3;
                }
                else if (verb == "flicker")
                {
                    FearFactorValue += 1;
                }
            }
            else if (objectType.Contains("door"))
            {
                if (verb == "open")
                {
                    FearFactorValue += 2;
                }
                else if (verb == "close")
                {
                    FearFactorValue += 2;
                }
                else if (verb == "lock")
                {
                    FearFactorValue += 5;
                }
            }
            else if (objectType.Contains("radio"))
            {
                if (verb == "playfreakymusicon")
                {
                    FearFactorValue += 5;
                }
            }
            else if (objectType.Contains("object"))
            {
                if (verb == "throw")
                {
                    FearFactorValue += 1.5;
                }
                else if (verb == "shift")
                {
                    FearFactorValue += 0.25;
                }
                else if (verb == "jolt")
                {
                    FearFactorValue += 0.5;
                }
            }
        };
    }

    public override void _PhysicsProcess(double delta)
    {
        FearFactorValue += -2.5 * delta;

        if (FearFactorValue < 0)
        {
            FearFactorValue = 0;
        }
        else if (FearFactorValue > 100)
        {
            FearFactorValue = 100;
        }

        if (Math.Abs(FearFactorValue - OldFearFactorValue) > 0.01)
        {
            Bus.EmitSignal(EventBus.SignalName.FearFactorChanged, (int)FearFactorValue);
        }

        OldFearFactorValue = FearFactorValue;
    }
}
