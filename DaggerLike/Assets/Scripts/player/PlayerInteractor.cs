using UnityEngine;
using UnityEngine.InputSystem;

// Put this on the Player. Press E to use what you're looking at, or what you're standing next to.
[RequireComponent(typeof(PlayerInventory))]
public class PlayerInteractor : MonoBehaviour
{
    public Transform cameraPivot;        // the same camera object used by PlayerController
    public float interactRange = 2.5f;   // how far the aiming check reaches
    public float aimRadius = 0.15f;      // a slightly "fat" ray, so you don't need pixel-perfect aim

    [Header("Standing next to it")]
    public bool useNearbyFallback = true;   // if you're not aiming at anything, use the closest thing around your feet
    public float nearbyRadius = 1.8f;

    public bool debugLogs = true;        // prints to the Console when the target changes

    public PlayerInventory Inventory { get; private set; }

    readonly RaycastHit[] hits = new RaycastHit[16];
    IInteractable lastTarget;

    void Awake()
    {
        Inventory = GetComponent<PlayerInventory>();

        // The most common mistake: forgetting to drag the camera into the slot
        if (cameraPivot == null)
        {
            Camera cam = Camera.main;
            if (cam != null)
            {
                cameraPivot = cam.transform;
                Debug.LogWarning("PlayerInteractor: the Camera Pivot slot was empty, so I used the Main Camera. Drag your camera into the slot to remove this warning.", this);
            }
            else
            {
                Debug.LogError("PlayerInteractor: no Camera Pivot assigned and no Main Camera found, so aiming can't work.", this);
            }
        }
    }

    void Start()
    {
        Debug.Log($"PlayerInteractor is running on '{name}' (camera: '{(cameraPivot != null ? cameraPivot.name : "none")}'). Press E near a body or item.");

        if (InteractPrompt.Instance == null)
            Debug.LogWarning("PlayerInteractor: there is no InteractPrompt in the scene. Add the InteractPrompt script to your Canvas to see the '[E] ...' text.", this);
    }

    void Update()
    {
        IInteractable target = FindAimedTarget();

        if (target == null && useNearbyFallback)
            target = FindNearbyTarget();

        string prompt = target != null ? target.GetPrompt() : null;
        bool canUse = !string.IsNullOrEmpty(prompt);

        if (debugLogs && target != lastTarget)
        {
            if (canUse) Debug.Log("PlayerInteractor: target is now -> " + prompt);
            lastTarget = target;
        }

        if (InteractPrompt.Instance != null)
        {
            if (canUse) InteractPrompt.Instance.Show(prompt);
            else InteractPrompt.Instance.Hide();
        }

        if (canUse && Keyboard.current.eKey.wasPressedThisFrame)
            target.Interact(this);
    }

    // Looks along the camera for something usable. Solid walls in the way block it.
    IInteractable FindAimedTarget()
    {
        if (cameraPivot == null) return null;

        Ray ray = new Ray(cameraPivot.position, cameraPivot.forward);

        // Collide = also detect trigger colliders, which is what corpses and items use
        int count = Physics.SphereCastNonAlloc(ray, aimRadius, hits, interactRange, ~0, QueryTriggerInteraction.Collide);

        IInteractable best = null;
        float bestDistance = float.MaxValue;
        float blockedAt = float.MaxValue;

        for (int i = 0; i < count; i++)
        {
            RaycastHit hit = hits[i];
            IInteractable candidate = hit.collider.GetComponentInParent<IInteractable>();

            if (candidate != null)
            {
                if (hit.distance < bestDistance)
                {
                    best = candidate;
                    bestDistance = hit.distance;
                }
            }
            else if (!hit.collider.isTrigger)
            {
                // A solid thing that isn't usable (a wall, a live enemy) blocks the view
                blockedAt = Mathf.Min(blockedAt, hit.distance);
            }
        }

        return bestDistance < blockedAt ? best : null;
    }

    // The closest usable thing within reach of the player's feet ("press E over the body")
    IInteractable FindNearbyTarget()
    {
        IInteractable best = null;
        float bestDistance = nearbyRadius;

        foreach (IInteractable candidate in InteractableRegistry.All)
        {
            if (string.IsNullOrEmpty(candidate.GetPrompt())) continue;

            Vector3 offset = candidate.Position - transform.position;
            offset.y = 0f;   // ignore height, so standing on top of it counts
            float distance = offset.magnitude;

            if (distance < bestDistance)
            {
                best = candidate;
                bestDistance = distance;
            }
        }

        return best;
    }
}
