using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

// Fades in a "YOU DIED" screen when the player's Health hits 0. Press R to restart.
// Put this on your Canvas. The screen is built in code when the game starts.
public class DeathScreen : MonoBehaviour
{
    public Health playerHealth;              // leave empty to find the object tagged Player
    public float fadeInTime = 2f;
    public float restartDelay = 2.5f;        // seconds before the restart prompt appears
    public Color overlayColor = new Color(0.25f, 0f, 0f, 0.85f);   // dark red; the alpha is how dark it ends up
    public Sprite  deathBackground;

    CanvasGroup group;
    RectTransform overlay;
    TextMeshProUGUI prompt;
    bool dead;
    float timeSinceDeath;

    void Awake()
    {
        if (playerHealth == null)
        {
            GameObject p = GameObject.FindWithTag("Player");
            if (p != null) playerHealth = p.GetComponent<Health>();
        }

        BuildUI();
    }

    void OnEnable()
    {
        if (playerHealth != null) playerHealth.Died += OnPlayerDied;
    }

    void OnDisable()
    {
        if (playerHealth != null) playerHealth.Died -= OnPlayerDied;
    }

    void BuildUI()
    {
        // Full-screen overlay: stretch the anchors to all four corners
        GameObject go = new GameObject("DeathScreen", typeof(RectTransform), typeof(Image), typeof(CanvasGroup));
        overlay = go.GetComponent<RectTransform>();
        overlay.SetParent(transform, false);
        overlay.anchorMin = Vector2.zero;
        overlay.anchorMax = Vector2.one;
        overlay.offsetMin = Vector2.zero;
        overlay.offsetMax = Vector2.zero;

        Image image = go.GetComponent<Image>();
        image.color = overlayColor;
        image.raycastTarget = false;

        // Optional image behind the death text
        if (deathBackground != null)
        {
            GameObject bgObject = new GameObject(
                "DeathBackground",
                typeof(RectTransform),
                typeof(Image)
            );

            RectTransform bgRect = bgObject.GetComponent<RectTransform>();
            bgRect.SetParent(overlay, false);
            bgRect.anchorMin = Vector2.zero;
            bgRect.anchorMax = Vector2.one;
            bgRect.offsetMin = Vector2.zero;
            bgRect.offsetMax = Vector2.zero;
            bgRect.SetAsFirstSibling();

            Image bgImage = bgObject.GetComponent<Image>();
            bgImage.sprite = deathBackground;
            bgImage.color = Color.white;
            bgImage.raycastTarget = false;
            bgImage.preserveAspect = false;
        }

        // CanvasGroup lets us fade the overlay AND its text together with one alpha value
        group = go.GetComponent<CanvasGroup>();
        group.alpha = 0f;
        group.interactable = false;
        group.blocksRaycasts = false;

        MakeText("Title", "YOU DIED", 96f, new Color(0.8f, 0.1f, 0.1f), new Vector2(0f, 40f));
        prompt = MakeText("Prompt", "Press R to try again", 36f, Color.white, new Vector2(0f, -70f));
        prompt.gameObject.SetActive(false);
    }

    TextMeshProUGUI MakeText(string objectName, string content, float size, Color color, Vector2 position)
    {
        GameObject go = new GameObject(objectName, typeof(RectTransform), typeof(TextMeshProUGUI));
        go.transform.SetParent(overlay, false);

        TextMeshProUGUI text = go.GetComponent<TextMeshProUGUI>();
        text.text = content;
        text.fontSize = size;
        text.color = color;
        text.alignment = TextAlignmentOptions.Center;
        text.raycastTarget = false;

        RectTransform rect = text.rectTransform;
        rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);   // centre of the screen
        rect.sizeDelta = new Vector2(1200f, 200f);
        rect.anchoredPosition = position;

        return text;
    }

    void OnPlayerDied()
    {
        dead = true;
        timeSinceDeath = 0f;
        overlay.SetAsLastSibling();   // draw on top of the bars and everything else

        // Take control away from the player
        PlayerController controller = playerHealth.GetComponent<PlayerController>();
        if (controller != null) controller.enabled = false;

        PlayerCombat combat = playerHealth.GetComponent<PlayerCombat>();
        if (combat != null) combat.enabled = false;

        PlayerInteractor interactor = playerHealth.GetComponent<PlayerInteractor>();
        if (interactor != null) interactor.enabled = false;

        if (InteractPrompt.Instance != null) InteractPrompt.Instance.Hide();
    }

    void Update()
    {
        if (!dead) return;

        timeSinceDeath += Time.deltaTime;
        group.alpha = Mathf.Clamp01(timeSinceDeath / fadeInTime);

        if (timeSinceDeath >= restartDelay)
        {
            if (!prompt.gameObject.activeSelf) prompt.gameObject.SetActive(true);

            if (Keyboard.current.rKey.wasPressedThisFrame)
                SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
        }
    }
}
