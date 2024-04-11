using System;
using System.Collections.Generic;
using System.Linq;
using Godot;

public partial class PlayerStats : Node3D
{
    struct RoomEnterTime
    {
        public string room;
        public double time;
    }

    private Queue<double> speeds = new Queue<double>();
    private double interval = 0.5f; // Sample interval in seconds
    private double timeSinceLastSample = 0f;
    public Vector3 currentPosition;
    public Node3D ghostNode;
    public CharacterBody3D character;

    private List<RoomEnterTime> RoomHistory = new();

    [Export]
    public RoomLocator locator;

    public bool HasPlayerSteppedInsideHouse { get; private set; }

    public override void _Ready()
    {
        character = GetParent<CharacterBody3D>();
        ghostNode = GetTree().CurrentScene.GetNode<Node3D>("Ghost");
        // Initialize currentPosition with the player's starting position
        currentPosition = this.Position;
        // Assuming ghostNode is assigned to the actual ghost node in the scene
    }

    public override void _PhysicsProcess(double delta)
    {
        if (RoomHistory.Count == 0)
        {
            var room = locator.Room == "None" ? "Outside The House" : locator.Room;
            RoomHistory.Add(new RoomEnterTime { room = room, time = Time.GetTicksMsec() / 1000f });
        }
        else
        {
            RoomHistory = RoomHistory.TakeLast(10).ToList();
            var room = locator.Room == "None" ? "Outside The House" : locator.Room;

            if (RoomHistory[RoomHistory.Count - 1].room != room)
            {
                RoomHistory.Add(
                    new RoomEnterTime { room = room, time = Time.GetTicksMsec() / 1000f }
                );
            }
        }
    }

    public override void _Process(double delta)
    {
        timeSinceLastSample += delta;

        // Sample the player's position at defined intervals
        if (timeSinceLastSample >= interval)
        {
            if (speeds.Count >= 20) // Ensure only the last 20 samples are kept
            {
                speeds.Dequeue();
            }

            speeds.Enqueue(character.Velocity.Length()); // Update currentPosition accordingly in your logic
            timeSinceLastSample = 0f;
        }
    }

    public string SpeedToNatural(double speed)
    {
        if (speed > 5)
        {
            return "Sprinting";
        }
        else if (speed > 3)
        {
            return "Walking";
        }
        else
        {
            return "Still";
        }
    }

    public string DistanceToNatural(double distance)
    {
        if (distance > 10)
        {
            return "Far";
        }
        else if (distance > 5)
        {
            return "Near";
        }
        else if (distance > 1)
        {
            return "Very Near";
        }
        else
        {
            return "Touching";
        }
    }

    public string HeightDependentDistanceToNatural(Vector3 a, Vector3 b)
    {
        float distance = a.DistanceTo(b);

        if (Math.Abs(a.Y - b.Y) > 2.5)
        {
            return "Very Far (Different Stories)";
        }

        if (distance > 10)
        {
            return "Far";
        }
        else if (distance > 5)
        {
            return "Near";
        }
        else if (distance > 1)
        {
            return "Very Near";
        }
        else
        {
            return "Touching";
        }
    }

    public string RelativeTime(double time)
    {
        double currentTime = Time.GetTicksMsec() / 1000f;
        double timeDifference = currentTime - time;

        if (timeDifference > 30)
        {
            return "A long time ago (" + timeDifference.ToString("0") + "s ago)";
        }
        else if (timeDifference > 15)
        {
            return "A while ago (" + timeDifference.ToString("0") + "s ago)";
        }
        else if (timeDifference > 5)
        {
            return "A few moments ago (" + timeDifference.ToString("0") + "s ago)";
        }
        else
        {
            return "Just now";
        }
    }

    public string getRoomHistory()
    {
        string history = "Location History:\n";
        foreach (var room in RoomHistory)
        {
            history += $"{room.time.ToString("0")}s - {room.room} ({RelativeTime(room.time)})\n";
        }
        return history;
    }

    public string getStatus()
    {
        if (speeds.Count == 0)
            return "Insufficient data (for now)";

        // Calculate average speed
        double totalDistance = 0;
        double prevSpeed = speeds.Peek();
        foreach (var speed in speeds)
        {
            totalDistance += speed + prevSpeed;
            prevSpeed = speed;
        }
        double averageSpeed = speeds.Average();

        // Calculate current speed (distance between the last two positions divided by interval)
        double currentSpeed = character.Velocity.Length();

        string averageSpeedNatural = SpeedToNatural(averageSpeed);
        string currentSpeedNatural = SpeedToNatural(currentSpeed);

        // Calculate distance from ghost
        float distanceFromGhost = character.GlobalPosition.DistanceTo((ghostNode).GlobalPosition);

        if (currentSpeed > 1)
        {
            locator.FindRoom();
        }

        string room = locator.Room == "None" ? "Outside The House" : locator.Room;

        if (room != "Outside The House" && !HasPlayerSteppedInsideHouse)
        {
            HasPlayerSteppedInsideHouse = true;
        }

        // Convert speeds to units per second and compile the status message
        string status =
            getRoomHistory()
            + "\n---\nCurrent Room: "
            + room
            + "\n"
            + $"Current Speed: {currentSpeedNatural} ({currentSpeed:F1}u/s)\n"
            + $"Average Speed (last 10s): {averageSpeedNatural} ({averageSpeed:F1}u/s)\n"
            + $"Total Distance Travelled (last 10s): {DistanceToNatural(totalDistance)} ({totalDistance:F1}u)\n"
            + $"Distance from Ghost: {HeightDependentDistanceToNatural(character.GlobalPosition, ghostNode.GlobalPosition)} ({distanceFromGhost:F1}u)\n\n";

        return status;
    }
}
