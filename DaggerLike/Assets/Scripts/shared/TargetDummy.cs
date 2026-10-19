using System.Collections;
using UnityEngine;

// Hit feedback: flash and hit sound. Works with a SpriteRenderer on a child
// (sprite enemies) or a normal mesh Renderer.
[RequireComponent(typeof(Health))]
public class TargetDummy : MonoBehaviour
{
    [Header("Audio")]
    public AudioClip hitSound;
    [Range(0f, 1f)] public float hitVolume = 1f;

    [Header("Death")]
    public bool destroyOnDeath = false; // tick for simple test dummies; leave off for enemies that leave corpses

    Health health;
    SpriteRenderer sprite;
    Renderer rend;
    Color baseColor;

    void Awake()
    {
        health = GetComponent<Health>();

        sprite = GetComponentInChildren<SpriteRenderer>();
        if (sprite != null)
        {
            baseColor = sprite.color;
        }
        else
        {
            rend = GetComponentInChildren<Renderer>();
            if (rend != null) baseColor = rend.material.color;
        }
    }

    void OnEnable()
    {
        health.Damaged += OnDamaged;
        health.Died += OnDied;
    }

    void OnDisable()
    {
        health.Damaged -= OnDamaged;
        health.Died -= OnDied;
    }

    void SetColor(Color c)
    {
        if (sprite != null) sprite.color = c;
        else if (rend != null) rend.material.color = c;
    }

    // Used by party members: changes the "normal" colour the hit flash returns to
    public void SetBaseColor(Color c)
    {
        baseColor = c;
        SetColor(c);
    }

    void OnDamaged(float amount)
    {
        Debug.Log($"{name} took {amount} damage ({health.Current}/{health.maxHealth})");

        if (hitSound != null)
            AudioSource.PlayClipAtPoint(hitSound, transform.position, hitVolume);

        StopAllCoroutines();
        StartCoroutine(Flash());
    }

    IEnumerator Flash()
    {
        SetColor(Color.red);
        yield return new WaitForSeconds(0.1f);
        SetColor(baseColor);
    }

    void OnDied()
    {
        if (destroyOnDeath)
            Destroy(gameObject);
    }
}
