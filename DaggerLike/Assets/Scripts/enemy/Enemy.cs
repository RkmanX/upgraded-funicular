using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

[RequireComponent(typeof(NavMeshAgent), typeof(Health))]
public class Enemy : MonoBehaviour
{
    enum State { Idle, Wander, Interested, Staring, Chase, Attack, Search, Flee, Follow }

    [Header("Target")]
    public Transform target;              // the player. Leave empty to auto-find the object tagged "Player"

    [Header("Factions and rank")]
    public string faction = "Monsters";   // the main faction
    public string[] extraFactions;        // any number of other factions this enemy also belongs to
    public int rank = 0;                  // 0 = grunt. Higher = leader. A dead leader hurts the morale of lower ranks.
    public Attitude attitudeToPlayer = Attitude.Hate;   // used when nothing else decides (reputation, shared factions, relations)
    public FactionRelation[] relations;   // how this enemy feels about other factions. Shared faction = Like unless listed here.
    [Range(0f, 1f)] public float ignorePlayerChance = 0.6f;   // busy fighting another enemy and you hit it: chance it ignores you
    [Range(0f, 1f)] public float turnOnAttackerChance = 0.5f; // busy with the player and another enemy hits it: chance, per hit, to turn on that enemy

    [Header("Party (companion)")]
    public bool autoJoinSharedFaction = true;   // shares a faction with the player = becomes a companion
    public float followStopDistance = 2.5f;     // stays about this close to you
    public float followResumeDistance = 4f;     // starts walking after you when you get this far
    public Color partyColor = new Color(0.35f, 1f, 0.35f);
    public float loyaltyLossPerHit = 12f;       // loyalty lost each time YOU hit it (it starts at 100)
    public float loyaltyLossAllyHit = 6f;       // loyalty lost when you hit a friend of its
    public float loyaltyLossAllyKilled = 40f;   // loyalty lost when you kill a friend of its

    [Header("Senses")]
    public float viewDistance = 14f;
    public float viewAngle = 100f;        // total width of the vision cone, in degrees
    public float hearingRange = 10f;      // multiplied by the player's noise level
    public float eyeHeight = 0.5f;
    public LayerMask sightBlockers;       // set to Default so walls block line of sight

    [Header("Suspicion")]
    public float suspicionRise = 0.6f;    // base speed it fills while it can SEE you (1 = a full second)
    public float hearingRise = 1.2f;      // base speed it fills while it can HEAR you
    public float closeBonus = 3f;         // sight multiplier when you're right next to it...
    public float farPenalty = 0.3f;       // ...and at the very edge of its view distance
    public float gazeBonus = 1.5f;        // multiplier when you're dead centre in its gaze...
    public float peripheralFactor = 0.5f; // ...and at the edge of its vision cone
    public float suspicionDecay = 0.25f;  // how fast it calms down once it can't sense you
    public float decayDelay = 1f;         // seconds before calming down starts
    public float investigateAbove = 0.2f; // above this it stops what it's doing and turns toward you
    public float turnToSourceSpeed = 120f;

    [Header("Idle and wandering")]
    public bool wander = true;
    public Vector2 wanderRadiusRange = new Vector2(4f, 12f);   // a random radius between these is picked for each wander
    [Range(0f, 1f)] public float wanderChance = 0.5f;          // chance to stroll instead of just looking around
    public float wanderSpeedMultiplier = 0.4f;                 // wandering is slower than chasing
    public Vector2 idleTimeRange = new Vector2(2f, 6f);        // pause between actions
    public float lookTurnSpeed = 60f;
    public float lookAroundAngle = 120f;
    public bool randomizeFacing = true;

    [Header("Hallways")]
    public float hallwayMaxWidth = 4f;                         // narrower than this counts as a hallway
    public Vector2 hallwayWalkRange = new Vector2(6f, 16f);    // how far it walks along a hallway
    [Range(0f, 1f)] public float hallwayKeepDirection = 0.7f;  // chance to keep going the same way instead of turning back

    [Header("Friends and points of interest")]
    [Range(0f, 1f)] public float socialChance = 0.35f;         // chance to wander over to a friendly enemy
    public float socialRadius = 14f;
    public float curiosity = 1f;                               // multiplies how strongly points of interest pull it

    [Header("Combat")]
    public float attackRange = 1.8f;      // must be larger than the agent's Stopping Distance
    public float attackDamage = 10f;      // weapon damage, before strength, skill and material
    public float attackCooldown = 1.2f;
    public float attackDuration = 0.6f;   // length of the attack animation
    [Range(0.1f, 0.9f)] public float attackHitPoint = 0.5f;   // how far through the animation the hit lands
    public float attackStaminaCost = 12f;
    [Range(0f, 1f)] public float critChance = 0.1f;
    public float critMultiplier = 2f;
    public float giveUpTime = 8f;         // stops chasing a lost target after this long
    public float searchDuration = 5f;
    public float searchTurnSpeed = 90f;

    [Header("Blocking")]
    public float blockStaminaCost = 10f;
    public float blockArc = 120f;         // it can only block attackers in front of it, within this angle
    public float blockAnimDuration = 0.4f;

    [Header("Morale")]
    public bool canFlee = true;
    public float fleeBelow = 25f;         // considers running when morale drops below this
    public float rallyAbove = 45f;        // comes back to fight once morale recovers past this (and health too)
    public float moraleRecoveryPerSecond = 2f;
    public float calmDelay = 4f;          // seconds without being hurt or frightened before morale recovers
    public float painMoraleFactor = 35f;  // morale lost for taking 100% of its max health in damage
    public float maxShockPerHit = 25f;    // no single blow can cost more morale than this
    public float lowHealthFraction = 0.3f;// below this health, morale takes an extra hit
    public float allyDeathLoss = 12f;     // base morale lost when a friend dies nearby (more for dead leaders)
    public float deathHearRadius = 25f;
    public float crowdRadius = 12f;       // "nearby" for the ally bonus and for being surrounded
    public float allyBonusPerUnit = 6f;   // flat morale bonus per friendly enemy nearby...
    public float allyBonusMax = 18f;      // ...up to this much
    public float surroundPenaltyPerUnit = 8f; // morale penalty per hostile beyond the first nearby
    public float cowardiceFleeChance = 0.06f; // chance per check that a 100 cowardice enemy bolts (scales with cowardice and low health)

    [Header("Fleeing: how often it really runs")]
    [Range(0f, 1f)] public float baseFleeChance = 0.25f;   // when morale is low: chance to run for a 0 cowardice enemy (100 cowardice = always)
    public float steelTime = 4f;          // after deciding NOT to run, it won't reconsider for this long
    public Vector2 fleeHesitation = new Vector2(1f, 2f);   // seconds it freezes in fear between deciding to run and actually running

    [Header("Desperation (cornered rats fight)")]
    public float desperationRange = 7f;       // a threat this close counts as pressing it
    public float desperationPerSecond = 12f;  // how fast desperation builds while pressed (cowards build it faster)
    public float desperationDecay = 6f;       // how fast it drains once the threat is far away
    public float desperationGrace = 2.5f;     // it can't turn back until it has been running this long
    [Range(0f, 1f)] public float desperateTurnChance = 0.8f;   // chance per second to turn and fight at full desperation (rises with desperation squared)

    [Header("Fleeing and healing")]
    public float fleeSpeedMultiplier = 1.15f;
    public float fleeDistance = 15f;
    public float recoverHealthFraction = 0.6f; // stops fleeing once health is back above this
    public float healBelowFraction = 0.5f;     // uses a healing item or spell below this health
    public float healSafeDistance = 5f;        // only stops to heal when the threat is at least this far away

    [Header("Calling for help")]
    public float callOutRadius = 15f;
    [Range(0f, 1f)] public float callOutChance = 0.5f;   // base chance. Friends: x1.5 and fast. Strangers: x0.25 and slow.
    public float helpCallCooldown = 6f;   // seconds between shouts when being hit
    public bool announceEvents = true;    // shows ALERT / FLEE / HEAL lines on the readout (handy while testing)

    // ---------- Read by other scripts ----------

    // Hasn't noticed anything yet (so it can be backstabbed)
    public bool IsUnaware => state == State.Idle || state == State.Wander || state == State.Interested || state == State.Staring;

    // Hunting the PLAYER (drives the crosshair icon and combat music)
    public bool IsAlerted => !isCompanion && foeEnemy == null && (state == State.Chase || state == State.Attack || state == State.Flee);

    // Busy with anything: chasing, fighting or fleeing
    public bool IsEngaged => state == State.Chase || state == State.Attack || state == State.Flee;

    public bool IsCompanion => isCompanion;
    public bool HostileToPlayer => !isCompanion && (attitudeNow == Attitude.Hate || provokedByPlayer);
    public float Suspicion => suspicion;   // 0..1, shown next to the crosshair
    public IList<string> Memberships => memberships;
    public EnemyStats Stats => stats;
    public Sprite Portrait => visual != null ? visual.Portrait : null;
    public float HealthFraction => health.maxHealth > 0f ? health.Current / health.maxHealth : 0f;

    // Linked with an ally in a fight (used by the party link line)
    public bool IsLinkable => isActiveAndEnabled && health.Current > 0f && (state == State.Chase || state == State.Attack);
    public Vector3 LinkPoint => transform.position + Vector3.up * 0.4f;

    // The name from the LootDrop script (what you see when searching the body)
    public string DisplayName
    {
        get
        {
            if (lootDrop != null && !string.IsNullOrEmpty(lootDrop.corpseName))
                return lootDrop.corpseName;
            return name;
        }
    }

    // Every living, active enemy. Enemies add/remove themselves as they enable/disable.
    public static readonly List<Enemy> All = new List<Enemy>();

    // ---------- Internals ----------

    NavMeshAgent agent;
    Health health;
    EnemyStats stats;
    BillboardSprite visual;
    LootDrop lootDrop;
    Health targetHealth;
    PlayerController playerController;
    PlayerFactions playerFactions;
    PlayerStats playerStats;
    State state = State.Idle;

    List<string> memberships;
    Attitude attitudeNow;                       // how it feels about the player right now
    readonly List<Enemy> grudges = new List<Enemy>();   // enemies that attacked it or its friends

    // Who we're fighting. foeEnemy == null means the player.
    Enemy foeEnemy;
    bool provokedByPlayer;

    // Party
    bool isCompanion;
    bool recruited;
    bool betrayed;
    float loyalty = 100f;

    Vector3 home;
    Vector3 lastKnownPosition;
    float chaseSpeed;
    float suspicion;
    float timeSinceSensed;
    float timeSinceSeen;
    float searchTimer;
    float idleTimer;
    float idleLookYaw;
    float wanderTimer;
    float poiCheckTimer;
    float scanTimer;
    float hallwayDirection = 1f;
    float lastHelpCallTime = -100f;

    // Points of interest
    PointOfInterest currentPoi;
    PointOfInterest lastPoi;
    float poiCooldownUntil;
    float stareTimer;

    // Attacking
    float nextAttackTime;
    float attackTimer;
    bool attackHitPending;

    // Knockback / stun
    Vector3 knockDirection;
    float knockSpeed;
    float knockTimeLeft;
    float knockDuration = 0.01f;
    float stunTimeLeft;

    // Morale
    float morale;
    float allyBonus;
    float surroundPenalty;
    float crowdTimer;
    float allegianceTimer;
    float fleeCheckTimer;
    float lastShockTime = -100f;
    float fleeRepickTimer;
    float healTimer;
    int fleeFailures;
    float fleeHesitationLeft;
    float fleeRunTime;
    float desperation;      // 0..100, builds while it is being chased
    bool fightToDeath;      // once true, it never flees again
    float steelUntil;       // time until which it won't reconsider running

    static readonly Color eventColor = new Color(1f, 0.65f, 0.25f);
    static readonly Color healColor = new Color(0.4f, 1f, 0.8f);
    static readonly Color partyMessageColor = new Color(0.45f, 1f, 0.45f);
    static readonly Color missColor = new Color(0.75f, 0.75f, 0.8f);

    Transform Foe => foeEnemy != null ? foeEnemy.transform : target;
    float ViewDistance => viewDistance * stats.PerceptionMultiplier;
    float HearingRange => hearingRange * stats.PerceptionMultiplier;

    float EffectiveMorale
    {
        get
        {
            float hpFraction = HealthFraction;
            float lowHealthPenalty = hpFraction < lowHealthFraction ? (1f - hpFraction / lowHealthFraction) * 20f : 0f;
            return morale + allyBonus - surroundPenalty - lowHealthPenalty;
        }
    }

    // ================= Setup =================

    void Awake()
    {
        agent = GetComponent<NavMeshAgent>();
        health = GetComponent<Health>();
        visual = GetComponentInChildren<BillboardSprite>();
        lootDrop = GetComponent<LootDrop>();

        stats = GetComponent<EnemyStats>();
        if (stats == null) stats = gameObject.AddComponent<EnemyStats>();

        chaseSpeed = agent.speed * stats.SpeedMultiplier;
        agent.speed = chaseSpeed;

        // Everything this enemy belongs to
        memberships = new List<string>();
        if (!string.IsNullOrWhiteSpace(faction)) memberships.Add(faction.Trim());
        if (extraFactions != null)
        {
            foreach (string f in extraFactions)
                if (!string.IsNullOrWhiteSpace(f)) memberships.Add(f.Trim());
        }

        attitudeNow = attitudeToPlayer;
    }

    void OnEnable()
    {
        All.Add(this);
        health.Damaged += OnDamaged;
        health.Died += OnDied;
    }

    void OnDisable()
    {
        All.Remove(this);   // dead enemies disable this script, so corpses drop off the list
        health.Damaged -= OnDamaged;
        health.Died -= OnDied;
    }

    void Start()
    {
        if (target == null)
        {
            GameObject player = GameObject.FindWithTag("Player");
            if (player != null) target = player.transform;
        }

        if (target != null)
        {
            targetHealth = target.GetComponent<Health>();
            playerController = target.GetComponent<PlayerController>();
            playerFactions = target.GetComponent<PlayerFactions>();
            playerStats = target.GetComponent<PlayerStats>();
        }

        if (randomizeFacing)
            transform.rotation = Quaternion.Euler(0f, Random.Range(0f, 360f), 0f);

        home = transform.position;
        idleLookYaw = transform.eulerAngles.y;
        idleTimer = Random.Range(idleTimeRange.x, idleTimeRange.y);
        morale = stats.MaxMorale;

        RefreshAllegiance();   // may make it a companion straight away
    }

    // ================= Main loop =================

    void Update()
    {
        // ---------- Stunned: slide back, and do nothing else ----------
        if (stunTimeLeft > 0f)
        {
            stunTimeLeft -= Time.deltaTime;
            attackTimer = 0f;
            attackHitPending = false;

            if (knockTimeLeft > 0f)
            {
                knockTimeLeft -= Time.deltaTime;
                float strength = Mathf.Clamp01(knockTimeLeft / knockDuration);   // fast at first, then slows
                agent.Move(knockDirection * knockSpeed * strength * Time.deltaTime);
            }

            if (stunTimeLeft <= 0f && (state == State.Chase || state == State.Flee || state == State.Follow))
            {
                agent.isStopped = false;
                agent.SetDestination(state == State.Chase ? lastKnownPosition : transform.position);
            }
            return;
        }

        if (target == null) return;

        if (targetHealth != null && targetHealth.Current <= 0f)
        {
            agent.isStopped = true;
            return;
        }

        TickMorale();

        if (state == State.Flee)
        {
            FleeBehaviour();
            return;
        }

        // Our enemy died? Stop fighting it.
        if (foeEnemy != null && !IsAlive(foeEnemy))
        {
            foeEnemy = null;
            if (IsEngaged) EndEngagement();
        }

        // ---------- Sensing the player (only if we're hostile to them) ----------
        float distanceToPlayer = Vector3.Distance(transform.position, target.position);
        float rise = HostileToPlayer ? SenseTarget(distanceToPlayer) : 0f;
        bool sensed = rise > 0f;

        // ---------- Suspicion: only builds while the enemy hasn't noticed you yet ----------
        bool calm = state == State.Idle || state == State.Wander || state == State.Interested
                 || state == State.Staring || state == State.Search;

        if (calm)
        {
            if (sensed)
            {
                // A mesmerizing point of interest distracts it from you
                float distraction = 1f;
                if (state == State.Staring && currentPoi != null && currentPoi.mesmerizing)
                    distraction = currentPoi.distraction;

                suspicion = Mathf.Min(1f, suspicion + rise * distraction * Time.deltaTime);
                timeSinceSensed = 0f;

                if (suspicion >= 1f)
                {
                    foeEnemy = null;
                    StartChase(target.position);
                }
            }
            else
            {
                timeSinceSensed += Time.deltaTime;
                if (timeSinceSensed > decayDelay)
                    suspicion = Mathf.Max(0f, suspicion - suspicionDecay * Time.deltaTime);
            }
        }

        // Calm enemies (and companions) also look out for enemies of their own
        if (calm || state == State.Follow)
        {
            scanTimer -= Time.deltaTime;
            if (scanTimer <= 0f)
            {
                scanTimer = 0.4f;
                ScanForHatedEnemies();
            }
        }

        bool investigating = sensed && suspicion > investigateAbove;

        // Who are we fighting, and can we still see them?
        Transform foe = Foe;
        float foeDistance = Vector3.Distance(transform.position, foe.position);
        bool foeSensed = foeEnemy != null ? CanSeeTransform(foe, foeDistance, isCompanion) : sensed;

        switch (state)
        {
            case State.Idle:
                if (investigating) TurnToward(target.position);   // something's up: face it
                else IdleBehaviour();
                break;

            case State.Wander:
                if (investigating)
                {
                    StartIdle();
                    TurnToward(target.position);
                    break;
                }

                poiCheckTimer -= Time.deltaTime;
                if (poiCheckTimer <= 0f)
                {
                    poiCheckTimer = 1f;
                    if (TryChoosePointOfInterest()) break;
                }

                wanderTimer += Time.deltaTime;
                if (ReachedDestination() || wanderTimer > 15f)
                    StartIdle();
                break;

            case State.Interested:
                if (investigating) { StartIdle(); TurnToward(target.position); break; }
                if (currentPoi == null) { StartIdle(); break; }

                float poiDistance = FlatDistance(transform.position, currentPoi.transform.position);
                if (poiDistance <= Mathf.Max(currentPoi.arriveDistance, agent.stoppingDistance + 0.3f) || ReachedDestination())
                {
                    state = State.Staring;
                    stareTimer = currentPoi.mesmerizing ? currentPoi.stareTime : 2f;
                    agent.isStopped = true;
                    agent.ResetPath();
                }
                break;

            case State.Staring:
                if (currentPoi == null) { StartIdle(); break; }

                // Mesmerized enemies can't be drawn away by suspicion. Ordinary ones stop to look at you.
                if (investigating && !currentPoi.mesmerizing)
                {
                    StartIdle();
                    TurnToward(target.position);
                    break;
                }

                TurnToward(currentPoi.transform.position);
                stareTimer -= Time.deltaTime;
                if (stareTimer <= 0f)
                {
                    lastPoi = currentPoi;
                    poiCooldownUntil = Time.time + 20f;   // don't go straight back to the same thing
                    StartIdle();
                }
                break;

            case State.Follow:
                FollowBehaviour(distanceToPlayer);
                break;

            case State.Chase:
                if (foeSensed)
                {
                    lastKnownPosition = foe.position;
                    timeSinceSeen = 0f;
                    agent.SetDestination(lastKnownPosition);

                    if (foeDistance <= attackRange)
                        state = State.Attack;
                }
                else
                {
                    // Lost them: keep heading to the last known spot, then give up
                    timeSinceSeen += Time.deltaTime;
                    if (ReachedDestination() || timeSinceSeen > giveUpTime)
                    {
                        foeEnemy = null;
                        EndEngagement();
                    }
                }
                break;

            case State.Attack:
                agent.isStopped = true;
                FaceFoe(foe);
                AttackBehaviour(foeDistance);
                break;

            case State.Search:
                transform.Rotate(0f, searchTurnSpeed * Time.deltaTime, 0f);
                searchTimer += Time.deltaTime;
                if (searchTimer >= searchDuration)
                    StartIdle();
                break;
        }
    }

    // ================= Factions =================

    // How it feels about another enemy, going only by factions and relations
    Attitude FactionAttitudeTo(Enemy other)
    {
        Attitude? result = FactionUtil.Resolve(memberships, relations, other.memberships);
        return result ?? Attitude.Moderate;
    }

    // How it feels about another enemy right now, including grudges and the party-vs-player war
    Attitude AttitudeToEnemy(Enemy other)
    {
        // Anyone who attacked it or its friends is an enemy for now
        if (grudges.Contains(other)) return Attitude.Hate;

        // The player's party and the player's enemies are at war
        if (isCompanion && other.HostileToPlayer) return Attitude.Hate;
        if (HostileToPlayer && other.isCompanion) return Attitude.Hate;
        if (isCompanion && other.isCompanion) return Attitude.Like;

        return FactionAttitudeTo(other);
    }

    static bool IsAlive(Enemy enemy)
    {
        return enemy != null && enemy.isActiveAndEnabled && enemy.health.Current > 0f;
    }

    void AddGrudge(Enemy enemy)
    {
        if (enemy != null && enemy != this && !grudges.Contains(enemy))
            grudges.Add(enemy);
    }

    // Works out how it feels about the player, and joins or leaves the party if needed
    void RefreshAllegiance()
    {
        attitudeNow = playerFactions != null
            ? playerFactions.AttitudeOf(memberships, relations, attitudeToPlayer)
            : attitudeToPlayer;

        if (!autoJoinSharedFaction || playerFactions == null) return;

        bool sharesFaction = FactionUtil.Shares(memberships, playerFactions.memberships);

        if (!isCompanion && sharesFaction && !betrayed && attitudeNow != Attitude.Hate)
            JoinParty(false);
        else if (isCompanion && !recruited && !sharesFaction)
            LeaveParty("no longer shares a cause with you", false);
    }

    // Looks for enemies it hates in view, and starts a fight with the nearest
    void ScanForHatedEnemies()
    {
        Enemy best = null;
        float bestDistance = float.MaxValue;

        foreach (Enemy other in All)
        {
            if (other == this) continue;
            if (AttitudeToEnemy(other) != Attitude.Hate) continue;

            float distance = Vector3.Distance(transform.position, other.transform.position);
            if (distance >= bestDistance || !CanSeeTransform(other.transform, distance, isCompanion)) continue;

            best = other;
            bestDistance = distance;
        }

        if (best != null)
        {
            foeEnemy = best;
            StartChase(best.transform.position);
        }
    }

    // ================= Party =================

    public void JoinParty(bool byRecruit)
    {
        if (isCompanion || !isActiveAndEnabled) return;

        isCompanion = true;
        recruited = byRecruit;
        betrayed = false;
        loyalty = 100f;
        provokedByPlayer = false;
        foeEnemy = null;
        suspicion = 0f;
        attackTimer = 0f;
        attackHitPending = false;

        if (visual != null) visual.SetTint(partyColor);
        stats.PlayJoinSound();
        Notify($"PARTY: {DisplayName} joins your party!", partyMessageColor);

        StartFollow();
    }

    void LeaveParty(string reason, bool turnHostile)
    {
        if (!isCompanion) return;

        isCompanion = false;
        recruited = false;

        if (visual != null) visual.ClearTint();
        Notify($"PARTY: {DisplayName} {reason} and leaves your party!", partyMessageColor);

        if (turnHostile)
        {
            betrayed = true;
            provokedByPlayer = true;
            foeEnemy = null;
            StartChase(target.position, false);
        }
        else
        {
            StartIdle();
        }
    }

    // Actions the player takes can raise or lower loyalty. At 0 it turns on you.
    public void AdjustLoyalty(float amount)
    {
        if (!isCompanion) return;

        loyalty = Mathf.Clamp(loyalty + amount, 0f, 100f);
        if (loyalty <= 0f)
            LeaveParty("has lost faith in you", true);
    }

    void StartFollow()
    {
        state = State.Follow;
        agent.isStopped = false;
        agent.speed = chaseSpeed;
        agent.ResetPath();
    }

    // Stay close to the player: walk after them when they get far, stand when near
    void FollowBehaviour(float distanceToPlayer)
    {
        if (distanceToPlayer > followResumeDistance)
        {
            agent.isStopped = false;
            agent.speed = distanceToPlayer > followResumeDistance * 2.5f ? chaseSpeed * 1.3f : chaseSpeed;
            agent.SetDestination(target.position);
        }
        else if (distanceToPlayer <= followStopDistance)
        {
            agent.isStopped = true;
        }
    }

    // Done fighting: companions go back to the player, everyone else searches
    void EndEngagement()
    {
        if (isCompanion) StartFollow();
        else BeginSearch();
    }

    // ================= Senses =================

    // Returns how fast suspicion should rise this frame (0 = nothing sensed)
    float SenseTarget(float distance)
    {
        if (playerController == null) return 0f;

        // Testing cheat on the PlayerController: sneaking = completely undetectable
        if (playerController.undetectableWhileSneaking && playerController.IsSneaking)
            return 0f;

        float rise = 0f;

        // Sight: faster when you're close, visible, and right in front of its face
        if (CanSeePlayer(distance, out float gaze))
        {
            float proximity = 1f - Mathf.Clamp01(distance / ViewDistance);
            float distanceFactor = Mathf.Lerp(farPenalty, closeBonus, proximity);
            rise += suspicionRise * distanceFactor * playerController.Visibility * gaze;
        }

        // Hearing: footsteps. The louder you are, the further it carries.
        float noise = playerController.NoiseLevel;
        float heardRange = HearingRange * noise;
        if (noise > 0f && distance <= heardRange)
        {
            float closeness = 1f - distance / heardRange;   // 1 right next to it, 0 at the edge
            rise += hearingRise * noise * closeness;
        }

        return rise;
    }

    bool CanSeePlayer(float distance, out float gaze)
    {
        gaze = 0f;
        if (distance > ViewDistance) return false;

        Vector3 toTarget = target.position - transform.position;
        toTarget.y = 0f;

        float angle = Vector3.Angle(transform.forward, toTarget);
        float halfCone = viewAngle * 0.5f;
        if (angle > halfCone) return false;

        if (!HasLineOfSightTo(target)) return false;

        // Dead centre in its gaze counts for more than the corner of its eye
        gaze = Mathf.Lerp(gazeBonus, peripheralFactor, angle / halfCone);
        return true;
    }

    // A plain "can I see that?" check, used for other enemies. Companions see all around them.
    bool CanSeeTransform(Transform other, float distance, bool allAround)
    {
        if (distance > ViewDistance) return false;

        if (!allAround)
        {
            Vector3 toOther = other.position - transform.position;
            toOther.y = 0f;
            if (Vector3.Angle(transform.forward, toOther) > viewAngle * 0.5f) return false;
        }

        return HasLineOfSightTo(other);
    }

    bool HasLineOfSightTo(Transform other)
    {
        Vector3 eye = transform.position + Vector3.up * eyeHeight;
        Vector3 toOther = other.position - eye;

        if (Physics.Raycast(eye, toOther.normalized, out RaycastHit hit, toOther.magnitude + 0.1f, sightBlockers))
            return hit.transform == other || hit.transform.IsChildOf(other);

        return true;
    }

    // A missed swing in front of it makes an unaware enemy more suspicious
    public void AddSuspicion(float amount)
    {
        if (!HostileToPlayer || !(IsUnaware || state == State.Search)) return;

        suspicion = Mathf.Min(1f, suspicion + amount);

        if (suspicion >= 1f && target != null)
        {
            foeEnemy = null;
            StartChase(target.position);
        }
    }

    // ================= Idle, wandering and interests =================

    void IdleBehaviour()
    {
        agent.isStopped = true;

        // Turn toward whatever direction it decided to look
        Quaternion look = Quaternion.Euler(0f, idleLookYaw, 0f);
        transform.rotation = Quaternion.RotateTowards(transform.rotation, look, lookTurnSpeed * Time.deltaTime);

        idleTimer -= Time.deltaTime;
        if (idleTimer > 0f) return;

        idleTimer = Random.Range(idleTimeRange.x, idleTimeRange.y);

        // 1. Something interesting nearby?
        if (TryChoosePointOfInterest()) return;

        // 2. Wander over to a friend?
        if (wander && Random.value < socialChance && TryJoinFriend(out Vector3 friendSpot))
        {
            BeginWanderTo(friendSpot);
            return;
        }

        // 3. Stroll somewhere (along the hallway if it's in one)?
        if (wander && Random.value < wanderChance && TryPickWanderPoint(out Vector3 point))
        {
            BeginWanderTo(point);
            return;
        }

        // 4. Otherwise just look somewhere else for a while
        idleLookYaw = transform.eulerAngles.y + Random.Range(-lookAroundAngle, lookAroundAngle);
    }

    void BeginWanderTo(Vector3 point)
    {
        state = State.Wander;
        wanderTimer = 0f;
        agent.isStopped = false;
        agent.speed = chaseSpeed * wanderSpeedMultiplier;
        agent.SetDestination(point);
    }

    bool TryPickWanderPoint(out Vector3 point)
    {
        // In a narrow hallway? Walk along it, like exploring or guarding.
        if (TryHallwayPoint(out point)) return true;

        // Otherwise pick a spot around home, using a random radius from the range
        float radius = Random.Range(wanderRadiusRange.x, wanderRadiusRange.y);
        Vector2 offset = Random.insideUnitCircle * radius;
        Vector3 random = home + new Vector3(offset.x, 0f, offset.y);

        if (NavMesh.SamplePosition(random, out NavMeshHit hit, 2f, NavMesh.AllAreas))
        {
            point = hit.position;
            return true;
        }

        point = transform.position;
        return false;
    }

    // Measures how wide the walkable space is. If it's narrow, returns a spot further along it.
    bool TryHallwayPoint(out Vector3 point)
    {
        point = transform.position;

        if (!NavMesh.SamplePosition(transform.position, out NavMeshHit here, 1.5f, NavMesh.AllAreas)) return false;
        if (!NavMesh.FindClosestEdge(here.position, out NavMeshHit edge, NavMesh.AllAreas)) return false;

        // From the nearest wall toward us = straight across the hallway
        Vector3 across = here.position - edge.position;
        across.y = 0f;
        if (across.sqrMagnitude < 0.0001f) return false;
        across.Normalize();

        // Keep going across to find the opposite wall, which tells us the width
        float limit = hallwayMaxWidth + 2f;
        if (!NavMesh.Raycast(here.position, here.position + across * limit, out NavMeshHit opposite, NavMesh.AllAreas))
            return false;   // no wall on the other side within range: a wide room, not a hallway

        float width = edge.distance + opposite.distance;
        if (width > hallwayMaxWidth) return false;

        // The hallway runs along the walls, at right angles to "across"
        Vector3 along = Vector3.Cross(Vector3.up, across).normalized;

        if (Random.value > hallwayKeepDirection)
            hallwayDirection = -hallwayDirection;

        for (int attempt = 0; attempt < 2; attempt++)
        {
            Vector3 direction = along * hallwayDirection;
            float distance = Random.Range(hallwayWalkRange.x, hallwayWalkRange.y);
            Vector3 end = here.position + direction * distance;

            // Stop short of a dead end
            if (NavMesh.Raycast(here.position, end, out NavMeshHit blocked, NavMesh.AllAreas))
                end = blocked.position - direction * 0.5f;

            if (Vector3.Distance(here.position, end) >= 2.5f &&
                NavMesh.SamplePosition(end, out NavMeshHit endHit, 1.5f, NavMesh.AllAreas))
            {
                point = endHit.position;
                return true;
            }

            hallwayDirection = -hallwayDirection;   // dead end this way: try the other way
        }

        return false;
    }

    // Picks a spot near a friendly enemy of equal or higher rank (so followers tag along behind leaders)
    bool TryJoinFriend(out Vector3 spot)
    {
        spot = transform.position;

        Enemy best = null;
        float bestDistance = float.MaxValue;

        foreach (Enemy other in All)
        {
            if (other == this || !other.IsUnaware) continue;
            if (AttitudeToEnemy(other) != Attitude.Like) continue;
            if (other.rank < rank) continue;

            float distance = Vector3.Distance(transform.position, other.transform.position);
            if (distance > socialRadius || distance < 3f || distance >= bestDistance) continue;

            best = other;
            bestDistance = distance;
        }

        if (best == null) return false;

        Vector2 ring = Random.insideUnitCircle.normalized * Random.Range(2f, 3.5f);
        Vector3 candidate = best.transform.position + new Vector3(ring.x, 0f, ring.y);

        if (NavMesh.SamplePosition(candidate, out NavMeshHit hit, 2f, NavMesh.AllAreas))
        {
            spot = hit.position;
            return true;
        }

        return false;
    }

    bool TryChoosePointOfInterest()
    {
        PointOfInterest best = null;
        float bestScore = 0f;

        foreach (PointOfInterest poi in PointOfInterest.All)
        {
            if (poi == null) continue;
            if (poi == lastPoi && Time.time < poiCooldownUntil) continue;

            float distance = Vector3.Distance(transform.position, poi.transform.position);
            if (distance > poi.noticeRadius) continue;

            // Stronger attraction and a closer object both make it more likely
            float score = poi.attraction * curiosity * Mathf.Lerp(1f, 0.5f, distance / poi.noticeRadius);
            if (score > bestScore)
            {
                best = poi;
                bestScore = score;
            }
        }

        if (best == null || Random.value > bestScore) return false;

        currentPoi = best;
        state = State.Interested;
        agent.isStopped = false;
        agent.speed = chaseSpeed * wanderSpeedMultiplier * 1.5f;
        agent.SetDestination(best.transform.position);
        return true;
    }

    void StartIdle()
    {
        if (isCompanion)
        {
            StartFollow();
            return;
        }

        state = State.Idle;
        desperation = 0f;
        fightToDeath = false;
        agent.isStopped = true;
        agent.ResetPath();
        agent.speed = chaseSpeed;
        idleTimer = Random.Range(idleTimeRange.x, idleTimeRange.y);
        idleLookYaw = transform.eulerAngles.y;
    }

    void TurnToward(Vector3 point)
    {
        Vector3 direction = point - transform.position;
        direction.y = 0f;
        if (direction.sqrMagnitude < 0.001f) return;

        Quaternion look = Quaternion.LookRotation(direction);
        transform.rotation = Quaternion.RotateTowards(transform.rotation, look, turnToSourceSpeed * Time.deltaTime);
        idleLookYaw = transform.eulerAngles.y;   // so it doesn't snap back afterwards
    }

    static float FlatDistance(Vector3 a, Vector3 b)
    {
        a.y = 0f;
        b.y = 0f;
        return Vector3.Distance(a, b);
    }

    // ================= State changes =================

    void StartChase(Vector3 position, bool callForHelp = true)
    {
        // A companion never chases the player: with no enemy to fight, it just follows
        if (isCompanion && foeEnemy == null)
        {
            StartFollow();
            return;
        }

        bool wasUnaware = state == State.Idle || state == State.Wander || state == State.Interested
                       || state == State.Staring || state == State.Search || state == State.Follow;

        state = State.Chase;
        suspicion = 1f;
        agent.isStopped = false;
        agent.speed = chaseSpeed;
        lastKnownPosition = position;
        timeSinceSeen = 0f;
        agent.SetDestination(position);

        // Newly alerted? Shout to anyone nearby
        if (wasUnaware && callForHelp)
            CallForHelp(foeEnemy);
    }

    void BeginSearch()
    {
        state = State.Search;
        searchTimer = 0f;
        suspicion = 0.6f;     // still on edge
        timeSinceSensed = 0f;
        attackTimer = 0f;
        attackHitPending = false;
        agent.isStopped = true;
        agent.ResetPath();
        agent.speed = chaseSpeed;
    }

    bool ReachedDestination()
    {
        if (agent.pathPending) return false;
        return agent.remainingDistance <= agent.stoppingDistance + 0.2f;
    }

    // ================= Calling for help =================

    // Shouts to nearby friends. aboutFoe = the enemy being fought, or null for the player.
    void CallForHelp(Enemy aboutFoe)
    {
        lastHelpCallTime = Time.time;
        int answered = 0;

        foreach (Enemy other in All)
        {
            if (other == this || other.IsEngaged) continue;
            if (Vector3.Distance(transform.position, other.transform.position) > callOutRadius) continue;

            Attitude attitude = AttitudeToEnemy(other);
            if (attitude == Attitude.Hate) continue;

            // Friends answer more often, and much faster. Strangers rarely, and slowly.
            float chance = attitude == Attitude.Like ? Mathf.Min(1f, callOutChance * 1.5f) : callOutChance * 0.25f;
            if (Random.value > chance) continue;

            float delay = attitude == Attitude.Like ? Random.Range(0.1f, 0.5f) : Random.Range(1.5f, 3.5f);
            other.HearCallOut(this, lastKnownPosition, aboutFoe, delay);
            answered++;
        }

        if (answered > 0)
            Announce($"ALERT: {DisplayName} called out ({answered} answered)", eventColor);
    }

    // Another enemy shouted. After a delay, join in if it concerns us.
    public void HearCallOut(Enemy caller, Vector3 where, Enemy callerFoe, float delay)
    {
        if (!isActiveAndEnabled || IsEngaged) return;
        StartCoroutine(CallOutRoutine(caller, where, callerFoe, delay));
    }

    IEnumerator CallOutRoutine(Enemy caller, Vector3 where, Enemy callerFoe, float delay)
    {
        yield return new WaitForSeconds(delay);

        if (IsEngaged || !IsAlive(caller)) yield break;   // got busy, or the caller is already gone

        if (callerFoe != null)
        {
            // The caller is fighting another enemy. Join in unless we're friends with that enemy.
            if (!IsAlive(callerFoe) || AttitudeToEnemy(callerFoe) == Attitude.Like) yield break;

            AddGrudge(callerFoe);
            foeEnemy = callerFoe;
            StartChase(where, false);   // false = don't shout again, so alerts don't chain forever
        }
        else if (HostileToPlayer)
        {
            foeEnemy = null;
            StartChase(where, false);
        }
        else
        {
            yield break;
        }

        // Show that they've grouped up
        PartyLinks.Connect(caller, this);
    }

    // ================= Attacking and blocking =================

    void FaceFoe(Transform foe)
    {
        Vector3 direction = foe.position - transform.position;
        direction.y = 0f;
        if (direction.sqrMagnitude < 0.001f) return;

        Quaternion look = Quaternion.LookRotation(direction);
        transform.rotation = Quaternion.Slerp(transform.rotation, look, 10f * Time.deltaTime);
    }

    void AttackBehaviour(float foeDistance)
    {
        // Mid-swing: wait for the hit moment in the animation
        if (attackTimer > 0f)
        {
            attackTimer -= Time.deltaTime;

            if (attackHitPending && attackTimer <= attackDuration * (1f - attackHitPoint))
            {
                attackHitPending = false;
                ResolveAttack();
            }
            return;
        }

        // Target moved out of reach: go after it again
        if (foeDistance > attackRange * 1.2f)
        {
            StartChase(Foe.position, false);
            return;
        }

        if (Time.time < nextAttackTime) return;

        // Swinging costs stamina. Too tired and it can't attack (and it pants).
        if (!stats.TrySpendStamina(attackStaminaCost)) return;

        BeginAttack();
    }

    void BeginAttack()
    {
        nextAttackTime = Time.time + attackCooldown / stats.AttackSpeedMultiplier;
        attackTimer = attackDuration;
        attackHitPending = true;

        if (visual != null)
        {
            int variants = visual.AttackVariantCount;
            visual.PlayAttack(variants > 0 ? Random.Range(0, variants) : 0, attackDuration);
        }
    }

    // The moment the swing actually lands
    void ResolveAttack()
    {
        // Companions only ever fight enemies, never the player
        if (isCompanion && foeEnemy == null) return;

        Transform foe = Foe;
        if (foe == null) return;

        // Did the target step out of reach during the windup?
        if (Vector3.Distance(transform.position, foe.position) > attackRange * 1.4f)
            return;

        AttackInfo info = stats.AttackInfo;
        int skill = stats.AttackSkillValue;
        int agility = stats.attributes.agility;

        float totalCritChance = Mathf.Clamp01(critChance + stats.CritBonus);
        bool critical = Random.value < totalCritChance;
        float damage = stats.RollDamage(attackDamage) * (critical ? critMultiplier : 1f);

        if (foeEnemy != null)
        {
            // Skill and agility decide whether it connects, then weaknesses decide how hard
            if (Random.value > foeEnemy.ChanceToBeHit(info, skill, agility))
                return;

            damage *= foeEnemy.DamageMultiplierFor(info, out string note);
            foeEnemy.ReceiveEnemyAttack(this, damage, critical);
        }
        else if (targetHealth != null)
        {
            // The player's agility lets them dodge
            int playerAgility = playerStats != null ? playerStats.attributes.agility : 50;
            float hitChance = Mathf.Clamp(CombatMath.BaseHitChance(skill, agility) - CombatMath.DodgeChance(playerAgility), 0.15f, 0.97f);

            if (Random.value > hitChance)
            {
                Notify($"DODGED: you avoided {DisplayName}'s attack", missColor);
                return;
            }

            targetHealth.TakeDamage(damage, gameObject);

            // Let the player react: red flash, flinch, and recoil + stun on a crit
            PlayerHitReaction reaction = target.GetComponent<PlayerHitReaction>();
            if (reaction != null)
                reaction.OnHit(damage, critical, transform.position);
        }
    }

    // How likely an attack is to land on this enemy: the attacker's skill and agility,
    // helped by weaknesses and hurt by resistances, then reduced by this enemy's own dodge.
    public float ChanceToBeHit(AttackInfo info, int attackerSkill, int attackerAgility)
    {
        float chance = CombatMath.BaseHitChance(attackerSkill, attackerAgility)
                     + stats.MatchupHitBonus(info)
                     - CombatMath.DodgeChance(stats.attributes.agility);

        return Mathf.Clamp(chance, 0.15f, 0.97f);
    }

    public float DamageMultiplierFor(AttackInfo info, out string note)
    {
        return stats.MatchupDamageMultiplier(info, out note);
    }

    // Another enemy swings at us
    public void ReceiveEnemyAttack(Enemy attacker, float damage, bool critical)
    {
        bool blocked = TryBlock(attacker.transform.position, damage, out float remaining);
        health.TakeDamage(remaining, attacker.gameObject);

        if (critical && !blocked && health.Current > 0f)
        {
            Vector3 push = transform.position - attacker.transform.position;
            Knockback(push, 1.5f, 0.5f, 1f);
        }
    }

    // Tries to block a hit. Skill decides how often it works, cowardice makes it less likely,
    // and every block costs stamina. A tired enemy can't block at all.
    public bool TryBlock(Vector3 attackerPosition, float incomingDamage, out float remainingDamage)
    {
        remainingDamage = incomingDamage;

        bool alert = state == State.Chase || state == State.Attack;
        if (!alert || attackTimer > 0f || stunTimeLeft > 0f || stats.Exhausted) return false;

        // It can only block what it can see coming
        Vector3 toAttacker = attackerPosition - transform.position;
        toAttacker.y = 0f;
        if (Vector3.Angle(transform.forward, toAttacker) > blockArc * 0.5f) return false;

        if (Random.value > stats.BlockChance) return false;

        float cost = blockStaminaCost + incomingDamage * 0.25f;
        if (!stats.TrySpendStamina(cost)) return false;   // out of breath

        remainingDamage = incomingDamage * (1f - stats.BlockReduction);

        if (visual != null) visual.PlayBlock(blockAnimDuration);
        return true;
    }

    // ================= Getting hurt =================

    void OnDamaged(float amount)
    {
        // Pain shakes morale
        float fraction = health.maxHealth > 0f ? amount / health.maxHealth : 0f;
        float willpowerFactor = Mathf.Lerp(1.3f, 0.7f, stats.attributes.willpower / 100f);
        ApplyMoraleShock(Mathf.Min(fraction * painMoraleFactor * willpowerFactor, maxShockPerHit));

        GameObject attacker = health.LastAttacker;
        Enemy enemyAttacker = attacker != null ? attacker.GetComponent<Enemy>() : null;
        bool byPlayer = attacker != null && target != null
                     && (attacker.transform == target || attacker.transform.IsChildOf(target));

        bool fightingPlayer = !isCompanion && foeEnemy == null && (state == State.Chase || state == State.Attack);
        bool fightingEnemy = foeEnemy != null && IsAlive(foeEnemy) && (state == State.Chase || state == State.Attack);

        if (byPlayer)
            HandleHitByPlayer(fightingPlayer, fightingEnemy);
        else if (enemyAttacker != null && enemyAttacker != this)
            HandleHitByEnemy(enemyAttacker, fightingPlayer, fightingEnemy);
        else if (HostileToPlayer && state != State.Flee && !fightingPlayer && !fightingEnemy)
            StartChase(target.position);   // damage from something unknown: assume it's the player

        CheckFlee();
    }

    void HandleHitByPlayer(bool fightingPlayer, bool fightingEnemy)
    {
        // Hitting a companion shakes its loyalty. It doesn't fight back until it has had enough.
        if (isCompanion)
        {
            AdjustLoyalty(-loyaltyLossPerHit);
            return;
        }

        provokedByPlayer = true;

        // The player's companions don't like it when you hurt their friends
        foreach (Enemy other in All)
        {
            if (other.isCompanion && other != this && other.FactionAttitudeTo(this) == Attitude.Like)
                other.AdjustLoyalty(-other.loyaltyLossAllyHit);
        }

        // Too busy with another enemy to hit back at the player?
        if (fightingEnemy && Random.value < ignorePlayerChance)
            return;

        if (state != State.Flee)
        {
            foeEnemy = null;
            if (!fightingPlayer) StartChase(target.position);
        }
    }

    void HandleHitByEnemy(Enemy attacker, bool fightingPlayer, bool fightingEnemy)
    {
        AddGrudge(attacker);

        // Shout for friends to swarm whoever hit us (not on every single hit)
        if (Time.time - lastHelpCallTime > helpCallCooldown)
            CallForHelp(attacker);

        if (state == State.Flee || !IsAlive(attacker)) return;

        if (fightingPlayer)
        {
            // Busy with the player. On each hit, decide: keep chasing the player, or turn on this new attacker?
            if (Random.value < turnOnAttackerChance)
            {
                foeEnemy = attacker;
                StartChase(attacker.transform.position, false);
                Announce($"TURNED: {DisplayName} turns on {attacker.DisplayName}", eventColor);
            }
        }
        else if (!fightingEnemy)
        {
            foeEnemy = attacker;
            StartChase(attacker.transform.position, false);
        }
        // Otherwise it's already fighting another enemy, and stays on it
    }

    // Heavy hits (crit/backstab) shove the enemy back and stun it
    public void Knockback(Vector3 direction, float distance, float slideDuration, float stunDuration)
    {
        stunTimeLeft = Mathf.Max(stunDuration, slideDuration);
        agent.isStopped = true;   // stop it walking its own path while it's being shoved

        direction.y = 0f;
        if (direction.sqrMagnitude > 0.001f && slideDuration > 0f && distance > 0f)
        {
            knockDirection = direction.normalized;
            knockDuration = slideDuration;
            knockTimeLeft = slideDuration;

            // Speed fades to zero over the duration, so the average speed is half the start speed.
            // Starting at 2 x distance / duration makes the total slide equal `distance`.
            knockSpeed = 2f * distance / slideDuration;
        }

        // The knockback animation plays for the whole stun
        if (visual != null) visual.PlayKnockback(stunTimeLeft);
    }

    void OnDied()
    {
        bool killedByPlayer = health.LastAttacker != null && target != null
            && (health.LastAttacker.transform == target || health.LastAttacker.transform.IsChildOf(target));

        foreach (Enemy other in All)
        {
            if (other == this) continue;

            // Companions don't like you killing their friends
            if (killedByPlayer && other.isCompanion && other.FactionAttitudeTo(this) == Attitude.Like)
                other.AdjustLoyalty(-other.loyaltyLossAllyKilled);

            // Everyone nearby who liked this enemy may lose heart
            other.WitnessDeath(this);
        }

        StopAllCoroutines();

        // Stop moving, stop being hit, show the corpse sprite, and switch the AI off
        agent.enabled = false;

        foreach (Collider c in GetComponents<Collider>())
            c.enabled = false;

        if (visual != null) visual.ShowDead();

        enabled = false;
    }

    // ================= Morale and fleeing =================

    void ApplyMoraleShock(float amount)
    {
        if (amount <= 0f) return;

        morale = Mathf.Max(0f, morale - amount);
        lastShockTime = Time.time;
    }

    // Called on every other enemy when one dies. Only friends' deaths matter:
    // other factions losing wouldn't bother it.
    public void WitnessDeath(Enemy dead)
    {
        if (FactionAttitudeTo(dead) != Attitude.Like) return;

        float distance = Vector3.Distance(transform.position, dead.transform.position);
        if (distance > deathHearRadius) return;

        float proximity = 1f - distance / deathHearRadius;   // closer = heard it better
        int rankGap = Mathf.Max(0, dead.rank - rank);        // a dead leader hurts more

        float loss = allyDeathLoss * (1f + rankGap) * Mathf.Lerp(0.3f, 1f, proximity);
        ApplyMoraleShock(loss);
        CheckFlee();
    }

    void TickMorale()
    {
        // Who's standing nearby?
        crowdTimer -= Time.deltaTime;
        if (crowdTimer <= 0f)
        {
            crowdTimer = 0.25f;
            RefreshCrowd();
        }

        // Who does it like, and is it a party member?
        allegianceTimer -= Time.deltaTime;
        if (allegianceTimer <= 0f)
        {
            allegianceTimer = 0.5f;
            RefreshAllegiance();
            if (grudges.Count > 0) grudges.RemoveAll(e => !IsAlive(e));
        }

        // Morale recovers once nothing has frightened it for a few seconds
        if (state != State.Flee && Time.time - lastShockTime > calmDelay)
            morale = Mathf.Min(stats.MaxMorale, morale + moraleRecoveryPerSecond * Time.deltaTime);

        fleeCheckTimer -= Time.deltaTime;
        if (fleeCheckTimer <= 0f)
        {
            fleeCheckTimer = 1f;
            CheckFlee();
        }
    }

    // Friends (same or liked factions) nearby give a flat morale bonus.
    // Being surrounded by hostiles (the player counts as one) takes it away. Strangers do nothing.
    void RefreshCrowd()
    {
        int friends = 0;
        int hostiles = 0;

        foreach (Enemy other in All)
        {
            if (other == this) continue;
            if (Vector3.Distance(transform.position, other.transform.position) > crowdRadius) continue;

            if (AttitudeToEnemy(other) == Attitude.Hate) hostiles++;
            else if (FactionAttitudeTo(other) == Attitude.Like || (isCompanion && other.isCompanion)) friends++;
        }

        if (target != null && HostileToPlayer &&
            Vector3.Distance(transform.position, target.position) <= crowdRadius)
            hostiles++;

        allyBonus = Mathf.Min(allyBonusMax, friends * allyBonusPerUnit);
        surroundPenalty = Mathf.Max(0, hostiles - 1) * surroundPenaltyPerUnit;
    }

    // Decides whether it's time to run. Low morale only makes it CONSIDER running.
    void CheckFlee()
    {
        if (!canFlee || fightToDeath) return;
        if (state != State.Chase && state != State.Attack) return;

        float hpFraction = HealthFraction;
        float willpowerFactor = Mathf.Lerp(1.2f, 0.7f, stats.attributes.willpower / 100f);
        bool wantsToRun = false;

        if (EffectiveMorale <= fleeBelow && Time.time >= steelUntil)
        {
            // Brave enemies usually stand their ground, cowardly ones usually bolt
            float chance = (baseFleeChance + stats.Cowardice01 * (1f - baseFleeChance)) * willpowerFactor;
            if (Random.value < Mathf.Clamp01(chance))
                wantsToRun = true;
            else
                steelUntil = Time.time + steelTime;   // it steels itself and won't reconsider for a while
        }

        // Cowardly enemies may also just bolt, more so when they're hurt
        if (!wantsToRun)
        {
            float panicChance = stats.Cowardice01 * cowardiceFleeChance * (1.5f - hpFraction);
            if (Random.value < panicChance)
                wantsToRun = true;
        }

        if (wantsToRun)
            StartFlee();
    }

    void StartFlee()
    {
        state = State.Flee;
        attackTimer = 0f;
        attackHitPending = false;
        fleeFailures = 0;
        fleeRepickTimer = 0f;
        fleeRunTime = 0f;
        healTimer = 1f;
        desperation = 0f;

        // It decides to run now, but freezes in fear for a moment before it actually bolts
        fleeHesitationLeft = Random.Range(fleeHesitation.x, fleeHesitation.y);
        agent.isStopped = true;
        agent.speed = chaseSpeed * fleeSpeedMultiplier;

        stats.PlayFleeSound();
        Announce($"FLEE: {DisplayName} loses its nerve!", eventColor);
    }

    void FleeBehaviour()
    {
        Transform threat = Foe;
        float threatDistance = Vector3.Distance(transform.position, threat.position);

        // Calms down and slowly heals while it runs
        morale = Mathf.Min(stats.MaxMorale, morale + moraleRecoveryPerSecond * Time.deltaTime);
        health.Heal(stats.HealthRegenPerSecond * Time.deltaTime);

        float hpFraction = HealthFraction;

        // Frozen in fear for a moment before it bolts
        if (fleeHesitationLeft > 0f)
        {
            fleeHesitationLeft -= Time.deltaTime;
            agent.isStopped = true;

            if (fleeHesitationLeft <= 0f)
            {
                agent.isStopped = false;
                fleeRepickTimer = 0f;
            }
            return;
        }

        fleeRunTime += Time.deltaTime;

        // Once it's far enough away, stop to use a healing item or spell
        healTimer -= Time.deltaTime;
        if (healTimer <= 0f && hpFraction < healBelowFraction && threatDistance > healSafeDistance)
        {
            healTimer = 1.5f;
            if (stats.TryHealSelf(health, out string how))
                Announce($"HEAL: {DisplayName} {how}", healColor);
        }

        // Being chased makes it desperate. Cowards panic harder. Getting away calms it down.
        bool pressed = threatDistance < desperationRange;
        if (pressed)
            desperation += desperationPerSecond * (0.5f + stats.Cowardice01) * Time.deltaTime;
        else
            desperation -= desperationDecay * Time.deltaTime;
        desperation = Mathf.Clamp(desperation, 0f, 100f);

        // The more desperate, the more likely it is to turn and fight (but not in the first moments of running)
        float desperate01 = desperation / 100f;
        float turnChancePerSecond = desperate01 * desperate01 * desperateTurnChance;
        if (pressed && fleeRunTime >= desperationGrace &&
            (desperation >= 100f || Random.value < turnChancePerSecond * Time.deltaTime))
        {
            TurnAndFight("has no other choice");
            return;
        }

        // Recovered? Go back to being wary
        if (hpFraction >= recoverHealthFraction && EffectiveMorale >= rallyAbove)
        {
            Announce($"RALLY: {DisplayName} has recovered", eventColor);
            EndEngagement();
            return;
        }

        // Keep running: pick a new spot when we arrive, or every so often
        fleeRepickTimer -= Time.deltaTime;
        if (ReachedDestination() || fleeRepickTimer <= 0f)
        {
            fleeRepickTimer = 1.5f;

            if (TryPickFleePoint(threat.position, out Vector3 point))
            {
                fleeFailures = 0;
                agent.SetDestination(point);
            }
            else if (++fleeFailures >= 3)
            {
                // Nowhere left to run: turn and fight
                TurnAndFight("is cornered");
            }
        }
    }

    // Gives up running and fights until one of them is dead
    void TurnAndFight(string why)
    {
        fightToDeath = true;
        desperation = 100f;
        stats.PlayDesperateSound();
        Announce($"DESPERATE: {DisplayName} {why} and fights to the death!", eventColor);
        StartChase(Foe.position, false);
    }

    bool TryPickFleePoint(Vector3 threatPosition, out Vector3 point)
    {
        Vector3 away = transform.position - threatPosition;
        away.y = 0f;
        if (away.sqrMagnitude < 0.01f) away = -transform.forward;
        away.Normalize();

        float currentDistance = Vector3.Distance(transform.position, threatPosition);

        // Try straight away first, then fan out to the sides
        for (int i = 0; i < 6; i++)
        {
            float angle = i == 0 ? 0f : Random.Range(-70f, 70f);
            Vector3 direction = Quaternion.Euler(0f, angle, 0f) * away;
            Vector3 candidate = transform.position + direction * fleeDistance;

            if (NavMesh.SamplePosition(candidate, out NavMeshHit hit, 3f, NavMesh.AllAreas) &&
                Vector3.Distance(hit.position, threatPosition) > currentDistance + 2f)
            {
                point = hit.position;
                return true;
            }
        }

        point = transform.position;
        return false;
    }

    // ================= Misc =================

    // Debug-style event lines (can be switched off with Announce Events)
    void Announce(string message, Color color)
    {
        if (!announceEvents) return;
        Notify(message, color);
    }

    // Always shown: combat results and party news
    void Notify(string message, Color color)
    {
        if (DamageReadout.Instance != null)
            DamageReadout.Instance.ShowMessage(message, color);
    }

    // Select the enemy in the Scene view to see its senses
    void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.yellow;
        Vector3 left = Quaternion.Euler(0f, -viewAngle * 0.5f, 0f) * transform.forward;
        Vector3 right = Quaternion.Euler(0f, viewAngle * 0.5f, 0f) * transform.forward;
        Gizmos.DrawRay(transform.position, left * viewDistance);
        Gizmos.DrawRay(transform.position, right * viewDistance);

        Gizmos.color = Color.cyan;
        Gizmos.DrawWireSphere(transform.position, hearingRange);

        Gizmos.color = Color.green;
        Gizmos.DrawWireSphere(Application.isPlaying ? home : transform.position, wanderRadiusRange.y);

        Gizmos.color = new Color(1f, 0.5f, 0f, 0.6f);
        Gizmos.DrawWireSphere(transform.position, crowdRadius);
    }
}
