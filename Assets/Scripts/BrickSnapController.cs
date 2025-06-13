using System;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using System.Collections.Generic;
using System.Linq;

public class BrickSnapController : MonoBehaviour
{
    public List<SnapPoint> snapStuds;   // Top studs
    public List<SnapPoint> snapRecepts; // Bottom recepts

    private Rigidbody _rb;
    private bool _isGrabbed = false;

    public FixedJoint _joint;

    private float _snapDistanceThreshold = 0.04f;
    private List<Collider> _snapPointColliders = new List<Collider>();

    void Awake()
    {
        _rb = GetComponent<Rigidbody>();
        // Find snap points based on naming convention
        
        Collider[] allComponents = gameObject.GetComponentsInChildren<Collider>()
            .Where(c => c.gameObject != this.gameObject)
            .ToArray();

        // Filter components that are on GameObjects with tag "XXX"
        foreach (var comp in allComponents)
        {
            this._snapPointColliders.Add(comp);
            foreach (var comp2 in comp.GetComponentsInChildren<SnapPoint>())
            {
                //Debug.Log(gameObject.name + " SnapStud/Recept found: " + comp2);
                if (comp.CompareTag("SnapPointTop"))
                {
                    this.snapStuds.Add(comp2);
                }
                if (comp.CompareTag("SnapPointBottom"))
                {
                    this.snapRecepts.Add(comp2);
                }
            }
        }
        
        Debug.Log(gameObject.name + " AllComponents found: " + string.Join(", ", allComponents.Select(sp => sp.name)));
        Debug.Log(gameObject.name + " SnapStuds found: " + string.Join(", ", this.snapStuds.Select(sp => sp.name)));
        Debug.Log(gameObject.name + " SnapRecepts found: " + string.Join(", ", this.snapRecepts.Select(sp => sp.name)));
        Debug.Log(gameObject.name + " SnapPointColliders found: " + string.Join(", ", this._snapPointColliders.Select(sp => sp.name)));
    }

    // Called by XR interaction events
    public void OnSelectEntered(SelectEnterEventArgs args)
    {
        if (_joint != null)
        {
            Destroy(_joint);
            Debug.Log(gameObject.name + ": joint destroyed!");
            //gameObject.GetComponent<Collider>().enabled = true;
            foreach (var snapZone in _snapPointColliders)
            {
                snapZone.GetComponent<Collider>().enabled = true;
            }
        }
        _isGrabbed = true;
        //gameObject.GetComponent<Collider>().enabled = false;
        foreach (var snapZone in _snapPointColliders)
        {
            snapZone.GetComponent<Collider>().enabled = false;
        }
    }

    public void OnSelectExited(SelectExitEventArgs args)
    {
        _isGrabbed = false;
        if (!TrySnap())
        {
            //gameObject.GetComponent<Collider>().enabled = true;
            foreach (var snapZone in _snapPointColliders)
            {
                snapZone.GetComponent<Collider>().enabled = true;
            }
        }
    }

    // void Update()
    // {
    //     if (_isGrabbed)
    //     {
    //         UpdateActiveSnapPoints();
    //     }
    // }

    void UpdateActiveSnapPoints()
    {
        // Disable all snap colliders first
        foreach (var col in _snapPointColliders)
            col.enabled = false;

        // Find closest snap point within threshold
        float minDist = float.MaxValue;
        Collider closestCollider = null;
        foreach (var sp in snapStuds)
        {
            float dist = sp.GetDistanceTo(transform.position);
            if (dist < minDist && dist < _snapDistanceThreshold)
            {
                minDist = dist;
                closestCollider = sp.GetComponent<Collider>();
            }
        }
        if (closestCollider != null)
            closestCollider.enabled = true;
    }

    internal Boolean TrySnap()
    {
        BrickSnapController[] otherBlocks = GameObject.FindObjectsByType<BrickSnapController>(FindObjectsSortMode.None)            
            .Where(c => c.gameObject != this.gameObject)
            .ToArray();

        var candidatePairs = new List<(SnapPoint recept, SnapPoint stud, float distance)>();

        Debug.Log(gameObject.name + " other Blocks found: " + string.Join(", ", otherBlocks.Select(sp => sp.name)));
        foreach (var block in otherBlocks)
        {
            foreach (var recept in snapRecepts)
            {
                foreach (var otherStud in block.snapStuds)
                {
                    float dist = Vector3.Distance(recept.transform.position, otherStud.transform.position);
                    if (dist < _snapDistanceThreshold)
                    {
                        candidatePairs.Add((recept, otherStud, dist));
                        Debug.Log(gameObject.name + " candidate Pair found (recept + other stud): " + recept.name + " -> " + block.name + ": " + otherStud.name + " distance: " + dist);
                    }
                }
            }
            foreach (var stud in snapStuds)
            {
                foreach (var otherRecept in block.snapRecepts)
                {
                    float dist = Vector3.Distance(stud.transform.position, otherRecept.transform.position);
                    if (dist < _snapDistanceThreshold)
                    {
                        candidatePairs.Add((stud, otherRecept, dist));
                        Debug.Log(gameObject.name + " candidate Pair found (stud + other recept): " + stud.name + " -> " + block.name + ": " + otherRecept.name + " distance: " + dist);
                    }
                }
            }
        }

        if (candidatePairs.Any())
        {
            var bestPair = candidatePairs.OrderBy(p => p.distance).First();
            Debug.Log(gameObject.name + " BEST Pair found: " + bestPair.recept.name + " -> " + bestPair.stud.name + " distance: " + bestPair.distance);
            AttachBricks(bestPair.recept, bestPair.stud);
            return true;
        }
        return false;
    }
    
    void AttachBricks(SnapPoint thisBlock, SnapPoint otherBlock)
    {
        Debug.Log(gameObject.name + " this Block position before snap: " +  thisBlock.transform.position);
        Debug.Log(gameObject.name + " other Block position before snap: " +  otherBlock.transform.position);

        // Add a joint for stability (optional)
        _joint = gameObject.AddComponent<FixedJoint>();
        _joint.connectedBody = otherBlock.GetComponentInParent<Rigidbody>();
        _joint.breakForce = Mathf.Infinity;
        _joint.breakTorque = Mathf.Infinity;
        _joint.anchor = Vector3.zero;
        _joint.connectedAnchor = Vector3.zero;
        gameObject.GetComponent<Collider>().enabled = true;
        
        Debug.Log(gameObject.name + ": joint created.");
        
        Collider colA = thisBlock.GetComponentInParent<Collider>();
        Collider colB = otherBlock.GetComponentInParent<Collider>();

       if (colA == null || colB == null)
        {
            Debug.LogError("Both objects need colliders to align.");
            return;
        }

        Bounds boundsA = colA.bounds;
        Bounds boundsB = colB.bounds;

        // Distance between min/max faces on each axis
        float[] distances = new float[6];

        // X axis
        distances[0] = Mathf.Abs(boundsA.max.x - boundsB.min.x); // A right - B left
        distances[1] = Mathf.Abs(boundsA.min.x - boundsB.max.x); // A left - B right

        // Y axis
        distances[2] = Mathf.Abs(boundsA.max.y - boundsB.min.y); // A top - B bottom
        distances[3] = Mathf.Abs(boundsA.min.y - boundsB.max.y); // A bottom - B top

        // Z axis
        distances[4] = Mathf.Abs(boundsA.max.z - boundsB.min.z); // A front - B back
        distances[5] = Mathf.Abs(boundsA.min.z - boundsB.max.z); // A back - B front

        // Find the smallest distance index
        int minIndex = 0;
        float minDistance = distances[0];
        for (int i = 1; i < distances.Length; i++)
        {
            if (distances[i] < minDistance)
            {
                minDistance = distances[i];
                minIndex = i;
            }
        }

        // We'll move objB to align the corresponding faces depending on minIndex
        Vector3 move = Vector3.zero;

        switch (minIndex)
        {
            case 0:
                // Align B's left to A's right (along X+)
                move.x = boundsA.max.x - boundsB.min.x;
                break;
            case 1:
                // Align B's right to A's left (along X-)
                move.x = boundsA.min.x - boundsB.max.x;
                break;
            case 2:
                // Align B's bottom to A's top (along Y+)
                move.y = boundsA.max.y - boundsB.min.y;
                break;
            case 3:
                // Align B's top to A's bottom (along Y-)
                move.y = boundsA.min.y - boundsB.max.y;
                break;
            case 4:
                // Align B's back to A's front (along Z+)
                move.z = boundsA.max.z - boundsB.min.z;
                break;
            case 5:
                // Align B's front to A's back (along Z-)
                move.z = boundsA.min.z - boundsB.max.z;
                break;
        }

        // Apply position offset to objB
        otherBlock.transform.position += move;
        
        //Vector3 targetPos = thisBlock.transform.position;
        //Vector3 offset = targetPos - otherBlock.transform.position;
        //Debug.Log(gameObject.name + " Offset: " + offset);
        //transform.position -= offset;
        
        Debug.Log(gameObject.name + " this Block position AFTER snap: " +  thisBlock.transform.position);
        Debug.Log(gameObject.name + " other Block position AFTER snap: " +  otherBlock.transform.position);

    }
}