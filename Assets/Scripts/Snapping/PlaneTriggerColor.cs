using UnityEngine;

public class PlaneTriggerColor : MonoBehaviour
{
    [Tooltip("Name of the foot cube this plane responds to")]
    public string cubeName = "Cube_LeftFoot"; // Or "Cube_RightFoot"

    public Color defaultColor = Color.grey;
    public Color triggeredColor = Color.green;

    private Renderer rend;
    private int isTouching = 0; // allows for multi-trigger safety

    void Start()
    {
        rend = GetComponent<Renderer>();
        rend.material.color = defaultColor;
    }

    void OnTriggerEnter(Collider other)
    {
        if (other.gameObject.name == cubeName)
        {
            isTouching++;
            rend.material.color = triggeredColor;
        }
    }

    void OnTriggerExit(Collider other)
    {
        if (other.gameObject.name == cubeName)
        {
            isTouching--;
            if (isTouching <= 0)
            {
                rend.material.color = defaultColor;
                isTouching = 0;
            }
        }
    }
}