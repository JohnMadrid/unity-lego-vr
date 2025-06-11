using System.Collections.Generic;
using UnityEngine;

public class LegoBrick : MonoBehaviour
{
    public List<Transform> topStuds = new List<Transform>();
    public List<Transform> bottomReceptacles = new List<Transform>();
    public List<Transform> frontStuds = new List<Transform>();
    public List<Transform> rightStuds = new List<Transform>();
    public List<Transform> backReceptacles = new List<Transform>();
    public List<Transform> leftReceptacles = new List<Transform>();

    public float snapThreshold = 0.05f;  // Max distance for snapping
    public float forceThreshold = 3.0f;  // Force required to remove
    public float removalImpulse = 2.0f;  // Impulse force upon detachment

    private Rigidbody rb;
    private FixedJoint snapJoint;

    void Awake()
    {
        rb = GetComponent<Rigidbody>();

        // Find connection points and categorize them based on naming convention
        Transform connectionParent = transform.Find("ConnectionPoints");
        if (connectionParent)
        {
            foreach (Transform child in connectionParent)
            {
                string name = child.name;

                if (name.Contains("Stud") && name.Contains("Top"))
                    topStuds.Add(child);
                else if (name.Contains("Rec") && name.Contains("Bottom"))
                    bottomReceptacles.Add(child);
                else if (name.Contains("Stud") && name.Contains("Front"))
                    frontStuds.Add(child);
                else if (name.Contains("Stud") && name.Contains("Right"))
                    rightStuds.Add(child);
                else if (name.Contains("Rec") && name.Contains("Back"))
                    backReceptacles.Add(child);
                else if (name.Contains("Rec") && name.Contains("Left"))
                    leftReceptacles.Add(child);
            }
        }
    }

    void FixedUpdate()
    {
        // Only check force when snapped
        if (snapJoint && rb.linearVelocity.magnitude > forceThreshold)
        {
            RemoveBrick();
        }
    }

    void TrySnap(LegoBrick targetBrick)
    {
        // Check snapping for **top-bottom**, **front-back**, and **left-right** pairs
        TrySnapBetweenGroups(targetBrick.topStuds, targetBrick.bottomReceptacles);
        TrySnapBetweenGroups(targetBrick.frontStuds, targetBrick.backReceptacles);
        TrySnapBetweenGroups(targetBrick.rightStuds, targetBrick.leftReceptacles);
    }

    void TrySnapBetweenGroups(List<Transform> studs, List<Transform> receptacles)
    {
        foreach (Transform movingStud in studs)
        {
            Transform closestReceptacle = FindClosestReceptacle(receptacles, movingStud.position);

            if (closestReceptacle != null && Vector3.Distance(movingStud.position, closestReceptacle.position) < snapThreshold)
            {
                SnapBrick(closestReceptacle, movingStud);
                return; // Stop once snap occurs
            }
        }
    }

    Transform FindClosestReceptacle(List<Transform> receptacles, Vector3 position)
    {
        float minDistance = Mathf.Infinity;
        Transform closest = null;

        foreach (Transform receptacle in receptacles)
        {
            float distance = Vector3.Distance(position, receptacle.position);
            if (distance < minDistance)
            {
                minDistance = distance;
                closest = receptacle;
            }
        }
        return closest;
    }

    void SnapBrick(Transform targetMarker, Transform movingMarker)
    {
        // Calculate correct vertical offset for stacking
        Vector3 newPosition = targetMarker.position;
        newPosition.y += movingMarker.localPosition.y; // Ensure vertical stacking

        // Set position & lock rotation to avoid misalignment
        transform.position = newPosition;
        transform.rotation = Quaternion.Euler(0, targetMarker.rotation.eulerAngles.y, 0); // Keep Y-axis rotation only

        // Create a FixedJoint to attach the brick
        snapJoint = gameObject.AddComponent<FixedJoint>();
        snapJoint.connectedBody = targetMarker.GetComponentInParent<Rigidbody>();

        rb.isKinematic = true; // Reduce physics computations when snapped
    }


    void RemoveBrick()
    {
        if (snapJoint)
        {
            Destroy(snapJoint);  // Remove snap joint
            rb.isKinematic = false;
            rb.useGravity = true; // Ensure the brick falls naturally

            // Apply force to make detachment feel natural
            rb.AddForce(Vector3.up * removalImpulse, ForceMode.Impulse);
        }
    }


    void OnTriggerEnter(Collider other)
    {
        LegoBrick otherBrick = other.GetComponent<LegoBrick>();
        if (otherBrick)
        {
            TrySnap(otherBrick); // Run snap logic **only when near** another brick
        }
    }
}
