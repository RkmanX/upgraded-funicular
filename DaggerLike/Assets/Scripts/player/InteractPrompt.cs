using TMPro;
using UnityEngine;
using UnityEngine.UI;

// The "[E] Search Goblin" text under the crosshair, on a dark panel so it's readable on any background.
// Put this on your Canvas.
public class InteractPrompt : MonoBehaviour
{
    public static InteractPrompt Instance { get; private set; }

    public Vector2 offsetFromCenter = new Vector2(0f, -70f);
    public float fontSize = 30f;
    public Color backingColor = new Color(0f, 0f, 0f, 0.6f);
    public Vector2 padding = new Vector2(40f, 16f);   // extra space around the text

    TextMeshProUGUI text;
    RectTransform backing;

    void Awake()
    {
        Instance = this;

        // The dark panel
        GameObject panel = new GameObject("InteractPromptBacking", typeof(RectTransform), typeof(Image));
        panel.transform.SetParent(transform, false);

        backing = panel.GetComponent<RectTransform>();
        backing.anchorMin = backing.anchorMax = new Vector2(0.5f, 0.5f);   // centre of the screen
        backing.anchoredPosition = offsetFromCenter;

        Image image = panel.GetComponent<Image>();
        image.color = backingColor;
        image.raycastTarget = false;

        // The text, stretched to fill the panel
        GameObject go = new GameObject("InteractPrompt", typeof(RectTransform), typeof(TextMeshProUGUI));
        go.transform.SetParent(backing, false);

        text = go.GetComponent<TextMeshProUGUI>();
        text.fontSize = fontSize;
        text.alignment = TextAlignmentOptions.Center;
        text.color = Color.white;
        text.raycastTarget = false;
        text.outlineWidth = 0.25f;
        text.outlineColor = new Color32(0, 0, 0, 255);
        text.text = "";

        RectTransform rect = text.rectTransform;
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;

        panel.SetActive(false);   // hidden until there's something to show
    }

    void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    public void Show(string message)
    {
        if (text.text != message)
        {
            text.text = message;

            // Size the panel to fit the text
            Vector2 size = text.GetPreferredValues(message);
            backing.sizeDelta = new Vector2(size.x + padding.x, size.y + padding.y);
        }

        if (!backing.gameObject.activeSelf)
            backing.gameObject.SetActive(true);
    }

    public void Hide()
    {
        if (backing.gameObject.activeSelf)
        {
            backing.gameObject.SetActive(false);
            text.text = "";
        }
    }
}
