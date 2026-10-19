using UnityEngine;

// A spot that makes the narrator speak when the player walks into it.
// Put it on an empty GameObject with a Box Collider (it switches the collider to a trigger for you).
public class NarratorTrigger : MonoBehaviour
{
    [Header("What it says")]
    [TextArea(2, 5)] public string[] firstVisitMessages;   // shown the first time you walk in
    [TextArea(2, 5)] public string[] repeatMessages;       // possible lines on later visits
    public bool pickRandom = true;                         // random line from each list, or go through them in order

    [Header("When")]
    public bool onlyOnce = false;                          // true = speaks on the first touch only, then never again
    [Range(0f, 1f)] public float repeatChance = 0.5f;      // chance to speak on a later visit
    public float cooldown = 20f;                           // minimum seconds between times it can speak

    bool visited;
    bool used;
    float nextAllowedTime;
    int firstIndex;
    int repeatIndex;

    void Awake()
    {
        Collider[] colliders = GetComponents<Collider>();

        if (colliders.Length == 0)
        {
            Debug.LogWarning($"NarratorTrigger '{name}' has no collider on this GameObject. Add a Box Collider here.", this);
            return;
        }

        foreach (Collider c in colliders)
        {
            if (c is MeshCollider mesh) mesh.convex = true;   // only convex mesh colliders can be triggers
            c.isTrigger = true;
        }
    }

    void Start()
    {
        if (NarratorPopup.Instance == null)
            Debug.LogWarning("No NarratorPopup in the scene. Add the NarratorPopup script to your Canvas to see narration.", this);
    }

    void OnTriggerEnter(Collider other)
    {
        // Recognise the player by its PlayerController script
        if (other.GetComponentInParent<PlayerController>() == null) return;

        if (used || Time.time < nextAllowedTime) return;

        string message = null;

        if (!visited)
        {
            visited = true;
            message = Pick(firstVisitMessages, ref firstIndex);
            if (onlyOnce) used = true;
        }
        else if (Random.value < repeatChance)
        {
            message = Pick(repeatMessages, ref repeatIndex);
        }

        nextAllowedTime = Time.time + cooldown;

        if (!string.IsNullOrEmpty(message) && NarratorPopup.Instance != null)
            NarratorPopup.Instance.Show(message);
    }

    string Pick(string[] list, ref int index)
    {
        if (list == null || list.Length == 0) return null;

        if (pickRandom)
            return list[Random.Range(0, list.Length)];

        string message = list[index % list.Length];
        index++;
        return message;
    }

    // Draws the area as an amber box in the Scene view
    void OnDrawGizmos()
    {
        BoxCollider box = GetComponent<BoxCollider>();
        if (box == null) return;

        Gizmos.matrix = transform.localToWorldMatrix;
        Gizmos.color = new Color(1f, 0.75f, 0.2f, 0.2f);
        Gizmos.DrawCube(box.center, box.size);
        Gizmos.color = new Color(1f, 0.75f, 0.2f, 0.9f);
        Gizmos.DrawWireCube(box.center, box.size);
    }
}
