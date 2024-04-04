using System;
using System.Collections.Generic;
using System.Linq;
using Godot;

public partial class PlayerStats : Node3D
{
    private Queue<double> speeds = new Queue<double>();
    private double interval = 0.5f; // Sample interval in seconds
    private double timeSinceLastSample = 0f;
    public Vector3 currentPosition;
    public Node3D ghostNode;
    public CharacterBody3D character;

    public override void _Ready()
    {
        character = GetParent<CharacterBody3D>();
        ghostNode = GetTree().CurrentScene.GetNode<Node3D>("Ghost");
        // Initialize currentPosition with the player's starting position
        currentPosition = this.Position;
        // Assuming ghostNode is assigned to the actual ghost node in the scene
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
        if (speed > 6)
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
        float distanceFromGhost = currentPosition.DistanceTo((ghostNode).GlobalPosition);

        // Convert speeds to units per second and compile the status message
        string status =
            $"Current Speed: {currentSpeed:F1}u/s ({currentSpeedNatural})\n"
            + $"Average Speed (last 10s): {averageSpeed:F1}u/s ({averageSpeedNatural})\n"
            + $"Total Distance Travelled (last 10s): {totalDistance:F1}u\n"
            + $"Distance from Ghost: {distanceFromGhost:F1}u";

        return status;
    }
}
