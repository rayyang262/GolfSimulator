using UnityEngine;
using System.Collections;
using StarterAssets;

/// <summary>
/// Tracks the last safe ball positions during flight and handles all out-of-bounds
/// respawn logic — water hazards, below-terrain falls, and missing bounds volumes.
///
/// Attach to PlayerCapsule alongside GolfSwingController and BallStateManager.
///
/// HOW SAFE POSITIONS ARE STORED:
///   Every 0.5 s while ballInFlight == true, the ball's world position is checked:
///     - Must be above terrain surface (not clipping underground)
///     - Must not be flagged as an active hazard
///   If valid, it is written into a ring buffer of 5 positions.
///   On OOB, the most recent valid position is used for respawn.
///
/// OOB IS TRIGGERED BY:
///   1. WaterHazard.cs calling TriggerOOB("water")
///   2. Below-terrain detection (ball.y < terrain.y - 1.5 m)
///   3. Any other hazard script calling TriggerOOB(reason)
/// </summary>
[RequireComponent(typeof(GolfSwingController))]
public class OutOfBoundsTracker : MonoBehaviour
{
    // ── Inspector ─────────────────────────────────────────────────────────────

    [Header("Safe Position Tracking")]
    [Tooltip("How often (seconds) a new safe position is sampled during flight.")]
    public float sampleInterval = 0.5f;
    [Tooltip("How many safe positions to remember. The latest valid one is used on OOB.")]
    public int   bufferSize     = 5;
    [Tooltip("Ball must be at least this far above terrain to count as a safe position.")]
    public float minHeightAboveTerrain = 0.3f;

    [Header("Below-Terrain OOB")]
    [Tooltip("If ball drops this far below terrain surface, OOB is triggered immediately.")]
    public float belowTerrainThreshold = 1.5f;

    [Header("Respawn")]
    [Tooltip("Horizontal offset from safe position where the player is repositioned.")]
    public float playerRespawnOffset = 1.5f;
    [Tooltip("Seconds to wait before respawning the ball (gives the penalty panel time to show).")]
    public float respawnDelay = 1.2f;

    // ── Private ───────────────────────────────────────────────────────────────

    private GolfSwingController  _swing;
    private BallStateManager     _bsm;
    private CharacterController  _cc;
    private FirstPersonController _fpc;
    private StarterAssetsInputs  _inputs;
    private GolfBallInteraction  _interact;

    private Vector3[] _safePositions;
    private int       _safeHead    = 0;   // points to next write slot (ring buffer)
    private int       _safeCount   = 0;   // how many valid entries have been stored

    private bool      _oobActive   = false;   // guard against double-trigger
    private float     _sampleTimer = 0f;

    private static readonly Vector3 FallbackSafePosition = new Vector3(320f, 105f, 240f);

    // ── Lifecycle ─────────────────────────────────────────────────────────────

    void Awake()
    {
        _swing    = GetComponent<GolfSwingController>();
        _bsm      = GetComponent<BallStateManager>();
        _cc       = GetComponent<CharacterController>();
        _fpc      = GetComponent<FirstPersonController>();
        _inputs   = GetComponent<StarterAssetsInputs>();
        _interact = GetComponent<GolfBallInteraction>();

        _safePositions = new Vector3[bufferSize];
        for (int i = 0; i < bufferSize; i++)
            _safePositions[i] = FallbackSafePosition;
    }

    void Update()
    {
        if (_oobActive) return;
        if (!_swing.ballInFlight) return;

        _sampleTimer += Time.deltaTime;
        if (_sampleTimer >= sampleInterval)
        {
            _sampleTimer = 0f;
            TrySampleSafePosition();
        }

        CheckBelowTerrain();
    }

    // ── Safe position sampling ────────────────────────────────────────────────

    void TrySampleSafePosition()
    {
        GameObject ballGO = GetBallGO();
        if (ballGO == null) return;

        Vector3 pos = ballGO.transform.position;

        // Reject positions underground
        if (!IsAboveTerrain(pos, minHeightAboveTerrain)) return;

        // Write into ring buffer
        _safePositions[_safeHead] = pos;
        _safeHead  = (_safeHead + 1) % bufferSize;
        if (_safeCount < bufferSize) _safeCount++;
    }

    bool IsAboveTerrain(Vector3 pos, float minClearance)
    {
        Terrain terrain = Terrain.activeTerrain;
        if (terrain != null)
        {
            float terrainY = terrain.SampleHeight(pos) + terrain.transform.position.y;
            return pos.y >= terrainY + minClearance;
        }
        // No terrain — raycast down
        if (Physics.Raycast(pos + Vector3.up * 2f, Vector3.down, out RaycastHit hit, 50f))
            return pos.y >= hit.point.y + minClearance;
        return true;  // can't verify — accept it
    }

    // ── Below-terrain OOB detection ───────────────────────────────────────────

    void CheckBelowTerrain()
    {
        GameObject ballGO = GetBallGO();
        if (ballGO == null) return;

        Vector3 pos = ballGO.transform.position;

        Terrain terrain = Terrain.activeTerrain;
        if (terrain != null)
        {
            float terrainY = terrain.SampleHeight(pos) + terrain.transform.position.y;
            if (pos.y < terrainY - belowTerrainThreshold)
            {
                TriggerOOB("below-terrain");
                return;
            }
        }

        // Hard floor fallback — if ball falls below Y = 50 on this course
        if (pos.y < 50f)
            TriggerOOB("fell-off-map");
    }

    // ── Public OOB entry points ───────────────────────────────────────────────

    /// <summary>
    /// Generic OOB (below-terrain, fell off map, etc.).
    /// Uses the last safe position from the ring buffer.
    /// </summary>
    public void TriggerOOB(string reason)
    {
        if (_oobActive) return;
        _oobActive = true;

        Vector3 safePos = GetLastSafePosition();
        Debug.Log($"[OutOfBounds] OOB triggered: '{reason}'  respawn → {safePos}");

        GolfGameManager.Instance?.AddPenalty();
        StartCoroutine(RespawnSequence(safePos));
    }

    /// <summary>
    /// Hazard OOB with a fixed drop-zone (water hazard, etc.).
    /// Ignores the ring buffer and always respawns at <paramref name="dropZone"/>.
    /// Player AND ball both teleport to that position so the lie is predictable.
    /// </summary>
    public void TriggerOOBAtPosition(string reason, Vector3 dropZone)
    {
        if (_oobActive) return;
        _oobActive = true;

        Debug.Log($"[OutOfBounds] OOB triggered: '{reason}'  drop zone → {dropZone}");

        GolfGameManager.Instance?.AddPenalty();
        StartCoroutine(RespawnSequence(dropZone));
    }

    // ── Respawn sequence ──────────────────────────────────────────────────────

    IEnumerator RespawnSequence(Vector3 safePos)
    {
        // 1. Immediately freeze and move ball to last safe position
        FreezeAndMoveBall(safePos);

        // 2. Clear in-flight flag so BSM stop detection doesn't fire during the wait
        _swing.ballInFlight = false;

        // 3. Wait so the penalty panel / screen-flash has time to register
        yield return new WaitForSeconds(respawnDelay);

        // 4. Move player beside safe position
        TeleportPlayerToSafePos(safePos);

        // 5. Spawn a fresh ball at the safe position and freeze it immediately
        //    so it cannot roll off a slope before the player swings
        _swing.UpdateSpawnPoint(safePos);
        _swing.RespawnBall();
        _swing.FreezeBall();

        // 6. Notify club system (OOB shot counts as a used shot)
        GetComponent<ClubSystem>()?.OnShotCompleted();

        // 7. Re-enable player movement components
        //    (CC + FPC were both disabled in BallStateManager.OnEnterBallFollow)
        if (_cc  != null) _cc.enabled  = true;
        if (_fpc != null) _fpc.enabled = true;

        // 8. Force BallStateManager straight back to Aim — this also clears
        //    blockBInput and the "Press B" prompt, so the player can swing immediately
        if (_bsm != null)
            _bsm.ForceReturnToAim();
        else if (_interact != null)
            _interact.blockBInput = false;

        // 9. Grace period — _oobActive stays TRUE so that any OnTriggerEnter
        //    callbacks from the newly-spawned ball (physics runs one frame later)
        //    are silently absorbed and cannot re-start this coroutine.
        yield return new WaitForSeconds(1.5f);

        // 10. Now safe to accept the next real OOB event
        _oobActive   = false;
        _sampleTimer = 0f;

        Debug.Log("[OutOfBounds] Respawn complete. Ready for next shot.");
    }

    void FreezeAndMoveBall(Vector3 targetPos)
    {
        GameObject ballGO = GetBallGO();
        if (ballGO == null) return;

        var rb = ballGO.GetComponent<Rigidbody>();
        if (rb != null)
        {
            rb.linearVelocity  = Vector3.zero;
            rb.angularVelocity = Vector3.zero;
            rb.isKinematic     = true;
        }

        // Snap to terrain surface at safe position
        Terrain terrain = Terrain.activeTerrain;
        Vector3 pos = targetPos;
        if (terrain != null)
            pos.y = terrain.SampleHeight(pos) + terrain.transform.position.y + 0.08f;

        ballGO.transform.position = pos;

        var trail = ballGO.GetComponent<TrailRenderer>();
        if (trail != null) { trail.emitting = false; trail.Clear(); }
    }

    void TeleportPlayerToSafePos(Vector3 safePos)
    {
        // Stand to the right of the safe position (facing the fallback forward)
        Vector3 forward     = transform.forward;
        forward.y           = 0f;
        if (forward.sqrMagnitude < 0.001f) forward = Vector3.forward;
        forward.Normalize();

        Vector3 rightOfLine = Quaternion.Euler(0f, 90f, 0f) * forward;
        Vector3 tpPos       = safePos + rightOfLine * playerRespawnOffset;

        Terrain terrain = Terrain.activeTerrain;
        if (terrain != null)
            tpPos.y = terrain.SampleHeight(tpPos) + terrain.transform.position.y + 0.05f;

        if (_cc != null) _cc.enabled = false;
        transform.position = tpPos;
        // Don't force a rotation — let the player re-aim naturally
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    Vector3 GetLastSafePosition()
    {
        if (_safeCount == 0) return FallbackSafePosition;

        // Use 2 samples back (= 1 full second before OOB at default 0.5s interval).
        // The most-recent sample is often right at the hazard edge — stepping back
        // gives clearance so the ball doesn't immediately fall into the hazard again.
        int stepsBack = Mathf.Min(2, _safeCount - 1);
        int idx       = (_safeHead - 1 - stepsBack + bufferSize * 3) % bufferSize;
        return _safePositions[idx];
    }

    static GameObject GetBallGO()
    {
        GameObject go = null;
        try   { go = GameObject.FindWithTag("GolfBall"); } catch { }
        return go != null ? go : GameObject.Find("GolfBall_Active");
    }

    // ── Gizmos ────────────────────────────────────────────────────────────────

    void OnDrawGizmos()
    {
        if (_safeCount == 0 || _safePositions == null) return;

        Gizmos.color = new Color(0f, 1f, 0.4f, 0.8f);
        int lastIdx  = (_safeHead - 1 + bufferSize) % bufferSize;

        for (int i = 0; i < _safeCount; i++)
        {
            int idx   = (_safeHead - 1 - i + bufferSize * 2) % bufferSize;
            float alpha = 1f - (float)i / bufferSize;
            Gizmos.color  = new Color(0f, 1f, 0.4f, alpha * 0.8f);
            Gizmos.DrawSphere(_safePositions[idx], 0.25f);

            if (i > 0)
            {
                int prev = (_safeHead - i + bufferSize * 2) % bufferSize;
                Gizmos.DrawLine(_safePositions[idx], _safePositions[prev]);
            }
        }

        // Highlight the most recent safe position
        Gizmos.color = Color.green;
        Gizmos.DrawWireSphere(_safePositions[lastIdx], 0.4f);
    }
}
