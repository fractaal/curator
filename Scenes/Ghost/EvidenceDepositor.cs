using System;
using System.Collections.Generic;
using Godot;

public partial class EvidenceDepositor : Node
{
    // WIP

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
    private PackedScene BloodstainsPrefab;

    [Export]
    private RoomLocator Locator;

    private Random random = new Random();

    public Dictionary<string, List<string>> GhostToEvidences =
        new()
        {
            {
                "Demon",
                new() { "Bloodstains", "EMF Level 5", "Shadow Movement" }
            },
            {
                "Wraith",
                new() { "Freezing Temperatures", "Toxic Residue", "Fingerprints" }
            },
            {
                "Phantom",
                new() { "EMF Level 5", "Disembodied Footsteps", "Shadow Movement" }
            },
            {
                "Shade",
                new() { "Fingerprints", "Freezing Temperatures", "Disembodied Footsteps" }
            },
            {
                "Poltergeist",
                new() { "EMF Level 5", "Fingerprints", "Bloodstains" }
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

        if (DepositedEvidences.Count == evidences.Count)
        {
            DepositedEvidences.Clear();
        }

        while (true)
        {
            chosenEvidence = evidences[random.Next() % evidences.Count];

            GD.Print("Chose evidence: " + chosenEvidence.GetBaseName());

            if (!DepositedEvidences.Contains(chosenEvidence))
            {
                break;
            }
        }

        DepositedEvidences.Add(chosenEvidence);

        PackedScene evidencePrefab = chosenEvidence switch
        {
            "Bloodstains" => BloodstainsPrefab,
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
            GetTree().Root.AddChild(evidence);
            evidence.GlobalPosition = Locator.RoomObject?.GetRandomPosition() ?? Vector3.Zero;
        }
        else
        {
            GD.PrintErr("Evidence prefab not found for " + chosenEvidence);
        }
    }
}
