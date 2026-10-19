using UnityEngine;

// Magical damage types. A weapon (or later a spell) can carry one.
public enum Element { None, Fire, Water, Wind, Thunder, Poison, Magic, Light, Darkness }

// What a weapon is made of. Some enemies are weak to certain materials (undead and silver, for example).
public enum WeaponMaterial { Iron, Steel, Silver, Mithril, Ebony }

// Everything about an attack that matters for weaknesses and resistances
public struct AttackInfo
{
    public AttackSkill weapon;        // the weapon type (long blade, axe...). Destruction means magic.
    public WeaponMaterial material;
    public Element element;

    public AttackInfo(AttackSkill weapon, WeaponMaterial material, Element element)
    {
        this.weapon = weapon;
        this.material = material;
        this.element = element;
    }
}

// The formulas for hitting and dodging, shared by the player and every enemy
public static class CombatMath
{
    // Chance an attack lands before dodging: skill and agility help. 50 agility = average.
    public static float BaseHitChance(int skill, int agility)
    {
        return Mathf.Clamp(0.7f + skill / 250f + (agility - 50) / 500f, 0.3f, 0.95f);
    }

    // Chance the DEFENDER sidesteps it. Based on their agility.
    public static float DodgeChance(int agility)
    {
        return Mathf.Clamp(agility / 600f, 0f, 0.3f);
    }

    public static float MaterialMultiplier(WeaponMaterial material)
    {
        switch (material)
        {
            case WeaponMaterial.Iron:    return 1.0f;
            case WeaponMaterial.Steel:   return 1.1f;
            case WeaponMaterial.Silver:  return 1.1f;
            case WeaponMaterial.Mithril: return 1.25f;
            case WeaponMaterial.Ebony:   return 1.4f;
            default: return 1f;
        }
    }
}
