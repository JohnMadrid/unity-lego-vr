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
        Debug.Log($"[{brick.name}] OnGrabStarted() - Handling grab in connection manager");
        
        if (brick.ConnectedNeighbors.Count > 0)
        {
            Debug.Log($"[{brick.name}] OnGrabStarted() - Brick is part of connected group with {brick.ConnectedNeighbors.Count} neighbors");
            
            // Find all connected bricks in the group
            List<BrickBehavior> groupBricks = new List<BrickBehavior>();
            FindAllConnected(brick, groupBricks);
            Debug.Log($"[{brick.name}] OnGrabStarted() - Found {groupBricks.Count} total bricks in group");
            
            // IMPORTANT: When grabbing a connected brick, maintain the existing group structure
            // Only change the master if this brick is not already the master
            if (m_MasterBrick != brick)
            {
                Debug.Log($"[{brick.name}] OnGrabStarted() - Making grabbed brick the new master of the group");
                UpdateMaster(brick);
            }
            else
            {
                Debug.Log($"[{brick.name}] OnGrabStarted() - Grabbed brick is already the master, maintaining group structure");
            }
            
            // IMPORTANT: For connected groups, we need to ensure the entire group moves as one
            // The grabbed brick will be controlled by XRGrabInteractable
            // Other bricks in the group should follow via FixedJoints
            // DO NOT change physics here - let UpdateMaster handle it properly
            
            // Ensure all bricks in the group have proper physics for group movement
            foreach (var groupBrick in groupBricks)
            {
                if (groupBrick != brick && !groupBrick.GetComponent<UnityEngine.XR.Interaction.Toolkit.Interactables.XRGrabInteractable>().isSelected)
                {
                    // Non-grabbed bricks in the group should be dynamic with gravity
                    // They will follow the grabbed brick via FixedJoints
                    groupBrick.GetComponent<Rigidbody>().isKinematic = false;
                    groupBrick.GetComponent<Rigidbody>().useGravity = true;
                    Debug.Log($"[{brick.name}] OnGrabStarted() - Set group brick {groupBrick.name} physics: isKinematic=false, useGravity=true");
                }
            }
        }
        else
        {
            Debug.Log($"[{brick.name}] OnGrabStarted() - Grabbed standalone snapped brick - XRGrabInteractable will handle physics");
            // DO NOT change physics - let XRGrabInteractable handle it
        }
    }

    public void OnGrabReleased()
    {
        Debug.Log($"[{brick.name}] OnGrabReleased() - Handling release in connection manager");
        
        if (brick.ConnectedNeighbors.Count > 0)
        {
            Debug.Log($"[{brick.name}] OnGrabReleased() - Brick is connected to {brick.ConnectedNeighbors.Count} neighbors");
            
            // Find all connected bricks in the group
            List<BrickBehavior> groupBricks = new List<BrickBehavior>();
            FindAllConnected(brick, groupBricks);
            Debug.Log($"[{brick.name}] OnGrabReleased() - Found {groupBricks.Count} total bricks in group");
            
            // IMPORTANT: When releasing a grabbed brick, maintain the group connection
            // Only change the master if this brick was not originally the master
            // Use the tracked original master
            BrickBehavior originalMaster = m_OriginalMaster;
            
            Debug.Log($"[{brick.name}] OnGrabReleased() - Restoring original master: {originalMaster.name}");
            
            // Update the master for the entire group to restore original structure
            UpdateMaster(originalMaster);
            Debug.Log($"[{brick.name}] OnGrabReleased() - Updated master for entire group");
            
            // Stabilize the group after release to prevent weird dynamic behavior
            brick.StartCoroutine(StabilizeGroupAfterRelease(groupBricks));
        }
        else
        {
            Debug.Log($"[{brick.name}] OnGrabReleased() - Brick is not connected to any neighbors");
            
            // Don't restore physics if the brick is currently snapping
            if (!brick.isSnapping)
            {
                // Restore normal physics for standalone brick
                brick.GetComponent<Rigidbody>().isKinematic = false;
                brick.GetComponent<Rigidbody>().useGravity = true;
                Debug.Log($"[{brick.name}] OnGrabReleased() - Set physics for standalone brick: isKinematic=false, useGravity=true");
            }
            else
            {
                Debug.Log($"[{brick.name}] OnGrabReleased() - Brick is currently snapping, deferring physics restoration");
            }
        }
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
        List<BrickBehavior> groupToUpdate = new List<BrickBehavior>();
        
        FindAllConnected(brick, groupToUpdate);

        foreach (var groupBrick in groupToUpdate)
        {
            // IMPORTANT: Directly update the master brick reference to avoid infinite recursion
            // Don't call groupBrick.UpdateMaster() as it would call this method again
            if (groupBrick.ConnectionManager != null)
            {
                groupBrick.ConnectionManager.m_MasterBrick = newMaster;
            }
            
            // Update the original master for the entire group
            // The original master should be the one that was originally its own master
            if (groupBrick.OriginalMaster == groupBrick) // This brick was originally its own master
            {
                // This brick becomes the original master for the entire group
                foreach (var groupBrickInGroup in groupToUpdate)
                {
                    if (groupBrickInGroup.ConnectionManager != null)
                    {
                        groupBrickInGroup.ConnectionManager.m_MasterBrick = groupBrick;
                    }
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

    private void FindAllConnected(BrickBehavior brick, List<BrickBehavior> visited)
    {
        if (brick == null || visited.Contains(brick))
        {
            return;
        }

        visited.Add(brick);
        
        foreach (var neighbor in brick.ConnectedNeighbors)
        {
            FindAllConnected(neighbor, visited);
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
            Debug.Log($"[{brick.name}] UnsnapFrom() - Restored physics for this brick: isKinematic=false, useGravity=true");
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
        FindAllConnected(brick, groupBricks);
        
        foreach (var groupBrick in groupBricks)
        {
            // Strengthen the rigidbody properties
            if (groupBrick.GetComponent<Rigidbody>() != null)
            {
                groupBrick.GetComponent<Rigidbody>().mass = Mathf.Max(groupBrick.GetComponent<Rigidbody>().mass, 1.0f);
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