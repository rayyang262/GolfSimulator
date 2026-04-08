using UnityEngine;

/// <summary>
/// Locks this object's local scale to whatever it was on Start.
/// Attach to IntroCanvas to prevent parent animations from affecting its scale.
/// </summary>
public class LockScale : MonoBehaviour
{
    private Vector3 lockedScale;

    void Start()
    {
        lockedScale = transform.localScale;
    }

    void LateUpdate()
    {
        transform.localScale = lockedScale;
    }
}
