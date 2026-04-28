using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// Attach to an empty GameObject on the putting green.
/// Auto-adds a trigger BoxCollider sized to zoneSize.
/// When the PLAYER enters:
///   - GolfSwingController switches to putter settings (low power, near-zero loft)
///   - Optional Cinemachine Virtual Camera activates
///   - A "PUTTER MODE" UI banner appears
/// On exit everything is restored.
///
/// Unity setup:
///   1. Place empty GameObject on the green, add this component.
///   2. Size the green box via Zone Size in Inspector (visible as green wire cube in Scene view).
///   3. Optionally drag a Cinemachine Virtual Camera into Putting VCam.
/// </summary>
public class PuttingGreenTrigger : MonoBehaviour
{
    [Header("Zone Size")]
    public Vector3 zoneSize = new Vector3(20f, 4f, 20f);

    [Header("Putter Settings")]
    [Tooltip("Max impulse power while in putter mode.")]
    public float putterMaxPower   = 0.8f;
    [Tooltip("Loft angle (degrees) while putting — near 0 for ground-rolling shots.")]
    public float putterLoftAngle  = 2f;
    [Tooltip("Power scale while putting. Lower = shorter shots.")]
    public float putterPowerScale = 0.12f;

    [Header("Camera (optional)")]
    [Tooltip("Cinemachine VirtualCamera to enable when putting. Leave empty to keep current camera.")]
    public GameObject puttingVCam;

    // ── private ──────────────────────────────────────────────────────────────

    private float               _savedMaxPower;
    private float               _savedLoftAngle;
    private float               _savedPowerScale;
    private GolfSwingController _swing;
    private GolfBallInteraction _ballInteraction;
    private bool                _inZone;

    // Banner UI
    private Canvas   _bannerCanvas;
    private TMP_Text _bannerText;

    // ── lifecycle ─────────────────────────────────────────────────────────────

    void Awake()
    {
        var col       = gameObject.AddComponent<BoxCollider>();
        col.size      = zoneSize;
        col.isTrigger = true;
        col.center    = Vector3.zero;
    }

    void Start()
    {
        _swing           = FindFirstObjectByType<GolfSwingController>();
        _ballInteraction = FindFirstObjectByType<GolfBallInteraction>();

        if (_swing == null)
            Debug.LogWarning("[PuttingGreenTrigger] GolfSwingController not found in scene.");

        BuildBannerUI();
    }

    // ── trigger callbacks ─────────────────────────────────────────────────────

    void OnTriggerEnter(Collider other)
    {
        if (_inZone) return;
        if (!IsPlayer(other)) return;

        _inZone = true;

        if (_swing != null)
        {
            _savedMaxPower   = _swing.maxPower;
            _savedLoftAngle  = _swing.loftAngle;
            _savedPowerScale = _swing.powerScale;

            _swing.maxPower   = putterMaxPower;
            _swing.loftAngle  = putterLoftAngle;
            _swing.powerScale = putterPowerScale;
        }

        if (_ballInteraction != null)
            _ballInteraction.stanceMessage = "← → Aim   |   Swing gently for putting";

        SetPuttingCam(true);
        ShowBanner(true);
        Debug.Log("[PuttingGreenTrigger] Entered putting zone — putter mode active.");
    }

    void OnTriggerExit(Collider other)
    {
        if (!_inZone) return;
        if (!IsPlayer(other)) return;

        _inZone = false;

        if (_swing != null)
        {
            _swing.maxPower   = _savedMaxPower;
            _swing.loftAngle  = _savedLoftAngle;
            _swing.powerScale = _savedPowerScale;
        }

        if (_ballInteraction != null)
            _ballInteraction.stanceMessage = "← → Aim   |   ↑ ↓ Loft   |   Swing your phone!";

        SetPuttingCam(false);
        ShowBanner(false);
        Debug.Log("[PuttingGreenTrigger] Exited putting zone — settings restored.");
    }

    // ── helpers ───────────────────────────────────────────────────────────────

    // Only match the player capsule, not the ball rolling through
    static bool IsPlayer(Collider other) =>
        other.CompareTag("Player") || other.name.Contains("PlayerCapsule");

    void SetPuttingCam(bool active)
    {
        if (puttingVCam == null) return;
        puttingVCam.SetActive(active);
    }

    void ShowBanner(bool visible)
    {
        if (_bannerCanvas != null)
            _bannerCanvas.gameObject.SetActive(visible);
    }

    // ── "PUTTER MODE" banner UI ───────────────────────────────────────────────

    void BuildBannerUI()
    {
        var root = new GameObject("PutterModeBanner");

        _bannerCanvas              = root.AddComponent<Canvas>();
        _bannerCanvas.renderMode   = RenderMode.ScreenSpaceOverlay;
        _bannerCanvas.sortingOrder = 55;

        var scaler = root.AddComponent<CanvasScaler>();
        scaler.uiScaleMode         = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);

        // Green pill banner at top-centre
        var panel = new GameObject("Panel");
        panel.transform.SetParent(root.transform, false);
        var img = panel.AddComponent<Image>();
        img.color = new Color(0.05f, 0.6f, 0.05f, 0.85f);
        img.raycastTarget = false;
        var prt = panel.GetComponent<RectTransform>();
        prt.anchorMin        = new Vector2(0.5f, 1f);
        prt.anchorMax        = new Vector2(0.5f, 1f);
        prt.pivot            = new Vector2(0.5f, 1f);
        prt.sizeDelta        = new Vector2(480f, 60f);
        prt.anchoredPosition = new Vector2(0f, -20f);

        var tgo = new GameObject("Text");
        tgo.transform.SetParent(panel.transform, false);
        _bannerText              = tgo.AddComponent<TextMeshProUGUI>();
        _bannerText.text         = "PUTTER MODE";
        _bannerText.fontSize     = 26f;
        _bannerText.fontStyle    = FontStyles.Bold;
        _bannerText.alignment    = TextAlignmentOptions.Center;
        _bannerText.color        = Color.white;
        _bannerText.raycastTarget = false;
        var trt = tgo.GetComponent<RectTransform>();
        trt.anchorMin = Vector2.zero;
        trt.anchorMax = Vector2.one;
        trt.offsetMin = trt.offsetMax = Vector2.zero;

        root.SetActive(false);   // hidden until player enters the zone
    }

    // ── gizmos ────────────────────────────────────────────────────────────────

    void OnDrawGizmos()      => DrawZone(false);
    void OnDrawGizmosSelected() => DrawZone(true);

    void DrawZone(bool selected)
    {
        Matrix4x4 prev = Gizmos.matrix;
        Gizmos.matrix = Matrix4x4.TRS(transform.position, transform.rotation, Vector3.one);

        Gizmos.color = new Color(0f, 1f, 0f, selected ? 0.25f : 0.1f);
        Gizmos.DrawCube(Vector3.zero, zoneSize);

        Gizmos.color = Color.green;
        Gizmos.DrawWireCube(Vector3.zero, zoneSize);

        Gizmos.matrix = prev;

#if UNITY_EDITOR
        UnityEditor.Handles.Label(
            transform.position + Vector3.up * (zoneSize.y * 0.5f + 0.5f),
            "Putting Zone");
#endif
    }
}
