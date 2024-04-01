using System.Collections.Generic;
using Godot;

public partial class EvidenceDepositor : Node
{
    // WIP

    [Export]
    private PackedScene GhostWritingPrefab;

    [Export]
    private PackedScene EMFLevel5Prefab;

    [Export]
    private PackedScene DisembodiedFootstepsPrefab;

    [Export]
    private PackedScene FreezingTemperaturesPrefab;

    [Export]
    private PackedScene ToxicResiduePrefab;

    [Export]
    private PackedScene FingerprintsPrefab;

    [Export]
    private PackedScene ShadowMovementPrefab;

    [Export]
    private RoomLocator Locator;

    public Dictionary<string, List<string>> GhostToEvidences =
        new()
        {
            {
                "Demon",
                new() { "Ghost Writing", "EMF Level 5", "Disembodied Footsteps" }
            },
            {
                "Wraith",
                new() { "Freezing Temperatures", "Toxic Residue", "Fingerprints" }
            },
            {
                "Phantom",
                new() { "EMF Level 5, Disembodied Footsteps", "Shadow Movement" }
            },
            {
                "Shade",
                new() { "Fingerprints", "Freezing Temperatures", "Disembodied Footsteps" }
            },
            {
                "Poltergeist",
                new() { "EMF Level 5", "Fingerprints", "Toxic Residue" }
            },
            {
                "Banshee",
                new() { "Fingerprints", "Freezing Temperatures", "Disembodied Footsteps" }
            }
        };

    HashSet<string> DepositedEvidences = new();

    public void DepositEvidence(string ghostType)
    {
        var evidences = GhostToEvidences[ghostType];

        string chosenEvidence = "";

        while (true)
        {
            chosenEvidence = evidences[0];

            if (!DepositedEvidences.Contains(chosenEvidence))
            {
                break;
            }
        }

        DepositedEvidences.Add(chosenEvidence);

        PackedScene evidencePrefab = chosenEvidence switch
        {
            "Ghost Writing" => GhostWritingPrefab,
            "EMF Level 5" => EMFLevel5Prefab,
            "Disembodied Footsteps" => DisembodiedFootstepsPrefab,
            "Freezing Temperatures" => FreezingTemperaturesPrefab,
            "Toxic Residue" => ToxicResiduePrefab,
            "Fingerprints" => FingerprintsPrefab,
            "Shadow Movement" => ShadowMovementPrefab,
            _ => null
        };

        if (evidencePrefab != null)
        {
            var evidence = evidencePrefab.Instantiate<Node3D>();
            evidence.GlobalPosition = Locator.RoomObject?.GetRandomPosition() ?? Vector3.Zero;
            // GetTree().Root.AddChild(evidence);
        }
    }
}
