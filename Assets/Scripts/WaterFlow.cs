using UnityEngine;

[RequireComponent(typeof(Renderer))]
public class WaterFlow : MonoBehaviour
{
    public float scrollSpeedX = 0.04f;
    public float scrollSpeedZ = 0.015f;

    private Material mat;

    void Start()
    {
        mat = GetComponent<Renderer>().material; // per-instance copy
    }

    void Update()
    {
        if (mat == null) return;
        float t = Time.time;

        // Scroll the ripple texture to simulate flowing water
        mat.SetTextureOffset("_MainTex", new Vector2(scrollSpeedX * t, scrollSpeedZ * t));

        // Subtle emission shimmer (each water body gets a unique phase)
        float shimmer = 0.5f + 0.5f * Mathf.Sin(t * 1.8f + GetInstanceID() * 0.05f);
        mat.SetColor("_EmissionColor",
            new Color(0f, 0.02f + shimmer * 0.04f, 0.14f + shimmer * 0.10f));
    }
}
