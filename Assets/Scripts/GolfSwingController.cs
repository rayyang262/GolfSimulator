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
    public bool usePhoneInput = false;

    // ── private state ────────────────────────────────────────────────
    private GameObject  _ball;
    private Rigidbody   _ballRb;
    private bool        _isHolding;
    private bool        _isSwinging;
    private float       _holdTime;
    private float       _currentClubAngle;   // local X rotation of clubPivot
    private AudioSource _audio;
    private Camera      _cam;

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
        if (usePhoneInput)  return; // TouchOscSwingController drives input instead

        bool pressing = Mouse.current != null && Mouse.current.leftButton.isPressed;

        if (pressing && !_isHolding)
            BeginBackswing();

        if (_isHolding && pressing)
            ContinueBackswing();

        if (_isHolding && !pressing)
            StartCoroutine(Downswing());
    }

    // ── backswing ────────────────────────────────────────────────────

    void BeginBackswing()
    {
        _isHolding  = true;
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
        _isHolding = true;
        _holdTime  = power / powerPerSecond;
        StartCoroutine(Downswing());
    }

    /// <summary>
    /// Called when the phone swing timed out without a valid hit — just returns club to rest.
    /// </summary>
    public void PhoneWhiff()
    {
        if (_isSwinging) return;
        _isHolding = false;
        StartCoroutine(ReturnClubToRest());
    }

    // ── downswing + hit ──────────────────────────────────────────────

    IEnumerator Downswing()
    {
        _isHolding  = false;
        _isSwinging = true;

        float power = Mathf.Clamp(_holdTime * powerPerSecond, 2f, maxPower);

        // Swing the club forward fast
        float angle = _currentClubAngle;
        while (angle > -10f)
        {
            angle -= downswingSpeed * Time.deltaTime;
            if (clubPivot != null)
                clubPivot.localRotation = Quaternion.Euler(angle, 0f, 0f);
            yield return null;
        }

        // Impact moment — check if ball is close enough
        TryHitBall(power);

        // Return club to rest
        yield return ReturnClubToRest();

        _isSwinging = false;
        _holdTime   = 0f;
    }

    void TryHitBall(float power)
    {
        if (_ball == null) { SpawnBall(); return; }

        float dist = Vector3.Distance(_cam.transform.position, _ball.transform.position);
        if (dist > hitRadius) return;  // whiff — ball too far away

        // Direction = camera forward + loft
        Vector3 dir = _cam.transform.forward;
        dir = Quaternion.AngleAxis(-loftAngle, _cam.transform.right) * dir;
        dir.Normalize();

        // Unfreeze ball and apply force
        _ballRb.isKinematic = false;
        _ballRb.AddForce(dir * power, ForceMode.Impulse);
        _ballRb.AddTorque(_cam.transform.right * power * 2f, ForceMode.Impulse);

        PlaySound(ballHit, 1f);
        Debug.Log($"[GolfSwing] Hit! Power={power:F1}  Dir={dir}");

        // Monitor ball until it stops, then respawn
        StartCoroutine(WaitForBallToStop());
    }

    IEnumerator WaitForBallToStop()
    {
        // Give ball time to launch before checking
        yield return new WaitForSeconds(1.5f);

        while (_ballRb != null && _ballRb.linearVelocity.magnitude > 0.3f)
            yield return new WaitForSeconds(0.5f);

        // Move spawn point to where ball landed for next shot
        if (_ball != null && ballSpawnPoint != null)
            ballSpawnPoint.position = _ball.transform.position + Vector3.up * 0.05f;

        yield return new WaitForSeconds(1f);
        SpawnBall();
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
            pos.y = terrainY + 0.035f;  // golf ball radius ≈ 3.5 cm
        }

        _ball = Instantiate(golfBallPrefab, pos, Quaternion.identity);
        _ball.name = "GolfBall_Active";
        try { _ball.tag = "GolfBall"; } catch { /* tag not yet added in Project Settings */ }

        // Ensure Rigidbody exists
        _ballRb = _ball.GetComponent<Rigidbody>();
        if (_ballRb == null) _ballRb = _ball.AddComponent<Rigidbody>();

        _ballRb.mass          = 0.046f; // real golf ball: 46g  (with maxPower=5 → launch ~108 m/s)
        _ballRb.linearDamping = 0.15f;  // air drag — increase to shorten carry distance
        _ballRb.angularDamping = 0.2f;
        _ballRb.isKinematic   = true;   // frozen until hit
        _ballRb.useGravity    = true;

        // Bounce + friction material (so ball rolls and bounces realistically on the grass)
        var physMat = new PhysicsMaterial("GolfBall")
        {
            bounciness       = 0.45f,
            dynamicFriction  = 0.4f,
            staticFriction   = 0.5f,
            bounceCombine    = PhysicsMaterialCombine.Average,
            frictionCombine  = PhysicsMaterialCombine.Average
        };
        foreach (var col in _ball.GetComponents<Collider>())
            col.material = physMat;

        // Ensure MeshCollider is convex for physics
        var mc = _ball.GetComponent<MeshCollider>();
        if (mc != null) mc.convex = true;

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
