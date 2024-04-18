using System;
using Godot;

public partial class FuzzySharpGodotBridge : Node
{
    public static int PartialRatio(string s1, string s2)
    {
        return FuzzySharp.Fuzz.PartialRatio(s1, s2);
    }

    public static int TokenSortRatio(string s1, string s2)
    {
        return FuzzySharp.Fuzz.TokenSortRatio(s1, s2);
    }

    public static int Ratio(string s1, string s2)
    {
        return FuzzySharp.Fuzz.Ratio(s1, s2);
    }
}
