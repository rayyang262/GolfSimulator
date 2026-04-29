using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;
using TMPro;
using StarterAssets;

/// <summary>
/// Attach to PlayerCapsule.
///
/// FLOW:
///   1. Red ring appears around the ball at all times.
///   2. Walk inside the ring → "Press B to get into position to swing".
///   3. Press B → player snaps to golf stance (90° to aim direction, ball to their left).
///   4. While in stance:
///        ← / → arrows rotate the aim direction (yellow line shows where ball will go).
///        ↑ / ↓ arrows adjust loft angle.
///        WASD movement is locked.
///   5. Swing iPhone → ball launches in aim direction.
///   6. After shot, ring moves to landing spot. Approach again to swing.
/// </summary>
[RequireComponent(typeof(CharacterController))]
public class GolfBallInteraction : MonoBehaviour
{
    // ── Inspector ────────────────────────────────────────────────────────

    [Header("Interaction")]
    [Tooltip("Radius of the red ring and detection zone.")]
    public float interactionRadius = 4f;

    [Tooltip("How far to the right of the target line the player stands (like a real golfer).")]
    public float stanceWidth = 1.1f;

    [Tooltip("Degrees per second the aim rotates when holding ← or →.")]
    public float aimRotateSpeed = 55f;

    [Tooltip("How many degrees the aim rotates per pixel of mouse movement while in stance.")]
    public float mouseSensitivity = 0.25f;

    [Header("Ring Indicator")]
    [Tooltip("Optional red unlit material. Auto-created if empty.")]
    public Material ringMaterial;
    public float ringThickness = 0.18f;

    [Header("Aim Line")]
    [Tooltip("Length of the yellow aim arrow drawn from the ball.")]
    public float aimLineLength = 10f;

    [Header("UI Messages")]
    public string approachMessage = "Press  B  to get into position to swing";
    public string stanceMessage   = "← → Aim   |   ↑ ↓ Loft   |   Swing your phone!";

    // ── private ──────────────────────────────────────────────────────────

    private CharacterController _cc;
    private Camera              _cam;
    private GolfSwingController _swing;
    private StarterAssetsInputs _inputs;

    private Transform _ballTransform;
    private Transform _lastBallTransform;

    private GameObject   _ringGO;
    private LineRenderer _aimLine;
    private Canvas       _promptCanvas;
    private TMP_Text     _promptText;

    private bool      _inStance;
    private Vector3   _aimDirection;
    private Transform _camRoot;              // PlayerCameraRoot — drives camera pitch
    private Quaternion _preSstanceCamRot;   // saved so we restore it when exiting stance

    /// <summary>Set true by BallStateManager during BallFollow/WaitingForContinue
    /// to prevent B key from opening stance while the ball is in flight.</summary>
    [HideInInspector] public bool blockBInput = false;

    // ── lifecycle ────────────────────────────────────────────────────────

    void Start()
    {
        _cc     = GetComponent<CharacterController>();
        _cam    = Camera.main;
        _swing  = GetComponent<GolfSwingController>();
        _inputs = GetComponent<StarterAssetsInputs>();

        // Find the Cinemachine follow target (PlayerCameraRoot) — child of PlayerCapsule
        foreach (Transform t in GetComponentsInChildren<Transform>(true))
        {
            if (t.name == "PlayerCameraRoot" || t.name.Contains("CameraRoot"))
            {
                _camRoot = t;
                break;
            }
        }
        if (_camRoot == null)
            Debug.LogWarning("[GolfBallInteraction] PlayerCameraRoot not found — vertical camera tilt won't work.");

        BuildRingIndicator();
        BuildAimLine();
        BuildPromptUI();

        if (_swing == null)
            Debug.LogError("[GolfBallInteraction] GolfSwingController not found on PlayerCapsule.");
        if (_inputs == null)
            Debug.LogWarning("[GolfBallInteraction] StarterAssetsInputs not found — movement lock won't work.");

        // Lock cursor immediately so the player can look around from the first frame
        LockCursor();
    }

    void OnApplicationFocus(bool hasFocus)
    {
        // Re-lock cursor whenever the game window regains focus (e.g. after Alt-Tab)
        if (hasFocus) LockCursor();
    }

    static void LockCursor()
    {
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible   = false;
    }

    void Update()
    {
        RefreshBall();

        // ── Always block jump — spacebar is reserved for swing only ──────────
        if (_inputs != null) _inputs.jump = false;

        // ── Ring: follow ball ─────────────────────────────────────────────
        if (_ballTransform != null)
        {
            _ringGO.SetActive(true);
            Vector3 rp = _ballTransform.position;
            rp.y -= 0.05f;
            _ringGO.transform.position = rp;
        }
        else
        {
            _ringGO.SetActive(false);
            SetAimLineVisible(false);
        }

        // ── Distance ──────────────────────────────────────────────────────
        float dist   = _ballTransform != null
            ? Vector3.Distance(transform.position, _ballTransform.position)
            : float.MaxValue;
        bool inRange = dist <= interactionRadius;

        // ── Stance mode ───────────────────────────────────────────────────
        if (_inStance)
        {
            // Lock WASD / camera look so mouse only moves the aim line
            if (_inputs != null)
            {
                _inputs.move   = Vector2.zero;
                _inputs.sprint = false;
                _inputs.look   = Vector2.zero;
            }

            HandleAimKeys();
            UpdateAimLine();
        }
        else
        {
            // Approach mode: show/hide prompt based on range
            _promptCanvas.gameObject.SetActive(inRange);
            if (inRange) _promptText.text = approachMessage;
        }

        // ── B key ─────────────────────────────────────────────────────────
        if (!blockBInput &&
            Keyboard.current != null && Keyboard.current.bKey.wasPressedThisFrame)
        {
            Debug.Log($"[GolfBall] B pressed — inRange={inRange}  inStance={_inStance}  dist={dist:F1}m");

            if (!_inStance && inRange)
                EnterStance();
        }
    }

    // ── Ball tracking ─────────────────────────────────────────────────────

    void RefreshBall()
    {
        if (_ballTransform == null)
        {
            GameObject go = null;
            try   { go = GameObject.FindWithTag("GolfBall"); }
            catch { /* "GolfBall" tag may not be defined yet in Project Settings */ }
            if (go == null) go = GameObject.Find("GolfBall_Active");
            if (go != null)
            {
                _ballTransform = go.transform;
                Debug.Log($"[GolfBall] Ball found: {go.name}  pos={go.transform.position}");
            }
        }

        // Detect respawn → exit stance so player must approach again
        if (_ballTransform != _lastBallTransform)
        {
            if (_inStance) ExitStance();
            _lastBallTransform = _ballTransform;
        }
    }

    // ── Golf stance ───────────────────────────────────────────────────────

    void EnterStance()
    {
        if (_ballTransform == null) return;

        // Default aim = camera's horizontal forward direction
        _aimDirection   = _cam.transform.forward;
        _aimDirection.y = 0f;
        if (_aimDirection.sqrMagnitude < 0.001f) _aimDirection = transform.forward;
        _aimDirection.Normalize();

        // ── Golf stance position ──────────────────────────────────────────
        // Right-handed golfer: stand BEHIND and to the RIGHT of the ball so that
        // when the player faces the aim direction the ball is at their lower-left.
        // This keeps camera, aim line, and ball launch all pointing the same way.
        Vector3 rightOfTarget = Quaternion.Euler(0f, 90f, 0f) * _aimDirection;
        Vector3 stancePos     = _ballTransform.position
                                - _aimDirection   * 0.8f   // 0.8 m behind the ball
                                + rightOfTarget   * 0.55f; // 0.55 m to the right
        stancePos.y           = GetTerrainY(stancePos);

        // Teleport
        _cc.enabled        = false;
        transform.position = stancePos;
        _cc.enabled        = true;

        // ── Face the aim direction (NOT the ball) ─────────────────────────────
        // Camera, aim line and launch vector all point the same way now.
        transform.rotation = Quaternion.LookRotation(_aimDirection);

        _inStance = true;

        // ── Save camera pitch so we can restore it after the shot ────────────
        if (_camRoot != null)
        {
            _preSstanceCamRot = _camRoot.localRotation;
            // Look down and slightly left so the ball at lower-left is visible
            _camRoot.localRotation = Quaternion.Euler(48f, -22f, 0f);
        }

        // ── Tell GolfSwingController we're ready ──────────────────────────
        if (_swing != null)
        {
            _swing.readyToHit   = true;
            _swing.aimDirection = _aimDirection;
        }

        // Switch UI to aim instructions
        _promptText.text = stanceMessage;
        _promptCanvas.gameObject.SetActive(true);

        UpdateAimLine();
        Debug.Log("[GolfBall] In stance. Use ← → to aim, ↑ ↓ for loft, then swing.");
    }

    void ExitStance()
    {
        _inStance = false;
        SetAimLineVisible(false);
        _promptCanvas.gameObject.SetActive(false);

        // Restore camera pitch to what it was before stance
        if (_camRoot != null)
            _camRoot.localRotation = _preSstanceCamRot;

        if (_swing != null)
        {
            _swing.readyToHit   = false;
            _swing.aimDirection = Vector3.zero;
        }
    }

    // ── Arrow key aim control ─────────────────────────────────────────────

    void HandleAimKeys()
    {
        float rotateH   = 0f;   // degrees to pan camera/aim horizontally
        float loftDelta = 0f;   // degrees to adjust loft (vertical aim)

        // ── Mouse: cursor moves aim + camera ───────────────────────────────────
        if (Mouse.current != null)
        {
            Vector2 delta = Mouse.current.delta.ReadValue();
            rotateH   +=  delta.x * mouseSensitivity;
            loftDelta +=  delta.y * mouseSensitivity * 0.4f;  // up = more loft
        }

        // ── Arrow keys: horizontal pan + loft ─────────────────────────────────
        if (Keyboard.current != null)
        {
            if (Keyboard.current.rightArrowKey.isPressed) rotateH   +=  aimRotateSpeed * Time.deltaTime;
            if (Keyboard.current.leftArrowKey.isPressed)  rotateH   -=  aimRotateSpeed * Time.deltaTime;
            if (Keyboard.current.upArrowKey.isPressed)    loftDelta +=  18f * Time.deltaTime;
            if (Keyboard.current.downArrowKey.isPressed)  loftDelta -=  18f * Time.deltaTime;
        }

        // ── Horizontal: rotate aim direction, yaw capsule to match ───────────
        // Player position NEVER changes while aiming — only body rotation (yaw).
        // Moving position every frame via CC toggle caused the jitter.
        if (Mathf.Abs(rotateH) > 0.001f)
        {
            _aimDirection = Quaternion.Euler(0f, rotateH, 0f) * _aimDirection;
            _aimDirection.Normalize();
            if (_swing != null) _swing.aimDirection = _aimDirection;

            // Pure yaw — feet stay planted, camera pans with the body
            transform.rotation = Quaternion.LookRotation(_aimDirection);
        }

        // ── Vertical: update loft and tilt camera root to match ───────────────
        if (_swing != null)
        {
            if (Mathf.Abs(loftDelta) > 0.001f)
                _swing.loftAngle = Mathf.Clamp(_swing.loftAngle + loftDelta, 5f, 45f);

            // Keep camera pitch in sync with loft, preserving the -22° left tilt
            // so the ball stays visible at lower-left throughout aim adjustment.
            // loft  5° → pitch +48° (looking steeply down)
            // loft 45° → pitch -15° (looking up to follow the arc)
            if (_camRoot != null)
            {
                float camPitch = Mathf.Lerp(48f, -15f, (_swing.loftAngle - 5f) / 40f);
                _camRoot.localRotation = Quaternion.Euler(camPitch, -22f, 0f);
            }
        }
    }

    // ── Aim line ──────────────────────────────────────────────────────────

    void UpdateAimLine()
    {
        if (_aimLine == null || _ballTransform == null || _swing == null) return;
        SetAimLineVisible(true);

        // ── Launch parameters at 75 % power (representative preview) ─────────
        float loftDeg = _swing.loftAngle;
        float loftRad = loftDeg * Mathf.Deg2Rad;

        // force → speed:  F = m·a → v = F / m  (impulse over 1 s approximation)
        const float BallMass = 0.046f;
        float previewForce   = _swing.maxPower * 0.75f * _swing.powerScale;
        float speed          = previewForce / BallMass;

        float vH = speed * Mathf.Cos(loftRad);   // horizontal component
        float vV = speed * Mathf.Sin(loftRad);   // vertical component
        float g  = Mathf.Abs(Physics.gravity.y);  // 9.81

        // Time of flight (y = 0 again).  Cap at 8 s so putter doesn't draw forever.
        float tFlight = (g > 0f && vV > 0f) ? (2f * vV / g) : 0.3f;
        tFlight = Mathf.Clamp(tFlight, 0.2f, 8f);

        // ── Draw parabola ─────────────────────────────────────────────────────
        const int MaxPts = 48;
        Vector3   origin  = _ballTransform.position + Vector3.up * 0.05f;
        Vector3   flatDir = new Vector3(_aimDirection.x, 0f, _aimDirection.z).normalized;

        Terrain terrain = Terrain.activeTerrain;

        int pts = MaxPts;
        for (int i = 0; i < MaxPts; i++)
        {
            float t     = (float)i / (MaxPts - 1) * tFlight;
            float hDist = vH * t;
            float vDist = vV * t - 0.5f * g * t * t;

            Vector3 pt = origin + flatDir * hDist + Vector3.up * vDist;

            // Clamp to terrain so arc doesn't tunnel underground
            if (terrain != null)
            {
                float ty = terrain.SampleHeight(pt) + terrain.transform.position.y + 0.04f;
                if (pt.y < ty)
                {
                    pt.y = ty;
                    _aimLine.SetPosition(i, pt);
                    pts = i + 1;
                    break;
                }
            }

            _aimLine.SetPosition(i, pt);
        }

        // Trim unused segments so the LineRenderer doesn't draw stale points
        _aimLine.positionCount = pts;
    }

    void SetAimLineVisible(bool visible)
    {
        if (_aimLine != null) _aimLine.enabled = visible;
    }

    // ── Terrain height ────────────────────────────────────────────────────

    float GetTerrainY(Vector3 pos)
    {
        Terrain t = Terrain.activeTerrain;
        if (t != null) return t.SampleHeight(pos) + t.transform.position.y;
        if (Physics.Raycast(pos + Vector3.up * 50f, Vector3.down, out RaycastHit hit, 100f))
            return hit.point.y + 0.02f;
        return pos.y;
    }

    // ── Ring indicator (LineRenderer circle) ──────────────────────────────

    void BuildRingIndicator()
    {
        _ringGO = new GameObject("BallZoneRing");
        var lr  = _ringGO.AddComponent<LineRenderer>();
        lr.useWorldSpace     = false;
        lr.loop              = true;
        lr.positionCount     = 64;
        lr.startWidth        = ringThickness;
        lr.endWidth          = ringThickness;
        lr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;

        if (ringMaterial != null)
            lr.material = ringMaterial;
        else
        {
            var m = new Material(Shader.Find("Sprites/Default"));
            m.color   = Color.red;
            lr.material = m;
        }
        lr.startColor = lr.endColor = new Color(1f, 0.05f, 0.05f, 1f);

        for (int i = 0; i < 64; i++)
        {
            float a = (float)i / 64f * Mathf.PI * 2f;
            lr.SetPosition(i, new Vector3(Mathf.Cos(a) * interactionRadius, 0.06f,
                                          Mathf.Sin(a) * interactionRadius));
        }
        _ringGO.SetActive(false);
    }

    // ── Aim line (yellow arrow from ball) ─────────────────────────────────

    void BuildAimLine()
    {
        var go  = new GameObject("AimLine");
        _aimLine = go.AddComponent<LineRenderer>();
        _aimLine.positionCount     = 48;   // enough resolution for a smooth arc
        _aimLine.startWidth        = 0.18f;
        _aimLine.endWidth          = 0.04f;
        _aimLine.useWorldSpace     = true;
        _aimLine.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;

        var m = new Material(Shader.Find("Sprites/Default"));
        m.color = Color.yellow;
        _aimLine.material    = m;
        _aimLine.startColor  = Color.yellow;
        _aimLine.endColor    = new Color(1f, 1f, 0f, 0.15f);
        _aimLine.enabled     = false;
    }

    // ── Prompt UI ─────────────────────────────────────────────────────────

    void BuildPromptUI()
    {
        var root = new GameObject("GolfPromptCanvas");

        _promptCanvas              = root.AddComponent<Canvas>();
        _promptCanvas.renderMode   = RenderMode.ScreenSpaceOverlay;
        _promptCanvas.sortingOrder = 50;

        var scaler = root.AddComponent<CanvasScaler>();
        scaler.uiScaleMode         = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        // NO GraphicRaycaster — prompts are display-only and must never block mouse input

        // Dark pill at bottom-centre
        var panel = new GameObject("Panel");
        panel.transform.SetParent(root.transform, false);
        var img         = panel.AddComponent<Image>();
        img.color       = new Color(0f, 0f, 0f, 0.72f);
        img.raycastTarget = false;   // never block mouse
        var prt   = panel.GetComponent<RectTransform>();
        prt.anchorMin        = new Vector2(0.5f, 0f);
        prt.anchorMax        = new Vector2(0.5f, 0f);
        prt.pivot            = new Vector2(0.5f, 0f);
        prt.sizeDelta        = new Vector2(680f, 74f);
        prt.anchoredPosition = new Vector2(0f, 55f);

        var tgo           = new GameObject("Text");
        tgo.transform.SetParent(panel.transform, false);
        _promptText              = tgo.AddComponent<TextMeshProUGUI>();
        _promptText.text         = approachMessage;
        _promptText.fontSize     = 22f;
        _promptText.alignment    = TextAlignmentOptions.Center;
        _promptText.color        = Color.white;
        _promptText.raycastTarget = false;   // never block mouse
        var trt               = tgo.GetComponent<RectTransform>();
        trt.anchorMin         = Vector2.zero;
        trt.anchorMax         = Vector2.one;
        trt.offsetMin         = new Vector2(20f, 0f);
        trt.offsetMax         = new Vector2(-20f, 0f);

        _promptCanvas.gameObject.SetActive(false);
    }
}
