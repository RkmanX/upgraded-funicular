using System.Collections.Generic;
using UnityEngine;

// Put this on any object (or spawn it from an item or spell) that enemies might be drawn to.
// Idle enemies nearby may walk over to it and stare at it for a while.
public class PointOfInterest : MonoBehaviour
{
    // Every active point of interest in the scene
    public static readonly List<PointOfInterest> All = new List<PointOfInterest>();

    [Range(0f, 1f)] public float attraction = 0.5f;   // how strongly it draws enemies (1 = almost always)
    public float noticeRadius = 15f;                  // enemies within this distance can be drawn to it
    public float arriveDistance = 1.5f;               // how close they walk before they stop

    [Header("Mesmerizing")]
    public bool mesmerizing = false;                  // true: enemies stop and stare when they arrive
    public float stareTime = 6f;                      // how long they stare
    [Range(0f, 1f)] public float distraction = 0.3f;  // while staring, suspicion builds at this fraction of normal (0 = totally entranced)

    [Header("Lifetime")]
    public float lifetime = 0f;                       // 0 = lasts forever. Spells and consumables will set this.

    void OnEnable()
    {
        All.Add(this);
    }

    void OnDisable()
    {
        All.Remove(this);
    }

    void Start()
    {
        if (lifetime > 0f)
            Destroy(gameObject, lifetime);
    }

    // For later: items and spells can create a point of interest in code, like
    //   PointOfInterest.Spawn(position, 0.9f, true, 8f, 20f);
    public static PointOfInterest Spawn(Vector3 position, float attraction, bool mesmerizing, float stareTime, float lifetime)
    {
        GameObject go = new GameObject("PointOfInterest");
        go.transform.position = position;

        PointOfInterest poi = go.AddComponent<PointOfInterest>();
        poi.attraction = attraction;
        poi.mesmerizing = mesmerizing;
        poi.stareTime = stareTime;
        poi.lifetime = lifetime;
        return poi;
    }

    void OnDrawGizmosSelected()
    {
        Gizmos.color = new Color(1f, 0.4f, 1f, 0.8f);
        Gizmos.DrawWireSphere(transform.position, noticeRadius);
    }
}
