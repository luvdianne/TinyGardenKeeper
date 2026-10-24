using UnityEngine;
using UnityEngine.InputSystem;
using System.Collections;

public class Seed : MonoBehaviour
{
    private Camera cam;
    private bool isPlanting = false;

    [Header("Planting Settings")]
    public Transform potTransform; 
    public Vector3 customSpawnPosition = new Vector3(24f, 83f, 7f); 
    public float plantSpeed = 0.4f; 

    void Start()
    {
        cam = Camera.main;
        if (cam == null) cam = Camera.current;
    }

    void Update()
    {
        if (isPlanting || Mouse.current == null) return;

        if (Mouse.current.leftButton.wasPressedThisFrame)
        {
            Vector2 mousePos = Mouse.current.position.ReadValue();
            Ray ray = cam.ScreenPointToRay(mousePos);
            RaycastHit hit;

            if (Physics.Raycast(ray, out hit))
            {
                if (hit.transform == transform || hit.transform.IsChildOf(transform))
                {
                    if (potTransform != null)
                    {
                        StartCoroutine(PlantRoutine());
                    }
                    else
                    {
                        Debug.LogWarning("Pot Transform (Soil) is not assigned in the Inspector!");
                    }
                }
            }
        }
    }

    IEnumerator PlantRoutine()
    {
        isPlanting = true;
        Transform seedRoot = transform.root;

        Vector3 finalSoilPos = potTransform.position;

        seedRoot.position = customSpawnPosition;

        yield return new WaitForSeconds(0.3f);

        float elapsedTime = 0f;
        while (elapsedTime < 1f)
        {
            seedRoot.position = Vector3.Lerp(customSpawnPosition, finalSoilPos, elapsedTime);
            elapsedTime += Time.deltaTime * plantSpeed;
            yield return null;
        }

        seedRoot.position = finalSoilPos;
        Debug.Log("Seed planted successfully!");

        Destroy(seedRoot.gameObject);
    }
}