using UnityEngine;

public class WaterHazard : MonoBehaviour
{
    [Header("Penalty Spawn")]
    public Vector3 penaltyPosition = new Vector3(320f, 105f, 246f);

    private void Awake()
    {
        foreach (Collider col in GetComponents<Collider>())
        {
            if (col is MeshCollider mc)
                mc.convex = true;
            col.isTrigger = true;
        }
    }

    private void OnTriggerEnter(Collider other)
    {
        bool isGolfBall = other.name == "GolfBall_Active";
        if (!isGolfBall)
        {
            try { isGolfBall = other.CompareTag("GolfBall"); }
            catch { isGolfBall = false; }
        }

        if (!isGolfBall) return;

        GolfSwingController swing = FindFirstObjectByType<GolfSwingController>();

        if (GolfGameManager.Instance != null)
        {
            GolfGameManager.Instance.AddWaterPenalty(swing);
            Debug.Log("[WaterHazard] Ball entered water hazard. Penalty applied.");
        }
        else
        {
            Debug.LogWarning("[WaterHazard] GolfGameManager.Instance not found.");
        }
    }

    private void OnDrawGizmos()
    {
        Gizmos.color = new Color(0f, 0.4f, 1f, 0.5f);
        Gizmos.matrix = transform.localToWorldMatrix;
        Gizmos.DrawWireCube(Vector3.zero, Vector3.one);
    }
}
