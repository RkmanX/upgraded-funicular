using TMPro;
using UnityEngine;
using UnityEngine.UI;

// Centre-of-screen icon that changes with your stealth state, plus a suspicion % beside it.
// Put this on the Crosshair Image.
public class CrosshairUI : MonoBehaviour
{
    public Image image;
    public PlayerController player;   // leave empty to auto-find the object tagged "Player"

    public Sprite normalSprite;
    public Sprite sneakSprite;
    public Sprite detectedSprite;

    [Header("Suspicion readout")]
    public Vector2 suspicionOffset = new Vector2(52f, 0f);   // where the % sits, relative to the crosshair
    public float suspicionFontSize = 28f;
    public Color suspicionLow = new Color(1f, 0.95f, 0.4f);
    public Color suspicionHigh = new Color(1f, 0.2f, 0.1f);

    TextMeshProUGUI suspicionText;
    Camera cam;
    int lastShown = -1;

    void Start()
    {
        if (player == null)
        {
            GameObject p = GameObject.FindWithTag("Player");
            if (p != null) player = p.GetComponent<PlayerController>();
        }

        cam = Camera.main;
        CreateSuspicionText();
    }

    void CreateSuspicionText()
    {
        GameObject go = new GameObject("SuspicionReadout", typeof(RectTransform), typeof(TextMeshProUGUI));
        go.transform.SetParent(transform, false);   // a child of the crosshair, so it follows it

        suspicionText = go.GetComponent<TextMeshProUGUI>();
        suspicionText.fontSize = suspicionFontSize;
        suspicionText.alignment = TextAlignmentOptions.Left;
        suspicionText.raycastTarget = false;
        suspicionText.outlineWidth = 0.25f;
        suspicionText.outlineColor = new Color32(0, 0, 0, 255);
        suspicionText.text = "";

        RectTransform rect = suspicionText.rectTransform;
        rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.pivot = new Vector2(0f, 0.5f);          // text starts at the offset and grows to the right
        rect.sizeDelta = new Vector2(200f, 40f);
        rect.anchoredPosition = suspicionOffset;
    }

    void Update()
    {
        UpdateIcon();
        UpdateSuspicion();
    }

    void UpdateIcon()
    {
        Sprite wanted = normalSprite;

        // The stealth icons only appear while sneaking (like Skyrim's eye).
        // Standing or walking normally always shows the regular crosshair.
        if (player != null && player.IsSneaking)
            wanted = AnyEnemyAlerted() ? detectedSprite : sneakSprite;

        if (image.sprite != wanted)
            image.sprite = wanted;
    }

    // Shows the highest suspicion among enemies on screen that haven't spotted you yet
    void UpdateSuspicion()
    {
        if (suspicionText == null) return;

        float best = 0f;

        foreach (Enemy enemy in Enemy.All)
        {
            if (enemy.IsAlerted) continue;            // already hunting you: the detected icon covers that
            if (enemy.Suspicion <= best) continue;
            if (!IsOnScreen(enemy.transform.position)) continue;
            best = enemy.Suspicion;
        }

        int percent = best > 0.02f ? Mathf.RoundToInt(best * 100f) : 0;
        if (percent == lastShown) return;
        lastShown = percent;

        if (percent == 0)
        {
            suspicionText.text = "";
        }
        else
        {
            suspicionText.text = percent + "%";
            suspicionText.color = Color.Lerp(suspicionLow, suspicionHigh, best);
        }
    }

    bool IsOnScreen(Vector3 worldPosition)
    {
        if (cam == null) return false;

        Vector3 v = cam.WorldToViewportPoint(worldPosition);
        return v.z > 0f && v.x > 0f && v.x < 1f && v.y > 0f && v.y < 1f;
    }

    bool AnyEnemyAlerted()
    {
        foreach (Enemy enemy in Enemy.All)
        {
            if (enemy.IsAlerted) return true;
        }
        return false;
    }
}
