using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using System.Collections.Generic;
using System.Linq;

public class BrickSnapController : MonoBehaviour
{
    public List<SnapPoint> snapStuds;   // Top studs
    public List<SnapPoint> snapRecepts; // Bottom recepts

    private Rigidbody rb;
    private bool isGrabbed = false;

    public float snapDistanceThreshold = 0.2f;
    private List<Collider> snapPointColliders = new List<Collider>();

    void Awake()
    {
        rb = GetComponent<Rigidbody>();
        // Find snap points based on naming conventions
        snapStuds = new List<SnapPoint>();
        for (int i = 1; i <= 4; i++)
        {
            var spObj = transform.Find($"SnapStud{i}");
            if (spObj != null)
            {
                var sp = spObj.GetComponentInChildren<SnapPoint>();
                if (sp != null && !snapStuds.Contains(sp))
                    snapStuds.Add(sp);
            }
        }
        snapRecepts = new List<SnapPoint>();
        for (int i = 1; i <= 4; i++)
        {
            var rpObj = transform.Find($"SnapRecept{i}");
            if (rpObj != null)
            {
                var sp = rpObj.GetComponentInChildren<SnapPoint>();
                if (sp != null && !snapRecepts.Contains(sp))
                    snapRecepts.Add(sp);
            }
        }
        foreach (var sp in snapStuds.Concat(snapRecepts))
        {
            var collider = sp.GetComponent<Collider>();
            if (collider != null)
                snapPointColliders.Add(collider);
        }
    }

    // Called by XR interaction events
    public void OnSelectEntered(SelectEnterEventArgs args)
    {
        isGrabbed = true;
    }

    public void OnSelectExited(SelectExitEventArgs args)
    {
        isGrabbed = false;
        TrySnap();
    }

    void Update()
    {
        if (isGrabbed)
        {
            UpdateActiveSnapPoints();
        }
    }

    void UpdateActiveSnapPoints()
    {
        // Disable all snap colliders first
        foreach (var col in snapPointColliders)
            col.enabled = false;

        // Find closest snap point within threshold
        float minDist = float.MaxValue;
        Collider closestCollider = null;
        foreach (var sp in snapStuds)
        {
            float dist = sp.GetDistanceTo(transform.position);
            if (dist < minDist && dist < snapDistanceThreshold)
            {
                minDist = dist;
                closestCollider = sp.GetComponent<Collider>();
            }
        }
        if (closestCollider != null)
            closestCollider.enabled = true;
    }

    void TrySnap()
    {
        // Find the candidate snap pairs: recept and stud within range
        var candidatePairs = new List<(SnapPoint recept, SnapPoint stud, float distance)>();

        foreach (var recept in snapRecepts)
        {
            foreach (var stud in snapStuds)
            {
                float dist = Vector3.Distance(recept.snapTransform.position, stud.transform.position);
                if (dist < snapDistanceThreshold)
                {
                    candidatePairs.Add((recept, stud, dist));
                }
            }
        }

        if (candidatePairs.Any())
        {
            var bestPair = candidatePairs.OrderBy(p => p.distance).First();
            AttachBricks(bestPair.recept, bestPair.stud);
        }
    }

    void AttachBricks(SnapPoint recept, SnapPoint stud)
    {
        Vector3 targetPos = recept.snapTransform.position;
        Vector3 offset = targetPos - stud.transform.position;
        transform.position += offset;

        // Optional: set rotation explicitly (assuming zero offset)
        transform.rotation = Quaternion.identity;

        // Add a joint for stability (optional)
        var joint = gameObject.AddComponent<FixedJoint>();
        var connectedRb = recept.GetComponentInParent<Rigidbody>();
        if (connectedRb != null)
            joint.connectedBody = connectedRb;
    }
}