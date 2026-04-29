using UnityEngine;
using UnityEngine.InputSystem;
using TMPro;
using UnityEngine.UI;
using StarterAssets;

/// <summary>
/// Owns the post-shot state machine.
/// Attach to PlayerCapsule alongside GolfSwingController, GolfBallInteraction, ClubSystem.
///
/// STATE MACHINE:
///   Aim              → GolfBallInteraction handles approach + B-press into stance
///   Swing            → ball just launched, waiting for ballInFlight flag
///   BallFollow       → camera (capsule) tracks the ball through the air
///   WaitingForContinue → ball stopped, "Press B to walk up" prompt
///   Reposition       → teleport player beside ball, respawn ball, return to Aim
/// </summary>
[RequireComponent(typeof(GolfSwingController))]
[RequireComponent(typeof(GolfBallInteraction))]
[RequireComponent(typeof(CharacterController))]
public class BallStateManager : MonoBehaviour
{
    public enum GameState { Aim, Swing, BallFollow, WaitingForContinue, Reposition }

    // ── Inspector ─────────────────────────────────────────────────────────────

    [Header("References")]
    [Tooltip("The GolfHole Transform in the scene — player faces this after repositioning.")]
    public Transform holeTarget;

    [Header("Ball Follow Camera")]
    [Tooltip("How far behind the ball the camera trails.")]
    public float followDistance = 14f;
    [Tooltip("Height offset above ball during follow.")]
    public float followHeight   = 5f;
    [Tooltip("How fast the capsule catches up to the ball position.")]
    public float followSpeed    = 4f;
    [Tooltip("Fixed camera pitch (degrees down) during ball follow. 15 = slightly down, 0 = level.")]
    public float followCameraPitch = 12f;

    [Header("Stop Detection")]
    [Tooltip("Speed (m/s) below which the ball is considered 'slowing'.")]
    public float stoppedSpeedThreshold  = 0.35f;
    [Tooltip("Ball must stay below threshold for this many seconds to be declared stopped.")]
    public float stoppedDuration        = 1.0f;
    [Tooltip("If velocity direction changes more than this (degrees) while slowing, reset the timer.")]
    public float directionChangeTolerance = 25f;

    [Header("Teleport")]
    [Tooltip("Metres to the right of the ball→hole line where the player stands.")]
    public float stanceOffset = 1.5f;

    // ── State ─────────────────────────────────────────────────────────────────

    public GameState CurrentState { get; private set; } = GameState.Aim;

    // ── Private refs ──────────────────────────────────────────────────────────

    private GolfSwingController  _swing;
    private GolfBallInteraction  _interact;
    private ClubSystem           _clubs;
    private CharacterController  _cc;
    private FirstPersonController _fpc;
    private StarterAssetsInputs  _inputs;
    private Transform            _camRoot;

    private Rigidbody _ballRb;
    private Transform _ballTf;

    private float   _stoppedTimer;
    private Vector3 _prevVelocityDir = Vector3.forward;

    private Canvas   _continueCanvas;
    private TMP_Text _continueText;

    // ── Lifecycle ─────────────────────────────────────────────────────────────

    void Awake()
    {
        _swing    = GetComponent<GolfSwingController>();
        _interact = GetComponent<GolfBallInteraction>();
        _clubs    = GetComponent<ClubSystem>();
        _cc       = GetComponent<CharacterController>();
        _fpc      = GetComponent<FirstPersonController>();
        _inputs   = GetComponent<StarterAssetsInputs>();

        // Find PlayerCameraRoot for camera tilt resets
        foreach (Transform t in GetComponentsInChildren<Transform>(true))
        {
            if (t.name == "PlayerCameraRoot" || t.name.Contains("CameraRoot"))
            { _camRoot = t; break; }
        }
    }

    void Start()
    {
        BuildContinueUI();
    }

    void Update()
    {
        // Always block jump — spacebar is reserved for swing
        if (_inputs != null) _inputs.jump = false;

        switch (CurrentState)
        {
            case GameState.Aim:
                // If swing was just triggered watch for ballInFlight to flip
                if (_swing.ballInFlight)
                    TransitionTo(GameState.BallFollow);
                break;

            case GameState.Swing:
                if (_swing.ballInFlight)
                    TransitionTo(GameState.BallFollow);
                break;

            case GameState.BallFollow:
                RefreshBallRefs();
                UpdateFollowCamera();
                CheckBallStopped();
                break;

            case GameState.WaitingForContinue:
                if (Keyboard.current != null && Keyboard.current.bKey.wasPressedThisFrame)
                    TransitionTo(GameState.Reposition);
                break;

            case GameState.Reposition:
                // One-shot — handled entirely inside OnEnterReposition
                break;
        }
    }

    // ── Transition hub ────────────────────────────────────────────────────────

    void TransitionTo(GameState next)
    {
        Debug.Log($"[BallStateManager] {CurrentState} → {next}");
        CurrentState = next;

        switch (next)
        {
            case GameState.BallFollow:         OnEnterBallFollow();         break;
            case GameState.WaitingForContinue: OnEnterWaitingForContinue(); break;
            case GameState.Reposition:         OnEnterReposition();         break;
            case GameState.Aim:                OnEnterAim();                break;
        }
    }

    // ── BallFollow ────────────────────────────────────────────────────────────

    void OnEnterBallFollow()
    {
        _stoppedTimer    = 0f;
        _prevVelocityDir = Vector3.forward;

        // Disable FirstPersonController first — this stops it calling CC.Move()
        // which would throw "CharacterController.Move called on inactive controller"
        if (_fpc != null) _fpc.enabled = false;
        if (_cc  != null) _cc.enabled  = false;

        // Lock player inputs during flight
        if (_inputs != null)
        {
            _inputs.move   = Vector2.zero;
            _inputs.sprint = false;
            _inputs.look   = Vector2.zero;
        }

        // Block B key in GolfBallInteraction so it doesn't open stance mid-flight
        if (_interact != null) _interact.blockBInput = true;

        // Lock camera to a fixed, slightly-downward angle for the whole flight
        if (_camRoot != null)
            _camRoot.localRotation = Quaternion.Euler(followCameraPitch, 0f, 0f);

        RefreshBallRefs();
    }

    void RefreshBallRefs()
    {
        // Only search if we don't already have a valid ref
        if (_ballTf != null && _ballTf.gameObject.activeInHierarchy) return;

        _ballTf = null;
        _ballRb = null;

        GameObject go = null;
        try   { go = GameObject.FindWithTag("GolfBall"); } catch { }
        if (go == null) go = GameObject.Find("GolfBall_Active");
        if (go == null) return;

        _ballTf = go.transform;
        _ballRb = go.GetComponent<Rigidbody>();
    }

    void UpdateFollowCamera()
    {
        if (_ballTf == null) return;

        Vector3 ballPos = _ballTf.position;

        // Stay behind the ball along the shot direction
        Vector3 behindDir = _swing.aimDirection.sqrMagnitude > 0.01f
            ? -_swing.aimDirection.normalized
            : -transform.forward;
        behindDir.y = 0f;
        behindDir.Normalize();

        Vector3 target = ballPos + behindDir * followDistance + Vector3.up * followHeight;

        // Never sink below terrain
        Terrain terrain = Terrain.activeTerrain;
        if (terrain != null)
        {
            float ty = terrain.SampleHeight(target) + terrain.transform.position.y + 2f;
            target.y = Mathf.Max(target.y, ty);
        }

        // Move capsule position only — camera angle is FIXED (set on entry, never updated here)
        transform.position = Vector3.Lerp(transform.position, target,
                                          Time.deltaTime * followSpeed);
    }

    // ── Stop detection ────────────────────────────────────────────────────────

    void CheckBallStopped()
    {
        if (_ballRb == null) return;

        float speed = _ballRb.linearVelocity.magnitude;

        if (speed > stoppedSpeedThreshold)
        {
            // Ball still moving — reset timer and record direction
            _stoppedTimer    = 0f;
            _prevVelocityDir = _ballRb.linearVelocity.normalized;
            return;
        }

        // Below speed threshold — check direction stability
        // A bouncing ball has rapidly changing direction even at low speed
        if (speed > 0.05f)
        {
            Vector3 currentDir = _ballRb.linearVelocity.normalized;
            float   dirDelta   = Vector3.Angle(currentDir, _prevVelocityDir);
            if (dirDelta > directionChangeTolerance)
            {
                // Direction changed too much — still bouncing
                _stoppedTimer    = 0f;
                _prevVelocityDir = currentDir;
                return;
            }
        }

        // Accumulate stopped time
        _stoppedTimer += Time.deltaTime;
        if (_stoppedTimer >= stoppedDuration)
            TransitionTo(GameState.WaitingForContinue);
    }

    // ── WaitingForContinue ────────────────────────────────────────────────────

    void OnEnterWaitingForContinue()
    {
        // Hard-stop the ball so it doesn't drift on a slope
        if (_ballRb != null)
        {
            _ballRb.linearVelocity  = Vector3.zero;
            _ballRb.angularVelocity = Vector3.zero;
            _ballRb.isKinematic     = true;   // frozen until next shot
        }

        // Clear the in-flight flag so TouchOSC cooldown can complete
        _swing.ballInFlight = false;

        ShowContinuePrompt(true);
    }

    // ── Reposition ────────────────────────────────────────────────────────────

    void OnEnterReposition()
    {
        ShowContinuePrompt(false);

        Vector3 ballPos = _ballTf != null ? _ballTf.position : transform.position;

        // ── 1. Determine golf stance position beside the ball ─────────────────
        Vector3 toHole = holeTarget != null
            ? (holeTarget.position - ballPos)
            : transform.forward;
        toHole.y = 0f;
        if (toHole.sqrMagnitude < 0.001f) toHole = Vector3.forward;
        toHole.Normalize();

        // Stand to the right of ball→hole line (right-handed golfer)
        Vector3 rightOfLine  = Quaternion.Euler(0f, 90f, 0f) * toHole;
        Vector3 teleportPos  = ballPos + rightOfLine * stanceOffset;

        // ── 2. Snap to terrain ────────────────────────────────────────────────
        Terrain terrain = Terrain.activeTerrain;
        if (terrain != null)
            teleportPos.y = terrain.SampleHeight(teleportPos) + terrain.transform.position.y + 0.05f;
        else if (Physics.Raycast(teleportPos + Vector3.up * 30f, Vector3.down,
                                 out RaycastHit hit, 60f))
            teleportPos.y = hit.point.y + 0.05f;

        // ── 3. Teleport capsule ───────────────────────────────────────────────
        transform.position = teleportPos;
        transform.rotation = Quaternion.LookRotation(toHole);   // face the hole

        // ── 4. Reset camera pitch to neutral ──────────────────────────────────
        if (_camRoot != null)
            _camRoot.localRotation = Quaternion.Euler(10f, 0f, 0f);

        // ── 5. Re-enable CharacterController + FirstPersonController ─────────
        if (_cc  != null) _cc.enabled  = true;
        if (_fpc != null) _fpc.enabled = true;

        // ── 6. Notify club system — Driver is done, next shot uses 7-Iron ────
        _clubs?.OnShotCompleted();

        // ── 7. Update spawn point and respawn ball at landing position ─────────
        if (_ballTf != null)
            _swing.UpdateSpawnPoint(_ballTf.position);
        _swing.RespawnBall();

        // ── 8. Return to Aim ──────────────────────────────────────────────────
        _ballTf = null;
        _ballRb = null;
        TransitionTo(GameState.Aim);
    }

    void OnEnterAim()
    {
        if (_interact != null) _interact.blockBInput = false;
        // Safety net — always restore movement components when returning to Aim
        if (_cc  != null) _cc.enabled  = true;
        if (_fpc != null) _fpc.enabled = true;
    }

    // ── OOB bypass ────────────────────────────────────────────────────────────

    /// <summary>
    /// Called by OutOfBoundsTracker after an OOB respawn to skip the normal
    /// BallFollow → WaitingForContinue → Reposition chain and jump straight
    /// back to Aim. This ensures the "Press B" prompt is hidden and CC/FPC
    /// are re-enabled immediately after the penalty sequence completes.
    /// </summary>
    public void ForceReturnToAim()
    {
        ShowContinuePrompt(false);
        _ballTf = null;
        _ballRb = null;
        _stoppedTimer = 0f;
        TransitionTo(GameState.Aim);
    }

    // ── UI ────────────────────────────────────────────────────────────────────

    void ShowContinuePrompt(bool show)
    {
        if (_continueCanvas != null)
            _continueCanvas.gameObject.SetActive(show);
    }

    void BuildContinueUI()
    {
        var root   = new GameObject("ContinuePromptCanvas");
        var canvas = root.AddComponent<Canvas>();
        canvas.renderMode   = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 60;

        var scaler = root.AddComponent<CanvasScaler>();
        scaler.uiScaleMode         = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);

        // Dark pill panel at bottom
        var panel = new GameObject("Panel");
        panel.transform.SetParent(root.transform, false);
        var img = panel.AddComponent<Image>();
        img.color         = new Color(0f, 0f, 0f, 0.75f);
        img.raycastTarget = false;
        var prt = panel.GetComponent<RectTransform>();
        prt.anchorMin        = new Vector2(0.5f, 0f);
        prt.anchorMax        = new Vector2(0.5f, 0f);
        prt.pivot            = new Vector2(0.5f, 0f);
        prt.sizeDelta        = new Vector2(640f, 70f);
        prt.anchoredPosition = new Vector2(0f, 55f);

        var tgo = new GameObject("Text");
        tgo.transform.SetParent(panel.transform, false);
        _continueText              = tgo.AddComponent<TextMeshProUGUI>();
        _continueText.text         = "Ball stopped  ·  Press  B  to walk up";
        _continueText.fontSize     = 22f;
        _continueText.alignment    = TextAlignmentOptions.Center;
        _continueText.color        = Color.white;
        _continueText.raycastTarget = false;
        var trt = tgo.GetComponent<RectTransform>();
        trt.anchorMin = Vector2.zero;
        trt.anchorMax = Vector2.one;
        trt.offsetMin = new Vector2(20f, 0f);
        trt.offsetMax = new Vector2(-20f, 0f);

        root.SetActive(false);
        _continueCanvas = canvas;
    }

    // ── Gizmos ────────────────────────────────────────────────────────────────

    void OnDrawGizmos()
    {
        if (_ballTf == null || CurrentState == GameState.Aim) return;
        Gizmos.color = Color.cyan;
        Gizmos.DrawWireSphere(_ballTf.position, 0.3f);
    }
}
