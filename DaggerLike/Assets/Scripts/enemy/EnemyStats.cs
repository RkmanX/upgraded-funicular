using UnityEngine;

public enum AttackSkill { ShortBlade, LongBlade, Axe, Blunt, HandToHand, Destruction }   // Destruction = magic (not used yet)

public enum ItemEffect { HealHealth, RestoreStamina, RestoreMagicka }   // only HealHealth is used so far

public enum AbilityType { HealSelf, DamageSpell, SpecialMove }          // only HealSelf is used so far

[System.Serializable]
public class EnemyAttributes
{
    public int strength = 50;
    public int intelligence = 50;
    public int willpower = 50;
    public int agility = 50;
    public int endurance = 50;
    public int perception = 50;
    public int speed = 50;
    public int luck = 50;
    [Range(0, 100)] public int cowardice = 30;   // higher = more likely to run away, and less likely to block
}

[System.Serializable]
public class EnemySkills
{
    [Range(0, 100)] public int shortBlade = 25;
    [Range(0, 100)] public int longBlade = 25;
    [Range(0, 100)] public int axe = 25;
    [Range(0, 100)] public int blunt = 25;
    [Range(0, 100)] public int handToHand = 25;
    [Range(0, 100)] public int block = 25;
    [Range(0, 100)] public int destruction = 0;
    [Range(0, 100)] public int restoration = 0;

    public int Get(AttackSkill skill)
    {
        switch (skill)
        {
            case AttackSkill.ShortBlade: return shortBlade;
            case AttackSkill.LongBlade:  return longBlade;
            case AttackSkill.Axe:        return axe;
            case AttackSkill.Blunt:      return blunt;
            case AttackSkill.HandToHand: return handToHand;
            case AttackSkill.Destruction: return destruction;
            default: return 0;
        }
    }
}

[System.Serializable]
public class EnemyItem
{
    public string itemName = "Healing Potion";
    public int amount = 1;                    // how many it carries
    public ItemEffect effect = ItemEffect.HealHealth;
    public float magnitude = 30f;             // how much it restores
}

[System.Serializable]
public class EnemyAbility
{
    public string abilityName = "Minor Heal";
    public AbilityType type = AbilityType.HealSelf;
    public float magickaCost = 15f;
    public float magnitude = 25f;
    public float cooldown = 8f;

    [HideInInspector] public float readyAt;   // time when it can be used again
}

// The enemy "lookup table": resources, attributes, skills, items and abilities.
// Enemy.cs reads everything about how strong/fast/brave this enemy is from here.
public class EnemyStats : MonoBehaviour
{
    [Header("Resources")]
    public float maxHealth = 60f;
    public float maxMagicka = 0f;
    public float maxStamina = 60f;
    public bool applyHealthFromTable = true;  // copies Max Health onto the Health component at start

    [Header("Attributes")]
    public EnemyAttributes attributes = new EnemyAttributes();

    [Header("Skills")]
    public EnemySkills skills = new EnemySkills();
    public AttackSkill attackSkill = AttackSkill.LongBlade;   // which skill its attacks use

    [Header("Weapon")]
    public WeaponMaterial weaponMaterial = WeaponMaterial.Iron;
    public Element attackElement = Element.None;              // for fire-breathers, frost swords and so on

    [Header("Weaknesses (easier to hit, takes more damage)")]
    public AttackSkill[] weakToWeapons;
    public WeaponMaterial[] weakToMaterials;
    public Element[] weakToElements;

    [Header("Resistances (harder to hit, takes less damage)")]
    public AttackSkill[] resistWeapons;
    public WeaponMaterial[] resistMaterials;
    public Element[] resistElements;

    [Header("Usable items (name, how many, what it does)")]
    public EnemyItem[] items;

    [Header("Abilities (spells and special moves)")]
    public EnemyAbility[] abilities;

    [Header("Stamina and panting")]
    public float staminaRegenDelay = 1.5f;     // pause after spending before it refills
    [Range(0f, 1f)] public float exhaustRecoverFraction = 0.3f;   // exhausted until stamina climbs back to this fraction
    public AudioClip pantSound;                // plays when it runs out of stamina, and repeats while it's tired
    public float pantInterval = 2.5f;
    [Range(0f, 1f)] public float pantVolume = 1f;
    public bool announceEvents = true;         // show "out of stamina" lines on the readout

    [Header("Morale sounds")]
    public AudioClip fleeSound;                // plays when it decides to run away
    public AudioClip desperateSound;           // plays when it turns back to fight to the death
    public AudioClip joinPartySound;           // plays when it joins your party
    [Range(0f, 1f)] public float moraleSoundVolume = 1f;

    public float Stamina { get; private set; }
    public float Magicka { get; private set; }
    public bool Exhausted { get; private set; }

    // ---------- Values worked out from the table ----------
    // (these formulas are easy to tweak: 50 in a stat means "average")

    public float StrengthMultiplier => 1f + (attributes.strength - 50) / 100f;
    public int AttackSkillValue => skills.Get(attackSkill);
    public float SkillMultiplier => 0.75f + AttackSkillValue / 200f;                 // skill 0 = x0.75, skill 100 = x1.25
    public float HitChance => CombatMath.BaseHitChance(AttackSkillValue, attributes.agility);
    public AttackInfo AttackInfo => new AttackInfo(attackSkill, weaponMaterial, attackElement);
    public float CritBonus => (attributes.luck - 50) / 500f;
    public float SpeedMultiplier => 0.5f + attributes.speed / 100f;
    public float PerceptionMultiplier => 0.6f + attributes.perception / 125f;
    public float AttackSpeedMultiplier => 0.75f + attributes.agility / 200f;       // higher agility = shorter cooldown
    public float MaxMorale => 40f + attributes.willpower * 0.6f;
    public float HealthRegenPerSecond => attributes.endurance * 0.03f;
    public float StaminaRegenPerSecond => 4f + attributes.endurance * 0.12f;
    public float MagickaRegenPerSecond => 0.5f + attributes.willpower * 0.04f;
    public float Cowardice01 => attributes.cowardice / 100f;

    // Cowardly enemies block less often
    public float BlockChance
    {
        get
        {
            float chance = Mathf.Clamp01(skills.block / 100f * 0.75f + attributes.agility / 500f);
            chance *= 1f - attributes.cowardice / 150f;
            return Mathf.Min(chance, 0.85f);
        }
    }

    // How much of the damage a successful block stops (0.4 = 40%, up to 0.9)
    public float BlockReduction => Mathf.Lerp(0.4f, 0.9f, skills.block / 100f);

    // Damage for one hit: weapon damage, scaled by strength and skill, with a little randomness
    public float RollDamage(float weaponDamage)
    {
        return weaponDamage * StrengthMultiplier * SkillMultiplier * CombatMath.MaterialMultiplier(weaponMaterial) * Random.Range(0.9f, 1.1f);
    }

    float staminaRegenTimer;
    float panTimer;

    void Awake()
    {
        Stamina = maxStamina;
        Magicka = maxMagicka;

        if (applyHealthFromTable)
        {
            Health health = GetComponent<Health>();
            if (health != null)
            {
                health.maxHealth = maxHealth;
                health.ResetToMax();
            }
        }
    }

    void Update()
    {
        staminaRegenTimer -= Time.deltaTime;
        if (staminaRegenTimer <= 0f)
            Stamina = Mathf.Min(maxStamina, Stamina + StaminaRegenPerSecond * Time.deltaTime);

        Magicka = Mathf.Min(maxMagicka, Magicka + MagickaRegenPerSecond * Time.deltaTime);

        if (Exhausted)
        {
            // Keep panting until it has caught its breath
            panTimer -= Time.deltaTime;
            if (panTimer <= 0f) Pant();

            if (Stamina >= maxStamina * exhaustRecoverFraction)
                Exhausted = false;
        }
    }

    // ---------- Stamina ----------

    // All-or-nothing, like the player's. If it can't pay, it becomes exhausted (and pants).
    public bool TrySpendStamina(float cost)
    {
        if (Exhausted) return false;

        if (Stamina < cost)
        {
            BecomeExhausted();
            return false;
        }

        Stamina -= cost;
        staminaRegenTimer = staminaRegenDelay;

        if (Stamina <= 0.01f)
            BecomeExhausted();

        return true;
    }

    void BecomeExhausted()
    {
        if (Exhausted) return;

        Exhausted = true;
        staminaRegenTimer = staminaRegenDelay;
        panTimer = 0f;   // pant right away

        if (announceEvents && DamageReadout.Instance != null)
            DamageReadout.Instance.ShowMessage($"TIRED: {name} is out of stamina!", new Color(0.7f, 0.9f, 0.7f));
    }

    void Pant()
    {
        panTimer = pantInterval;

        if (pantSound != null)
            AudioSource.PlayClipAtPoint(pantSound, transform.position, pantVolume);
    }

    // ---------- Morale sounds ----------

    public void PlayFleeSound()
    {
        if (fleeSound != null)
            AudioSource.PlayClipAtPoint(fleeSound, transform.position, moraleSoundVolume);
    }

    public void PlayDesperateSound()
    {
        if (desperateSound != null)
            AudioSource.PlayClipAtPoint(desperateSound, transform.position, moraleSoundVolume);
    }

    public void PlayJoinSound()
    {
        if (joinPartySound != null)
            AudioSource.PlayClipAtPoint(joinPartySound, transform.position, moraleSoundVolume);
    }

    // ---------- Weaknesses and resistances ----------

    static bool Has<T>(T[] list, T value)
    {
        return list != null && System.Array.IndexOf(list, value) >= 0;
    }

    void CountMatchups(AttackInfo info, out int weak, out int resist)
    {
        weak = 0;
        resist = 0;

        if (Has(weakToWeapons, info.weapon)) weak++;
        if (Has(weakToMaterials, info.material)) weak++;
        if (info.element != Element.None && Has(weakToElements, info.element)) weak++;

        if (Has(resistWeapons, info.weapon)) resist++;
        if (Has(resistMaterials, info.material)) resist++;
        if (info.element != Element.None && Has(resistElements, info.element)) resist++;
    }

    // Each weakness makes it a bit easier to hit, each resistance a bit harder
    public float MatchupHitBonus(AttackInfo info)
    {
        CountMatchups(info, out int weak, out int resist);
        return weak * 0.06f - resist * 0.04f;
    }

    // Each weakness: x1.2 damage. Each resistance: x0.75. The note is shown on the readout.
    public float MatchupDamageMultiplier(AttackInfo info, out string note)
    {
        CountMatchups(info, out int weak, out int resist);

        note = weak > resist ? " (WEAK)" : (resist > weak ? " (RESIST)" : "");
        return Mathf.Pow(1.2f, weak) * Mathf.Pow(0.75f, resist);
    }

    // ---------- Healing ----------

    // Tries a healing spell first, then a healing item. Returns true if it healed.
    public bool TryHealSelf(Health health, out string description)
    {
        description = null;

        if (abilities != null)
        {
            foreach (EnemyAbility ability in abilities)
            {
                if (ability.type != AbilityType.HealSelf) continue;
                if (Time.time < ability.readyAt || Magicka < ability.magickaCost) continue;

                Magicka -= ability.magickaCost;
                ability.readyAt = Time.time + ability.cooldown;
                health.Heal(ability.magnitude);
                description = $"cast {ability.abilityName}";
                return true;
            }
        }

        if (items != null)
        {
            foreach (EnemyItem item in items)
            {
                if (item.effect != ItemEffect.HealHealth || item.amount <= 0) continue;

                item.amount--;
                health.Heal(item.magnitude);
                description = $"used {item.itemName} ({item.amount} left)";
                return true;
            }
        }

        return false;
    }
}
