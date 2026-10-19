using System.Collections.Generic;
using UnityEngine;

// The searchable "body" part of a corpse: gold and gear, found by pressing E. (Created by LootDrop.)
public class CorpseLoot : MonoBehaviour, IInteractable
{
    public string corpseName = "body";
    public int gold;
    public List<string> items = new List<string>();
    public AudioClip sound;

    bool looted;

    static readonly Color lootColor = new Color(1f, 0.85f, 0.3f);

    public Vector3 Position => transform.position;

    void OnEnable()
    {
        InteractableRegistry.All.Add(this);
    }

    void OnDisable()
    {
        InteractableRegistry.All.Remove(this);
    }

    public string GetPrompt()
    {
        return looted ? null : $"[E] Search {corpseName}";
    }

    public void Interact(PlayerInteractor interactor)
    {
        if (looted) return;
        looted = true;

        PlayerInventory inventory = interactor.Inventory;

        if (gold <= 0 && items.Count == 0)
        {
            Say("LOOT: nothing on this body");
        }
        else
        {
            if (gold > 0)
            {
                inventory.AddGold(gold);
                Say($"LOOT: +{gold} gold (total {inventory.gold})");
            }

            foreach (string item in items)
            {
                inventory.AddItem(item);
                Say($"LOOT: {item}");
            }
        }

        if (sound != null)
            AudioSource.PlayClipAtPoint(sound, transform.position);

        // Nothing left to find, so stop it being a target
        Collider c = GetComponent<Collider>();
        if (c != null) c.enabled = false;
    }

    // Draws the searchable area as a yellow box in the Scene view (and the Game view if Gizmos is on)
    void OnDrawGizmos()
    {
        BoxCollider box = GetComponent<BoxCollider>();
        if (box == null || looted) return;

        Gizmos.matrix = transform.localToWorldMatrix;
        Gizmos.color = new Color(1f, 0.85f, 0.3f, 0.35f);
        Gizmos.DrawCube(box.center, box.size);
    }

    void Say(string message)
    {
        if (DamageReadout.Instance != null)
            DamageReadout.Instance.ShowMessage(message, lootColor);
    }
}
