using UnityEngine;

/// <summary>
/// Added to GolfBall_Active at spawn time by GolfSwingController.
///
/// Two-phase physics:
///   FLIGHT  — low air drag (set by ClubSystem), normal bounciness
///   ROLLING — on first ground contact, switches to high friction + heavy damping
///             to simulate grass deceleration realistically
///
/// Per-club rolling values are written by GolfSwingController.ApplyClubBallPhysics()
/// before the shot so Driver rolls further than 7-Iron, putter barely rolls at all.
/// </summary>
[RequireComponent(typeof(Rigidbody))]
[RequireComponent(typeof(SphereCollider))]
public class GolfBallRoller : MonoBehaviour
{
    // ── Set by GolfSwingController before each shot ────────────────────────────
    [HideInInspector] public float rollingLinearDamping  = 1.4f;
    [HideInInspector] public float rollingAngularDamping = 2.8f;
    [HideInInspector] public float rollingFriction       = 0.75f;
    // Putter skips the strong resistance formula — Unity linear damping handles it
    [HideInInspector] public bool  skipRollingResistance = false;

    // ── Private ───────────────────────────────────────────────────────────────
    private Rigidbody     _rb;
    private SphereCollider _sc;
    private bool          _hasLanded  = false;
    private float         _flightTime = 0f;
    private float         _crawlTimer = 0f;  // time spent rolling below crawl speed

    // How many degrees from vertical a collision normal can be and still count as ground
    private const float GroundNormalTolerance = 55f;

    void Awake()
    {
        _rb = GetComponent<Rigidbody>();
        _sc = GetComponent<SphereCollider>();
    }

    void FixedUpdate()
    {
        if (!_hasLanded)
        {
            _flightTime += Time.fixedDeltaTime;
            return;
        }

        // ── Rolling resistance ─────────────────────────────────────────────────
        float speed = _rb.linearVelocity.magnitude;
        if (!skipRollingResistance && speed > 0.02f && speed < 5f)
        {
            // Stronger coefficients ensure even gentle slopes can't sustain terminal
            // rolling indefinitely. At 0.5 m/s: ~0.35 m/s² deceleration (vs old ~0.05).
            float resistanceMag = speed * speed * 0.20f + speed * 0.60f;
            _rb.AddForce(-_rb.linearVelocity.normalized * resistanceMag,
                         ForceMode.Acceleration);
        }

        // ── Crawl-stop backstop ────────────────────────────────────────────────
        // If the ball has been rolling slowly for 4 s (e.g. trapped on a slope),
        // force it to sleep so the game can declare it stopped and continue.
        if (speed < 0.8f)
            _crawlTimer += Time.fixedDeltaTime;
        else
            _crawlTimer = 0f;

        if (_crawlTimer >= 4.0f)
        {
            _rb.linearVelocity  = Vector3.zero;
            _rb.angularVelocity = Vector3.zero;
            _rb.Sleep();
        }
    }

    void OnCollisionEnter(Collision collision)
    {
        if (_hasLanded) return;

        // Ignore the first ~0.25 s so tee-ground contact doesn't trigger this
        if (_flightTime < 0.25f) return;

        // Only accept contacts where the surface faces mostly upward (ground/terrain),
        // not walls or other balls
        foreach (ContactPoint cp in collision.contacts)
        {
            if (Vector3.Angle(cp.normal, Vector3.up) <= GroundNormalTolerance)
            {
                SwitchToRollingPhysics();
                return;
            }
        }
    }

    /// <summary>
    /// Called by GolfSwingController immediately before each shot.
    /// Resets flight-time and landed state so that a previously frozen
    /// (kinematic) ball does not trigger SwitchToRollingPhysics() the
    /// instant it becomes non-kinematic — which would apply heavy rolling
    /// drag before the ball has left the ground, causing extremely short shots.
    /// </summary>
    public void ResetForLaunch()
    {
        _hasLanded            = false;
        _flightTime           = 0f;
        _crawlTimer           = 0f;
        skipRollingResistance = false;
    }

    /// <summary>
    /// Called for putter shots: bypass the 0.25s flight guard and apply rolling
    /// physics immediately so the ball rolls from the first frame.
    /// </summary>
    public void ForceRollingMode()
    {
        if (_hasLanded) return;
        _flightTime = 1f;   // skip the flight-time guard in OnCollisionEnter
        SwitchToRollingPhysics();
    }

    void SwitchToRollingPhysics()
    {
        _hasLanded = true;

        // ── Rigidbody damping ──────────────────────────────────────────────────
        _rb.linearDamping  = rollingLinearDamping;
        _rb.angularDamping = rollingAngularDamping;

        // ── PhysicsMaterial ────────────────────────────────────────────────────
        // Use Maximum combine so the ball's friction always wins over low-friction
        // terrain materials — grass feel is always preserved.
        if (_sc != null && _sc.material != null)
        {
            var mat                 = _sc.material;
            mat.dynamicFriction     = rollingFriction;
            mat.staticFriction      = rollingFriction + 0.15f;
            // Putter (low rollingFriction) needs Minimum combine so the ball's
            // smooth-green value (0.18) isn't overridden by terrain friction (~0.6).
            // All other clubs use Maximum so high-friction terrain stops them correctly.
            mat.frictionCombine = rollingFriction < 0.3f
                ? PhysicsMaterialCombine.Minimum
                : PhysicsMaterialCombine.Maximum;
            mat.bounciness          = Mathf.Min(mat.bounciness * 0.4f, 0.12f); // much less bounce after landing
            mat.bounceCombine       = PhysicsMaterialCombine.Minimum;
        }

        Debug.Log($"[GolfBallRoller] Landed after {_flightTime:F1}s — rolling physics active " +
                  $"(linDrag={rollingLinearDamping}, angDrag={rollingAngularDamping}, friction={rollingFriction:F2})");
    }
}
