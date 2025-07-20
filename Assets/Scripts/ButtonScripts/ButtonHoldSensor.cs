using UnityEngine;

public class ButtonHoldSensor : MonoBehaviour
{
    // True if this button is currently being held by the user
    public bool IsHeld { get; private set; } = false;

    // Reference to the object's Renderer so we can change its material color
    public Renderer visualRenderer;

    // Color when the button is not pressed
    public Color restingColor = Color.gray;

    // Color when the button is held down
    public Color holdingColor = Color.red;

    void Start()
    {
        // Set the initial color to the resting state when the game starts
        SetColor(restingColor);
    }

    // Called when the button is first grabbed/held (via XR event)
    public void OnPressStart()
    {
        IsHeld = true;
        SetColor(holdingColor);
        Debug.Log($"{gameObject.name} — OnPressStart triggered (IsHeld = {IsHeld})");
    }

    // Called when the button is released (via XR event)
    public void OnPressEnd()
    {
        IsHeld = false;
        SetColor(restingColor);
        Debug.Log($"{gameObject.name} — OnPressEnd triggered (IsHeld = {IsHeld})");
    }

    // Changes the color of the button's material
    public void SetColor(Color color)
    {
        if (visualRenderer != null)
            visualRenderer.material.color = color;
    }
}