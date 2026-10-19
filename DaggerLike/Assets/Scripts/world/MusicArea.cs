using UnityEngine;

// Put this on its OWN empty GameObject with a collider (a Box Collider is easiest).
// While the player is inside, this area's music plays. Leave Music empty for a silent zone.
public class MusicArea : MonoBehaviour
{
    public AudioClip music;
    public bool debugLogs = true;   // prints what enters the area to the Console

    void Awake()
    {
        Collider[] colliders = GetComponents<Collider>();

        if (colliders.Length == 0)
        {
            Debug.LogWarning($"MusicArea '{name}' has no collider on this same GameObject. Add a Box Collider here.", this);
            return;
        }

        foreach (Collider c in colliders)
        {
            // Only CONVEX mesh colliders are allowed to be triggers, so switch that on first
            if (c is MeshCollider mesh) mesh.convex = true;
            c.isTrigger = true;
        }
    }

    void Start()
    {
        if (MusicManager.Instance == null)
            Debug.LogWarning("No MusicManager in the scene, so area music can't play.", this);
    }

    // Recognise the player by its PlayerController script instead of a tag,
    // so this works even if the Player object's tag isn't set
    bool IsPlayer(Collider other)
    {
        return other.GetComponentInParent<PlayerController>() != null;
    }

    void OnTriggerEnter(Collider other)
    {
        if (debugLogs)
            Debug.Log($"MusicArea '{name}': '{other.name}' (tag: {other.tag}) entered the trigger");

        if (!IsPlayer(other)) return;

        if (MusicManager.Instance != null)
            MusicManager.Instance.EnterArea(this);
    }

    void OnTriggerExit(Collider other)
    {
        if (!IsPlayer(other)) return;

        if (MusicManager.Instance != null)
            MusicManager.Instance.ExitArea(this);
    }

    // If the area is disabled or destroyed while the player is inside, let go of it
    void OnDisable()
    {
        if (MusicManager.Instance != null)
            MusicManager.Instance.ExitArea(this);
    }

    // Draws the area in the Scene view
    void OnDrawGizmos()
    {
        Gizmos.matrix = transform.localToWorldMatrix;

        BoxCollider box = GetComponent<BoxCollider>();
        if (box != null)
        {
            Gizmos.color = new Color(0.2f, 0.8f, 1f, 0.25f);
            Gizmos.DrawCube(box.center, box.size);
            Gizmos.color = new Color(0.2f, 0.8f, 1f, 0.9f);
            Gizmos.DrawWireCube(box.center, box.size);
            return;
        }

        MeshCollider mc = GetComponent<MeshCollider>();
        if (mc != null && mc.sharedMesh != null)
        {
            Gizmos.color = new Color(0.2f, 0.8f, 1f, 0.9f);
            Gizmos.DrawWireMesh(mc.sharedMesh);
        }
    }
}
