using System;
using UnityEngine;

public class Health : MonoBehaviour
{
    public float maxHealth = 100f;

    public float Current { get; private set; }

    // Who dealt the most recent damage (null if unknown). Enemies use this to decide who to fight.
    public GameObject LastAttacker { get; private set; }

    // Other scripts subscribe to these to react (flash, play sound, update UI...)
    public event Action<float> Damaged;   // passes the damage amount
    public event Action Died;

    void Awake()
    {
        Current = maxHealth;
    }

    public void ResetToMax()
    {
        Current = maxHealth;
    }

    public void TakeDamage(float amount, GameObject attacker = null)
    {
        if (Current <= 0f) return;   // already dead, ignore

        LastAttacker = attacker;
        Current = Mathf.Max(0f, Current - amount);

        RaiseDamaged(amount);

        if (Current <= 0f)
            RaiseDied();
    }

    public void Heal(float amount)
    {
        if (Current <= 0f) return;
        Current = Mathf.Min(maxHealth, Current + amount);
    }

    // Each listener is called inside its own try/catch. If one listener has a bug, the others
    // (like loot dropping on death) still run, instead of everything after it being skipped.
    void RaiseDamaged(float amount)
    {
        if (Damaged == null) return;

        foreach (Delegate handler in Damaged.GetInvocationList())
        {
            try { ((Action<float>)handler)(amount); }
            catch (Exception e) { Debug.LogException(e, this); }
        }
    }

    void RaiseDied()
    {
        if (Died == null) return;

        foreach (Delegate handler in Died.GetInvocationList())
        {
            try { ((Action)handler)(); }
            catch (Exception e) { Debug.LogException(e, this); }
        }
    }
}
