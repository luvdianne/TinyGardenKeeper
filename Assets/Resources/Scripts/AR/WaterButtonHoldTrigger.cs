using UnityEngine;
using UnityEngine.EventSystems;

namespace TinyGardenKeeper.AR
{
    /// <summary>
    /// Bridges UI PointerDown, PointerUp, and PointerExit events to the ARWateringController
    /// for accurate 3-second hold detection.
    /// </summary>
    public class WaterButtonHoldTrigger : MonoBehaviour, IPointerDownHandler, IPointerUpHandler, IPointerExitHandler
    {
        [SerializeField] private ARWateringController wateringController;

        public void SetController(ARWateringController controller)
        {
            wateringController = controller;
        }

        public void OnPointerDown(PointerEventData eventData)
        {
            if (wateringController != null)
            {
                wateringController.OnPointerDown();
            }
        }

        public void OnPointerUp(PointerEventData eventData)
        {
            if (wateringController != null)
            {
                wateringController.OnPointerUp();
            }
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            if (wateringController != null)
            {
                wateringController.OnPointerExit();
            }
        }
    }
}
