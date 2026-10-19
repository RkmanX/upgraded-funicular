using UnityEngine;

// Put this on the Player. Enemies call OnHit() when they hurt you; this decides how it feels:
// red flash, a small camera flinch, and on a critical hit: recoil + a stun where you can't attack.
public class PlayerHitReaction : MonoBehaviour
{
    [Header("Normal hit")]
    public float flashIntensity = 0.3f;
    public float flinchDegrees = 3f;         // camera snaps back a little

    [Header("Critical hit")]
    public float critFlashIntensity = 0.65f;
    public float critFlinchDegrees = 10f;
    public float recoilSpeed = 7f;           // how hard you get shoved away
    public float stunDuration = 1f;          // seconds you can't attack

    static readonly Color takenColor = new Color(1f, 0.5f, 0.35f);

    PlayerController controller;
    PlayerCombat combat;

    void Awake()
    {
        controller = GetComponent<PlayerController>();
        combat = GetComponent<PlayerCombat>();
    }

    public void OnHit(float damage, bool wasCritical, Vector3 attackerPosition)
    {
        if (HurtFlash.Instance != null)
            HurtFlash.Instance.Flash(wasCritical ? critFlashIntensity : flashIntensity);

        if (DamageReadout.Instance != null)
        {
            string message = wasCritical ? $"CRIT TAKEN {damage:0} DMG" : $"TAKEN {damage:0} DMG";
            DamageReadout.Instance.ShowMessage(message, takenColor);
        }

        if (controller != null)
            controller.KickCamera(-(wasCritical ? critFlinchDegrees : flinchDegrees)); // negative = head snaps back

        if (wasCritical)
        {
            if (combat != null) combat.Stun(stunDuration);

            if (controller != null)
            {
                Vector3 away = transform.position - attackerPosition;
                controller.Recoil(away, recoilSpeed);
            }
        }
    }
}
