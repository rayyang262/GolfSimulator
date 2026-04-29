using UnityEngine;

/// <summary>
/// Data + auto-select logic for Driver / 7-Iron / Putter.
/// Attach to PlayerCapsule alongside GolfSwingController.
/// PuttingGreenTrigger overrides to Putter when the ball enters the green —
/// this system defers to that by checking _putterOverride.
/// </summary>
public class ClubSystem : MonoBehaviour
{
    public enum ClubType { Driver, SevenIron, Putter }

    // ── Club data ─────────────────────────────────────────────────────────────
    [System.Serializable]
    public struct ClubData
    {
        public string  name;
        public float   loftAngle;             // degrees upward from horizontal
        public float   powerScale;            // multiplier on GolfSwingController.maxPower
        public float   spinMultiplier;        // multiplier on torque AddForce
        public float   linearDamping;         // Rigidbody air drag during flight
        public float   angularDamping;        // Rigidbody angular drag during flight
        public float   bounciness;            // PhysicsMaterial bounciness on hit
        public float   groundFriction;        // PhysicsMaterial dynamic friction on spawn
        public float   rollingLinearDamping;  // Rigidbody linear drag after first ground contact
        public float   rollingAngularDamping; // Rigidbody angular drag after first ground contact
        public float   rollingFriction;       // PhysicsMaterial friction after first ground contact
        public Color   uiColor;               // club label color
    }

    [Header("Club Definitions")]
    public ClubData driver = new ClubData
    {
        name                  = "Driver",
        loftAngle             = 10f,
        powerScale            = 0.224f,  // -20 % power
        spinMultiplier        = 0.6f,
        linearDamping         = 0.10f,   // low air drag = long flight
        angularDamping        = 0.15f,
        bounciness            = 0.38f,
        groundFriction        = 0.25f,
        rollingLinearDamping  = 0.40f,   // reduced so ball can roll on slopes
        rollingAngularDamping = 1.2f,
        rollingFriction       = 0.50f,
        uiColor               = new Color(1f, 0.82f, 0.1f)
    };

    public ClubData sevenIron = new ClubData
    {
        name                  = "7-Iron",
        loftAngle             = 33f,
        powerScale            = 0.144f,  // -20 % power
        spinMultiplier        = 2.2f,
        linearDamping         = 0.18f,
        angularDamping        = 0.45f,
        bounciness            = 0.22f,
        groundFriction        = 0.55f,
        rollingLinearDamping  = 0.80f,   // reduced so ball can roll on slopes
        rollingAngularDamping = 2.8f,
        rollingFriction       = 0.80f,
        uiColor               = new Color(0.4f, 0.8f, 1f)
    };

    public ClubData putter = new ClubData
    {
        name                  = "Putter",
        loftAngle             = 3f,
        powerScale            = 0.12f,
        spinMultiplier        = 0.15f,
        linearDamping         = 0.30f,
        angularDamping        = 0.85f,
        bounciness            = 0.08f,
        groundFriction        = 0.80f,
        rollingLinearDamping  = 2.8f,    // stops quickly — precision putting feel
        rollingAngularDamping = 4.5f,
        rollingFriction       = 0.92f,
        uiColor               = new Color(0.5f, 1f, 0.5f)
    };

    // ── State ─────────────────────────────────────────────────────────────────
    public ClubType  CurrentClub  { get; private set; } = ClubType.Driver;
    public ClubData  CurrentData  => GetData(CurrentClub);

    // Set true by PuttingGreenTrigger.OnTriggerEnter, false on OnTriggerExit
    [HideInInspector] public bool putterOverride = false;

    private GolfSwingController _swing;
    private bool                _teeShot = true;  // first shot of the hole = Driver

    // ── Lifecycle ─────────────────────────────────────────────────────────────

    void Awake()
    {
        _swing = GetComponent<GolfSwingController>();
        if (_swing == null)
            Debug.LogError("[ClubSystem] GolfSwingController not found on same GameObject.");
    }

    void Start()
    {
        SelectClub();
    }

    // ── Public API ────────────────────────────────────────────────────────────

    /// <summary>
    /// Call once after each shot lands (from BallStateManager.OnBallStopped).
    /// </summary>
    public void OnShotCompleted()
    {
        _teeShot = false;
        SelectClub();
    }

    /// <summary>
    /// Call when the hole is holed out to reset state for next hole.
    /// </summary>
    public void ResetForNewHole()
    {
        _teeShot = true;
        putterOverride = false;
        SelectClub();
    }

    // ── Selection logic ───────────────────────────────────────────────────────

    public void SelectClub()
    {
        if (putterOverride)
            ApplyClub(ClubType.Putter);
        else if (_teeShot)
            ApplyClub(ClubType.Driver);
        else
            ApplyClub(ClubType.SevenIron);
    }

    void ApplyClub(ClubType type)
    {
        CurrentClub = type;
        ClubData d  = GetData(type);

        if (_swing != null)
        {
            _swing.loftAngle  = d.loftAngle;
            _swing.powerScale = d.powerScale;
        }

        Debug.Log($"[ClubSystem] Active club: {d.name}  loft={d.loftAngle}°  powerScale={d.powerScale}");
    }

    public ClubData GetData(ClubType type) => type switch
    {
        ClubType.Driver    => driver,
        ClubType.SevenIron => sevenIron,
        ClubType.Putter    => putter,
        _                  => sevenIron
    };
}
