using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// Attach to PlayerCapsule.
/// - Red ring appears on the ground, centered on the ball.
/// - Walking inside the ring shows "Press B to get into position to swing".
/// - B snaps the player directly behind the ball ready to swing.
/// </summary>
[RequireComponent(typeof(CharacterController))]
public class GolfBallInteraction : MonoBehaviour
{
    [Header("Interaction Settings")]
    public float interactionRadius   = 4f;
    public float swingStandDistance  = 1.8f;

    [Header("Ring Indicator")]
    public Material ringMaterial;   // optional — code creates a red one if empty
    public float    ringThickness = 0.18f;

    [Header("Prompt")]
    public string promptMessage = "Press  B  to get into position to swing";

    // ── private ──────────────────────────────────────────────────────────
    private CharacterController _cc;
    private Camera              _cam;

    private Transform _ballTransform;
    private Transform _lastBallTransform;

    private GameObject _ringGO;
    private Canvas     _promptCanvas;
    private bool       _snapped;

    // ── lifecycle ────────────────────────────────────────────────────────

    void Start()
    {
        _cc  = GetComponent<CharacterController>();
        _cam = Camera.main;

        BuildRingIndicator();
        BuildPromptUI();

        Debug.Log("[GolfBallInteraction] Started. Looking for GolfBall_Active...");
    }

    void Update()
    {
        RefreshBallReference();

        // ── Ring: center exactly on the ball, sitting just above terrain ──
        if (_ballTransform != null)
        {
            _ringGO.SetActive(true);

            // Place ring at ball's XZ, just below ball center (avoids floating-ring look)
            Vector3 rp = _ballTransform.position;
            rp.y -= 0.05f;          // nudge down to ball's base
            _ringGO.transform.position = rp;
        }
        else
        {
            _ringGO.SetActive(false);
        }

        // ── Proximity ─────────────────────────────────────────────────────
        float dist = _ballTransform != null
            ? Vector3.Distance(transform.position, _ballTransform.position)
            : float.MaxValue;

        bool inRange = dist <= interactionRadius;

        _promptCanvas.gameObject.SetActive(inRange && !_snapped);

        // ── B key ─────────────────────────────────────────────────────────
        if (Keyboard.current != null && Keyboard.current.bKey.wasPressedThisFrame)
        {
            Debug.Log($"[GolfBallInteraction] B pressed — inRange={inRange}  snapped={_snapped}  dist={dist:F1}m");
            if (inRange && !_snapped)
                SnapToSwingPosition();
        }
    }

    // ── Ball finding ──────────────────────────────────────────────────────

    void RefreshBallReference()
    {
        if (_ballTransform == null)
        {
            // Try tag first (requires "GolfBall" tag in Project Settings → Tags)
            GameObject go = null;
            try   { go = GameObject.FindWithTag("GolfBall"); }
            catch { /* tag not yet defined in Project Settings */ }

            // Fallback: find by the name GolfSwingController assigns
            if (go == null) go = GameObject.Find("GolfBall_Active");

            if (go != null)
            {
                _ballTransform = go.transform;
                Debug.Log($"[GolfBallInteraction] Ball found: {go.name} at {go.transform.position}");
            }
        }

        // Detect ball respawn → reset snapped so player can approach again
        if (_ballTransform != _lastBallTransform)
        {
            _snapped           = false;
            _lastBallTransform = _ballTransform;
        }
    }

    // ── Snap to swing position ────────────────────────────────────────────

    void SnapToSwingPosition()
    {
        if (_ballTransform == null) return;

        // Stand directly behind the ball along the camera's horizontal forward
        Vector3 fwd = _cam.transform.forward;
        fwd.y = 0f;
        if (fwd.sqrMagnitude < 0.001f) fwd = transform.forward;
        fwd.Normalize();

        Vector3 targetPos = _ballTransform.position - fwd * swingStandDistance;

        // Find the terrain surface at that position (ignore the ball itself)
        targetPos.y = GetTerrainY(targetPos);

        // Teleport CharacterController safely
        _cc.enabled        = false;
        transform.position = targetPos;
        _cc.enabled        = true;

        // Face toward ball
        Vector3 look = _ballTransform.position - transform.position;
        look.y = 0f;
        if (look.sqrMagnitude > 0.001f)
            transform.rotation = Quaternion.LookRotation(look.normalized);

        _snapped = true;
        _promptCanvas.gameObject.SetActive(false);
        Debug.Log("[GolfBallInteraction] Snapped to swing position!");
    }

    // ── Terrain height helper ─────────────────────────────────────────────

    /// <summary>
    /// Returns the terrain Y at the given XZ position.
    /// Casts a ray downward, ignoring the GolfBall layer/tag.
    /// Falls back to the UnityEngine.Terrain API if no raycast hit.
    /// </summary>
    float GetTerrainY(Vector3 worldPos)
    {
        // Use UnityEngine.Terrain for a guaranteed ground height
        Terrain terrain = Terrain.activeTerrain;
        if (terrain != null)
            return terrain.SampleHeight(worldPos) + terrain.transform.position.y;

        // Raycast fallback
        Vector3 origin = new Vector3(worldPos.x, worldPos.y + 50f, worldPos.z);
        if (Physics.Raycast(origin, Vector3.down, out RaycastHit hit, 100f))
            return hit.point.y + 0.02f;

        return worldPos.y;
    }

    // ── Ring indicator ────────────────────────────────────────────────────

    void BuildRingIndicator()
    {
        _ringGO = new GameObject("BallZoneRing");

        var lr = _ringGO.AddComponent<LineRenderer>();
        lr.useWorldSpace     = false;   // positions are local → move by setting transform.position
        lr.loop              = true;
        lr.positionCount     = 64;
        lr.startWidth        = ringThickness;
        lr.endWidth          = ringThickness;
        lr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        lr.receiveShadows    = false;

        if (ringMaterial != null)
        {
            lr.material = ringMaterial;
        }
        else
        {
            // Sprites/Default is the most reliable cross-setup fallback for LineRenderer
            var mat = new Material(Shader.Find("Sprites/Default"));
            mat.color    = Color.red;
            lr.material  = mat;
        }
        lr.startColor = lr.endColor = new Color(1f, 0.05f, 0.05f, 1f);

        // Circle in local XZ plane, slightly above Y=0 to sit on terrain surface
        for (int i = 0; i < 64; i++)
        {
            float a = (float)i / 64f * Mathf.PI * 2f;
            lr.SetPosition(i, new Vector3(
                Mathf.Cos(a) * interactionRadius,
                0.06f,                             // 6 cm above ring pivot
                Mathf.Sin(a) * interactionRadius
            ));
        }

        _ringGO.SetActive(false);
    }

    // ── Prompt UI ─────────────────────────────────────────────────────────

    void BuildPromptUI()
    {
        // Root canvas
        var root = new GameObject("GolfPromptCanvas");
        DontDestroyOnLoad(root);   // survive scene reloads during testing

        _promptCanvas = root.AddComponent<Canvas>();
        _promptCanvas.renderMode  = RenderMode.ScreenSpaceOverlay;
        _promptCanvas.sortingOrder = 50;            // on top of everything

        var scaler = root.AddComponent<CanvasScaler>();
        scaler.uiScaleMode         = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        root.AddComponent<GraphicRaycaster>();

        // Dark pill — bottom-centre
        var panel  = new GameObject("Panel");
        panel.transform.SetParent(root.transform, false);
        var img    = panel.AddComponent<Image>();
        img.color  = new Color(0f, 0f, 0f, 0.72f);
        var prt    = panel.GetComponent<RectTransform>();
        prt.anchorMin        = new Vector2(0.5f, 0f);
        prt.anchorMax        = new Vector2(0.5f, 0f);
        prt.pivot            = new Vector2(0.5f, 0f);
        prt.sizeDelta        = new Vector2(580f, 74f);
        prt.anchoredPosition = new Vector2(0f, 55f);

        // Text
        var textGO  = new GameObject("Text");
        textGO.transform.SetParent(panel.transform, false);
        var tmp     = textGO.AddComponent<TextMeshProUGUI>();
        tmp.text    = promptMessage;
        tmp.fontSize = 24f;
        tmp.alignment = TextAlignmentOptions.Center;
        tmp.color   = Color.white;
        var trt     = textGO.GetComponent<RectTransform>();
        trt.anchorMin = Vector2.zero;
        trt.anchorMax = Vector2.one;
        trt.offsetMin = new Vector2(20f, 0f);
        trt.offsetMax = new Vector2(-20f, 0f);

        _promptCanvas.gameObject.SetActive(false);
    }
}
