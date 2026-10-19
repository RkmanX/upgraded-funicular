using UnityEngine;

// An item lying on the ground. Look at it and press E. (Quest items dropped by enemies use this.)
public class ItemPickup : MonoBehaviour, IInteractable
{
    public string itemName;
    public bool isQuestItem = true;
    public AudioClip sound;

    static readonly Color questColor = new Color(0.8f, 0.55f, 1f);
    static readonly Color itemColor = new Color(1f, 0.85f, 0.3f);

    void OnDrawGizmos()
    {
        SphereCollider sphere = GetComponent<SphereCollider>();
        if (sphere == null) return;

        Gizmos.color = new Color(0.8f, 0.55f, 1f, 0.9f);
        Gizmos.DrawWireSphere(transform.TransformPoint(sphere.center), sphere.radius);
    }

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
        return $"[E] Pick up {itemName}";
    }

    public void Interact(PlayerInteractor interactor)
    {
        string message;

        if (isQuestItem)
        {
            interactor.Inventory.AddQuestItem(itemName);
            message = $"QUEST ITEM: {itemName}";
        }
        else
        {
            interactor.Inventory.AddItem(itemName);
            message = $"LOOT: {itemName}";
        }

        if (DamageReadout.Instance != null)
            DamageReadout.Instance.ShowMessage(message, isQuestItem ? questColor : itemColor);

        if (sound != null)
            AudioSource.PlayClipAtPoint(sound, transform.position);

        Destroy(gameObject);
    }
}
