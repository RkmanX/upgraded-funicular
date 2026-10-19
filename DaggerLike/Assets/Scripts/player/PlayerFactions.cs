using System.Collections.Generic;
using UnityEngine;

// Put this on the Player. Which factions you belong to, how you feel about others,
// and how popular you are with each (quests will change that later).
public class PlayerFactions : MonoBehaviour
{
    [Header("Membership (you can belong to several)")]
    public List<string> memberships = new List<string>();   // e.g. "Holy Alliance", "Local God", "Ascroft"

    [Header("How YOU feel about factions (decides who your companions attack)")]
    public FactionRelation[] relations;

    [Header("Reputation (-100 hated .. +100 adored)")]
    public List<FactionReputation> reputation = new List<FactionReputation>();
    public float likeThreshold = 25f;     // at or above this, that faction's members like you
    public float hateThreshold = -25f;    // at or below this, they hate you

    public bool IsMember(string faction)
    {
        return FactionUtil.Contains(memberships, faction);
    }

    public float GetReputation(string faction)
    {
        foreach (FactionReputation r in reputation)
        {
            if (FactionUtil.Same(r.faction, faction))
                return r.value;
        }
        return 0f;
    }

    // For quests and actions: PlayerFactions.AddReputation("Mages", 10f)
    public void AddReputation(string faction, float amount)
    {
        foreach (FactionReputation r in reputation)
        {
            if (FactionUtil.Same(r.faction, faction))
            {
                r.value = Mathf.Clamp(r.value + amount, -100f, 100f);
                Announce(faction, amount);
                return;
            }
        }

        reputation.Add(new FactionReputation { faction = faction, value = Mathf.Clamp(amount, -100f, 100f) });
        Announce(faction, amount);
    }

    void Announce(string faction, float amount)
    {
        if (DamageReadout.Instance == null) return;

        string sign = amount >= 0f ? "+" : "";
        DamageReadout.Instance.ShowMessage($"REPUTATION: {faction} {sign}{amount:0}", new Color(0.85f, 0.7f, 1f));
    }

    // How members of a group (with their own relations list) feel about the player.
    // Priority: strong reputation, then relations and shared factions, then the fallback.
    public Attitude AttitudeOf(IList<string> theirFactions, FactionRelation[] theirRelations, Attitude fallback)
    {
        bool loved = false;
        bool hated = false;

        foreach (string f in theirFactions)
        {
            float r = GetReputation(f);
            if (r <= hateThreshold) hated = true;
            else if (r >= likeThreshold) loved = true;
        }

        Attitude? byFaction = FactionUtil.Resolve(theirFactions, theirRelations, memberships);

        if (hated || byFaction == Attitude.Hate) return Attitude.Hate;
        if (loved || byFaction == Attitude.Like) return Attitude.Like;
        return byFaction ?? fallback;
    }

    // How the PLAYER feels about a group (used to pick who companions attack)
    public Attitude AttitudeTo(IList<string> theirFactions)
    {
        Attitude? result = FactionUtil.Resolve(memberships, relations, theirFactions);
        return result ?? Attitude.Moderate;
    }
}
