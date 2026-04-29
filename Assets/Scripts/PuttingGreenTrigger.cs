using UnityEngine;
using UnityEngine.UI;
using TMPro;
using UnityEngine.Rendering;

/// <summary>
/// Attach to the "PuttingZone" empty GameObject on the putting green.
///
/// NEW FEATURES:
///   • Camera tilts steep-down on entry so ball + hole are visible together
///   • Distance reference card shows estimated roll for tap / medium / full swing
///   • Green LineRenderer rectangle marks the zone boundary in Play mode
///     so you can resize it at runtime by adjusting Zone Size in the Inspector
/// </summary>
public class PuttingGreenTrigger : MonoBehaviour
{
    // ── Inspector ─────────────────────────────────────────────────────────────

    [Header("Zone Size")]
    [Tooltip("Size of the trigger box AND the green boundary lines drawn in Play mode.")]
    public Vector3 zoneSize = new Vector3(20f, 4f, 20f);

    [Header("Putter Settings (fallback — ClubSystem overrides these if present)")]
    public float putterMaxPower   = 0.8f;
    public float putterLoftAngle  = 2f;
    public float putterPowerScale = 0.12f;

    [Header("Camera on Entry")]
    [Tooltip("How steeply the camera tilts downward when entering the putting zone.")]
    public float putterCameraPitch = 62f;

    [Header("Camera (optional Cinemachine VCam)")]
    public GameObject puttingVCam;

    [Header("Zone Lines (Play Mode)")]
    [Tooltip("Color of the boundary lines drawn around the putting green during Play mode.")]
    public Color zoneLineColor = new Color(0.1f, 1f, 0.1f, 0.9f);
    [Tooltip("Width of the boundary lines in world metres.")]
    public float zoneLineWidth = 0.12f;

    // ── Private ───────────────────────────────────────────────────────────────

    private GolfSwingController _swing;
    private GolfBallInteraction _ballInteraction;
    private ClubSystem          _clubs;
    private bool                _inZone;

    // Camera
    private Transform  _camRoot;
    private Quaternion _savedCamRot;

    // Saved swing values (fallback when ClubSystem absent)
    private float _savedMaxPower;
    private float _savedLoftAngle;
    private float _savedPowerScale;

    // Banner UI ("PUTTER MODE" pill at top)
    private Canvas   _bannerCanvas;
    private TMP_Text _bannerText;

    // Distance reference card
    private Canvas   _distCanvas;
    private TMP_Text _distText;

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
        _swing           = FindFirstObjectByType<GolfSwingController>();
        _ballInteraction = FindFirstObjectByType<GolfBallInteraction>();
        _clubs           = FindFirstObjectByType<ClubSystem>();

        if (_swing == null)
            Debug.LogWarning("[PuttingGreenTrigger] GolfSwingController not found in scene.");

        // Find PlayerCameraRoot (child of PlayerCapsule)
        if (_swing != null)
        {
            foreach (Transform t in _swing.GetComponentsInChildren<Transform>(true))
            {
                if (t.name == "PlayerCameraRoot" || t.name.Contains("CameraRoot"))
                { _camRoot = t; break; }
            }
        }

        BuildBannerUI();
        BuildDistanceUI();
        BuildZoneLines();
    }

    // ── Trigger callbacks ─────────────────────────────────────────────────────

    void OnTriggerEnter(Collider other)
    {
        if (_inZone) return;
        if (!IsPlayer(other)) return;

        _inZone = true;

        // ── Club settings ──────────────────────────────────────────────────────
        if (_clubs != null)
        {
            _clubs.putterOverride = true;
            _clubs.SelectClub();
        }
        else if (_swing != null)
        {
            _savedMaxPower   = _swing.maxPower;
            _savedLoftAngle  = _swing.loftAngle;
            _savedPowerScale = _swing.powerScale;
            _swing.maxPower   = putterMaxPower;
            _swing.loftAngle  = putterLoftAngle;
            _swing.powerScale = putterPowerScale;
        }

        // ── Camera: tilt steeply down so ball + hole are both visible ──────────
        if (_camRoot != null)
        {
            _savedCamRot           = _camRoot.localRotation;
            _camRoot.localRotation = Quaternion.Euler(putterCameraPitch, 0f, 0f);
        }

        if (_ballInteraction != null)
            _ballInteraction.stanceMessage = "← → Aim   |   Swing gently for putting";

        SetPuttingCam(true);
        ShowBanner(true);
        ShowDistancePanel(true);
        RefreshDistanceText();

        Debug.Log("[PuttingGreenTrigger] Entered putting zone — putter mode active.");
    }

    void OnTriggerExit(Collider other)
    {
        if (!_inZone) return;
        if (!IsPlayer(other)) return;

        _inZone = false;

        // ── Restore club settings ──────────────────────────────────────────────
        if (_clubs != null)
        {
            _clubs.putterOverride = false;
            _clubs.SelectClub();
        }
        else if (_swing != null)
        {
            _swing.maxPower   = _savedMaxPower;
            _swing.loftAngle  = _savedLoftAngle;
            _swing.powerScale = _savedPowerScale;
        }

        // ── Restore camera ─────────────────────────────────────────────────────
        if (_camRoot != null)
            _camRoot.localRotation = _savedCamRot;

        if (_ballInteraction != null)
            _ballInteraction.stanceMessage = "← → Aim   |   ↑ ↓ Loft   |   Swing your phone!";

        SetPuttingCam(false);
        ShowBanner(false);
        ShowDistancePanel(false);

        Debug.Log("[PuttingGreenTrigger] Exited putting zone — settings restored.");
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    static bool IsPlayer(Collider other) =>
        other.CompareTag("Player") || other.name.Contains("PlayerCapsule");

    void SetPuttingCam(bool active)
    {
        if (puttingVCam != null) puttingVCam.SetActive(active);
    }

    void ShowBanner(bool visible)
    {
        if (_bannerCanvas != null) _bannerCanvas.gameObject.SetActive(visible);
    }

    void ShowDistancePanel(bool visible)
    {
        if (_distCanvas != null) _distCanvas.gameObject.SetActive(visible);
    }

    // ── Distance estimate ─────────────────────────────────────────────────────

    void RefreshDistanceText()
    {
        float maxPwr   = _swing   != null ? _swing.maxPower   : putterMaxPower;
        float pScale   = _swing   != null ? _swing.powerScale : putterPowerScale;
        float rollDrag = _clubs   != null ? _clubs.putter.rollingLinearDamping : 2.8f;

        float tapDist    = EstimateRollDistance(0.25f, maxPwr, pScale, rollDrag);
        float mediumDist = EstimateRollDistance(0.75f, maxPwr, pScale, rollDrag);
        float fullDist   = EstimateRollDistance(1.00f, maxPwr, pScale, rollDrag);

        if (_distText != null)
        {
            _distText.text =
                $"<b>PUTT RANGE</b>\n" +
                $"<size=80%>Tap     </size><color=#AAFFAA>{tapDist:F1} m</color>\n" +
                $"<size=80%>Medium  </size><color=#FFFF88>{mediumDist:F1} m</color>\n" +
                $"<size=80%>Full    </size><color=#FFAA88>{fullDist:F1} m</color>";
        }
    }

    /// <summary>
    /// Numerically simulates the ball rolling to a stop under putter physics.
    /// Matches GolfBallRoller.FixedUpdate deceleration exactly.
    /// </summary>
    static float EstimateRollDistance(float powerFraction, float maxPower,
                                      float powerScale, float rollingDrag)
    {
        const float BallMass      = 0.046f;
        const float StopThreshold = 0.35f;
        const float Dt            = 0.02f;   // fixed timestep
        const int   MaxSteps      = 4000;    // cap at 80 s of simulation

        float force = maxPower * powerFraction * powerScale;
        float v     = force / BallMass;      // initial rolling speed (m/s)
        float dist  = 0f;

        for (int i = 0; i < MaxSteps; i++)
        {
            if (v <= StopThreshold) break;

            // Unity linear drag (matches Rigidbody internal formula)
            v *= Mathf.Max(0f, 1f - rollingDrag * Dt);

            // Extra rolling resistance — must match GolfBallRoller.FixedUpdate coefficients
            float resistance = v * v * 0.08f + v * 0.05f;
            v = Mathf.Max(0f, v - resistance * Dt);

            dist += v * Dt;
        }

        return dist;
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

        var panel = new GameObject("Panel");
        panel.transform.SetParent(root.transform, false);
        var img = panel.AddComponent<Image>();
        img.color         = new Color(0.05f, 0.55f, 0.05f, 0.88f);
        img.raycastTarget = false;
        var prt = panel.GetComponent<RectTransform>();
        prt.anchorMin        = new Vector2(0.5f, 1f);
        prt.anchorMax        = new Vector2(0.5f, 1f);
        prt.pivot            = new Vector2(0.5f, 1f);
        prt.sizeDelta        = new Vector2(400f, 56f);
        prt.anchoredPosition = new Vector2(0f, -20f);

        var tgo = new GameObject("Text");
        tgo.transform.SetParent(panel.transform, false);
        _bannerText              = tgo.AddComponent<TextMeshProUGUI>();
        _bannerText.text         = "PUTTER MODE";
        _bannerText.fontSize     = 24f;
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

    // ── Distance reference card ───────────────────────────────────────────────

    void BuildDistanceUI()
    {
        var root = new GameObject("PutterDistanceCanvas");

        _distCanvas              = root.AddComponent<Canvas>();
        _distCanvas.renderMode   = RenderMode.ScreenSpaceOverlay;
        _distCanvas.sortingOrder = 56;

        var scaler = root.AddComponent<CanvasScaler>();
        scaler.uiScaleMode         = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);

        // Dark card — bottom-right corner
        var panel = new GameObject("Panel");
        panel.transform.SetParent(root.transform, false);
        var img = panel.AddComponent<Image>();
        img.color         = new Color(0f, 0f, 0f, 0.78f);
        img.raycastTarget = false;
        var prt = panel.GetComponent<RectTransform>();
        prt.anchorMin        = new Vector2(1f, 0f);
        prt.anchorMax        = new Vector2(1f, 0f);
        prt.pivot            = new Vector2(1f, 0f);
        prt.sizeDelta        = new Vector2(220f, 115f);
        prt.anchoredPosition = new Vector2(-20f, 20f);

        var tgo = new GameObject("Text");
        tgo.transform.SetParent(panel.transform, false);
        _distText              = tgo.AddComponent<TextMeshProUGUI>();
        _distText.text         = "PUTT RANGE\n— m\n— m\n— m";
        _distText.fontSize     = 18f;
        _distText.alignment    = TextAlignmentOptions.Left;
        _distText.color        = Color.white;
        _distText.raycastTarget = false;
        _distText.richText      = true;
        var trt = tgo.GetComponent<RectTransform>();
        trt.anchorMin = Vector2.zero;
        trt.anchorMax = Vector2.one;
        trt.offsetMin = new Vector2(12f, 8f);
        trt.offsetMax = new Vector2(-8f, -8f);

        root.SetActive(false);
    }

    // ── Play-mode zone boundary lines ─────────────────────────────────────────

    /// <summary>
    /// Draws two green rectangles (bottom and top of the trigger box) in Play mode
    /// so you can see exactly where the zone is and resize it via Zone Size.
    /// </summary>
    void BuildZoneLines()
    {
        _zoneLineBottom = BuildRect("ZoneLineBottom", -zoneSize.y * 0.5f + 0.08f);
        _zoneLineTop    = BuildRect("ZoneLineTop",     zoneSize.y * 0.5f - 0.08f);
    }

    LineRenderer BuildRect(string goName, float localY)
    {
        var go = new GameObject(goName);
        go.transform.SetParent(transform, false);

        var lr = go.AddComponent<LineRenderer>();
        lr.positionCount     = 5;          // 4 corners + close loop
        lr.loop              = false;
        lr.startWidth        = zoneLineWidth;
        lr.endWidth          = zoneLineWidth;
        lr.useWorldSpace     = false;      // local space → moves with this GameObject
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
        lr.SetPosition(4, new Vector3(-hx, localY, -hz));   // close the loop
    }

    // Rebuild lines if Zone Size changes in the Inspector during Play mode
    void OnValidate()
    {
        if (_zoneLineBottom != null)
            SetRectPositions(_zoneLineBottom, -zoneSize.y * 0.5f + 0.08f);
        if (_zoneLineTop != null)
            SetRectPositions(_zoneLineTop, zoneSize.y * 0.5f - 0.08f);

        // Also resize the BoxCollider
        var col = GetComponent<BoxCollider>();
        if (col != null) { col.size = zoneSize; col.center = Vector3.zero; }
    }

    // ── Gizmos (Scene view only) ──────────────────────────────────────────────

    void OnDrawGizmos()      => DrawZoneGizmo(false);
    void OnDrawGizmosSelected() => DrawZoneGizmo(true);

    void DrawZoneGizmo(bool selected)
    {
        Matrix4x4 prev = Gizmos.matrix;
        Gizmos.matrix  = Matrix4x4.TRS(transform.position, transform.rotation, Vector3.one);

        Gizmos.color = new Color(0f, 1f, 0f, selected ? 0.2f : 0.06f);
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
