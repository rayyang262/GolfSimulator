using UnityEngine;
using UnityEngine.UI;
using TMPro;
using UnityEngine.Rendering;

/// <summary>
/// Attach to a "SandWedgeZone" empty GameObject near the hole.
///
/// On entry  → ClubSystem switches to Sand Wedge (56° default loft).
///             Player can adjust loft between 45°–62° with ↑ ↓.
///             An orange banner appears at the top of the screen.
///             Orange boundary lines show the zone in Play mode.
///
/// On exit   → previous club (7-Iron) is restored automatically.
///
/// Setup in Unity:
///   1. Create an empty GameObject, name it "SandWedgeZone".
///   2. Add this script.
///   3. Position it near the hole approach area.
///   4. Adjust "Zone Size" in the Inspector to cover the desired area.
///      The orange rectangle drawn in Play mode shows the exact boundary.
/// </summary>
public class SandWedgeTrigger : MonoBehaviour
{
    // ── Inspector ─────────────────────────────────────────────────────────────

    [Header("Zone Size")]
    [Tooltip("Size of the trigger box AND the orange boundary lines drawn in Play mode.")]
    public Vector3 zoneSize = new Vector3(15f, 4f, 15f);

    [Header("Zone Lines (Play Mode)")]
    [Tooltip("Color of the boundary lines drawn around the sand wedge zone during Play mode.")]
    public Color zoneLineColor = new Color(1f, 0.65f, 0.1f, 0.9f);
    [Tooltip("Width of the boundary lines in world metres.")]
    public float zoneLineWidth = 0.12f;

    // ── Private ───────────────────────────────────────────────────────────────

    private GolfSwingController _swing;
    private ClubSystem          _clubs;
    private bool                _inZone;

    // Saved swing values (fallback when ClubSystem is absent)
    private float _savedLoftAngle;
    private float _savedPowerScale;

    // Banner UI
    private Canvas   _bannerCanvas;
    private TMP_Text _bannerText;

    // Zone boundary LineRenderers
    private LineRenderer _zoneLineBottom;
    private LineRenderer _zoneLineTop;

    // ── Lifecycle ─────────────────────────────────────────────────────────────

    void Awake()
    {
        var col       = gameObject.AddComponent<BoxCollider>();
        col.size      = zoneSize;
        col.isTrigger = true;
        col.center    = Vector3.zero;
    }

    void Start()
    {
        _swing = FindFirstObjectByType<GolfSwingController>();
        _clubs = FindFirstObjectByType<ClubSystem>();

        if (_swing == null)
            Debug.LogWarning("[SandWedgeTrigger] GolfSwingController not found in scene.");

        BuildBannerUI();
        BuildZoneLines();
    }

    // ── Trigger callbacks ─────────────────────────────────────────────────────

    void OnTriggerEnter(Collider other)
    {
        if (_inZone) return;
        if (!IsPlayer(other)) return;

        _inZone = true;

        if (_clubs != null)
        {
            _clubs.sandWedgeOverride = true;
            _clubs.SelectClub();
        }
        else if (_swing != null)
        {
            // Fallback: directly write sand wedge values if ClubSystem is absent
            _savedLoftAngle  = _swing.loftAngle;
            _savedPowerScale = _swing.powerScale;
            _swing.loftAngle  = 56f;
            _swing.powerScale = 0.09f;
        }

        ShowBanner(true);
        Debug.Log("[SandWedgeTrigger] Entered sand wedge zone — high-loft mode active.");
    }

    void OnTriggerExit(Collider other)
    {
        if (!_inZone) return;
        if (!IsPlayer(other)) return;

        _inZone = false;

        if (_clubs != null)
        {
            _clubs.sandWedgeOverride = false;
            _clubs.SelectClub();
        }
        else if (_swing != null)
        {
            _swing.loftAngle  = _savedLoftAngle;
            _swing.powerScale = _savedPowerScale;
        }

        ShowBanner(false);
        Debug.Log("[SandWedgeTrigger] Exited sand wedge zone — club restored.");
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    static bool IsPlayer(Collider other) =>
        other.CompareTag("Player") || other.name.Contains("PlayerCapsule");

    void ShowBanner(bool visible)
    {
        if (_bannerCanvas != null) _bannerCanvas.gameObject.SetActive(visible);
    }

    // ── "SAND WEDGE" banner UI ────────────────────────────────────────────────

    void BuildBannerUI()
    {
        var root = new GameObject("SandWedgeModeBanner");

        _bannerCanvas              = root.AddComponent<Canvas>();
        _bannerCanvas.renderMode   = RenderMode.ScreenSpaceOverlay;
        _bannerCanvas.sortingOrder = 55;

        var scaler = root.AddComponent<CanvasScaler>();
        scaler.uiScaleMode         = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);

        // Sandy-orange pill at top of screen
        var panel = new GameObject("Panel");
        panel.transform.SetParent(root.transform, false);
        var img = panel.AddComponent<Image>();
        img.color         = new Color(0.72f, 0.44f, 0.04f, 0.90f);
        img.raycastTarget = false;
        var prt = panel.GetComponent<RectTransform>();
        prt.anchorMin        = new Vector2(0.5f, 1f);
        prt.anchorMax        = new Vector2(0.5f, 1f);
        prt.pivot            = new Vector2(0.5f, 1f);
        prt.sizeDelta        = new Vector2(520f, 56f);
        prt.anchoredPosition = new Vector2(0f, -20f);

        var tgo = new GameObject("Text");
        tgo.transform.SetParent(panel.transform, false);
        _bannerText              = tgo.AddComponent<TextMeshProUGUI>();
        _bannerText.text         = "SAND WEDGE  ·  ↑ ↓ to adjust loft  (45°–62°)";
        _bannerText.fontSize     = 21f;
        _bannerText.fontStyle    = FontStyles.Bold;
        _bannerText.alignment    = TextAlignmentOptions.Center;
        _bannerText.color        = Color.white;
        _bannerText.raycastTarget = false;
        var trt = tgo.GetComponent<RectTransform>();
        trt.anchorMin = Vector2.zero;
        trt.anchorMax = Vector2.one;
        trt.offsetMin = trt.offsetMax = Vector2.zero;

        root.SetActive(false);
    }

    // ── Play-mode zone boundary lines ─────────────────────────────────────────

    void BuildZoneLines()
    {
        _zoneLineBottom = BuildRect("SWZoneLineBottom", -zoneSize.y * 0.5f + 0.08f);
        _zoneLineTop    = BuildRect("SWZoneLineTop",     zoneSize.y * 0.5f - 0.08f);
    }

    LineRenderer BuildRect(string goName, float localY)
    {
        var go = new GameObject(goName);
        go.transform.SetParent(transform, false);

        var lr = go.AddComponent<LineRenderer>();
        lr.positionCount     = 5;
        lr.loop              = false;
        lr.startWidth        = zoneLineWidth;
        lr.endWidth          = zoneLineWidth;
        lr.useWorldSpace     = false;
        lr.shadowCastingMode = ShadowCastingMode.Off;
        lr.receiveShadows    = false;

        var mat = new Material(Shader.Find("Sprites/Default"));
        mat.color = zoneLineColor;
        lr.material   = mat;
        lr.startColor = zoneLineColor;
        lr.endColor   = zoneLineColor;

        SetRectPositions(lr, localY);
        return lr;
    }

    void SetRectPositions(LineRenderer lr, float localY)
    {
        float hx = zoneSize.x * 0.5f;
        float hz = zoneSize.z * 0.5f;
        lr.SetPosition(0, new Vector3(-hx, localY, -hz));
        lr.SetPosition(1, new Vector3( hx, localY, -hz));
        lr.SetPosition(2, new Vector3( hx, localY,  hz));
        lr.SetPosition(3, new Vector3(-hx, localY,  hz));
        lr.SetPosition(4, new Vector3(-hx, localY, -hz));
    }

    void OnValidate()
    {
        if (_zoneLineBottom != null)
            SetRectPositions(_zoneLineBottom, -zoneSize.y * 0.5f + 0.08f);
        if (_zoneLineTop != null)
            SetRectPositions(_zoneLineTop, zoneSize.y * 0.5f - 0.08f);

        var col = GetComponent<BoxCollider>();
        if (col != null) { col.size = zoneSize; col.center = Vector3.zero; }
    }

    // ── Gizmos (Scene view only) ──────────────────────────────────────────────

    void OnDrawGizmos()           => DrawZoneGizmo(false);
    void OnDrawGizmosSelected()   => DrawZoneGizmo(true);

    void DrawZoneGizmo(bool selected)
    {
        Matrix4x4 prev = Gizmos.matrix;
        Gizmos.matrix  = Matrix4x4.TRS(transform.position, transform.rotation, Vector3.one);

        Gizmos.color = new Color(1f, 0.65f, 0.1f, selected ? 0.22f : 0.07f);
        Gizmos.DrawCube(Vector3.zero, zoneSize);

        Gizmos.color = new Color(1f, 0.65f, 0.1f, 1f);
        Gizmos.DrawWireCube(Vector3.zero, zoneSize);

        Gizmos.matrix = prev;

#if UNITY_EDITOR
        UnityEditor.Handles.Label(
            transform.position + Vector3.up * (zoneSize.y * 0.5f + 0.5f),
            "Sand Wedge Zone");
#endif
    }
}
