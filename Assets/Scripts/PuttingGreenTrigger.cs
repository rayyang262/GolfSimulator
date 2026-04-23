using UnityEngine;

/// <summary>
/// Place on any GameObject inside a putting green area.
/// Automatically adds a trigger BoxCollider sized to zoneSize.
/// When the player enters, maxPower and loftAngle on GolfSwingController
/// are swapped for putter values and an optional Cinemachine Virtual Camera
/// is promoted to priority 20. On exit everything is restored.
/// </summary>
public class PuttingGreenTrigger : MonoBehaviour
{
    [Header("Zone Size")]
    public Vector3 zoneSize = new Vector3(20f, 4f, 20f);

    [Header("Putter Settings")]
    public float putterMaxPower  = 0.8f;
    public float putterLoftAngle = 3f;

    [Header("Camera (optional)")]
    [Tooltip("Assign a Cinemachine Virtual Camera to activate when entering putting zone")]
    public GameObject puttingVCam;

    // ── private state ────────────────────────────────────────────────
    private float               _savedMaxPower;
    private float               _savedLoftAngle;
    private GolfSwingController _swing;
    private bool                _inZone;

    // ── lifecycle ────────────────────────────────────────────────────

    void Awake()
    {
        var col    = gameObject.AddComponent<BoxCollider>();
        col.size      = zoneSize;
        col.isTrigger = true;
        col.center    = Vector3.zero;
    }

    void Start()
    {
        _swing = FindFirstObjectOfType<GolfSwingController>();
        if (_swing == null)
            Debug.LogWarning("[PuttingGreenTrigger] GolfSwingController not found in scene.");
    }

    // ── trigger callbacks ─────────────────────────────────────────────

    void OnTriggerEnter(Collider other)
    {
        if (_inZone) return;
        if (!IsPlayer(other)) return;

        _inZone = true;

        if (_swing != null)
        {
            _savedMaxPower  = _swing.maxPower;
            _savedLoftAngle = _swing.loftAngle;
            _swing.maxPower  = putterMaxPower;
            _swing.loftAngle = putterLoftAngle;
        }

        SetPuttingCamPriority(20);
        Debug.Log("[PuttingGreenTrigger] Entered putting zone — putter mode active");
    }

    void OnTriggerExit(Collider other)
    {
        if (!_inZone) return;
        if (!IsPlayer(other)) return;

        _inZone = false;

        if (_swing != null)
        {
            _swing.maxPower  = _savedMaxPower;
            _swing.loftAngle = _savedLoftAngle;
        }

        SetPuttingCamPriority(0);
        Debug.Log("[PuttingGreenTrigger] Exited putting zone — settings restored");
    }

    // ── helpers ───────────────────────────────────────────────────────

    static bool IsPlayer(Collider other)
    {
        return other.CompareTag("Player")
            || other.name.Contains("PlayerCapsule")
            || other.name == "GolfBall_Active";
    }

    void SetPuttingCamPriority(int priority)
    {
        if (puttingVCam == null) return;

        try
        {
            var vcam = puttingVCam.GetComponent<Cinemachine.CinemachineVirtualCamera>();
            if (vcam != null)
                vcam.Priority = priority;
        }
        catch (System.Exception ex)
        {
            Debug.LogWarning($"[PuttingGreenTrigger] Cinemachine camera priority not set: {ex.Message}");
        }
    }

    // ── gizmos ────────────────────────────────────────────────────────

    void OnDrawGizmos()
    {
        DrawZoneGizmo(false);
    }

    void OnDrawGizmosSelected()
    {
        DrawZoneGizmo(true);
    }

    void DrawZoneGizmo(bool selected)
    {
        Matrix4x4 prev = Gizmos.matrix;
        Gizmos.matrix = Matrix4x4.TRS(transform.position, transform.rotation, Vector3.one);

        Gizmos.color = new Color(0f, 1f, 0f, 0.15f);
        Gizmos.DrawCube(Vector3.zero, zoneSize);

        Gizmos.color = Color.green;
        Gizmos.DrawWireCube(Vector3.zero, zoneSize);

        Gizmos.matrix = prev;

#if UNITY_EDITOR
        UnityEditor.Handles.Label(transform.position + Vector3.up * (zoneSize.y * 0.5f + 0.5f), "Putting Zone");
#endif
    }
}
