using System.Collections.Generic;
using UnityEngine;

// Draws a glowing line between enemies that have grouped up (like the targeting line in Final Fantasy XII).
// The line breaks when either one runs away, is defeated, or stops fighting.
// You never place this yourself: Enemy.cs creates it the first time two enemies link.
public class PartyLinks : MonoBehaviour
{
    public static readonly Color lineColor = new Color(1f, 0.95f, 0.5f);
    public static float lineWidth = 0.04f;

    class Link
    {
        public Enemy a;
        public Enemy b;
        public LineRenderer line;
    }

    static PartyLinks instance;

    readonly List<Link> links = new List<Link>();
    Material lineMaterial;

    public static void Connect(Enemy a, Enemy b)
    {
        if (a == null || b == null || a == b) return;

        if (instance == null)
            instance = new GameObject("PartyLinks").AddComponent<PartyLinks>();

        instance.AddLink(a, b);
    }

    void OnDestroy()
    {
        if (instance == this) instance = null;
    }

    void AddLink(Enemy a, Enemy b)
    {
        foreach (Link existing in links)
        {
            if ((existing.a == a && existing.b == b) || (existing.a == b && existing.b == a))
                return;   // already linked
        }

        GameObject go = new GameObject("PartyLine");
        go.transform.SetParent(transform, false);

        LineRenderer line = go.AddComponent<LineRenderer>();
        line.positionCount = 2;
        line.useWorldSpace = true;
        line.widthMultiplier = lineWidth;
        line.numCapVertices = 4;
        line.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        line.receiveShadows = false;
        line.material = GetMaterial();

        links.Add(new Link { a = a, b = b, line = line });
    }

    Material GetMaterial()
    {
        if (lineMaterial == null)
        {
            Shader shader = Shader.Find("Sprites/Default");
            if (shader == null) shader = Shader.Find("Unlit/Color");

            lineMaterial = new Material(shader);
            lineMaterial.color = lineColor;
        }
        return lineMaterial;
    }

    void LateUpdate()
    {
        for (int i = links.Count - 1; i >= 0; i--)
        {
            Link link = links[i];

            bool stillLinked = link.a != null && link.b != null && link.a.IsLinkable && link.b.IsLinkable;
            if (!stillLinked)
            {
                Destroy(link.line.gameObject);
                links.RemoveAt(i);
                continue;
            }

            link.line.SetPosition(0, link.a.LinkPoint);
            link.line.SetPosition(1, link.b.LinkPoint);

            // A gentle pulse
            Color c = lineColor;
            c.a = Mathf.Lerp(0.55f, 1f, Mathf.PingPong(Time.time * 1.5f, 1f));
            link.line.startColor = c;
            link.line.endColor = c;
        }
    }
}
