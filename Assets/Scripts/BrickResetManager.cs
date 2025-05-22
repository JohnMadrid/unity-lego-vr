using UnityEngine;

public class BrickReset : MonoBehaviour
{
    private Vector3 initialPosition;
    private Quaternion initialRotation; // Store initial rotation

    void Start()
    {
        // Save the initial position and rotation when the game starts
        initialPosition = transform.position;
        initialRotation = transform.rotation;
    }

    void OnCollisionEnter(Collision collision)
    {
        // Check if the brick collides with the floor
        if (collision.gameObject.CompareTag("Floor"))
        {
            ResetBrick();
        }
    }

    void ResetBrick()
    {
        // Move the brick back to its original position and rotation
        transform.position = initialPosition;
        transform.rotation = initialRotation;

        // Stop movement and rotation
        GetComponent<Rigidbody>().linearVelocity = Vector3.zero;
        GetComponent<Rigidbody>().angularVelocity = Vector3.zero;
    }
}