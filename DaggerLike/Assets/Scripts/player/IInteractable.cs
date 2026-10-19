using System.Collections.Generic;
using UnityEngine;

// Anything the player can use with the E key (corpses, dropped items, later: doors, chests, NPCs...)
public interface IInteractable
{
    // Text shown under the crosshair while you can use it, e.g. "[E] Search Goblin".
    // Return null or "" to show nothing (and make it unusable).
    string GetPrompt();

    // Called when the player presses E while it is the current target
    void Interact(PlayerInteractor interactor);

    // Where it is in the world (used for the "standing next to it" check)
    Vector3 Position { get; }
}

// Every usable thing in the scene registers itself here when it's enabled
public static class InteractableRegistry
{
    public static readonly List<IInteractable> All = new List<IInteractable>();
}
