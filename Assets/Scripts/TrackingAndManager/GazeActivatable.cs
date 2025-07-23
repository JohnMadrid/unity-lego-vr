using UnityEngine;

public class GazeActivatable : MonoBehaviour
{
    public float maxGazeDistance = 10f;
    public LayerMask gazeLayerMask;
    private Renderer objectRenderer;
    private bool isHovering = false;

    void Start()
    {
        objectRenderer = GetComponent<Renderer>();
        if (objectRenderer != null) objectRenderer.enabled = false;
    }

    void Update()
    {
        if (GazeManager.Instance == null) return;

        Vector3 origin = GazeManager.Instance.gazeOrigin;
        Vector3 direction = GazeManager.Instance.gazeDirection;

        if (Physics.Raycast(origin, direction, out RaycastHit hit, maxGazeDistance, gazeLayerMask))
        {
            if (hit.transform == transform)
            {
                if (!isHovering)
                {
                    Debug.Log("Hover Entered: " + gameObject.name);
                    objectRenderer.enabled = true;
                    isHovering = true;
                }
                return;
            }
        }

        // Hover Exited
        if (isHovering)
        {
            Debug.Log("Hover Exited: " + gameObject.name);
            objectRenderer.enabled = false;
            isHovering = false;
        }
    }
}
