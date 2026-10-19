using System.Collections.Generic;

// How one character feels about a faction
public enum Attitude
{
    Like,       // friends: gather together, call for help fast, boost each other's morale
    Moderate,   // strangers: mostly ignore each other, only a small chance to call for help
    Hate        // enemies: will fight on sight
}

// One line in a "relations" list: "toward THIS faction, I feel THIS way"
[System.Serializable]
public class FactionRelation
{
    public string faction = "";
    public Attitude attitude = Attitude.Moderate;
}

// How popular someone is with a faction (-100 hated .. +100 adored). Quests will change these.
[System.Serializable]
public class FactionReputation
{
    public string faction = "";
    [UnityEngine.Range(-100f, 100f)] public float value = 0f;
}

public static class FactionUtil
{
    public static bool Same(string a, string b)
    {
        if (string.IsNullOrWhiteSpace(a) || string.IsNullOrWhiteSpace(b)) return false;
        return string.Equals(a.Trim(), b.Trim(), System.StringComparison.OrdinalIgnoreCase);
    }

    public static bool Contains(IList<string> list, string faction)
    {
        if (list == null) return false;
        foreach (string f in list)
            if (Same(f, faction)) return true;
        return false;
    }

    public static bool Shares(IList<string> a, IList<string> b)
    {
        if (a == null || b == null) return false;
        foreach (string f in a)
            if (Contains(b, f)) return true;
        return false;
    }

    public static bool TryGetRelation(FactionRelation[] relations, string faction, out Attitude attitude)
    {
        if (relations != null)
        {
            foreach (FactionRelation relation in relations)
            {
                if (Same(relation.faction, faction))
                {
                    attitude = relation.attitude;
                    return true;
                }
            }
        }

        attitude = Attitude.Moderate;
        return false;
    }

    // How someone in myFactions (with myRelations) feels about someone in theirFactions.
    // Returns null if nothing decides it. Rules: any Hate wins, then any Like.
    // A faction you are both in counts as Like, unless a relation says otherwise.
    public static Attitude? Resolve(IList<string> myFactions, FactionRelation[] myRelations, IList<string> theirFactions)
    {
        bool hate = false;
        bool like = false;
        bool decided = false;

        if (theirFactions != null)
        {
            foreach (string theirs in theirFactions)
            {
                if (TryGetRelation(myRelations, theirs, out Attitude explicitAttitude))
                {
                    decided = true;
                    if (explicitAttitude == Attitude.Hate) hate = true;
                    else if (explicitAttitude == Attitude.Like) like = true;
                }
                else if (Contains(myFactions, theirs))
                {
                    decided = true;
                    like = true;
                }
            }
        }

        if (hate) return Attitude.Hate;
        if (like) return Attitude.Like;
        if (decided) return Attitude.Moderate;
        return null;
    }
}
