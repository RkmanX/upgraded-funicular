using System.Collections.Generic;
using System.Text;
using TMPro;
using UnityEngine;

public enum HitType { Normal, Crit, Backstab, Blocked }

// A small on-screen log of your recent hits, e.g. "BACKSTAB 45 DMG".
// Lines fade out and disappear on their own.
public class DamageReadout : MonoBehaviour
{
    public static DamageReadout Instance { get; private set; }

    public TMP_Text text;                  // the TextMeshPro text to write into
    public int maxLines = 4;               // how many recent hits to show at once
    public float lineLifetime = 2.5f;      // seconds before a line is gone
    public float fadeTime = 0.6f;          // a line fades during the last part of its life

    public Color normalColor = Color.white;
    public Color critColor = new Color(1f, 0.35f, 0.25f);
    public Color backstabColor = new Color(1f, 0.82f, 0.2f);
    public Color musicColor = new Color(0.55f, 0.8f, 1f); // used for music/area messages
    public Color blockedColor = new Color(0.7f, 0.7f, 0.78f);

    class Line
    {
        public string message;
        public Color color;
        public float timeLeft;
    }

    readonly List<Line> lines = new List<Line>();
    readonly StringBuilder sb = new StringBuilder();

    void Awake()
    {
        Instance = this;
        text.text = "";
    }

    void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    public void Show(float amount, HitType type, string suffix = "")
    {
        string label = "";
        Color color = normalColor;

        if (type == HitType.Backstab) { label = "BACKSTAB "; color = backstabColor; }
        else if (type == HitType.Crit) { label = "CRIT "; color = critColor; }
        else if (type == HitType.Blocked) { label = "BLOCKED "; color = blockedColor; }

        ShowMessage($"{label}{amount:0} DMG{suffix}", color);
    }

    // Adds any line of text to the readout (music changes, area changes, etc.)
    public void ShowMessage(string message, Color color)
    {
        message = Sanitize(message);

        lines.Add(new Line
        {
            message = message,
            color = color,
            timeLeft = lineLifetime
        });

        if (lines.Count > maxLines)
            lines.RemoveAt(0); // drop the oldest
    }

    // The default font only draws plain English letters, numbers and punctuation.
    // Anything else (like Japanese track names) would show as big boxes, so show '?' instead.
    static string Sanitize(string message)
    {
        message = message.Replace('\uFF1A', ':');   // full-width colon -> normal colon

        StringBuilder clean = new StringBuilder(message.Length);
        foreach (char c in message)
            clean.Append((c >= ' ' && c <= '~') || c == '\n' ? c : '?');

        return clean.ToString();
    }

    void Update()
    {
        if (lines.Count == 0)
        {
            if (text.text.Length > 0) text.text = "";
            return;
        }

        // Count down and remove expired lines (loop backwards so removing is safe)
        for (int i = lines.Count - 1; i >= 0; i--)
        {
            lines[i].timeLeft -= Time.deltaTime;
            if (lines[i].timeLeft <= 0f) lines.RemoveAt(i);
        }

        // Rebuild the text, using rich-text color tags so each line can fade on its own
        sb.Clear();
        foreach (Line line in lines)
        {
            Color c = line.color;
            c.a = Mathf.Clamp01(line.timeLeft / fadeTime);
            sb.Append("<color=#").Append(ColorUtility.ToHtmlStringRGBA(c)).Append('>')
              .Append(line.message)
              .Append("</color>\n");
        }
        text.text = sb.ToString();
    }
}
