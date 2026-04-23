using UnityEngine;

public class GolfHole : MonoBehaviour
{
    [Header("Hole Settings")]
    public float holeRadius = 0.108f;   // real golf hole radius ~10.8cm
    public float maxEntrySpeed = 4f;    // ball must be slower than this to hole out

    private void Start()
    {
        BuildVisuals();
        BuildTrigger();
    }

    private void BuildVisuals()
    {
        var litShader = Shader.Find("Universal Render Pipeline/Lit");

        // --- Hole disc ---
        var holeVisual = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        holeVisual.name = "HoleVisual";
        holeVisual.transform.SetParent(transform, false);
        holeVisual.transform.localScale = new Vector3(holeRadius * 2f, 0.005f, holeRadius * 2f);
        var holeMat = new Material(litShader) { color = Color.black };
        holeVisual.GetComponent<Renderer>().material = holeMat;
        Destroy(holeVisual.GetComponent<Collider>());

        // --- Flag pole ---
        var pole = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        pole.name = "FlagPole";
        pole.transform.SetParent(transform, false);
        pole.transform.localScale = new Vector3(0.015f, 0.5f, 0.015f);
        pole.transform.localPosition = new Vector3(0f, 0.25f, 0f);
        var poleMat = new Material(litShader) { color = Color.grey };
        pole.GetComponent<Renderer>().material = poleMat;
        Destroy(pole.GetComponent<Collider>());

        // --- Flag ---
        var flag = GameObject.CreatePrimitive(PrimitiveType.Cube);
        flag.name = "Flag";
        flag.transform.SetParent(transform, false);
        flag.transform.localScale = new Vector3(0.2f, 0.12f, 0.01f);
        flag.transform.localPosition = new Vector3(0.12f, 0.47f, 0f);
        var flagMat = new Material(litShader) { color = Color.red };
        flag.GetComponent<Renderer>().material = flagMat;
        Destroy(flag.GetComponent<Collider>());
    }

    private void BuildTrigger()
    {
        var sc = gameObject.AddComponent<SphereCollider>();
        sc.radius = holeRadius * 0.8f;
        sc.isTrigger = true;
    }

    private void OnTriggerEnter(Collider other)
    {
        bool isBall = other.name == "GolfBall_Active" || other.CompareTag("GolfBall");
        if (!isBall) return;

        var rb = other.attachedRigidbody;
        if (rb == null || rb.linearVelocity.magnitude >= maxEntrySpeed) return;

        Debug.Log("[GolfHole] Ball holed out!");

        // Centre ball in hole and freeze it
        rb.isKinematic = true;
        other.transform.position = transform.position;

        GolfGameManager.Instance?.HoleComplete();
    }

    private void OnDrawGizmos()
    {
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, holeRadius);

        // Wire disc approximation in the XZ plane
        int segments = 32;
        float step = 2f * Mathf.PI / segments;
        for (int i = 0; i < segments; i++)
        {
            float a0 = i * step, a1 = (i + 1) * step;
            var p0 = transform.position + new Vector3(Mathf.Cos(a0), 0f, Mathf.Sin(a0)) * holeRadius;
            var p1 = transform.position + new Vector3(Mathf.Cos(a1), 0f, Mathf.Sin(a1)) * holeRadius;
            Gizmos.DrawLine(p0, p1);
        }

#if UNITY_EDITOR
        UnityEditor.Handles.Label(transform.position + Vector3.up * (holeRadius + 0.05f), "Hole");
#endif
    }
}
