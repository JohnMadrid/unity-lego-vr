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
    private ConfigurableJoint m_Joint; 

    // A reference to the single "master" brick that controls the entire group's Rigidbody.
    // If this brick is its own master, this will point to itself.
    public BrickBehavior m_MasterBrick { get; set; }
    
    // Track the original master of the group (before any grabs)
    public BrickBehavior m_OriginalMaster { get; set; }

    public List<BrickBehavior> ConnectedNeighbors => m_ConnectedNeighbors;
    public ConfigurableJoint Joint 
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
        brick.LogDebug($"InitializeConnectionGraph() - Initialized as own master brick");
    }

    public void OnGrabStarted(IXRSelectInteractor interactor)
    {
        // Boards cannot be grabbed, so this should never be called for boards
        if (brick.IsBoard)
        {
            brick.LogWarning("OnGrabStarted() - WARNING: Attempted to grab a board in connection manager!");
            return;
        }

        brick.LogDebug($"OnGrabStarted() - Handling grab start in connection manager");
        
        // Find all bricks in the connected group (excluding boards)
        List<BrickBehavior> groupBricks = new List<BrickBehavior>();
        BrickGroupUtils.FindAllConnectedInGroup(brick, groupBricks, brick.name);
        brick.LogDebug($"OnGrabStarted() - Found {groupBricks.Count} bricks in group");
        
        // Store the original master before any changes
        BrickBehavior originalMaster = brick.MasterBrick;
        brick.LogDebug($"OnGrabStarted() - Original master: {originalMaster?.name ?? "null"}");
        
        // Update the master for all bricks in the group to the grabbed brick
        UpdateMaster(brick);
        
        brick.LogDebug($"OnGrabStarted() - Grab start handling complete");
    }

    public void OnGrabReleased()
    {
        // Boards cannot be grabbed, so this should never be called for boards
        if (brick.IsBoard)
        {
            brick.LogWarning("OnGrabReleased() - WARNING: Attempted to release a board in connection manager!");
            return;
        }

        brick.LogDebug($"OnGrabReleased() - Handling grab release in connection manager");
        
        // Find all bricks in the connected group
        List<BrickBehavior> groupBricks = new List<BrickBehavior>();
        BrickGroupUtils.FindAllConnectedInGroup(brick, groupBricks, brick.name);
        brick.LogDebug($"OnGrabReleased() - Found {groupBricks.Count} bricks in group");
        
        // Restore the original master
        BrickBehavior originalMaster = brick.OriginalMaster;
        brick.LogDebug($"OnGrabReleased() - Restoring original master: {originalMaster?.name ?? "null"}");
        
        // DO NOT change physics here - let UpdateMaster handle it properly
        UpdateMaster(originalMaster);
        
        brick.LogDebug($"OnGrabReleased() - Grab release handling complete");
    }

    // Coroutine to stabilize the group after release
    private System.Collections.IEnumerator StabilizeGroupAfterRelease(List<BrickBehavior> groupBricks)
    {
        brick.LogDebug($"StabilizeGroupAfterRelease() - Starting group stabilization");
        
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
                
                brick.LogDebug($"StabilizeGroupAfterRelease() - Stabilized {groupBrick.name}: isKinematic=false, useGravity=true, cleared velocities");
            }
        }
        
        brick.LogDebug($"StabilizeGroupAfterRelease() - Group stabilization complete");
    }

    public void UpdateMaster(BrickBehavior newMaster)
    {
        // Boards should not participate in group management
        if (brick.IsBoard)
        {
            brick.LogDebug($"UpdateMaster() - Skipping group management for board {brick.name}");
            return;
        }

        // Prevent recursive calls - if we're already updating to this master, skip
        if (m_MasterBrick == newMaster)
        {
            brick.LogDebug($"UpdateMaster() - Already master of {newMaster.name}, skipping update");
            return;
        }
        
        brick.LogDebug($"UpdateMaster() - Updating group master to: {newMaster.name}");
        
        List<BrickBehavior> groupToUpdate = new List<BrickBehavior>();
        brick.LogDebug($"UpdateMaster() - Created group members list");
        
        BrickGroupUtils.FindAllConnectedInGroup(brick, groupToUpdate, brick.name);
        brick.LogDebug($"UpdateMaster() - Found {groupToUpdate.Count} bricks in the group");

        foreach (var groupBrick in groupToUpdate)
        {
            brick.LogDebug($"UpdateMaster() - DEBUG: Processing brick: {groupBrick.name}", false);
            // Don't call groupBrick.UpdateMaster() as it would call this method again
            groupBrick.ConnectionManager.m_MasterBrick = newMaster;
            brick.LogDebug($"UpdateMaster() - Set {groupBrick.name}'s master to {newMaster.name}");
            
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
            // BUT only if the brick is not currently being grabbed AND is not a board
            bool isMaster = (groupBrick == newMaster);
            bool isGrabbed = groupBrick.IsGrabbed;
            
            // Boards should never have their physics changed
            if (!groupBrick.IsBoard && !isGrabbed)
            {
                // Master brick should be dynamic with gravity
                groupBrick.GetComponent<Rigidbody>().isKinematic = false;
                groupBrick.GetComponent<Rigidbody>().useGravity = true;
            }
            else if (groupBrick.IsBoard)
            {
                brick.LogDebug($"UpdateMaster() - Skipping physics change for board {groupBrick.name}");
            }
        }
        
        brick.LogDebug($"UpdateMaster() - Master update complete");
    }

    public void UnsnapFrom(BrickBehavior otherBrick)
    {
        brick.LogDebug($"UnsnapFrom() - Starting unsnap from {otherBrick.name}");

        // Check for joint on this brick first
        if (m_Joint != null && m_Joint.connectedBody == otherBrick.GetComponent<Rigidbody>())
        {
            brick.LogDebug($"UnsnapFrom() - Found joint on this brick. Destroying it.");
            Object.DestroyImmediate(m_Joint);
            m_Joint = null;
        }
        // Check for joint on the other brick
        else if (otherBrick.Joint != null && otherBrick.Joint.connectedBody == brick.GetComponent<Rigidbody>())
        {
            brick.LogDebug($"UnsnapFrom() - Found joint on other brick ({otherBrick.name}). Destroying it.");
            Object.DestroyImmediate(otherBrick.Joint);
            // The joint destruction will handle clearing the reference
        }
        // If neither brick has the joint, search for any ConfigurableJoint components
        else
        {
            brick.LogDebug($"UnsnapFrom() - No tracked joint found, searching for ConfigurableJoint components");
            
            ConfigurableJoint[] joints = brick.GetComponents<ConfigurableJoint>();
            foreach (var joint in joints)
            {
                if (joint.connectedBody == otherBrick.GetComponent<Rigidbody>())
                {
                    brick.LogDebug($"UnsnapFrom() - Found joint via component search. Destroying it.");
                    Object.DestroyImmediate(joint);
                    m_Joint = null;
                    break;
                }
            }
            
            // Also check the other brick
            ConfigurableJoint[] otherJoints = otherBrick.GetComponents<ConfigurableJoint>();
            foreach (var joint in otherJoints)
            {
                if (joint.connectedBody == brick.GetComponent<Rigidbody>())
                {
                    brick.LogDebug($"UnsnapFrom() - Found joint on other brick via component search. Destroying it.");
                    Object.DestroyImmediate(joint);
                    // The joint destruction will handle clearing the reference
                    break;
                }
            }
        }
        
        m_ConnectedNeighbors.Remove(otherBrick);
        brick.LogDebug($"UnsnapFrom() - Removed {otherBrick.name} from this brick's neighbors");
        
        otherBrick.RemoveNeighbor(brick);
        brick.LogDebug($"UnsnapFrom() - Removed this brick from {otherBrick.name}'s neighbors");

        // Restore physics for this brick ONLY if it's not currently grabbed AND is not a board
        if (!brick.IsBoard && !brick.IsGrabbed)
        {
            brick.GetComponent<Rigidbody>().isKinematic = false;
            brick.GetComponent<Rigidbody>().useGravity = true;
            
            // Restore original mass to prevent weight accumulation after separation
            brick.GetComponent<Rigidbody>().mass = 1.0f;
            
            brick.LogDebug($"UnsnapFrom() - Restored physics for this brick: isKinematic=false, useGravity=true, mass=1.0f");
        }
        else if (brick.IsBoard)
        {
            brick.LogDebug($"UnsnapFrom() - Skipping physics change for this brick (it's a board)");
        }
        else
        {
            brick.LogDebug($"UnsnapFrom() - Skipping physics change for this brick (currently grabbed by XRGrabInteractable)");
        }
        
        // IMPORTANT: Also restore physics for the other brick if it's not grabbed AND is not a board
        if (!otherBrick.IsBoard && !otherBrick.IsGrabbed)
        {
            otherBrick.GetComponent<Rigidbody>().isKinematic = false;
            otherBrick.GetComponent<Rigidbody>().useGravity = true;
            otherBrick.GetComponent<Rigidbody>().mass = 1.0f;
            
            brick.LogDebug($"UnsnapFrom() - Restored physics for other brick {otherBrick.name}: isKinematic=false, useGravity=true, mass=1.0f");
        }
        else if (otherBrick.IsBoard)
        {
            brick.LogDebug($"UnsnapFrom() - Skipping physics change for other brick {otherBrick.name} (it's a board)");
        }
        else
        {
            brick.LogDebug($"UnsnapFrom() - Skipping physics change for other brick {otherBrick.name} (currently grabbed by XRGrabInteractable)");
        }
        
        UpdateMaster(brick);
        brick.LogDebug($"UnsnapFrom() - Called UpdateMaster to set this brick as its own master");
        
        if (otherBrick.ConnectedNeighbors.Count == 0)
        {
            brick.LogDebug($"UnsnapFrom() - Other brick ({otherBrick.name}) now has no neighbors. It will become its own master.");
            otherBrick.UpdateMaster(otherBrick);
        }
        else
        {
            brick.LogDebug($"UnsnapFrom() - Other brick ({otherBrick.name}) still has neighbors. Updating its master to the master of its first remaining neighbor.");
            otherBrick.UpdateMaster(otherBrick.ConnectedNeighbors[0].MasterBrick);
        }
        
        brick.LogDebug($"UnsnapFrom() - Unsnap complete");
    }

    public void StrengthenGroupConnections()
    {
        brick.LogDebug($"StrengthenGroupConnections() - Strengthening all connections in group");
        
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
                
                brick.LogDebug($"StrengthenGroupConnections() - Strengthened {groupBrick.name}: mass={groupBrick.GetComponent<Rigidbody>().mass}, drag={groupBrick.GetComponent<Rigidbody>().linearDamping}, angularDrag={groupBrick.GetComponent<Rigidbody>().angularDamping}");
            }
            
            // Strengthen any joints on this brick
            ConfigurableJoint[] joints = groupBrick.GetComponents<ConfigurableJoint>();
            foreach (var joint in joints)
            {
                joint.breakForce = float.PositiveInfinity;
                joint.breakTorque = float.PositiveInfinity;
                joint.enableCollision = false;
                joint.enablePreprocessing = true;
                joint.anchor = Vector3.zero;
                joint.axis = Vector3.zero;
                
                brick.LogDebug($"StrengthenGroupConnections() - Strengthened joint on {groupBrick.name}");
            }
        }
        
        brick.LogDebug($"StrengthenGroupConnections() - Group strengthening complete");
    }

    public void RemoveNeighbor(BrickBehavior neighbor)
    {
        if (m_ConnectedNeighbors.Contains(neighbor))
        {
            m_ConnectedNeighbors.Remove(neighbor);
            brick.LogDebug($"RemoveNeighbor() - Removed {neighbor.name} from neighbors");
        }
    }

    public void Cleanup()
    {
        // Clean up any remaining joints
        if (m_Joint != null)
        {
            brick.LogDebug($"Cleanup() - Destroying tracked joint");
            Object.Destroy(m_Joint);
            m_Joint = null;
        }
        
        // Also check for any other ConfigurableJoint components that might not be tracked
        ConfigurableJoint[] joints = brick.GetComponents<ConfigurableJoint>();
        foreach (var joint in joints)
        {
            brick.LogDebug($"Cleanup() - Destroying untracked joint: {joint.name}");
            Object.Destroy(joint);
        }
        
        // Clear references
        m_ConnectedNeighbors.Clear();
        m_MasterBrick = null;
        m_OriginalMaster = null;
    }

    public void SetJoint(ConfigurableJoint joint)
    {
        brick.LogDebug($"SetJoint() - Setting joint: {joint}");
        m_Joint = joint;
    }
} 
