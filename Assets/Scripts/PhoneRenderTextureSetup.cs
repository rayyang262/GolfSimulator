using UnityEngine;

/// <summary>
/// Creates a RenderTexture at runtime, assigns it to a UI camera,
/// and applies it to the phone screen mesh.
///
/// Setup:
/// 1. Create an empty GameObject called "PhoneUICamera", add a Camera component
/// 2. Set that camera's Culling Mask to "UI" only
/// 3. Set Clear Flags to "Solid Color", background black
/// 4. Set the camera Depth lower than main camera (e.g. -1)
/// 5. Drag PhoneUICamera into the "uiCamera" field
/// 6. Drag the phone screen MeshRenderer (the part of the phone model
///    that should display the UI) into the "phoneScreen" field
/// 7. Keep IntroCanvas as "Screen Space - Camera" and assign PhoneUICamera
///    as its Render Camera
/// </summary>
public class PhoneRenderTextureSetup : MonoBehaviour
{
    [Tooltip("Camera that renders the UI canvas")]
    public Camera uiCamera;

    [Tooltip("The MeshRenderer on the phone model's screen surface")]
    public MeshRenderer phoneScreen;

    [Header("Render Texture Settings")]
    public int textureWidth  = 512;
    public int textureHeight = 1024;

    private RenderTexture rt;

    void Start()
    {
        // Create the render texture
        rt = new RenderTexture(textureWidth, textureHeight, 16);
        rt.name = "PhoneScreenRT";

        // Assign it to the UI camera
        if (uiCamera != null)
        {
            uiCamera.targetTexture = rt;
        }

        // Apply it to the phone screen mesh
        if (phoneScreen != null)
        {
            Material mat = new Material(Shader.Find("Unlit/Texture"));
            mat.mainTexture = rt;
            phoneScreen.material = mat;
        }
    }

    void OnDestroy()
    {
        if (rt != null)
        {
            rt.Release();
            Destroy(rt);
        }
    }
}
