using UnityEngine;

[System.Serializable]
public class LootItem
{
    public string itemName = "Rusty Dagger";
    [Range(0f, 1f)] public float dropChance = 0.25f;   // 0.25 = 25% chance
}

[System.Serializable]
public class QuestItemDrop
{
    public string itemName = "Sealed Letter";
    [Range(0f, 1f)] public float dropChance = 1f;      // quest items usually always drop
    public Sprite sprite;                              // leave empty for a purple placeholder square
}

// Put this on an enemy. When it dies:
//  - the body can be searched with E for gold and gear
//  - any quest items drop on the ground as visible pickups (E to take)
[RequireComponent(typeof(Health))]
public class LootDrop : MonoBehaviour
{
    [Header("Body loot (press E on the corpse)")]
    public string corpseName = "";               // shown in the prompt; empty uses this object's name
    [Range(0f, 1f)] public float goldChance = 0.8f;
    public int minGold = 2;
    public int maxGold = 12;
    public LootItem[] possibleItems;             // weapons, armour, junk...
    public float searchAreaSize = 1.4f;          // size of the invisible box you aim at to search
    public AudioClip searchSound;

    [Header("Quest items (drop on the ground)")]
    public QuestItemDrop[] questItems;
    public float scatterRadius = 0.7f;
    public float pickupSize = 0.5f;              // how tall the dropped sprite is in the world
    public AudioClip pickupSound;

    Health health;
    static Sprite fallbackSprite;

    void Awake()
    {
        health = GetComponent<Health>();
    }

    void OnEnable()
    {
        health.Died += Drop;
    }

    void OnDisable()
    {
        health.Died -= Drop;
    }

    void Drop()
    {
        CreateSearchableBody();

        int dropped = 0;
        if (questItems != null)
        {
            foreach (QuestItemDrop quest in questItems)
            {
                if (Random.value < quest.dropChance)
                {
                    SpawnQuestItem(quest);
                    dropped++;
                }
            }
        }

        Debug.Log($"LootDrop: {name} died. A searchable body was created, and {dropped} quest item(s) dropped.");
    }

    // An invisible trigger box on the floor where the body lies. This is what you aim at to search.
    void CreateSearchableBody()
    {
        Vector3 floor = FloorBelow(transform.position);

        // Not parented to the enemy, so it stays even if the enemy object is destroyed
        GameObject go = new GameObject("CorpseLoot");
        go.transform.position = floor;

        BoxCollider box = go.AddComponent<BoxCollider>();
        box.isTrigger = true;
        box.size = new Vector3(searchAreaSize, 1.2f, searchAreaSize);
        box.center = new Vector3(0f, 0.6f, 0f);

        CorpseLoot loot = go.AddComponent<CorpseLoot>();
        loot.corpseName = string.IsNullOrEmpty(corpseName) ? gameObject.name : corpseName;
        loot.sound = searchSound;

        // Roll the loot now, so it's decided at the moment of death
        if (maxGold > 0 && Random.value < goldChance)
            loot.gold = Random.Range(minGold, maxGold + 1);

        if (possibleItems != null)
        {
            foreach (LootItem item in possibleItems)
            {
                if (Random.value < item.dropChance)
                    loot.items.Add(item.itemName);
            }
        }
    }

    void SpawnQuestItem(QuestItemDrop quest)
    {
        // No sprite assigned? Use a plain coloured square so you can still test
        Sprite sprite = quest.sprite;
        bool usingFallback = sprite == null;
        if (usingFallback) sprite = GetFallbackSprite();

        // Scatter around the corpse so several drops don't sit on top of each other
        Vector2 offset = Random.insideUnitCircle * scatterRadius;
        Vector3 floor = FloorBelow(transform.position + new Vector3(offset.x, 0f, offset.y));

        // Parent object: holds the pickup logic and the collider you aim at
        GameObject root = new GameObject("Quest_" + quest.itemName);
        root.transform.position = floor;

        SphereCollider trigger = root.AddComponent<SphereCollider>();
        trigger.isTrigger = true;
        trigger.radius = 0.4f;
        trigger.center = new Vector3(0f, pickupSize * 0.5f, 0f);

        ItemPickup pickup = root.AddComponent<ItemPickup>();
        pickup.itemName = quest.itemName;
        pickup.isQuestItem = true;
        pickup.sound = pickupSound;

        // Child object: the visible sprite, scaled to size and sitting on the floor
        GameObject visual = new GameObject("Sprite");
        visual.transform.SetParent(root.transform, false);

        float scale = pickupSize / sprite.bounds.size.y;
        visual.transform.localScale = Vector3.one * scale;
        visual.transform.localPosition = new Vector3(0f, -sprite.bounds.min.y * scale + 0.02f, 0f);

        SpriteRenderer sr = visual.AddComponent<SpriteRenderer>();
        sr.sprite = sprite;
        if (usingFallback) sr.color = new Color(0.8f, 0.55f, 1f);

        visual.AddComponent<FaceCamera>();
    }

    // Straight down from a point to find the floor
    Vector3 FloorBelow(Vector3 position)
    {
        if (Physics.Raycast(position + Vector3.up * 0.5f, Vector3.down, out RaycastHit hit, 10f, ~0, QueryTriggerInteraction.Ignore))
            return hit.point;
        return position;
    }

    static Sprite GetFallbackSprite()
    {
        if (fallbackSprite == null)
        {
            Texture2D tex = Texture2D.whiteTexture;
            fallbackSprite = Sprite.Create(tex, new Rect(0f, 0f, tex.width, tex.height), new Vector2(0.5f, 0.5f), tex.width);
        }
        return fallbackSprite;
    }
}
