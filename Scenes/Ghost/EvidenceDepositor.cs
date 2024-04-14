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

    private List<string> Evidences =
        new()
        {
            "Bloodstains",
            "EMF Level 5",
            "Disembodied Footsteps",
            "Freezing Temperatures",
            "Toxic Residue",
            "Fingerprints",
            "Shadow Movement"
        };

    public static Dictionary<string, List<string>> GhostToEvidences;

    public EvidenceDepositor()
    {
        GhostToEvidences = new()
        {
            {
                "Demon",
                new() { Evidences[0], Evidences[1], Evidences[2] }
            },
            {
                "Wraith",
                new() { Evidences[1], Evidences[2], Evidences[3] }
            },
            {
                "Phantom",
                new() { Evidences[2], Evidences[3], Evidences[4] }
            },
            {
                "Shade",
                new() { Evidences[3], Evidences[4], Evidences[5] }
            },
            {
                "Poltergeist",
                new() { Evidences[4], Evidences[5], Evidences[6] }
            },
            {
                "Banshee",
                new() { Evidences[5], Evidences[6], Evidences[0] }
            }
        };
    }

    public string GetEvidenceForGhost(string ghostType)
    {
        var evidences = GhostToEvidences[ghostType];

        if (evidences == null)
        {
            return "";
        }

        string output = "";

        for (int i = 0; i < evidences.Count; i++)
        {
            output += evidences[i];
            if (i < evidences.Count - 1)
            {
                output += ", ";
            }
        }

        return output;
    }

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
            EventBus
                .Get()
                .EmitSignal(EventBus.SignalName.NotableEventOccurred, "Ghost evidence deposited");
        }
        else
        {
            GD.PrintErr("Evidence prefab not found for " + chosenEvidence);
        }
    }
}
