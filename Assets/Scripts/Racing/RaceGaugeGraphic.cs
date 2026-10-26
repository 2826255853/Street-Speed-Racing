using UnityEngine;
using UnityEngine.UI;

public sealed class RaceGaugeGraphic : MaskableGraphic
{
    public enum Layer { Dial, Needle }
    public Layer layer;
    private float maximum = 7600f;
    private float rangeMin, rangeMax, target;
    private float rpm;
    private bool launch, inRange;
    public static readonly Color Green = new Color(0.48f, 0.95f, 0.30f);
    public static readonly Color Red = new Color(1f, 0.24f, 0.24f);
    public Color NeedleTint { get; private set; } = Color.white;

    public void SetData(float max, float min, float maxRange, float launchTarget, bool isLaunch, bool hit, float displayedRpm)
    {
        bool changed = maximum != max || rangeMin != min || rangeMax != maxRange || target != launchTarget ||
            launch != isLaunch || inRange != hit || (layer == Layer.Needle && Mathf.Abs(rpm - displayedRpm) > 0.5f);
        maximum = Mathf.Max(1f, max);
        rangeMin = min;
        rangeMax = maxRange;
        target = launchTarget;
        launch = isLaunch;
        inRange = hit;
        if (layer == Layer.Needle) NeedleTint = hit ? Green : Color.white;
        rpm = displayedRpm;
        if (changed) SetVerticesDirty();
    }

    protected override void OnPopulateMesh(VertexHelper mesh)
    {
        mesh.Clear();
        Rect rect = GetPixelAdjustedRect();
        float radius = rect.width * 0.465f;
        Vector2 center = new Vector2(rect.center.x, rect.yMin + rect.height * 0.255f);
        if (layer == Layer.Needle)
        {
            Vector2 direction = Direction(rpm / maximum);
            Vector2 side = new Vector2(-direction.y, direction.x);
            Vector2 basePoint = center - direction * (radius * 0.12f);
            Vector2 shoulder = center + direction * (radius * 0.69f);
            Vector2 tip = center + direction * (radius * 0.90f);
            Color outline = new Color(0.025f, 0.029f, 0.034f, 0.95f);

            // A dark edge keeps the pointer visible over the gauge markings.
            Quad(mesh, basePoint + side * 8f, shoulder + side * 6f, shoulder - side * 6f, basePoint - side * 8f, outline);
            Triangle(mesh, tip, shoulder + side * 7f, shoulder - side * 7f, outline);
            Quad(mesh, basePoint + side * 4.5f, shoulder + side * 3.2f, shoulder - side * 3.2f, basePoint - side * 4.5f, NeedleTint);
            Triangle(mesh, tip, shoulder + side * 4f, shoulder - side * 4f, NeedleTint);
            Disc(mesh, center, 13f, outline);
            Disc(mesh, center, 8f, NeedleTint);
            return;
        }

        Arc(mesh, center, 0f, radius + 8f, 0f, 1f, new Color(0.025f, 0.029f, 0.034f, 0.93f));
        Quad(mesh, new Vector2(center.x - radius - 8f, rect.yMin + 10f),
            center + Vector2.left * (radius + 8f), center + Vector2.right * (radius + 8f),
            new Vector2(center.x + radius + 8f, rect.yMin + 10f), new Color(0.025f, 0.029f, 0.034f, 0.93f));
        Arc(mesh, center, radius + 5f, radius + 8f, 0f, 1f, new Color(0.55f, 0.58f, 0.60f, 0.9f));
        Arc(mesh, center, radius - 3f, radius, 0f, 1f, new Color(0.14f, 0.16f, 0.17f));
        Arc(mesh, center, radius * 0.91f, radius * 0.95f, 0f, 1f, new Color(0.22f, 0.24f, 0.25f));
        Arc(mesh, center, radius * 0.91f, radius * 0.95f, 0.95f, 1f, Red);
        float min = Mathf.Clamp01(rangeMin / maximum);
        float max = Mathf.Clamp01(rangeMax / maximum);
        Arc(mesh, center, radius * 0.91f, radius * 0.95f, min, max, inRange ? Green : new Color(0.30f, 0.65f, 0.23f));
        Arc(mesh, center, radius * 0.67f, radius * 0.674f, 0.02f, 0.98f, new Color(0.29f, 0.32f, 0.33f));

        int subdivisions = Mathf.Clamp(Mathf.CeilToInt(maximum / 250f), 8, 80);
        for (int i = 0; i <= subdivisions; i++)
        {
            float t = i / (float)subdivisions;
            bool major = i % 4 == 0 || i == subdivisions;
            Vector2 direction = Direction(t);
            Vector2 side = new Vector2(-direction.y, direction.x) * (major ? 1.4f : 0.65f);
            Vector2 outer = center + direction * (radius * 0.88f);
            Vector2 inner = center + direction * (radius * (major ? 0.81f : 0.845f));
            Quad(mesh, inner + side, outer + side, outer - side, inner - side,
                t >= 0.95f ? Red : new Color(0.84f, 0.86f, 0.87f, major ? 1f : 0.55f));
        }
        if (launch)
        {
            Vector2 direction = Direction(target / maximum);
            Vector2 side = new Vector2(-direction.y, direction.x) * 4f;
            Vector2 point = center + direction * (radius * 0.965f);
            Triangle(mesh, point, point + direction * 10f + side, point + direction * 10f - side, Green);
        }
    }

    public static Vector2 Direction(float fraction)
    {
        float angle = Mathf.PI * (1f - Mathf.Clamp01(fraction));
        return new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));
    }

    private static void Arc(VertexHelper mesh, Vector2 center, float inner, float outer, float start, float end, Color tint)
    {
        if (end <= start) return;
        int steps = Mathf.Max(1, Mathf.CeilToInt((end - start) * 100f));
        for (int i = 0; i < steps; i++)
        {
            Vector2 a = Direction(Mathf.Lerp(start, end, i / (float)steps));
            Vector2 b = Direction(Mathf.Lerp(start, end, (i + 1f) / steps));
            Quad(mesh, center + a * inner, center + a * outer, center + b * outer, center + b * inner, tint);
        }
    }

    private static void Quad(VertexHelper mesh, Vector2 a, Vector2 b, Vector2 c, Vector2 d, Color tint)
    {
        int index = mesh.currentVertCount;
        mesh.AddVert(a, tint, Vector2.zero);
        mesh.AddVert(b, tint, Vector2.zero);
        mesh.AddVert(c, tint, Vector2.zero);
        mesh.AddVert(d, tint, Vector2.zero);
        mesh.AddTriangle(index, index + 1, index + 2);
        mesh.AddTriangle(index, index + 2, index + 3);
    }

    private static void Triangle(VertexHelper mesh, Vector2 a, Vector2 b, Vector2 c, Color tint)
    {
        int index = mesh.currentVertCount;
        mesh.AddVert(a, tint, Vector2.zero);
        mesh.AddVert(b, tint, Vector2.zero);
        mesh.AddVert(c, tint, Vector2.zero);
        mesh.AddTriangle(index, index + 1, index + 2);
    }

    private static void Disc(VertexHelper mesh, Vector2 center, float radius, Color tint)
    {
        const int segments = 24;
        for (int i = 0; i < segments; i++)
        {
            float start = i * Mathf.PI * 2f / segments;
            float end = (i + 1) * Mathf.PI * 2f / segments;
            Vector2 a = center + new Vector2(Mathf.Cos(start), Mathf.Sin(start)) * radius;
            Vector2 b = center + new Vector2(Mathf.Cos(end), Mathf.Sin(end)) * radius;
            Triangle(mesh, center, a, b, tint);
        }
    }
}
