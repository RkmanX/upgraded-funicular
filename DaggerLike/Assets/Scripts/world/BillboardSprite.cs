using UnityEngine;
using UnityEngine.AI;

// One animation: a list of frames
[System.Serializable]
public class SpriteSequence
{
    public Sprite[] frames;
}

// The sprites for ONE viewing direction (front, back or side)
[System.Serializable]
public class DirectionSprites
{
    [Tooltip("Element 0 is the standing-still frame. The whole list loops while the enemy walks.")]
    public Sprite[] walk;

    [Tooltip("Shown briefly whenever the enemy takes damage.")]
    public Sprite hit;

    [Tooltip("Played once, start to finish, while thrown back by a crit or backstab.")]
    public Sprite[] knockback;

    [Tooltip("One entry per attack animation. The enemy picks one at random for each attack.")]
    public SpriteSequence[] attacks;

    [Tooltip("Played when the enemy blocks a hit.")]
    public Sprite[] block;
}

// Put this on a CHILD object of the enemy (never on the enemy root itself) that has a SpriteRenderer.
// It faces the camera, picks front/back/side from the enemy's facing,
// and animates: idle, walking, attacking, blocking, hit, knockback, dead.
[RequireComponent(typeof(SpriteRenderer))]
public class BillboardSprite : MonoBehaviour
{
    public Transform enemyRoot;           // leave empty to use the parent

    [Header("Sprites")]
    public DirectionSprites front;
    public DirectionSprites back;
    public DirectionSprites side;
    public Sprite dead;
    public bool flipSideSprite;           // tick this if the side view looks backwards
    public float groundOffset = 0.02f;    // lifts the corpse a hair above the floor

    [Header("Animation")]
    public float walkFps = 8f;            // walk cycle speed
    public float hitDuration = 0.25f;     // how long the hit sprite shows

    SpriteRenderer sr;
    Transform cam;
    NavMeshAgent agent;
    Health health;
    bool isDead;
    Color originalColor = Color.white;

    float walkTime;
    float hitTimeLeft;
    float knockbackTimeLeft;
    float knockbackDuration = 0.01f;
    float attackTimeLeft;
    float attackDuration = 0.01f;
    int attackVariant;
    float blockTimeLeft;
    float blockDuration = 0.01f;

    // How many different attack animations are set up (the enemy picks one at random)
    public int AttackVariantCount
    {
        get { return Mathf.Max(CountAttacks(front), Mathf.Max(CountAttacks(back), CountAttacks(side))); }
    }

    static int CountAttacks(DirectionSprites set)
    {
        return set != null && set.attacks != null ? set.attacks.Length : 0;
    }

    void Awake()
    {
        sr = GetComponent<SpriteRenderer>();
        originalColor = sr.color;

        if (enemyRoot == null) enemyRoot = transform.parent;

        if (enemyRoot == null)
        {
            // This object has no parent, so there is no enemy to read. Use ourselves so nothing crashes,
            // but the sprite should really be a CHILD of the enemy object.
            Debug.LogWarning($"BillboardSprite on '{name}' has no parent. Make it a child of the enemy object.", this);
            enemyRoot = transform;
        }

        agent = enemyRoot.GetComponent<NavMeshAgent>();
        health = enemyRoot.GetComponent<Health>();
    }

    void OnEnable()
    {
        if (health != null) health.Damaged += OnDamaged;
    }

    void OnDisable()
    {
        if (health != null) health.Damaged -= OnDamaged;
    }

    void Start()
    {
        cam = Camera.main.transform;
    }

    void OnDamaged(float amount)
    {
        hitTimeLeft = hitDuration;
    }

    // The front-facing standing sprite, used as a portrait on the party panel
    public Sprite Portrait
    {
        get
        {
            if (front != null && front.walk != null && front.walk.Length > 0 && front.walk[0] != null)
                return front.walk[0];
            return sr != null ? sr.sprite : null;
        }
    }

    // Colours the whole sprite (party members go green)
    public void SetTint(Color tint)
    {
        sr.color = tint;

        TargetDummy dummy = enemyRoot.GetComponent<TargetDummy>();
        if (dummy != null) dummy.SetBaseColor(tint);   // so the hit flash returns to this colour
    }

    public void ClearTint()
    {
        SetTint(originalColor);
    }

    // ---------- Called by Enemy ----------

    public void PlayKnockback(float duration)
    {
        knockbackDuration = Mathf.Max(0.01f, duration);
        knockbackTimeLeft = knockbackDuration;
    }

    public void PlayAttack(int variant, float duration)
    {
        attackVariant = Mathf.Max(0, variant);
        attackDuration = Mathf.Max(0.01f, duration);
        attackTimeLeft = attackDuration;
    }

    public void PlayBlock(float duration)
    {
        blockDuration = Mathf.Max(0.01f, duration);
        blockTimeLeft = blockDuration;
    }

    // ---------- Drawing ----------

    void LateUpdate()
    {
        if (cam == null) return;

        // Face the camera, turning around the Y axis only so the sprite stays upright
        transform.rotation = Quaternion.Euler(0f, cam.eulerAngles.y, 0f);

        if (isDead) return;

        // Count down the timed states
        hitTimeLeft = Mathf.Max(0f, hitTimeLeft - Time.deltaTime);
        knockbackTimeLeft = Mathf.Max(0f, knockbackTimeLeft - Time.deltaTime);
        attackTimeLeft = Mathf.Max(0f, attackTimeLeft - Time.deltaTime);
        blockTimeLeft = Mathf.Max(0f, blockTimeLeft - Time.deltaTime);

        // Walk cycle runs only while the agent is actually moving; stopping resets to frame 0
        bool moving = agent != null && agent.enabled && agent.velocity.sqrMagnitude > 0.04f;
        walkTime = moving ? walkTime + Time.deltaTime : 0f;

        // Which side of the enemy is the camera looking at?
        Vector3 toCam = cam.position - enemyRoot.position;
        toCam.y = 0f;
        float angle = Vector3.SignedAngle(enemyRoot.forward, toCam, Vector3.up);
        float absAngle = Mathf.Abs(angle);

        DirectionSprites set;
        bool flip = false;

        if (absAngle < 45f) set = front;
        else if (absAngle > 135f) set = back;
        else
        {
            set = side;
            flip = (angle > 0f) != flipSideSprite;
        }

        Sprite chosen = PickSprite(set);
        if (chosen != null) sr.sprite = chosen;
        sr.flipX = flip;
    }

    // Picks the sprite to show. Higher priority states come first.
    Sprite PickSprite(DirectionSprites set)
    {
        if (set == null) return null;

        // 1. Thrown back
        if (knockbackTimeLeft > 0f)
        {
            if (set.knockback != null && set.knockback.Length > 0)
                return FrameAt(set.knockback, 1f - knockbackTimeLeft / knockbackDuration);
            if (set.hit != null) return set.hit;
        }

        // 2. Just damaged
        if (hitTimeLeft > 0f && set.hit != null)
            return set.hit;

        // 3. Blocking
        if (blockTimeLeft > 0f && set.block != null && set.block.Length > 0)
            return FrameAt(set.block, 1f - blockTimeLeft / blockDuration);

        // 4. Attacking
        if (attackTimeLeft > 0f && set.attacks != null && set.attacks.Length > 0)
        {
            SpriteSequence sequence = set.attacks[attackVariant % set.attacks.Length];
            if (sequence != null && sequence.frames != null && sequence.frames.Length > 0)
                return FrameAt(sequence.frames, 1f - attackTimeLeft / attackDuration);
        }

        // 5. Walking (frame 0 when standing still)
        if (set.walk != null && set.walk.Length > 0)
        {
            int index = (int)(walkTime * walkFps) % set.walk.Length;
            return set.walk[index];
        }

        return null;
    }

    // Spreads a list of frames evenly across an animation (progress goes 0 -> 1)
    static Sprite FrameAt(Sprite[] frames, float progress)
    {
        int index = Mathf.Min((int)(progress * frames.Length), frames.Length - 1);
        return frames[Mathf.Max(0, index)];
    }

    // Called by Enemy when it dies
    public void ShowDead()
    {
        isDead = true;
        if (dead != null) sr.sprite = dead;
        sr.flipX = false;

        // Find the floor under the enemy and sit the corpse's bottom edge on it
        if (sr.sprite != null &&
            Physics.Raycast(enemyRoot.position, Vector3.down, out RaycastHit hit, 10f, ~0, QueryTriggerInteraction.Ignore))
        {
            float spriteBottom = sr.sprite.bounds.min.y * transform.lossyScale.y;
            Vector3 p = transform.position;
            p.y = hit.point.y - spriteBottom + groundOffset;
            transform.position = p;
        }
    }
}
