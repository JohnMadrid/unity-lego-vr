using UnityEngine;
using System.Collections.Generic;

public class BrickPhysicsManager
{
    private readonly BrickBehavior brick;
    private float lastValidationTime = 0f;
    private const float VALIDATION_COOLDOWN = 0.1f; // Only validate every 100ms

    // Missing properties that are referenced by other classes
    public BrickBehavior m_MasterBrick { get; set; }
    public BrickBehavior m_OriginalMaster { get; set; }
    public FixedJoint m_Joint { get; set; }

    public BrickPhysicsManager(BrickBehavior brick)
    {
        this.brick = brick;
    }

    public void OnGrabStarted()
    {
        Debug.Log($"[{brick.name}] OnGrabStarted() - Handling physics in physics manager");
        // Physics state is managed by XRGrabInteractable during grabs
    }

    public void OnGrabReleased()
    {
        Debug.Log($"[{brick.name}] OnGrabReleased() - Handling grab release");
        
        // Find all bricks in the connected group
        List<BrickBehavior> groupBricks = new List<BrickBehavior>();
        BrickGroupUtils.FindAllConnectedInGroup(brick, groupBricks, brick.name);
        Debug.Log($"[{brick.name}] OnGrabReleased() - Found {groupBricks.Count} bricks in group");
        
        // Determine the new master brick (the one that was originally grabbed)
        BrickBehavior originalMaster = brick.OriginalMaster;
        Debug.Log($"[{brick.name}] OnGrabReleased() - Original master: {originalMaster?.name ?? "null"}");
        
        // Update the master for all bricks in the group
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

    public void ValidatePhysicsState()
    {
        // Throttle validation to prevent performance issues
        if (Time.time - lastValidationTime < VALIDATION_COOLDOWN)
        {
            return;
        }
        lastValidationTime = Time.time;
        
        Debug.Log($"[{brick.name}] ValidatePhysicsState() - Validating physics state for brick: {brick.name}");
        LogPhysicsState("ValidatePhysicsState");
        
        // Check if this brick is currently being grabbed
        bool isGrabbed = brick.GetComponent<UnityEngine.XR.Interaction.Toolkit.Interactables.XRGrabInteractable>().isSelected;
        if (isGrabbed)
        {
            Debug.Log($"[{brick.name}] ValidatePhysicsState() - Brick is currently grabbed by XRGrabInteractable - physics state managed by XR system");
            return; // Don't validate physics for grabbed bricks - XRGrabInteractable handles it
        }
        
        if (brick.ConnectedNeighbors.Count > 0)
        {
            Debug.Log($"[{brick.name}] ValidatePhysicsState() - Connected to {brick.ConnectedNeighbors.Count} neighbors");
            Debug.Log($"[{brick.name}] ValidatePhysicsState() - Master brick: {brick.MasterBrick.name}");
            
            if (brick.MasterBrick == brick)
            {
                if (brick.GetComponent<Rigidbody>().isKinematic)
                {
                    Debug.LogWarning($"[{brick.name}] ValidatePhysicsState() - WARNING: Master brick should not be kinematic!");
                }
                if (!brick.GetComponent<Rigidbody>().useGravity)
                {
                    Debug.LogWarning($"[{brick.name}] ValidatePhysicsState() - WARNING: Master brick should have gravity enabled!");
                }
            }
            else
            {
                if (brick.GetComponent<Rigidbody>().isKinematic)
                {
                    Debug.LogWarning($"[{brick.name}] ValidatePhysicsState() - WARNING: Non-master brick should not be kinematic!");
                }
                if (!brick.GetComponent<Rigidbody>().useGravity)
                {
                    Debug.LogWarning($"[{brick.name}] ValidatePhysicsState() - WARNING: Non-master brick should have gravity enabled!");
                }
            }
        }
        else
        {
            Debug.Log($"[{brick.name}] ValidatePhysicsState() - No connected neighbors");
            if (brick.GetComponent<Rigidbody>().isKinematic)
            {
                Debug.LogWarning($"[{brick.name}] ValidatePhysicsState() - WARNING: Standalone brick should not be kinematic!");
            }
            if (!brick.GetComponent<Rigidbody>().useGravity)
            {
                Debug.LogWarning($"[{brick.name}] ValidatePhysicsState() - WARNING: Standalone brick should have gravity enabled!");
            }
        }
    }

    private void LogPhysicsState(string context)
    {
        if (brick.GetComponent<Rigidbody>() != null)
        {
            var rb = brick.GetComponent<Rigidbody>();
            Debug.Log($"[{brick.name}] {context} - Physics State: isKinematic={rb.isKinematic}, useGravity={rb.useGravity}, velocity={rb.linearVelocity}, angularVelocity={rb.angularVelocity}");
        }
        else
        {
            Debug.LogWarning($"[{brick.name}] {context} - WARNING: Rigidbody is null!");
        }
    }

    public void StabilizeGroup()
    {
        Debug.Log($"[{brick.name}] StabilizeGroup() - Stabilizing group to prevent unwanted movement");
        
        // Find all bricks in the connected group
        List<BrickBehavior> groupBricks = new List<BrickBehavior>();
        BrickGroupUtils.FindAllConnectedInGroup(brick, groupBricks, brick.name);
        
        foreach (var groupBrick in groupBricks)
        {
            if (groupBrick.GetComponent<Rigidbody>() != null && !groupBrick.GetComponent<UnityEngine.XR.Interaction.Toolkit.Interactables.XRGrabInteractable>().isSelected)
            {
                // Clear any residual velocities that might cause unwanted movement
                groupBrick.GetComponent<Rigidbody>().linearVelocity = Vector3.zero;
                groupBrick.GetComponent<Rigidbody>().angularVelocity = Vector3.zero;
                
                // Ensure proper physics state
                groupBrick.GetComponent<Rigidbody>().isKinematic = false;
                groupBrick.GetComponent<Rigidbody>().useGravity = true;
                
                Debug.Log($"[{brick.name}] StabilizeGroup() - Stabilized {groupBrick.name}: cleared velocities");
            }
        }
        
        Debug.Log($"[{brick.name}] StabilizeGroup() - Group stabilization complete");
    }

    private void UpdateMaster(BrickBehavior newMaster)
    {
        Debug.Log($"[{brick.name}] UpdateMaster() - Updating group master to: {newMaster.name}");
        
        List<BrickBehavior> groupToUpdate = new List<BrickBehavior>();
        Debug.Log($"[{brick.name}] UpdateMaster() - Created group members list");
        
        BrickGroupUtils.FindAllConnectedInGroup(brick, groupToUpdate, brick.name);
        Debug.Log($"[{brick.name}] UpdateMaster() - Found {groupToUpdate.Count} bricks in the group");

        foreach (var groupBrick in groupToUpdate)
        {
            Debug.Log($"[{brick.name}] UpdateMaster() - Processing brick: {groupBrick.name}");
            // Use the public UpdateMaster method on BrickBehavior to update master brick
            groupBrick.UpdateMaster(newMaster);
            Debug.Log($"[{brick.name}] UpdateMaster() - Set {groupBrick.name}'s master to {newMaster.name}");
            
            // Update the original master for the entire group
            // The original master should be the one that was originally its own master
            if (groupBrick.OriginalMaster == groupBrick) // This brick was originally its own master
            {
                // This brick becomes the original master for the entire group
                foreach (var groupBrickInGroup in groupToUpdate)
                {
                    groupBrickInGroup.UpdateMaster(groupBrick);
                    Debug.Log($"[{brick.name}] UpdateMaster() - Set {groupBrickInGroup.name}'s original master to {groupBrick.name}");
                }
                break; // Found the original master, no need to continue
            }
            
            // Set physics properties based on whether this brick is the master
            // BUT only if the brick is not currently being grabbed
            bool isMaster = (groupBrick == newMaster);
            bool isGrabbed = groupBrick.GetComponent<UnityEngine.XR.Interaction.Toolkit.Interactables.XRGrabInteractable>().isSelected;
            
            if (isGrabbed)
            {
                Debug.Log($"[{brick.name}] UpdateMaster() - Skipping physics change for {groupBrick.name} (currently grabbed by XRGrabInteractable)");
                // Do NOT change physics of grabbed bricks - let XRGrabInteractable handle it
            }
            else
            {
                // Always restore physics for non-grabbed bricks
                groupBrick.GetComponent<Rigidbody>().isKinematic = false;
                groupBrick.GetComponent<Rigidbody>().useGravity = true;
                
                if (isMaster)
                {
                    Debug.Log($"[{brick.name}] UpdateMaster() - Set {groupBrick.name}'s physics (master): isKinematic=false, useGravity=true");
                }
                else
                {
                    Debug.Log($"[{brick.name}] UpdateMaster() - Set {groupBrick.name}'s physics (non-master): isKinematic=false, useGravity=true");
                }
            }
        }
        
        Debug.Log($"[{brick.name}] UpdateMaster() - Master update complete");
    }

    public void Cleanup()
    {
        // No specific cleanup needed for physics manager
        Debug.Log($"[{brick.name}] Cleanup() - Physics manager cleanup complete");
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
                // Set drag and angular drag from BrickBehavior
                groupBrick.GetComponent<Rigidbody>().linearDamping = brick.brickDrag;
                groupBrick.GetComponent<Rigidbody>().angularDamping = brick.brickAngularDrag;
                
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
} 