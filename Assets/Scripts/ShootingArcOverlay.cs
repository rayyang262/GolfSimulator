using UnityEngine;

/// <summary>
/// Attach to PlayerCapsule (alongside GolfSwingController).
///
/// Shows a dashed orange-red arc from the ball to the hole when:
///   (1) the shootingArc stat-card toggle is active, AND
///   (2) the player has pressed B and entered swing stance (swingController.readyToHit == true).
///
/// Wire up in the Inspector:
///   • shootingArcToggle  — the StatCardToggle component on the "shootingArc" UI card
///   • swingController    — auto-found on this GameObject if left blank
///   • holeTarget         — the Hole Transform in the scene
/// </summary>
public class ShootingArcOverlay : MonoBehaviour
{
    [Header("References")]
    public StatCardToggle   shootingArcToggle;
    public GolfSwingController swingController;
    [Tooltip("The Hole Transform the arc aims toward.")]
    public Transform        holeTarget;

    [Header("Arc Settings")]
    [Tooltip("Number of vertices along the arc — higher = smoother curve.")]
    public int   arcResolution  = 60;
    [Tooltip("Arc peak height as a fraction of the horizontal ball-to-hole distance.")]
    public float arcHeightScale = 0.13f;

    [Header("Line Appearance")]
    public float lineWidth = 0.10f;
    [Tooltip("World-space length (metres) of one dash + one gap.")]
    public float dashCycleLength = 5f;
    [Tooltip("Metres in front of the ball where the arc begins, so it doesn't overlap the player.")]
    public float startOffset = 2.5f;

    // ── private ───────────────────────────────────────────────────────────
    private LineRenderer _lr;
    private GameObject   _arcGO;
    private Transform    _ballTransform;

    // ── lifecycle ─────────────────────────────────────────────────────────

    void Start()
    {
        if (swingController == null)
            swingController = GetComponent<GolfSwingController>();

        BuildArcRenderer();
    }

    void Update()
    {
        RefreshBall();

        bool shouldShow = shootingArcToggle != null
                       && shootingArcToggle.IsActive
                       && swingController   != null
                       && swingController.readyToHit
                       && _ballTransform    != null
                       && holeTarget        != null;

        if (_arcGO.activeSelf != shouldShow)
            _arcGO.SetActive(shouldShow);

        if (shouldShow)
            UpdateArcPositions();
    }

    // ── ball tracking ─────────────────────────────────────────────────────

    void RefreshBall()
    {
        // Unity fake-null fires when the ball GO has been destroyed between spawns
        if (_ballTransform != null && _ballTransform.gameObject.activeInHierarchy)
            return;

        _ballTransform = null;
        GameObject go = null;
        try   { go = GameObject.FindWithTag("GolfBall"); } catch { }
        if (go == null) go = GameObject.Find("GolfBall_Active");
        if (go != null) _ballTransform = go.transform;
    }

    // ── arc math ──────────────────────────────────────────────────────────

    void UpdateArcPositions()
    {
        Vector3 ballPos = _ballTransform.position + Vector3.up * 0.1f;
        Vector3 end     = holeTarget.position;

        // Nudge the arc origin forward so it clears the player's body
        Vector3 toHole  = (end - ballPos);
        toHole.y        = 0f;
        float fullDist  = toHole.magnitude;
        Vector3 start   = fullDist > 0.001f
            ? ballPos + toHole.normalized * Mathf.Min(startOffset, fullDist * 0.15f)
            : ballPos;

        float horizDist = Vector3.Distance(
            new Vector3(start.x, 0f, start.z),
            new Vector3(end.x,   0f, end.z));

        float peakHeight = Mathf.Max(horizDist * arcHeightScale, 1.5f);

        float   arcLength = 0f;
        Vector3 prev      = start;

        for (int i = 0; i < arcResolution; i++)
        {
            float t   = (float)i / (arcResolution - 1);
            Vector3 p = Vector3.Lerp(start, end, t);
            // sin(π·t) gives a smooth arch peaking at t=0.5
            p.y += Mathf.Sin(t * Mathf.PI) * peakHeight;
            _lr.SetPosition(i, p);

            arcLength += Vector3.Distance(prev, p);
            prev       = p;
        }

        // Keep dash density constant in world space regardless of shot distance
        _lr.material.mainTextureScale = new Vector2(arcLength / dashCycleLength, 1f);
    }

    // ── renderer setup ────────────────────────────────────────────────────

    void BuildArcRenderer()
    {
        _arcGO = new GameObject("ShootingArcLine");

        _lr = _arcGO.AddComponent<LineRenderer>();
        _lr.positionCount     = arcResolution;
        _lr.startWidth        = lineWidth;
        _lr.endWidth          = lineWidth * 0.55f;   // taper slightly toward the hole
        _lr.useWorldSpace     = true;
        _lr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        _lr.textureMode       = LineTextureMode.Tile;

        // 4-pixel texture: 2 opaque → dash, 2 transparent → gap
        var tex        = new Texture2D(4, 1, TextureFormat.RGBA32, false);
        tex.filterMode = FilterMode.Point;
        tex.wrapMode   = TextureWrapMode.Repeat;
        tex.SetPixels(new Color[]
        {
            new Color(1f, 1f, 1f, 1f),
            new Color(1f, 1f, 1f, 1f),
            new Color(0f, 0f, 0f, 0f),
            new Color(0f, 0f, 0f, 0f),
        });
        tex.Apply();

        var mat         = new Material(Shader.Find("Sprites/Default"));
        mat.mainTexture = tex;
        _lr.material    = mat;

        // Orangish-red
        var arcColor    = new Color(1f, 0.32f, 0.08f, 1f);
        _lr.startColor  = arcColor;
        _lr.endColor    = new Color(arcColor.r, arcColor.g, arcColor.b, 0.55f);

        _arcGO.SetActive(false);
    }
}
