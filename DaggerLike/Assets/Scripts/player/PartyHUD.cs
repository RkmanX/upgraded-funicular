using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// Shows a small card on the left side of the screen for each party member:
// their name, a portrait, and vertical HP / MP / Stamina bars.
// Put this on your Canvas. Cards appear when an enemy joins your party and disappear when it leaves or dies.
public class PartyHUD : MonoBehaviour
{
    [Header("Layout")]
    public Vector2 startOffset = new Vector2(20f, -130f);   // from the top-left corner of the screen
    public float cardWidth = 96f;
    public float cardHeight = 190f;
    public float cardSpacing = 10f;
    public int maxCards = 4;

    [Header("Colors")]
    public Color healthColor = new Color(0.8f, 0.1f, 0.1f);
    public Color magickaColor = new Color(0.2f, 0.4f, 0.95f);
    public Color staminaColor = new Color(0.2f, 0.75f, 0.25f);
    public Color cardColor = new Color(0f, 0f, 0f, 0.55f);
    public Color barBackColor = new Color(0f, 0f, 0f, 0.7f);
    public float smoothSpeed = 2f;

    class Bar
    {
        public RectTransform fill;
        public float shown = 1f;
    }

    class Card
    {
        public RectTransform root;
        public Bar health;
        public Bar magicka;
        public Bar stamina;
    }

    readonly Dictionary<Enemy, Card> cards = new Dictionary<Enemy, Card>();
    readonly List<Enemy> toRemove = new List<Enemy>();

    void Update()
    {
        // New party members get a card
        foreach (Enemy enemy in Enemy.All)
        {
            if (enemy.IsCompanion && !cards.ContainsKey(enemy) && cards.Count < maxCards)
                cards[enemy] = BuildCard(enemy);
        }

        // Members that left or died lose theirs
        toRemove.Clear();
        foreach (KeyValuePair<Enemy, Card> pair in cards)
        {
            if (pair.Key == null || !pair.Key.isActiveAndEnabled || !pair.Key.IsCompanion)
                toRemove.Add(pair.Key);
        }

        foreach (Enemy gone in toRemove)
        {
            if (cards[gone].root != null) Destroy(cards[gone].root.gameObject);
            cards.Remove(gone);
        }

        // Stack the cards down the left side and update their bars
        int index = 0;
        foreach (KeyValuePair<Enemy, Card> pair in cards)
        {
            Card card = pair.Value;
            card.root.anchoredPosition = new Vector2(startOffset.x, startOffset.y - index * (cardHeight + cardSpacing));
            index++;

            EnemyStats stats = pair.Key.Stats;
            Animate(card.health, pair.Key.HealthFraction);
            Animate(card.magicka, stats.maxMagicka > 0f ? stats.Magicka / stats.maxMagicka : 0f);
            Animate(card.stamina, stats.maxStamina > 0f ? stats.Stamina / stats.maxStamina : 0f);
        }
    }

    void Animate(Bar bar, float target)
    {
        target = Mathf.Clamp01(target);
        bar.shown = Mathf.MoveTowards(bar.shown, target, smoothSpeed * Time.deltaTime);
        bar.fill.anchorMax = new Vector2(1f, bar.shown);   // vertical bar: the TOP edge moves
    }

    Card BuildCard(Enemy enemy)
    {
        Card card = new Card();

        // The card background, pinned to the top-left of the screen
        GameObject rootObject = new GameObject("PartyCard_" + enemy.DisplayName, typeof(RectTransform), typeof(Image));
        card.root = rootObject.GetComponent<RectTransform>();
        card.root.SetParent(transform, false);
        card.root.anchorMin = card.root.anchorMax = new Vector2(0f, 1f);
        card.root.pivot = new Vector2(0f, 1f);
        card.root.sizeDelta = new Vector2(cardWidth, cardHeight);

        Image background = rootObject.GetComponent<Image>();
        background.color = cardColor;
        background.raycastTarget = false;

        // Name across the top (the font shrinks to fit long names)
        GameObject nameObject = new GameObject("Name", typeof(RectTransform), typeof(TextMeshProUGUI));
        nameObject.transform.SetParent(card.root, false);

        TextMeshProUGUI nameText = nameObject.GetComponent<TextMeshProUGUI>();
        nameText.text = enemy.DisplayName;
        nameText.alignment = TextAlignmentOptions.Center;
        nameText.color = Color.white;
        nameText.enableAutoSizing = true;
        nameText.fontSizeMin = 10f;
        nameText.fontSizeMax = 20f;
        nameText.raycastTarget = false;

        RectTransform nameRect = nameText.rectTransform;
        nameRect.anchorMin = new Vector2(0f, 1f);
        nameRect.anchorMax = new Vector2(1f, 1f);
        nameRect.pivot = new Vector2(0.5f, 1f);
        nameRect.anchoredPosition = new Vector2(0f, -4f);
        nameRect.sizeDelta = new Vector2(-8f, 24f);

        // Portrait under the name
        GameObject portraitObject = new GameObject("Portrait", typeof(RectTransform), typeof(Image));
        portraitObject.transform.SetParent(card.root, false);

        Image portrait = portraitObject.GetComponent<Image>();
        portrait.sprite = enemy.Portrait;
        portrait.preserveAspect = true;
        portrait.raycastTarget = false;
        portrait.enabled = portrait.sprite != null;

        RectTransform portraitRect = portrait.rectTransform;
        portraitRect.anchorMin = portraitRect.anchorMax = new Vector2(0.5f, 1f);
        portraitRect.pivot = new Vector2(0.5f, 1f);
        portraitRect.anchoredPosition = new Vector2(0f, -32f);
        portraitRect.sizeDelta = new Vector2(60f, 60f);

        // Three vertical bars at the bottom: HP, MP, Stamina
        float barWidth = 20f;
        float gap = 6f;
        float left = (cardWidth - (3f * barWidth + 2f * gap)) * 0.5f;
        float top = -98f;
        float barHeight = 84f;

        card.health  = CreateBar(card.root, new Vector2(left, top), new Vector2(barWidth, barHeight), healthColor);
        card.magicka = CreateBar(card.root, new Vector2(left + barWidth + gap, top), new Vector2(barWidth, barHeight), magickaColor);
        card.stamina = CreateBar(card.root, new Vector2(left + 2f * (barWidth + gap), top), new Vector2(barWidth, barHeight), staminaColor);

        return card;
    }

    Bar CreateBar(RectTransform parent, Vector2 position, Vector2 size, Color color)
    {
        // Dark frame
        GameObject back = new GameObject("BarBack", typeof(RectTransform), typeof(Image));
        back.transform.SetParent(parent, false);

        RectTransform backRect = back.GetComponent<RectTransform>();
        backRect.anchorMin = backRect.anchorMax = new Vector2(0f, 1f);
        backRect.pivot = new Vector2(0f, 1f);
        backRect.anchoredPosition = position;
        backRect.sizeDelta = size;

        Image backImage = back.GetComponent<Image>();
        backImage.color = barBackColor;
        backImage.raycastTarget = false;

        // Inner area, inset by 2 pixels
        RectTransform inner = new GameObject("Inner", typeof(RectTransform)).GetComponent<RectTransform>();
        inner.SetParent(backRect, false);
        inner.anchorMin = Vector2.zero;
        inner.anchorMax = Vector2.one;
        inner.offsetMin = new Vector2(2f, 2f);
        inner.offsetMax = new Vector2(-2f, -2f);

        // The colored fill: its top edge (anchorMax.y) moves up and down
        GameObject fillObject = new GameObject("Fill", typeof(RectTransform), typeof(Image));
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
}
