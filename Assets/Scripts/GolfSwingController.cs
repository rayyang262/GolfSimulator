using UnityEngine;
using UnityEngine.InputSystem;
using System.Collections;

/// <summary>
/// Attach to PlayerCapsule.
/// - Hold Left Mouse / hold screen → backswing (club rotates back)
/// - Release              → downswing + hit ball if in range
/// </summary>
public class GolfSwingController : MonoBehaviour
{
    [Header("References")]
    [Tooltip("The ClubPivot empty GameObject under MainCamera")]
    public Transform clubPivot;

    [Tooltip("Golf ball prefab (Assets/Saritasa/Models/Sport_Balls/Golf.prefab)")]
    public GameObject golfBallPrefab;

    [Tooltip("Where the ball spawns at address position (empty GameObject at tee)")]
    public Transform ballSpawnPoint;

    [Header("Swing Settings")]
    [Tooltip("Max impulse force. At mass=0.046 kg: 5 N → ~108 m/s launch → ~350m carry on this course.")]
    public float maxPower         = 5f;    // max launch force  (was 25 — reduced for 1:1 scale course)
    public float powerPerSecond   = 18f;   // how fast power builds while holding
    [Tooltip("Upward angle of ball launch. 20° gives a good arc on a real-scale course.")]
    public float loftAngle        = 20f;   // upward angle added to hit direction
    public float hitRadius        = 3.5f;  // how close ball must be to register hit

    [Header("Club Rotation")]
    public float maxBackswingAngle = 80f;  // degrees the club rotates back
    public float downswingSpeed    = 600f; // degrees/sec during downswing

    [Header("Audio (optional)")]
    public AudioClip swingWhoosh;
    public AudioClip ballHit;

    [Header("Phone Input (TouchOSC)")]
    [Tooltip("Check this when using TouchOscSwingController. Disables mouse swing input.")]
    public bool usePhoneInput = true;

    [Header("Shot Control")]
    [Tooltip("Set true by GolfBallInteraction after player presses B. Ball only launches when true.")]
    public bool readyToHit = false;
    [Tooltip("Direction ball travels. Set by GolfBallInteraction from arrow-key aim.")]
    public Vector3 aimDirection = Vector3.zero;

    [Tooltip("Global force multiplier. 0.25 = 75% reduction. PuttingGreenTrigger overrides this.")]
    [HideInInspector] public float powerScale = 0.25f;

    /// <summary>True while the ball is in flight. TouchOscSwingController waits on this.</summary>
    [HideInInspector] public bool ballInFlight = false;

    // ── private state ────────────────────────────────────────────────
    private GameObject   _ball;
    private Rigidbody    _ballRb;
    private TrailRenderer _ballTrail;

    private bool         _isSwinging;
    private float        _holdTime;
    private float        _currentClubAngle;   // local X rotation of clubPivot
    private AudioSource  _audio;
    private Camera       _cam;

    // ── lifecycle ────────────────────────────────────────────────────

    void Start()
    {
        _cam   = Camera.main;
        _audio = GetComponent<AudioSource>();
        if (_audio == null) _audio = gameObject.AddComponent<AudioSource>();

        SpawnBall();
    }

    void Update()
    {
        if (_isSwinging) return;   // mid-downswing: ignore new input

        // ── Spacebar: swing trigger only — no jumping, no mouse ──────────────
        if (Keyboard.current != null && Keyboard.current.spaceKey.wasPressedThisFrame)
        {
            _holdTime  = maxPower / powerPerSecond * 0.75f;  // 75 % power on spacebar
            StartCoroutine(Downswing());
        }

        // TouchOSC phone drives all other swing input via PhoneTriggerHit()
    }

    // ── backswing ────────────────────────────────────────────────────

    void BeginBackswing()
    {
        _holdTime   = 0f;
        PlaySound(swingWhoosh, 0.3f);
    }

    void ContinueBackswing()
    {
        _holdTime += Time.deltaTime;

        // Rotate clubPivot backward (negative X = club goes up behind player)
        float targetAngle = Mathf.Clamp(_holdTime * (maxBackswingAngle / (maxPower / powerPerSecond)),
                                        0f, maxBackswingAngle);
        _currentClubAngle = targetAngle;

        if (clubPivot != null)
            clubPivot.localRotation = Quaternion.Euler(-_currentClubAngle, 0f, 0f);
    }

    // ── phone input API (called by TouchOscSwingController) ─────────────

    /// <summary>
    /// Drives the club backswing visual while the phone is in motion.
    /// normalized: 0 = address position, 1 = full backswing angle.
    /// </summary>
    public void PhoneSetBackswingAngle(float normalized)
    {
        if (_isSwinging) return;
        _currentClubAngle = normalized * maxBackswingAngle;
        if (clubPivot != null)
            clubPivot.localRotation = Quaternion.Euler(-_currentClubAngle, 0f, 0f);
    }

    /// <summary>
    /// Continuously mirrors the phone's physical orientation onto the club pivot.
    /// xEuler = pitch-derived backswing angle, zEuler = roll-derived face angle.
    /// Called every frame by TouchOscSwingController while orientation tracking is active.
    /// </summary>
    public void PhoneSetClubOrientation(float xEuler, float zEuler)
    {
        if (_isSwinging) return;
        // Keep _currentClubAngle in sync so Downswing() knows where the backswing started
        _currentClubAngle = Mathf.Abs(xEuler);
        if (clubPivot != null)
            clubPivot.localRotation = Quaternion.Euler(xEuler, 0f, zEuler);
    }

    /// <summary>
    /// Triggers a downswing + hit with the given power (already calculated by TouchOscSwingController).
    /// </summary>
    public void PhoneTriggerHit(float power)
    {
        if (_isSwinging) return;
        // Back-calculate holdTime so Downswing() derives the same power value
        _holdTime  = power / powerPerSecond;
        StartCoroutine(Downswing());
    }

    /// <summary>
    /// Called when the phone swing timed out without a valid hit — just returns club to rest.
    /// </summary>
    public void PhoneWhiff()
    {
        if (_isSwinging) return;
        StartCoroutine(ReturnClubToRest());
    }

    // ── downswing + hit ──────────────────────────────────────────────

    IEnumerator Downswing()
    {
        _isSwinging = true;

        float power           = Mathf.Clamp(_holdTime * powerPerSecond, 2f, maxPower);
        float normalizedPower = Mathf.InverseLerp(2f, maxPower, power);

        // ── Backswing start rotation ──────────────────────────────────
        // Use whatever the phone is holding the club at; guarantee a minimum arc
        Quaternion backswingRot = clubPivot != null
            ? clubPivot.localRotation
            : Quaternion.Euler(-30f, 0f, 0f);

        float arcAngle = Quaternion.Angle(backswingRot, Quaternion.identity);
        if (arcAngle < 20f)
        {
            // Club too close to rest — snap to a minimum backswing so there is visible motion
            backswingRot = Quaternion.Euler(-Mathf.Max(_currentClubAngle, 30f), 0f, 0f);
            if (clubPivot != null) clubPivot.localRotation = backswingRot;
            yield return null;  // one frame at top of backswing
        }

        // ── Keyframe rotations ────────────────────────────────────────
        Quaternion impactRot     = Quaternion.identity;             // address = impact
        Quaternion followThruRot = Quaternion.Euler(38f, 0f, 0f);  // club follows through

        // Hard swing = faster animation (0.55s slow → 0.18s hard)
        float totalDuration  = Mathf.Lerp(0.55f, 0.18f, normalizedPower);
        float impactFraction = 0.65f;  // impact happens 65 % through the swing

        PlaySound(swingWhoosh, 0.4f + normalizedPower * 0.6f);

        float elapsed = 0f;
        bool  hasHit  = false;

        while (elapsed < totalDuration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / totalDuration);

            Quaternion rot;
            if (t < impactFraction)
            {
                // Backswing → Impact  (quadratic ease-in = accelerates into the ball)
                float tPhase = t / impactFraction;
                rot = Quaternion.Slerp(backswingRot, impactRot, tPhase * tPhase);
            }
            else
            {
                // Impact → Follow-through (linear decelerate)
                float tPhase = (t - impactFraction) / (1f - impactFraction);
                rot = Quaternion.Slerp(impactRot, followThruRot, tPhase);
            }

            if (clubPivot != null) clubPivot.localRotation = rot;

            // Hit the ball at the impact frame
            if (!hasHit && t >= impactFraction)
            {
                hasHit = true;
                TryHitBall(power);
            }

            yield return null;
        }

        if (!hasHit) TryHitBall(power);   // safety net — always hit

        yield return ReturnClubToRest();
        _isSwinging = false;
        _holdTime   = 0f;
    }

    void TryHitBall(float power)
    {
        if (_ball == null) { SpawnBall(); return; }

        // Only launch if player is in position (pressed B and stood beside ball)
        if (!readyToHit)
        {
            Debug.Log("[GolfSwing] Swing detected but player is not in position — press B first.");
            return;
        }

        // Direction: use arrow-key aim from GolfBallInteraction; fall back to camera forward
        Vector3 dir;
        if (aimDirection.sqrMagnitude > 0.01f)
            dir = aimDirection.normalized;
        else
        {
            dir   = _cam.transform.forward;
            dir.y = 0f;
            if (dir == Vector3.zero) dir = transform.forward;
            dir.Normalize();
        }

        // Apply loft (upward angle)
        Vector3 right = Vector3.Cross(Vector3.up, dir).normalized;
        dir = Quaternion.AngleAxis(-loftAngle, right) * dir;
        dir.Normalize();

        // Re-enable physics in case the ball was frozen after a penalty respawn
        if (_ballRb.isKinematic)
            _ballRb.isKinematic = false;

        _ballRb.AddForce(dir * power * powerScale, ForceMode.Impulse);
        _ballRb.AddTorque(right * power * 2f * powerScale, ForceMode.Impulse);

        // Enable green flight trail
        if (_ballTrail != null) _ballTrail.emitting = true;

        // Mark ball as in-flight (blocks next swing until ball stops)
        ballInFlight = true;

        // Record this stroke with the game manager
        GolfGameManager.Instance?.RecordStroke();

        readyToHit   = false;   // can't hit again until next B press
        aimDirection = Vector3.zero;

        PlaySound(ballHit, 1f);
        Debug.Log($"[GolfSwing] Hit! Power={power:F1}  Dir={dir}");

        StartCoroutine(WaitForBallToStop());
    }

    IEnumerator WaitForBallToStop()
    {
        // Give ball time to launch before checking
        yield return new WaitForSeconds(1.5f);

        while (_ballRb != null && _ballRb.linearVelocity.magnitude > 0.3f)
            yield return new WaitForSeconds(0.5f);

        // Stop trail and clear in-flight flag once ball is at rest
        if (_ballTrail != null) _ballTrail.emitting = false;
        ballInFlight = false;

        // Move spawn point to where ball landed for next shot
        if (_ball != null && ballSpawnPoint != null)
            ballSpawnPoint.position = _ball.transform.position + Vector3.up * 0.05f;

        yield return new WaitForSeconds(1f);
        SpawnBall();
    }

    /// <summary>
    /// Teleports the ball to a penalty position (e.g. water hazard drop zone).
    /// Clears velocity and trail so the ball sits still at the new spot.
    /// </summary>
    public void PenaltyRespawn(Vector3 worldPos)
    {
        if (_ball == null) { SpawnBall(); return; }

        // Snap to terrain surface
        Terrain terrain = Terrain.activeTerrain;
        if (terrain != null)
            worldPos.y = terrain.SampleHeight(worldPos) + terrain.transform.position.y + 0.08f;

        // Zero all motion first, then make kinematic so the ball cannot
        // slide or roll on any slope until the player actually swings.
        _ballRb.linearVelocity   = Vector3.zero;
        _ballRb.angularVelocity  = Vector3.zero;
        _ballRb.isKinematic      = true;   // ← frozen in place; physics cannot move it
        _ball.transform.position = worldPos;

        if (_ballTrail != null) { _ballTrail.emitting = false; _ballTrail.Clear(); }

        readyToHit = false;
        ballInFlight = false;
        Debug.Log($"[GolfSwing] Penalty respawn → {worldPos}  (ball locked until next shot)");
    }

    IEnumerator ReturnClubToRest()
    {
        float elapsed = 0f;
        float duration = 0.35f;
        Quaternion start = clubPivot != null ? clubPivot.localRotation : Quaternion.identity;

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            if (clubPivot != null)
                clubPivot.localRotation = Quaternion.Slerp(start, Quaternion.identity, elapsed / duration);
            yield return null;
        }

        if (clubPivot != null)
            clubPivot.localRotation = Quaternion.identity;
    }

    // ── ball management ──────────────────────────────────────────────

    void SpawnBall()
    {
        if (_ball != null) Destroy(_ball);

        Vector3 pos = ballSpawnPoint != null
            ? ballSpawnPoint.position
            : transform.position + transform.forward * 1.5f;

        // Snap spawn position to actual terrain surface so ball sits on the ground
        Terrain terrain = Terrain.activeTerrain;
        if (terrain != null)
        {
            float terrainY = terrain.SampleHeight(pos) + terrain.transform.position.y;
            pos.y = terrainY + 0.08f;  // spawn a little above surface to avoid clipping through
        }

        // Spawn the ball — use the assigned prefab, or fall back to a plain sphere
        if (golfBallPrefab != null)
        {
            _ball = Instantiate(golfBallPrefab, pos, Quaternion.identity);
        }
        else
        {
            // No prefab assigned — create a white sphere so the game still works
            _ball = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            _ball.transform.position   = pos;
            _ball.transform.localScale = Vector3.one * 0.043f;  // golf ball ≈ 4.3 cm
            Debug.LogWarning("[GolfSwing] golfBallPrefab is not assigned — using a plain sphere. " +
                             "Drag the golf ball prefab into GolfSwingController to use the real model.");
        }
        _ball.name = "GolfBall_Active";
        try { _ball.tag = "GolfBall"; } catch { /* tag not yet defined in Project Settings */ }

        // Ensure Rigidbody exists
        _ballRb = _ball.GetComponent<Rigidbody>();
        if (_ballRb == null) _ballRb = _ball.AddComponent<Rigidbody>();

        _ballRb.mass                        = 0.046f;   // real golf ball: 46g
        _ballRb.linearDamping               = 0.15f;   // air drag
        _ballRb.angularDamping              = 0.2f;
        _ballRb.isKinematic                 = false;   // gravity on from spawn
        _ballRb.useGravity                  = true;
        _ballRb.collisionDetectionMode      = CollisionDetectionMode.ContinuousDynamic; // no tunneling
        readyToHit                          = false;   // player must press B before swinging

        // ── Guarantee a working collider ────────────────────────────────────
        // Non-convex MeshColliders cannot interact with Rigidbodies and cause
        // the ball to fall through terrain. Replace / supplement with SphereCollider.
        foreach (var col in _ball.GetComponents<Collider>())
        {
            var mc = col as MeshCollider;
            if (mc != null)
                mc.enabled = false;   // disable mesh collider — SphereCollider takes over
        }

        var sc = _ball.GetComponent<SphereCollider>();
        if (sc == null) sc = _ball.AddComponent<SphereCollider>();
        sc.radius  = 0.5f;   // local-space radius (scales with transform.localScale)
        sc.enabled = true;

        // Bounce + friction material
        var physMat = new PhysicsMaterial("GolfBall")
        {
            bounciness      = 0.45f,
            dynamicFriction = 0.4f,
            staticFriction  = 0.5f,
            bounceCombine   = PhysicsMaterialCombine.Average,
            frictionCombine = PhysicsMaterialCombine.Average
        };
        sc.material = physMat;

        // ── Green flight trail ────────────────────────────────────────────────
        _ballTrail                  = _ball.AddComponent<TrailRenderer>();
        _ballTrail.time             = 5f;          // trail lingers 5 s before fading
        _ballTrail.startWidth       = 0.12f;
        _ballTrail.endWidth         = 0f;
        _ballTrail.minVertexDistance = 0.1f;
        _ballTrail.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        var trailMat                = new Material(Shader.Find("Sprites/Default"));
        _ballTrail.material         = trailMat;
        _ballTrail.startColor       = new Color(0.15f, 1f, 0.15f, 1f);   // bright green
        _ballTrail.endColor         = new Color(0.15f, 1f, 0.15f, 0f);   // fade out
        _ballTrail.emitting         = false;   // only emit during flight

        Debug.Log("[GolfSwing] Ball spawned at " + pos);
    }

    // ── helpers ──────────────────────────────────────────────────────

    void PlaySound(AudioClip clip, float volume)
    {
        if (clip != null && _audio != null)
            _audio.PlayOneShot(clip, volume);
    }

    // Shows swing power gizmo in Scene view
    void OnDrawGizmosSelected()
    {
        if (_cam == null) return;
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(_cam.transform.position, hitRadius);
    }
}
