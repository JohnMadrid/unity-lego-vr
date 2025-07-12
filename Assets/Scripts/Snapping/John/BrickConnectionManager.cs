using UnityEngine;
using System.Collections.Generic;
using UnityEngine.XR.Interaction.Toolkit.Interactors;

public class BrickConnectionManager
{
    private readonly BrickBehavior brick;
    
    // ========================================
    // PRIVATE FIELDS - CONNECTION GRAPH
    // ========================================
    // Keeps track of which bricks this one is directly connected to.
    private List<BrickBehavior> m_ConnectedNeighbors = new List<BrickBehavior>();

    // Stores the joint connecting this brick to another.
    private FixedJoint m_Joint; 

    // A reference to the single "master" brick that controls the entire group's Rigidbody.
    // If this brick is its own master, this will point to itself.
    public BrickBehavior m_MasterBrick { get; set; }
    
    // Track the original master of the group (before any grabs)
    public BrickBehavior m_OriginalMaster { get; set; }

    public List<BrickBehavior> ConnectedNeighbors => m_ConnectedNeighbors;
    public FixedJoint Joint 
    { 
        get => m_Joint; 
        set => m_Joint = value; 
    }
    public BrickBehavior MasterBrick => m_MasterBrick;
    public BrickBehavior OriginalMaster => m_OriginalMaster;

    public BrickConnectionManager(BrickBehavior brick)
    {
        this.brick = brick;
        InitializeConnectionGraph();
    }

    private void InitializeConnectionGraph()
    {
        // Initialize connection graph
        m_MasterBrick = brick;
        m_OriginalMaster = brick; // Initially, each brick is its own original master
        Debug.Log($"[{brick.name}] InitializeConnectionGraph() - Initialized as own master brick");
    }

    public void OnGrabStarted(IXRSelectInteractor interactor)
    {
        Debug.Log($"[{brick.name}] OnGrabStarted() - Handling grab start in connection manager");
        
        // Find all bricks in the connected group
        List<BrickBehavior> groupBricks = new List<BrickBehavior>();
        BrickGroupUtils.FindAllConnectedInGroup(brick, groupBricks, brick.name);
        Debug.Log($"[{brick.name}] OnGrabStarted() - Found {groupBricks.Count} bricks in group");
        
        // Store the original master before any changes
        BrickBehavior originalMaster = brick.MasterBrick;
        Debug.Log($"[{brick.name}] OnGrabStarted() - Original master: {originalMaster?.name ?? "null"}");
        
        // Update the master for all bricks in the group to the grabbed brick
        UpdateMaster(brick);
        
        Debug.Log($"[{brick.name}] OnGrabStarted() - Grab start handling complete");
    }

    public void OnGrabReleased()
    {
        Debug.Log($"[{brick.name}] OnGrabReleased() - Handling grab release in connection manager");
        
        // Find all bricks in the connected group
        List<BrickBehavior> groupBricks = new List<BrickBehavior>();
        BrickGroupUtils.FindAllConnectedInGroup(brick, groupBricks, brick.name);
        Debug.Log($"[{brick.name}] OnGrabReleased() - Found {groupBricks.Count} bricks in group");
        
        // Restore the original master
        BrickBehavior originalMaster = brick.OriginalMaster;
        Debug.Log($"[{brick.name}] OnGrabReleased() - Restoring original master: {originalMaster?.name ?? "null"}");
        
        // DO NOT change physics here - let UpdateMaster handle it properly
        UpdateMaster(originalMaster);
        
        Debug.Log($"[{brick.name}] OnGrabReleased() - Grab release handling complete");
    }

    // Coroutine to stabilize the group after release
    private System.Collections.IEnumerator StabilizeGroupAfterRelease(List<BrickBehavior> groupBricks)
    {
        Debug.Log($"[{brick.name}] StabilizeGroupAfterRelease() - Starting group stabilization");
        
        // Wait a frame to let physics settle
        yield return null;
        
        // Ensure all bricks in the group have proper physics state
        foreach (var groupBrick in groupBricks)
        {
            if (!groupBrick.GetComponent<UnityEngine.XR.Interaction.Toolkit.Interactables.XRGrabInteractable>().isSelected)
            {
                // Ensure all bricks are dynamic with gravity
                groupBrick.GetComponent<Rigidbody>().isKinematic = false;
                groupBrick.GetComponent<Rigidbody>().useGravity = true;
                
                // Clear any residual velocity to prevent weird behavior
                groupBrick.GetComponent<Rigidbody>().linearVelocity = Vector3.zero;
                groupBrick.GetComponent<Rigidbody>().angularVelocity = Vector3.zero;
                
                Debug.Log($"[{brick.name}] StabilizeGroupAfterRelease() - Stabilized {groupBrick.name}: isKinematic=false, useGravity=true, cleared velocities");
            }
        }
        
        Debug.Log($"[{brick.name}] StabilizeGroupAfterRelease() - Group stabilization complete");
    }

    public void UpdateMaster(BrickBehavior newMaster)
    {
        Debug.Log($"[{brick.name}] UpdateMaster() - Updating group master to: {newMaster.name}");
        
        List<BrickBehavior> groupToUpdate = new List<BrickBehavior>();
        Debug.Log($"[{brick.name}] UpdateMaster() - Created group members list");
        
        BrickGroupUtils.FindAllConnectedInGroup(brick, groupToUpdate, brick.name);
        Debug.Log($"[{brick.name}] UpdateMaster() - Found {groupToUpdate.Count} bricks in the group");

        foreach (var groupBrick in groupToUpdate)
        {
            Debug.Log($"[{brick.name}] UpdateMaster() - Processing brick: {groupBrick.name}");
            // Don't call groupBrick.UpdateMaster() as it would call this method again
            groupBrick.ConnectionManager.m_MasterBrick = newMaster;
            Debug.Log($"[{brick.name}] UpdateMaster() - Set {groupBrick.name}'s master to {newMaster.name}");
            
            // Update the original master for the entire group
            // The original master should be the one that was originally its own master
            if (groupBrick.OriginalMaster == groupBrick) // This brick was originally its own master
            {
                // This brick becomes the original master for the entire group
                foreach (var groupBrickInGroup in groupToUpdate)
                {
                    groupBrickInGroup.ConnectionManager.m_OriginalMaster = groupBrick;
                    groupBrickInGroup.ConnectionManager.m_MasterBrick = groupBrick;
                }
                break; // Found the original master, no need to continue
            }
            
            // Set physics properties based on whether this brick is the master
            // BUT only if the brick is not currently being grabbed
            bool isMaster = (groupBrick == newMaster);
            bool isGrabbed = groupBrick.GetComponent<UnityEngine.XR.Interaction.Toolkit.Interactables.XRGrabInteractable>().isSelected;
            
            if (!isGrabbed)
            {
                // Master brick should be dynamic with gravity
                groupBrick.GetComponent<Rigidbody>().isKinematic = false;
                groupBrick.GetComponent<Rigidbody>().useGravity = true;
            }
        }
    }

    public void UnsnapFrom(BrickBehavior otherBrick)
    {
        Debug.Log($"[{brick.name}] UnsnapFrom() - Starting unsnap from {otherBrick.name}");

        // Check for joint on this brick first
        if (m_Joint != null && m_Joint.connectedBody == otherBrick.GetComponent<Rigidbody>())
        {
            Debug.Log($"[{brick.name}] UnsnapFrom() - Found joint on this brick. Destroying it.");
            Object.DestroyImmediate(m_Joint);
            m_Joint = null;
        }
        // Check for joint on the other brick
        else if (otherBrick.Joint != null && otherBrick.Joint.connectedBody == brick.GetComponent<Rigidbody>())
        {
            Debug.Log($"[{brick.name}] UnsnapFrom() - Found joint on other brick ({otherBrick.name}). Destroying it.");
            Object.DestroyImmediate(otherBrick.Joint);
            // The joint destruction will handle clearing the reference
        }
        // If neither brick has the joint, search for any FixedJoint components
        else
        {
            Debug.Log($"[{brick.name}] UnsnapFrom() - No tracked joint found, searching for FixedJoint components");
            
            FixedJoint[] joints = brick.GetComponents<FixedJoint>();
            foreach (var joint in joints)
            {
                if (joint.connectedBody == otherBrick.GetComponent<Rigidbody>())
                {
                    Debug.Log($"[{brick.name}] UnsnapFrom() - Found joint via component search. Destroying it.");
                    Object.DestroyImmediate(joint);
                    m_Joint = null;
                    break;
                }
            }
            
            // Also check the other brick
            FixedJoint[] otherJoints = otherBrick.GetComponents<FixedJoint>();
            foreach (var joint in otherJoints)
            {
                if (joint.connectedBody == brick.GetComponent<Rigidbody>())
                {
                    Debug.Log($"[{brick.name}] UnsnapFrom() - Found joint on other brick via component search. Destroying it.");
                    Object.DestroyImmediate(joint);
                    // The joint destruction will handle clearing the reference
                    break;
                }
            }
        }
        
        m_ConnectedNeighbors.Remove(otherBrick);
        Debug.Log($"[{brick.name}] UnsnapFrom() - Removed {otherBrick.name} from this brick's neighbors");
        
        otherBrick.RemoveNeighbor(brick);
        Debug.Log($"[{brick.name}] UnsnapFrom() - Removed this brick from {otherBrick.name}'s neighbors");

        // Restore physics for this brick ONLY if it's not currently grabbed
        if (!brick.GetComponent<UnityEngine.XR.Interaction.Toolkit.Interactables.XRGrabInteractable>().isSelected)
        {
            brick.GetComponent<Rigidbody>().isKinematic = false;
            brick.GetComponent<Rigidbody>().useGravity = true;
            
            // Restore original mass to prevent weight accumulation after separation
            brick.GetComponent<Rigidbody>().mass = 1.0f;
            
            Debug.Log($"[{brick.name}] UnsnapFrom() - Restored physics for this brick: isKinematic=false, useGravity=true, mass=1.0f");
        }
        else
        {
            Debug.Log($"[{brick.name}] UnsnapFrom() - Skipping physics change for this brick (currently grabbed by XRGrabInteractable)");
        }
        
        UpdateMaster(brick);
        Debug.Log($"[{brick.name}] UnsnapFrom() - Called UpdateMaster to set this brick as its own master");
        
        if (otherBrick.ConnectedNeighbors.Count == 0)
        {
            Debug.Log($"[{brick.name}] UnsnapFrom() - Other brick ({otherBrick.name}) now has no neighbors. It will become its own master.");
            otherBrick.UpdateMaster(otherBrick);
        }
        else
        {
            Debug.Log($"[{brick.name}] UnsnapFrom() - Other brick ({otherBrick.name}) still has neighbors. Updating its master to the master of its first remaining neighbor.");
            otherBrick.UpdateMaster(otherBrick.ConnectedNeighbors[0].MasterBrick);
        }
        
        Debug.Log($"[{brick.name}] UnsnapFrom() - Unsnap complete");
    }

    public void StrengthenGroupConnections()
    {
        Debug.Log($"[{brick.name}] StrengthenGroupConnections() - Strengthening all connections in group");
        
        // Find all bricks in the connected group
        List<BrickBehavior> groupBricks = new List<BrickBehavior>();
        BrickGroupUtils.FindAllConnectedInGroup(brick, groupBricks, brick.name);
        
        foreach (var groupBrick in groupBricks)
        {
            // Strengthen the rigidbody properties
            if (groupBrick.GetComponent<Rigidbody>() != null)
            {
                groupBrick.GetComponent<Rigidbody>().mass = 1.0f; // Normalize mass to prevent group weight accumulation
                groupBrick.GetComponent<Rigidbody>().linearDamping = 0.5f;
                groupBrick.GetComponent<Rigidbody>().angularDamping = 0.5f;
                
                // Ensure the brick is not kinematic (should be dynamic for proper physics)
                groupBrick.GetComponent<Rigidbody>().isKinematic = false;
                groupBrick.GetComponent<Rigidbody>().useGravity = true;
                
                Debug.Log($"[{brick.name}] StrengthenGroupConnections() - Strengthened {groupBrick.name}: mass={groupBrick.GetComponent<Rigidbody>().mass}, drag={groupBrick.GetComponent<Rigidbody>().linearDamping}, angularDrag={groupBrick.GetComponent<Rigidbody>().angularDamping}");
            }
            
            // Strengthen any joints on this brick
            FixedJoint[] joints = groupBrick.GetComponents<FixedJoint>();
            foreach (var joint in joints)
            {
                joint.breakForce = float.PositiveInfinity;
                joint.breakTorque = float.PositiveInfinity;
                joint.enableCollision = false;
                joint.enablePreprocessing = true;
                joint.anchor = Vector3.zero;
                joint.axis = Vector3.zero;
                
                Debug.Log($"[{brick.name}] StrengthenGroupConnections() - Strengthened joint on {groupBrick.name}");
            }
        }
        
        Debug.Log($"[{brick.name}] StrengthenGroupConnections() - Group strengthening complete");
    }

    public void RemoveNeighbor(BrickBehavior neighbor)
    {
        if (m_ConnectedNeighbors.Contains(neighbor))
        {
            m_ConnectedNeighbors.Remove(neighbor);
            Debug.Log($"[{brick.name}] RemoveNeighbor() - Removed {neighbor.name} from neighbors");
        }
    }

    public void Cleanup()
    {
        // Clean up any remaining joints
        if (m_Joint != null)
        {
            Debug.Log($"[{brick.name}] Cleanup() - Destroying tracked joint");
            Object.DestroyImmediate(m_Joint);
            m_Joint = null;
        }
        
        // Also check for any other FixedJoint components that might not be tracked
        FixedJoint[] joints = brick.GetComponents<FixedJoint>();
        foreach (var joint in joints)
        {
            Debug.Log($"[{brick.name}] Cleanup() - Destroying untracked joint: {joint.name}");
            Object.DestroyImmediate(joint);
        }
        
        // Clear references
        m_ConnectedNeighbors.Clear();
        m_MasterBrick = null;
        m_OriginalMaster = null;
    }

    public void SetJoint(FixedJoint joint)
    {
        Debug.Log($"[{brick.name}] SetJoint() - Setting joint: {joint}");
        m_Joint = joint;
    }
} 