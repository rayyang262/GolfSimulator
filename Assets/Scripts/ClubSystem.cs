using UnityEngine;

/// <summary>
/// Data + auto-select logic for Driver / 5-Iron / 7-Iron / Sand Wedge / Putter.
/// Attach to PlayerCapsule alongside GolfSwingController.
///
/// Power scale baseline: driver = 0.085
/// Real-world distance ratios (avg amateur, 250 yd driver):
///   5-Iron   175 yd  → 0.70 × driver = 0.060
///   7-Iron   150 yd  → 0.60 × driver = 0.051
///   SandWedge 80 yd  → 0.32 × driver = 0.027
/// </summary>
public class ClubSystem : MonoBehaviour
{
    public enum ClubType { Driver, FiveIron, SevenIron, SandWedge, Putter }

    [System.Serializable]
    public struct ClubData
    {
        public string  name;
        public float   loftAngle;
        public float   powerScale;
        public float   spinMultiplier;
        public float   linearDamping;
        public float   angularDamping;
        public float   bounciness;
        public float   groundFriction;
        public float   rollingLinearDamping;
        public float   rollingAngularDamping;
        public float   rollingFriction;
        public Color   uiColor;
    }

    [Header("Club Definitions")]
    public ClubData driver = new ClubData
    {
        name                  = "Driver",
        loftAngle             = 10f,
        powerScale            = 0.055f,  // baseline
        spinMultiplier        = 0.6f,
        linearDamping         = 0.10f,
        angularDamping        = 0.15f,
        bounciness            = 0.38f,
        groundFriction        = 0.25f,
        rollingLinearDamping  = 0.40f,
        rollingAngularDamping = 1.2f,
        rollingFriction       = 0.50f,
        uiColor               = new Color(1f, 0.82f, 0.1f)
    };

    public ClubData fiveIron = new ClubData
    {
        name                  = "5-Iron",
        loftAngle             = 25f,
        powerScale            = 0.120f,  // high loft + spin + damping eat horizontal distance; needs ~2× driver
        spinMultiplier        = 1.5f,
        linearDamping         = 0.14f,
        angularDamping        = 0.30f,
        bounciness            = 0.28f,
        groundFriction        = 0.40f,
        rollingLinearDamping  = 0.60f,
        rollingAngularDamping = 2.0f,
        rollingFriction       = 0.65f,
        uiColor               = new Color(0.6f, 1f, 0.6f)
    };

    public ClubData sevenIron = new ClubData
    {
        name                  = "7-Iron",
        loftAngle             = 33f,
        powerScale            = 0.051f,  // 150 yd / 250 yd = 0.60 × driver
        spinMultiplier        = 2.2f,
        linearDamping         = 0.18f,
        angularDamping        = 0.45f,
        bounciness            = 0.22f,
        groundFriction        = 0.55f,
        rollingLinearDamping  = 0.80f,
        rollingAngularDamping = 2.8f,
        rollingFriction       = 0.80f,
        uiColor               = new Color(0.4f, 0.8f, 1f)
    };

    public ClubData sandWedge = new ClubData
    {
        name                  = "Sand Wedge",
        loftAngle             = 56f,
        powerScale            = 0.027f,  // 80 yd / 250 yd = 0.32 × driver
        spinMultiplier        = 4.0f,
        linearDamping         = 0.20f,
        angularDamping        = 0.55f,
        bounciness            = 0.10f,
        groundFriction        = 0.85f,
        rollingLinearDamping  = 2.4f,
        rollingAngularDamping = 4.0f,
        rollingFriction       = 0.95f,
        uiColor               = new Color(1f, 0.65f, 0.15f)
    };

    public ClubData putter = new ClubData
    {
        name                  = "Putter",
        loftAngle             = 0f,
        powerScale            = 0.035f,  // soft tap — ball stays low
        spinMultiplier        = 0.10f,
        linearDamping         = 0.15f,
        angularDamping        = 0.40f,
        bounciness            = 0.04f,
        groundFriction        = 0.35f,
        rollingLinearDamping  = 0.12f,   // low so ball coasts across the green
        rollingAngularDamping = 0.90f,
        rollingFriction       = 0.18f,
        uiColor               = new Color(0.5f, 1f, 0.5f)
    };

    // ── State ─────────────────────────────────────────────────────────────────
    public ClubType CurrentClub { get; private set; } = ClubType.Driver;
    public ClubData CurrentData => GetData(CurrentClub);

    [HideInInspector] public bool putterOverride    = false;
    [HideInInspector] public bool sandWedgeOverride = false;

    // Set by ClubRecommendationDisplay after each ball landing
    private ClubType _nextClub    = ClubType.SevenIron;
    private bool     _nextClubSet = false;

    private GolfSwingController _swing;
    private bool                _teeShot = true;

    // ── Lifecycle ─────────────────────────────────────────────────────────────

    void Awake()
    {
        _swing = GetComponent<GolfSwingController>();
        if (_swing == null)
            Debug.LogError("[ClubSystem] GolfSwingController not found on same GameObject.");
    }

    void Start() => SelectClub();

    // ── Public API ────────────────────────────────────────────────────────────

    public void OnShotCompleted()
    {
        _teeShot          = false;
        sandWedgeOverride = false;
        // putterOverride is managed by BallStateManager (ball-on-green check)
        // and PuttingGreenTrigger (player walks into zone) — don't touch it here
        SelectClub();
    }

    /// <summary>
    /// Called by ClubRecommendationDisplay after the ball lands to set which
    /// club should be used for the NEXT shot. Persists through reposition.
    /// </summary>
    public void SetNextClub(ClubType type)
    {
        _nextClub    = type;
        _nextClubSet = true;
        SelectClub();
    }

    public void ResetForNewHole()
    {
        _teeShot          = true;
        _nextClubSet      = false;
        putterOverride    = false;
        sandWedgeOverride = false;
        SelectClub();
    }

    // ── Selection logic ───────────────────────────────────────────────────────

    public void SelectClub()
    {
        if (putterOverride)
            ApplyClub(ClubType.Putter);
        else if (sandWedgeOverride)
            ApplyClub(ClubType.SandWedge);
        else if (_nextClubSet)
            ApplyClub(_nextClub);
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
        ClubType.FiveIron  => fiveIron,
        ClubType.SevenIron => sevenIron,
        ClubType.SandWedge => sandWedge,
        ClubType.Putter    => putter,
        _                  => sevenIron
    };
}
