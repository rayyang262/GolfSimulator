using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Custom UI Graphic that draws three staggered squiggly wind arrows.
/// Arrows point in the local +X direction; the parent RectTransform rotates
/// to match the projected world wind direction.
/// Alphas are driven each frame by WindSpeedOverlay.
/// </summary>
[RequireComponent(typeof(CanvasRenderer))]
public class WindArrowsGraphic : Graphic
{
    // Per-arrow alpha set by WindSpeedOverlay every frame
    private float _a0, _a1, _a2;

    public void SetAlphas(float a0, float a1, float a2)
    {
        if (Mathf.Approximately(_a0, a0) &&
            Mathf.Approximately(_a1, a1) &&
            Mathf.Approximately(_a2, a2)) return;
        _a0 = a0; _a1 = a1; _a2 = a2;
        SetVerticesDirty();
    }

    protected override void OnPopulateMesh(VertexHelper vh)
    {
        vh.Clear();
        // Three arrows: back-left, centre, front-right — staggered diagonally
        DrawArrow(vh, -13f, -16f, _a0);
        DrawArrow(vh,   0f,   0f, _a1);
        DrawArrow(vh,  13f,  16f, _a2);
    }

    // ── arrow drawing ─────────────────────────────────────────────────

    void DrawArrow(VertexHelper vh, float cx, float cy, float alpha)
    {
        if (alpha <= 0.01f) return;

        const int   steps      = 20;
        const float bodyLen    = 34f;
        const float amplitude  = 6f;
        const float waveCycles = 1.5f;   // 1.5 cycles → wave ends at y=0
        const float thickness  = 3.8f;
        const float headLen    = 11f;
        const float headWidth  = 8f;

        Color c      = new Color(1f, 1f, 1f, alpha);
        float startX = cx - bodyLen * 0.5f;

        // Wavy body
        Vector2 prev = WavePoint(startX, cy, bodyLen, amplitude, waveCycles, 0f);
        for (int i = 1; i <= steps; i++)
        {
            float   t    = (float)i / steps;
            Vector2 curr = WavePoint(startX, cy, bodyLen, amplitude, waveCycles, t);
            Segment(vh, prev, curr, thickness, c);
            prev = curr;
        }

        // Arrowhead (solid triangle pointing +X from the wave end, which is at cy)
        Vector2 tip = new Vector2(startX + bodyLen + headLen, cy);
        Vector2 b1  = new Vector2(startX + bodyLen, cy + headWidth * 0.5f);
        Vector2 b2  = new Vector2(startX + bodyLen, cy - headWidth * 0.5f);

        int idx = vh.currentVertCount;
        UIVertex v = UIVertex.simpleVert;
        v.color = c;
        v.position = tip; vh.AddVert(v);
        v.position = b1;  vh.AddVert(v);
        v.position = b2;  vh.AddVert(v);
        vh.AddTriangle(idx, idx + 1, idx + 2);
    }

    static Vector2 WavePoint(float startX, float cy, float len, float amp, float cycles, float t)
        => new Vector2(startX + t * len,
                       cy + Mathf.Sin(t * cycles * Mathf.PI * 2f) * amp);

    static void Segment(VertexHelper vh, Vector2 a, Vector2 b, float thickness, Color c)
    {
        Vector2 dir = b - a;
        if (dir.sqrMagnitude < 0.0001f) return;
        dir.Normalize();
        Vector2 perp = new Vector2(-dir.y, dir.x) * (thickness * 0.5f);

        int idx = vh.currentVertCount;
        UIVertex v = UIVertex.simpleVert;
        v.color = c;

        v.position = a + perp;  vh.AddVert(v);
        v.position = a - perp;  vh.AddVert(v);
        v.position = b + perp;  vh.AddVert(v);
        v.position = b - perp;  vh.AddVert(v);

        vh.AddTriangle(idx,     idx + 2, idx + 1);
        vh.AddTriangle(idx + 1, idx + 2, idx + 3);
    }
}
