using UnityEngine;

/// <summary>
/// Attach to any water mesh/volume.
/// Routes OOB through OutOfBoundsTracker (uses last safe position + ring buffer).
/// Falls back to the hardcoded penalty position if OutOfBoundsTracker is absent.
/// </summary>
public class WaterHazard : MonoBehaviour
{
    [Header("Water Hazard Drop Zone")]
    [Tooltip("Ball AND player always respawn here after a water penalty. " +
             "Adjust in the Inspector to match your course drop zone.")]
    public Vector3 penaltyPosition = new Vector3(320f, 105f, 240f);

    private void Awake()
    {
        // Unity does not support triggers on concave MeshColliders.
        // For each non-convex MeshCollider, add a BoxCollider trigger that matches
        // the renderer bounds instead. Convex MeshColliders and other collider
        // types are converted to triggers directly.
        foreach (Collider col in GetComponents<Collider>())
        {
            if (col is MeshCollider mc && !mc.convex)
            {
                // Can't make it a trigger — add a BoxCollider sized to the mesh bounds
                Renderer rend = GetComponent<Renderer>();
                Bounds bounds = rend != null ? rend.localBounds : new Bounds(Vector3.zero, Vector3.one);

                BoxCollider box = gameObject.AddComponent<BoxCollider>();
                box.center    = bounds.center;
                box.size      = bounds.size;
                box.isTrigger = true;

                // Disable the mesh collider so only the box trigger is active
                mc.enabled = false;
            }
            else
            {
                col.isTrigger = true;
            }
        }
    }

    private void OnTriggerEnter(Collider other)
    {
        if (!IsBall(other)) return;

        Debug.Log("[WaterHazard] Ball entered water hazard.");

        // Use the fixed drop-zone so ball + player always appear at the
        // designated penalty area rather than wherever the ball hit the water.
        OutOfBoundsTracker tracker = FindFirstObjectByType<OutOfBoundsTracker>();
        if (tracker != null)
        {
            tracker.TriggerOOBAtPosition("water", penaltyPosition);
            return;
        }

        // Legacy fallback: use hardcoded penalty position via GolfGameManager
        GolfSwingController swing = FindFirstObjectByType<GolfSwingController>();
        if (GolfGameManager.Instance != null && swing != null)
        {
            GolfGameManager.Instance.AddWaterPenalty(swing);
            Debug.Log("[WaterHazard] Fallback: penalty applied at hardcoded position.");
        }
        else
        {
            Debug.LogWarning("[WaterHazard] Neither OutOfBoundsTracker nor GolfGameManager found.");
        }
    }

    static bool IsBall(Collider other)
    {
        if (other.name == "GolfBall_Active") return true;
        try { return other.CompareTag("GolfBall"); }
        catch { return false; }
    }

    private void OnDrawGizmos()
    {
        Gizmos.color  = new Color(0f, 0.4f, 1f, 0.5f);
        Gizmos.matrix = transform.localToWorldMatrix;
        Gizmos.DrawWireCube(Vector3.zero, Vector3.one);
    }
}
