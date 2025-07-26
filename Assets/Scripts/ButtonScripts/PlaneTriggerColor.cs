using UnityEngine;

/// <summary>
/// Manages the color change of a plane when a specific foot cube enters or exits its trigger
public class PlaneTriggerColor : MonoBehaviour
{
    [Tooltip("Name of the foot cube this plane responds to")]
    public string cubeName = "LeftFoot"; // "RightFoot" for the right side

    public Color defaultColor = Color.grey;
    public Color triggeredColor = Color.green;

    private Renderer rend;
    private int isTouching = 0; // Tracks how many colliders are touching

    public static bool LeftFootOnPlane = false;
    public static bool RightFootOnPlane = false;

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
            SetFlag(true);
        }
    }

    void OnTriggerExit(Collider other)
    {
        if (other.gameObject.name == cubeName)
        {
            isTouching--;
            if (isTouching <= 0)
            {
                isTouching = 0;
                rend.material.color = defaultColor;
                SetFlag(false);
            }
        }
    }

    private void SetFlag(bool value)
    {
        if (cubeName == "LeftFoot")
            LeftFootOnPlane = value;
        else if (cubeName == "RightFoot")
            RightFootOnPlane = value;
    }
}
