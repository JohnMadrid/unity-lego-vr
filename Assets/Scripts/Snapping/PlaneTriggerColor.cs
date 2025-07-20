using UnityEngine;

public class PlaneTriggerColor : MonoBehaviour
{
    [Tooltip("Name of the foot cube this plane responds to")]
    public string cubeName = "LeftFoot"; // Or "Cube_RightFoot"

    public Color defaultColor = Color.grey;
    public Color triggeredColor = Color.green;

    private Renderer rend;
    private int isTouching = 0; // allows for multi-trigger safety

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
            // --- Set static flag ---
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
                rend.material.color = defaultColor;
                isTouching = 0;
                // --- Clear static flag ---
                SetFlag(false);
            }
        }
    }

    // --- Helper to set appropriate flag ---
    private void SetFlag(bool value)
    {
        if (cubeName == "LeftFoot")
            LeftFootOnPlane = value;
        else if (cubeName == "RightFoot")
            RightFootOnPlane = value;
    }
}