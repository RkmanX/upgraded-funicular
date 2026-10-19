using System.Collections.Generic;
using UnityEngine;

// Holds what the player has picked up. Public so you can watch it change in the Inspector.
public class PlayerInventory : MonoBehaviour
{
    public int gold;
    public List<string> items = new List<string>();
    public List<string> questItems = new List<string>();

    public void AddGold(int amount)
    {
        gold += amount;
    }

    public void AddItem(string itemName)
    {
        items.Add(itemName);
    }

    public void AddQuestItem(string itemName)
    {
        questItems.Add(itemName);
    }
}
