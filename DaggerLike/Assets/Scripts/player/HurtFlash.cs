using UnityEngine;
using UnityEngine.UI;

// A red flash across the whole screen when the player is hurt. Put this on your Canvas.
public class HurtFlash : MonoBehaviour
{
    public static HurtFlash Instance { get; private set; }

    public Color flashColor = new Color(0.8f, 0f, 0f);
    public float fadeSpeed = 1.5f;      // alpha lost per second (1.5 = a full flash fades in about 0.7s)

    Image image;
    float alpha;

    void Awake()
    {
        Instance = this;

        GameObject go = new GameObject("HurtFlash", typeof(RectTransform), typeof(Image));
        RectTransform rect = go.GetComponent<RectTransform>();
        rect.SetParent(transform, false);
        rect.anchorMin = Vector2.zero;       // stretch over the full screen
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
        rect.SetAsFirstSibling();            // draw behind the bars, crosshair and text

        image = go.GetComponent<Image>();
        image.raycastTarget = false;

        Color c = flashColor;
        c.a = 0f;
        image.color = c;
    }

    void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    // intensity 0..1: how strong the flash starts
    public void Flash(float intensity)
    {
        alpha = Mathf.Max(alpha, Mathf.Clamp01(intensity));
    }

    void Update()
    {
        if (alpha <= 0f) return;

        alpha = Mathf.MoveTowards(alpha, 0f, fadeSpeed * Time.deltaTime);

        Color c = flashColor;
        c.a = alpha;
        image.color = c;
    }
}
