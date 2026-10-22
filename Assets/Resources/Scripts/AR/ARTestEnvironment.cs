using UnityEngine;

namespace TinyGardenKeeper.AR
{
    /// <summary>
    /// Attached to the test room environment (e.g. RealisticRoom).
    /// Keeps the 3D room active in the Unity Editor for visual testing and physics raycasting.
    /// Automatically disables the visual mesh renderers on mobile AR builds so real-world camera video is visible.
    /// </summary>
    public class ARTestEnvironment : MonoBehaviour
    {
        [Tooltip("If true, room visual meshes are hidden at runtime on real mobile AR devices.")]
        [SerializeField] private bool hideOnDevice = true;

        private void Awake()
        {
            if (hideOnDevice && !Application.isEditor)
            {
                // Disable visual meshes on mobile so the AR camera background shows the real physical room
                var renderers = GetComponentsInChildren<Renderer>(true);
                foreach (var rend in renderers)
                {
                    rend.enabled = false;
                }
            }
        }
    }
}
