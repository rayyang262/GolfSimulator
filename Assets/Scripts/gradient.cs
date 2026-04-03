using UnityEngine;
using UnityEngine.UI;

[RequireComponent(typeof(Image))]
public class gradient : MonoBehaviour
{
    [Range(0.05f, 0.5f)]
    public float fadeHeight = 0.25f;

    [Range(0f, 1f)]
    public float edgeAlpha = 0.7f;

    private Material fadeMat;

    void Start()
    {
        var shader = Shader.Find("UI/EdgeFade");
        if (shader == null)
        {
            Debug.LogWarning("gradient: UI/EdgeFade shader not found.");
            return;
        }

        fadeMat = new Material(shader);
        fadeMat.SetFloat("_FadeTop", fadeHeight);
        fadeMat.SetFloat("_FadeBottom", fadeHeight);
        fadeMat.SetFloat("_EdgeAlpha", edgeAlpha);

        GetComponent<Image>().material = fadeMat;
    }

    void OnDestroy()
    {
        if (fadeMat != null)
            Destroy(fadeMat);
    }
}
