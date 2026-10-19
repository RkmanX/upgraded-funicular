using UnityEngine;
using UnityEngine.UI;

// Builds the health / mana / stamina bars at the top-middle of the screen:
//
//        [ MANA ] [ STAMINA ]
//        [      HEALTH      ]
//
// Put this on your Canvas. The bars are created in code when the game starts.
public class StatBarsHUD : MonoBehaviour
{
    [Header("Who to show (leave empty to find the object tagged Player)")]
    public Health playerHealth;
    public PlayerStats playerStats;

    [Header("Layout")]
    public float totalWidth = 420f;          // width of the health bar; the two small bars together match it
    public float healthHeight = 22f;
    public float smallBarHeight = 14f;
    public float gapBetweenSmallBars = 8f;
    public float gapBetweenRows = 6f;
    public float topMargin = 20f;            // distance from the top of the screen
    public float border = 2f;                // dark frame around each bar

    [Header("Colors")]
    public Color healthColor = new Color(0.8f, 0.1f, 0.1f);
    public Color staminaColor = new Color(0.2f, 0.75f, 0.25f);
    public Color manaColor = new Color(0.2f, 0.4f, 0.95f);
    public Color backColor = new Color(0f, 0f, 0f, 0.65f);

    public float smoothSpeed = 2f;           // how fast bars slide to a new value (2 = a full bar in half a second)

    class Bar
    {
        public RectTransform fill;
        public float shown = 1f;             // 0..1, what is currently drawn
    }

    Bar healthBar, staminaBar, manaBar;

    void Awake()
    {
        if (playerHealth == null || playerStats == null)
        {
            GameObject p = GameObject.FindWithTag("Player");
            if (p != null)
            {
                if (playerHealth == null) playerHealth = p.GetComponent<Health>();
                if (playerStats == null) playerStats = p.GetComponent<PlayerStats>();
            }
        }

        if (playerHealth == null || playerStats == null)
            Debug.LogWarning("StatBarsHUD: drag your Player's Health and PlayerStats into the slots (or tag the Player object).", this);

        BuildLayout();
    }

    void BuildLayout()
    {
        // A container pinned to the top-middle of the screen
        RectTransform root = new GameObject("StatBars", typeof(RectTransform)).GetComponent<RectTransform>();
        root.SetParent(transform, false);
        root.anchorMin = root.anchorMax = new Vector2(0.5f, 1f);   // anchor: top centre
        root.pivot = new Vector2(0.5f, 1f);
        root.anchoredPosition = new Vector2(0f, -topMargin);
        root.sizeDelta = new Vector2(totalWidth, smallBarHeight + gapBetweenRows + healthHeight);

        float smallWidth = (totalWidth - gapBetweenSmallBars) * 0.5f;

        // Positions are measured from the container's top-left corner, going right and down
        manaBar    = CreateBar(root, "Mana",    new Vector2(0f, 0f),                         new Vector2(smallWidth, smallBarHeight), manaColor);
        staminaBar = CreateBar(root, "Stamina", new Vector2(smallWidth + gapBetweenSmallBars, 0f), new Vector2(smallWidth, smallBarHeight), staminaColor);
        healthBar  = CreateBar(root, "Health",  new Vector2(0f, smallBarHeight + gapBetweenRows), new Vector2(totalWidth, healthHeight), healthColor);
    }

    Bar CreateBar(RectTransform parent, string label, Vector2 position, Vector2 size, Color color)
    {
        // Dark background frame
        GameObject back = new GameObject(label + "Back", typeof(RectTransform), typeof(Image));
        back.transform.SetParent(parent, false);
        RectTransform backRect = back.GetComponent<RectTransform>();
        backRect.anchorMin = backRect.anchorMax = new Vector2(0f, 1f);  // anchored to the top-left
        backRect.pivot = new Vector2(0f, 1f);
        backRect.anchoredPosition = new Vector2(position.x, -position.y);
        backRect.sizeDelta = size;
        Image backImage = back.GetComponent<Image>();
        backImage.color = backColor;
        backImage.raycastTarget = false;

        // An invisible inner area, inset by the border
        RectTransform inner = new GameObject(label + "Inner", typeof(RectTransform)).GetComponent<RectTransform>();
        inner.SetParent(backRect, false);
        inner.anchorMin = Vector2.zero;
        inner.anchorMax = Vector2.one;
        inner.offsetMin = new Vector2(border, border);
        inner.offsetMax = new Vector2(-border, -border);

        // The colored fill. Its right edge (anchorMax.x) is what we slide to show the amount.
        GameObject fillObject = new GameObject(label + "Fill", typeof(RectTransform), typeof(Image));
        fillObject.transform.SetParent(inner, false);
        RectTransform fillRect = fillObject.GetComponent<RectTransform>();
        fillRect.anchorMin = Vector2.zero;
        fillRect.anchorMax = Vector2.one;
        fillRect.offsetMin = Vector2.zero;
        fillRect.offsetMax = Vector2.zero;
        Image fillImage = fillObject.GetComponent<Image>();
        fillImage.color = color;
        fillImage.raycastTarget = false;

        return new Bar { fill = fillRect };
    }

    void Update()
    {
        if (playerHealth != null && playerHealth.maxHealth > 0f)
            Animate(healthBar, playerHealth.Current / playerHealth.maxHealth);

        if (playerStats != null)
        {
            if (playerStats.maxStamina > 0f) Animate(staminaBar, playerStats.Stamina / playerStats.maxStamina);
            if (playerStats.maxMana > 0f)    Animate(manaBar, playerStats.Mana / playerStats.maxMana);
        }
    }

    // Slide the drawn value toward the real one, then resize the fill to match
    void Animate(Bar bar, float target)
    {
        target = Mathf.Clamp01(target);
        bar.shown = Mathf.MoveTowards(bar.shown, target, smoothSpeed * Time.deltaTime);
        bar.fill.anchorMax = new Vector2(bar.shown, 1f);
    }
}
