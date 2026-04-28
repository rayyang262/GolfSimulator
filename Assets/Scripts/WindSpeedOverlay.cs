using UnityEngine;
using UnityEngine.UI;
using TMPro;
#if UNITY_EDITOR
using UnityEditor;
#endif

/// <summary>
/// Three-arrow wind indicator. Appears immediately when conditions are met,
/// then re-appears every 5 seconds. One large centre arrow and two smaller
/// flanking arrows slide in the wind direction with a fade in/out animation.
///
/// Wire up in the Inspector:
///   • windSpeedToggle  — StatCardToggle on the "windSpeed" UI card
///   • swingController  — auto-found on this GameObject if blank
///   • arrowSprite      — assign arrow.png (Sprite type) in Inspector
///   • windDirection    — horizontal world-space direction the wind blows toward
///   • windSpeedMph     — speed value shown below the arrows
/// </summary>
public class WindSpeedOverlay : MonoBehaviour
{
    [Header("References")]
    public StatCardToggle       windSpeedToggle;
    public GolfSwingController  swingController;
    public Sprite               arrowSprite;

    [Header("Wind")]
    [Tooltip("Horizontal world-space direction the wind blows toward (y is ignored).")]
    public Vector3 windDirection = new Vector3(1f, 0f, 0f);
    [Tooltip("Wind speed in mph displayed below the arrows.")]
    public float   windSpeedMph  = 5f;

    [Header("Position (offset from screen centre)")]
    public Vector2 panelOffset = new Vector2(0f, 80f);

    // ── private ───────────────────────────────────────────────────────────
    private GameObject        _root;
    private RectTransform[]   _arrowRTs    = new RectTransform[3];
    private Image[]           _arrowImages = new Image[3];
    private TextMeshProUGUI   _speedText;
    private Camera            _cam;

    // Slide animation
    private float _phase;
    private const float CYCLE      = 1.4f;   // seconds per slide loop
    private const float SLIDE_DIST = 55f;    // pixels to travel along wind dir

    // Interval — show for SHOW_WINDOW seconds, then hide until next interval
    private float _intervalTimer;
    private float _showWindowTimer;
    private bool  _wasActive;
    private const float INTERVAL    = 5f;    // seconds between appearances
    private const float SHOW_WINDOW = 2.2f;  // how long each appearance lasts

    // Arrow layout — index 0=left, 1=centre, 2=right (perpendicular to wind)
    private static readonly float[]   PerpOffset = { -44f, 0f, 44f };
    private static readonly Vector2[] ArrowSize  =
    {
        new Vector2(52f, 52f),   // left  — small
        new Vector2(85f, 85f),   // centre — large
        new Vector2(52f, 52f),   // right — small
    };

    // ── lifecycle ─────────────────────────────────────────────────────────

    void Start()
    {
        _cam = Camera.main;
        if (swingController == null)
            swingController = GetComponent<GolfSwingController>();
        if (arrowSprite == null)
            arrowSprite = LoadArrowSprite();
        BuildUI();
    }

    static Sprite LoadArrowSprite()
    {
#if UNITY_EDITOR
        var tex = AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/imported_textures/arrow.png");
        if (tex != null)
            return Sprite.Create(tex,
                new Rect(0, 0, tex.width, tex.height),
                new Vector2(0.5f, 0.5f));
        Debug.LogWarning("[WindSpeedOverlay] arrow.png not found at Assets/imported_textures/arrow.png");
#else
        Debug.LogWarning("[WindSpeedOverlay] arrowSprite not assigned — assign it in the Inspector.");
#endif
        return null;
    }

    void Update()
    {
        bool conditions = windSpeedToggle != null && windSpeedToggle.IsActive
                       && swingController != null && swingController.readyToHit;

        if (!conditions)
        {
            // Reset everything when player leaves swing stance
            _wasActive       = false;
            _intervalTimer   = 0f;
            _showWindowTimer = 0f;
            _phase           = 0f;
            _root.SetActive(false);
            return;
        }

        // First frame conditions become true: show immediately
        if (!_wasActive)
        {
            _wasActive       = true;
            _intervalTimer   = 0f;
            _showWindowTimer = SHOW_WINDOW;
        }
        else
        {
            _intervalTimer += Time.deltaTime;
            if (_intervalTimer >= INTERVAL)
            {
                _intervalTimer   = 0f;
                _showWindowTimer = SHOW_WINDOW;
            }
            _showWindowTimer = Mathf.Max(0f, _showWindowTimer - Time.deltaTime);
        }

        bool show = _showWindowTimer > 0f;
        _root.SetActive(show);

        if (!show) { _phase = 0f; return; }

        _speedText.text = $"{windSpeedMph:F0}mph";
        _phase = (_phase + Time.deltaTime / CYCLE) % 1f;
        UpdateArrows();
    }

    // ── animation ─────────────────────────────────────────────────────────

    void UpdateArrows()
    {
        if (_cam == null) return;

        Vector2 windScreen = GetScreenWindDir();
        Vector2 perpScreen = new Vector2(-windScreen.y, windScreen.x);

        // Rotate so sprite (pointing UP) faces screen wind direction
        float angle    = Mathf.Atan2(windScreen.x, windScreen.y) * Mathf.Rad2Deg;
        Quaternion rot = Quaternion.Euler(0f, 0f, -angle);

        // Slide upstream → downstream over one cycle
        float slide = Mathf.Lerp(-SLIDE_DIST * 0.5f, SLIDE_DIST * 0.5f, _phase);

        // Alpha: fade in first 25%, hold 25–75%, fade out last 25%
        float alpha;
        if      (_phase < 0.25f) alpha = _phase / 0.25f;
        else if (_phase < 0.75f) alpha = 1f;
        else                     alpha = 1f - (_phase - 0.75f) / 0.25f;

        for (int i = 0; i < 3; i++)
        {
            _arrowRTs[i].anchoredPosition = perpScreen * PerpOffset[i]
                                           + windScreen * slide;
            _arrowRTs[i].localRotation    = rot;
            var c = _arrowImages[i].color;
            c.a = alpha * (i == 1 ? 1f : 0.60f);   // side arrows slightly dimmer
            _arrowImages[i].color = c;
        }
    }

    // ── world wind direction → screen 2-D unit vector ─────────────────────

    Vector2 GetScreenWindDir()
    {
        Vector3 windFlat = new Vector3(windDirection.x, 0f, windDirection.z);
        if (windFlat.sqrMagnitude < 0.001f) return Vector2.up;
        windFlat.Normalize();

        Vector3 camRight   = _cam.transform.right;   camRight.y   = 0f;
        Vector3 camForward = _cam.transform.forward; camForward.y = 0f;
        if (camForward.sqrMagnitude < 0.001f) camForward = Vector3.forward;
        camRight.Normalize(); camForward.Normalize();

        float rx = Vector3.Dot(windFlat, camRight);
        float ry = Vector3.Dot(windFlat, camForward);
        return new Vector2(rx, ry).normalized;
    }

    // ── UI construction ───────────────────────────────────────────────────

    void BuildUI()
    {
        var canvasGO               = new GameObject("WindSpeedCanvas");
        var canvas                 = canvasGO.AddComponent<Canvas>();
        canvas.renderMode          = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder        = 55;
        var scaler                 = canvasGO.AddComponent<CanvasScaler>();
        scaler.uiScaleMode         = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);

        _root = new GameObject("WindRoot");
        _root.transform.SetParent(canvasGO.transform, false);
        var rootRT              = _root.AddComponent<RectTransform>();
        rootRT.anchorMin        = rootRT.anchorMax = new Vector2(0.5f, 0.5f);
        rootRT.pivot            = new Vector2(0.5f, 0.5f);
        rootRT.sizeDelta        = new Vector2(260f, 200f);
        rootRT.anchoredPosition = panelOffset;

        // Three arrow images
        for (int i = 0; i < 3; i++)
        {
            var go              = new GameObject($"Arrow{i}");
            go.transform.SetParent(_root.transform, false);
            _arrowRTs[i]        = go.AddComponent<RectTransform>();
            _arrowRTs[i].anchorMin        = _arrowRTs[i].anchorMax = new Vector2(0.5f, 0.5f);
            _arrowRTs[i].sizeDelta        = ArrowSize[i];
            _arrowRTs[i].anchoredPosition = Vector2.zero;
            _arrowImages[i]               = go.AddComponent<Image>();
            _arrowImages[i].sprite        = arrowSprite;
            _arrowImages[i].preserveAspect = true;
            _arrowImages[i].color         = new Color(1f, 1f, 1f, 0f);
            _arrowImages[i].raycastTarget  = false;
        }

        // Speed text
        var stGO       = new GameObject("SpeedText");
        stGO.transform.SetParent(_root.transform, false);
        _speedText               = stGO.AddComponent<TextMeshProUGUI>();
        _speedText.text          = $"{windSpeedMph:F0}mph";
        _speedText.fontSize      = 32f;
        _speedText.fontStyle     = FontStyles.Bold;
        _speedText.alignment     = TextAlignmentOptions.Center;
        _speedText.color         = Color.white;
        _speedText.raycastTarget = false;
        var strt       = stGO.GetComponent<RectTransform>();
        strt.anchorMin = strt.anchorMax = new Vector2(0.5f, 0.5f);
        strt.sizeDelta          = new Vector2(160f, 44f);
        strt.anchoredPosition   = new Vector2(0f, -80f);

        // Label text
        var ltGO       = new GameObject("LabelText");
        ltGO.transform.SetParent(_root.transform, false);
        var lbl                  = ltGO.AddComponent<TextMeshProUGUI>();
        lbl.text             = "Wind Speed";
        lbl.fontSize          = 14f;
        lbl.alignment         = TextAlignmentOptions.Center;
        lbl.color             = new Color(1f, 1f, 1f, 0.55f);
        lbl.raycastTarget     = false;
        var ltrt       = ltGO.GetComponent<RectTransform>();
        ltrt.anchorMin = ltrt.anchorMax = new Vector2(0.5f, 0.5f);
        ltrt.sizeDelta          = new Vector2(160f, 28f);
        ltrt.anchoredPosition   = new Vector2(0f, -112f);

        _root.SetActive(false);
    }
}
