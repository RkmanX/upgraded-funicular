using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(AudioSource))]
public class PlayerCombat : MonoBehaviour
{
    public Transform cameraPivot;
    public float damage = 15f;
    public float range = 2.5f;
    public float hitRadius = 0.3f;
    public float attackCooldown = 0.6f;
    public float attackStaminaCost = 12f;   // stamina spent per swing
    public LayerMask hitLayers;             // things that can take damage (Enemy)
    public LayerMask blockLayers;           // things that stop the swing (walls, floors)

    [Header("Weapon (decides weaknesses, material bonus and which skill is used)")]
    public AttackSkill weaponType = AttackSkill.LongBlade;
    public WeaponMaterial weaponMaterial = WeaponMaterial.Steel;
    public Element weaponElement = Element.None;      // set to Fire, Thunder... for an enchanted weapon

    [Header("Backstab")]
    public float backstabMultiplier = 3f;
    public float backstabAngle = 100f;      // total width of the "behind" zone, in degrees
    public bool backstabRequiresSneak = true; // sneak attacks only count while you are sneaking

    [Header("Crits & Knockback")]
    public float critChance = 0.1f;         // 0.1 = 10%. Set to 0 to turn crits off
    public float critMultiplier = 2f;
    public float knockbackDistance = 1.5f;  // how far heavy hits shove the enemy
    public float knockbackDuration = 0.5f;  // how long the shove lasts
    public float enemyStunDuration = 1f;    // how long a heavily hit enemy can't act

    [Header("Audio")]
    public AudioClip swingSound;
    public float pitchVariation = 0.08f;

    public bool IsStunned => Time.time < stunnedUntil;

    AudioSource audioSource;
    PlayerStats stats;
    PlayerController controller;
    float nextAttackTime;
    float stunnedUntil;

    static readonly Color missColor = new Color(0.75f, 0.75f, 0.8f);

    void Awake()
    {
        audioSource = GetComponent<AudioSource>();
        stats = GetComponent<PlayerStats>();
        controller = GetComponent<PlayerController>();
    }

    // Called when a critical hit stuns the player: no swinging for a while
    public void Stun(float seconds)
    {
        stunnedUntil = Time.time + seconds;
    }

    void Update()
    {
        if (!IsStunned && Mouse.current.leftButton.wasPressedThisFrame && Time.time >= nextAttackTime)
            Attack();
    }

    void Attack()
    {
        // Swinging costs stamina. Too tired and you can't swing.
        if (stats != null && !stats.TrySpendStamina(attackStaminaCost))
            return;

        nextAttackTime = Time.time + attackCooldown;

        if (swingSound != null)
        {
            audioSource.pitch = 1f + Random.Range(-pitchVariation, pitchVariation);
            audioSource.PlayOneShot(swingSound);
        }

        Ray ray = new Ray(cameraPivot.position, cameraPivot.forward);
        Debug.DrawRay(ray.origin, ray.direction * range, Color.red, 0.5f);

        int castMask = hitLayers | blockLayers;

        if (Physics.SphereCast(ray, hitRadius, out RaycastHit hit, range, castMask))
        {
            bool hitIsDamageable = (hitLayers.value & (1 << hit.collider.gameObject.layer)) != 0;
            if (!hitIsDamageable) return;

            Health health = hit.collider.GetComponentInParent<Health>();
            if (health == null) return;

            // What kind of attack is this? (used for weaknesses and the hit roll)
            AttackInfo info = new AttackInfo(weaponType, weaponMaterial, weaponElement);
            int skill = stats != null ? stats.skills.Get(weaponType) : 40;
            int agility = stats != null ? stats.attributes.agility : 50;

            float finalDamage = damage;
            HitType hitType = HitType.Normal;
            string matchupNote = "";
            Enemy enemy = health.GetComponent<Enemy>();

            // A backstab needs: enemy hasn't noticed you, you're behind it, and (optionally) you're sneaking
            bool sneakOk = !backstabRequiresSneak || (controller != null && controller.IsSneaking);

            if (enemy != null && enemy.IsUnaware && sneakOk && IsBehind(enemy.transform))
            {
                finalDamage *= backstabMultiplier;
                hitType = HitType.Backstab;
            }
            else if (Random.value < critChance)
            {
                finalDamage *= critMultiplier;
                hitType = HitType.Crit;
            }

            if (enemy != null)
            {
                // Backstabs always land. Everything else has to get past the enemy's dodge.
                if (hitType != HitType.Backstab)
                {
                    float chance = enemy.ChanceToBeHit(info, skill, agility);
                    if (Random.value > chance)
                    {
                        if (DamageReadout.Instance != null)
                            DamageReadout.Instance.ShowMessage($"MISS: {enemy.DisplayName} dodged", missColor);

                        enemy.AddSuspicion(0.5f);   // a swing in its face doesn't go unnoticed

                        if (MusicManager.Instance != null)
                            MusicManager.Instance.TriggerCombat();
                        return;
                    }
                }

                // Weaknesses and resistances, plus the weapon's material
                finalDamage *= enemy.DamageMultiplierFor(info, out matchupNote) * CombatMath.MaterialMultiplier(weaponMaterial);

                // An enemy that is on guard may block. Backstabs can't be blocked: it never saw them coming.
                if (hitType != HitType.Backstab && enemy.TryBlock(transform.position, finalDamage, out float remaining))
                {
                    finalDamage = remaining;
                    hitType = HitType.Blocked;   // a blocked crit isn't a crit any more
                }
            }

            if (DamageReadout.Instance != null)
                DamageReadout.Instance.Show(finalDamage, hitType, matchupNote);

            // Fighting an enemy keeps the combat music going
            if (enemy != null && MusicManager.Instance != null)
                MusicManager.Instance.TriggerCombat();

            // Passing "gameObject" tells the enemy WHO hit it
            health.TakeDamage(finalDamage, gameObject);

            // Heavy hits shove the enemy away and stun it (only if it survived)
            bool heavyHit = hitType == HitType.Crit || hitType == HitType.Backstab;
            if (heavyHit && enemy != null && health.Current > 0f)
            {
                Vector3 pushDirection = enemy.transform.position - transform.position;
                enemy.Knockback(pushDirection, knockbackDistance, knockbackDuration, enemyStunDuration);
            }
        }
    }

    bool IsBehind(Transform enemy)
    {
        Vector3 toPlayer = transform.position - enemy.position;
        toPlayer.y = 0f;

        float angle = Vector3.Angle(enemy.forward, toPlayer);
        return angle > 180f - backstabAngle * 0.5f;
    }
}
