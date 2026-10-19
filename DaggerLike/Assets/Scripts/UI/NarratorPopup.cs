using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// The "Dungeon Master" text: a line of narration that fades in near the bottom of the screen,
// stays a while, and fades out (like Daggerfall and Arena). Separate from the event readout.
// Put this on your Canvas. NarratorTrigger areas send messages to it.
public class NarratorPopup : MonoBehaviour
{
    public static NarratorPopup Instance { get; private set; }

    [Header("Look")]
    public float width = 900f;
    public float bottomMargin = 150f;
    public float fontSize = 30f;
    public Vector2 padding = new Vector2(40f, 24f);
    public Color textColor = new Color(1f, 0.92f, 0.72f);        // warm parchment
    public Color panelColor = new Color(0.05f, 0.03f, 0.02f, 0.8f);

    [Header("Timing")]
    public float fadeInTime = 0.5f;
    public float fadeOutTime = 1.2f;
    public float minShowTime = 3f;
    public float secondsPerCharacter = 0.05f;   // longer messages stay longer
    public int maxQueued = 3;                   // extra messages wait their turn; the oldest is dropped past this

    enum Phase { Hidden, FadingIn, Showing, FadingOut }

    Phase phase = Phase.Hidden;
    float phaseTimer;
    float showDuration;

    CanvasGroup group;
    RectTransform panel;
    TextMeshProUGUI text;
    readonly Queue<string> queue = new Queue<string>();

    void Awake()
    {
        Instance = this;

        // The panel, pinned to the bottom-centre of the screen
        GameObject panelObject = new GameObject("NarratorPanel", typeof(RectTransform), typeof(Image), typeof(CanvasGroup));
        panel = panelObject.GetComponent<RectTransform>();
        panel.SetParent(transform, false);
        panel.anchorMin = panel.anchorMax = new Vector2(0.5f, 0f);
        panel.pivot = new Vector2(0.5f, 0f);
        panel.anchoredPosition = new Vector2(0f, bottomMargin);
        panel.sizeDelta = new Vector2(width, 100f);

        Image background = panelObject.GetComponent<Image>();
        background.color = panelColor;
        background.raycastTarget = false;

        // CanvasGroup fades the panel and its text together
        group = panelObject.GetComponent<CanvasGroup>();
        group.alpha = 0f;
        group.interactable = false;
        group.blocksRaycasts = false;

        // The text, filling the panel (inset by the padding)
        GameObject textObject = new GameObject("NarratorText", typeof(RectTransform), typeof(TextMeshProUGUI));
        textObject.transform.SetParent(panel, false);

        text = textObject.GetComponent<TextMeshProUGUI>();
        text.fontSize = fontSize;
        text.color = textColor;
        text.alignment = TextAlignmentOptions.Center;
        text.raycastTarget = false;

        RectTransform textRect = text.rectTransform;
        textRect.anchorMin = Vector2.zero;
        textRect.anchorMax = Vector2.one;
        textRect.offsetMin = padding;
        textRect.offsetMax = -padding;
    }

    void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    // Queue a line of narration
    public void Show(string message)
    {
        if (string.IsNullOrEmpty(message)) return;

        if (queue.Count >= maxQueued)
            queue.Dequeue();

        queue.Enqueue(message);
    }

    void Update()
    {
        switch (phase)
        {
            case Phase.Hidden:
                if (queue.Count > 0) Begin(queue.Dequeue());
                break;

            case Phase.FadingIn:
                phaseTimer += Time.deltaTime;
                group.alpha = Mathf.Clamp01(phaseTimer / fadeInTime);
                if (phaseTimer >= fadeInTime)
                {
                    phase = Phase.Showing;
                    phaseTimer = 0f;
                }
                break;

            case Phase.Showing:
                phaseTimer += Time.deltaTime;
                if (phaseTimer >= showDuration)
                {
                    phase = Phase.FadingOut;
                    phaseTimer = 0f;
                }
                break;

            case Phase.FadingOut:
                phaseTimer += Time.deltaTime;
                group.alpha = 1f - Mathf.Clamp01(phaseTimer / fadeOutTime);
                if (phaseTimer >= fadeOutTime)
                {
                    group.alpha = 0f;
                    phase = Phase.Hidden;
                }
                break;
        }
    }

    void Begin(string message)
    {
        text.text = message;

        // Size the panel to fit the wrapped text
        Vector2 preferred = text.GetPreferredValues(message, width - padding.x * 2f, 0f);
        panel.sizeDelta = new Vector2(width, preferred.y + padding.y * 2f);

        showDuration = Mathf.Max(minShowTime, message.Length * secondsPerCharacter);
        phase = Phase.FadingIn;
        phaseTimer = 0f;
    }
}
