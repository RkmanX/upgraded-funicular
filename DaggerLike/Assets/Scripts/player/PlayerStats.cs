using UnityEngine;
using UnityEngine.InputSystem;

// Stamina and mana for the player. (Health lives in the separate Health component.)
public class PlayerStats : MonoBehaviour
{
    [Header("Stamina")]
    public float maxStamina = 100f;
    public float staminaRegenPerSecond = 20f;
    public float staminaRegenDelay = 1f;      // pause after spending before it starts refilling
    public float exhaustedRecoverAt = 20f;    // after hitting 0 you can't run until stamina climbs back to this

    [Header("Mana")]
    public float maxMana = 100f;
    public float manaRegenPerSecond = 4f;
    public float manaRegenDelay = 2f;

    [Header("Testing")]
    public bool debugManaKey = true;          // press M to burn 20 mana, so you can see the blue bar move

    [Header("Attributes and skills (the same table enemies use; Cowardice isn't used for the player)")]
    public EnemyAttributes attributes = new EnemyAttributes();
    public EnemySkills skills = new EnemySkills { shortBlade = 40, longBlade = 40, axe = 40, blunt = 40, handToHand = 40, block = 30 };

    public float Stamina { get; private set; }
    public float Mana { get; private set; }
    public bool Exhausted { get; private set; }
    public bool CanRun => !Exhausted && Stamina > 0f;

    float staminaRegenTimer;
    float manaRegenTimer;

    void Awake()
    {
        Stamina = maxStamina;
        Mana = maxMana;
    }

    void Update()
    {
        if (debugManaKey && Keyboard.current != null && Keyboard.current.mKey.wasPressedThisFrame)
            TrySpendMana(20f);

        // Each resource waits out its delay timer, then refills
        staminaRegenTimer -= Time.deltaTime;
        if (staminaRegenTimer <= 0f)
            Stamina = Mathf.Min(maxStamina, Stamina + staminaRegenPerSecond * Time.deltaTime);

        manaRegenTimer -= Time.deltaTime;
        if (manaRegenTimer <= 0f)
            Mana = Mathf.Min(maxMana, Mana + manaRegenPerSecond * Time.deltaTime);

        if (Exhausted && Stamina >= exhaustedRecoverAt)
            Exhausted = false;
    }

    // Burns stamina, even down to 0 (used for running, called every frame)
    public void Drain(float amount)
    {
        Stamina = Mathf.Max(0f, Stamina - amount);
        staminaRegenTimer = staminaRegenDelay;
        if (Stamina <= 0f) Exhausted = true;
    }

    // All-or-nothing: spends the stamina only if you have enough (used for sword swings)
    public bool TrySpendStamina(float amount)
    {
        if (Stamina < amount) return false;
        Drain(amount);
        return true;
    }

    // Same idea for spells, once we have them
    public bool TrySpendMana(float amount)
    {
        if (Mana < amount) return false;
        Mana -= amount;
        manaRegenTimer = manaRegenDelay;
        return true;
    }
}
