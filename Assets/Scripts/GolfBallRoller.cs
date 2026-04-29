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

    // ── Private ───────────────────────────────────────────────────────────────
    private Rigidbody     _rb;
    private SphereCollider _sc;
    private bool          _hasLanded  = false;
    private float         _flightTime = 0f;

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
        // Unity's PhysicsMaterial friction acts on surface contact area, but rolling
        // momentum bleeds off too slowly for a golf game feel. Apply an explicit
        // velocity-proportional deceleration force while the ball is rolling slowly.
        float speed = _rb.linearVelocity.magnitude;
        if (speed > 0.02f && speed < 5f)
        {
            // Gentler resistance so the ball can still glide and roll naturally
            // on slopes.  Old quadratic 0.35 was so strong it overcame gravity
            // on any hill steeper than ~12°, pinning the ball in place.
            // These coefficients still stop the ball decisively on flat grass
            // but let slope gravity win and carry the ball to a natural lie.
            float resistanceMag = speed * speed * 0.08f + speed * 0.05f;
            _rb.AddForce(-_rb.linearVelocity.normalized * resistanceMag,
                         ForceMode.Acceleration);
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
            mat.frictionCombine     = PhysicsMaterialCombine.Maximum;
            mat.bounciness          = Mathf.Min(mat.bounciness * 0.4f, 0.12f); // much less bounce after landing
            mat.bounceCombine       = PhysicsMaterialCombine.Minimum;
        }

        Debug.Log($"[GolfBallRoller] Landed after {_flightTime:F1}s — rolling physics active " +
                  $"(linDrag={rollingLinearDamping}, angDrag={rollingAngularDamping}, friction={rollingFriction:F2})");
    }
}
