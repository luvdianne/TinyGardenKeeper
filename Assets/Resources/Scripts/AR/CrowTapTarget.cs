using UnityEngine;

namespace TinyGardenKeeper.AR
{
    /// <summary>
    /// Attach to Crow GameObject/Collider to allow the player to tap or click the Crow to shoo it away.
    /// </summary>
    [RequireComponent(typeof(Collider))]
    public class CrowTapTarget : MonoBehaviour
    {
        [SerializeField] private CompanionVisitManager visitManager;

        private void Awake()
        {
            if (visitManager == null)
            {
                visitManager = GetComponentInParent<CompanionVisitManager>();
            }
            if (visitManager == null)
            {
                visitManager = FindAnyObjectByType<CompanionVisitManager>();
            }
        }

        private void OnMouseDown()
        {
            Shoo();
        }

        public void Shoo()
        {
            if (visitManager != null)
            {
                visitManager.ShooCrow();
            }
        }
    }
}
