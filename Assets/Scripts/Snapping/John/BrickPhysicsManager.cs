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
        Debug.Log($"[{brick.name}] OnGrabReleased() - Handling physics in physics manager");
        
        var rb = brick.GetComponent<Rigidbody>();
        if (rb != null)
        {
            Debug.Log($"[{brick.name}] OnGrabReleased() - DEBUG: Physics at start - isKinematic: {rb.isKinematic}, useGravity: {rb.useGravity}, velocity: {rb.linearVelocity}");
        }
        
        // Handle physics restoration based on connection state
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
            BrickBehavior originalMaster = brick.OriginalMaster;
            
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
            // Restore normal physics for standalone brick
            var standaloneRb = brick.GetComponent<Rigidbody>();
            Debug.Log($"[{brick.name}] OnGrabReleased() - DEBUG: Physics before standalone setup - isKinematic: {standaloneRb.isKinematic}, useGravity: {standaloneRb.useGravity}");
            
            standaloneRb.isKinematic = false;
            standaloneRb.useGravity = true;
            Debug.Log($"[{brick.name}] OnGrabReleased() - Set physics for standalone brick: isKinematic=false, useGravity=true");
            Debug.Log($"[{brick.name}] OnGrabReleased() - DEBUG: Physics after standalone setup - isKinematic: {standaloneRb.isKinematic}, useGravity: {standaloneRb.useGravity}");
        }
        
        // Validate physics state after release
        ValidatePhysicsState();
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
        FindAllConnected(brick, groupBricks);
        
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

    private void FindAllConnected(BrickBehavior brick, List<BrickBehavior> visited)
    {
        Debug.Log($"[{this.brick.name}] FindAllConnected() - Visiting brick: {brick.name}");

        if (brick == null || visited.Contains(brick))
        {
            Debug.Log($"[{this.brick.name}] FindAllConnected() - Brick is null or already visited, returning");
            return;
        }

        visited.Add(brick);
        Debug.Log($"[{this.brick.name}] FindAllConnected() - Added {brick.name} to visited list. Total visited: {visited.Count}");
        
        foreach (var neighbor in brick.ConnectedNeighbors)
        {
            Debug.Log($"[{this.brick.name}] FindAllConnected() - Recursively checking neighbor of {brick.name}: {neighbor.name}");
            FindAllConnected(neighbor, visited);
        }
        
        Debug.Log($"[{this.brick.name}] FindAllConnected() - Finished visiting all neighbors of {brick.name}");
    }

    private void UpdateMaster(BrickBehavior newMaster)
    {
        Debug.Log($"[{brick.name}] UpdateMaster() - Updating group master to: {newMaster.name}");
        
        List<BrickBehavior> groupToUpdate = new List<BrickBehavior>();
        Debug.Log($"[{brick.name}] UpdateMaster() - Created group members list");
        
        FindAllConnected(brick, groupToUpdate);
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
                if (isMaster)
                {
                    // Master brick should be dynamic with gravity
                    groupBrick.GetComponent<Rigidbody>().isKinematic = false;
                    groupBrick.GetComponent<Rigidbody>().useGravity = true;
                    Debug.Log($"[{brick.name}] UpdateMaster() - Set {groupBrick.name}'s physics (master): isKinematic=false, useGravity=true");
                }
                else
                {
                    // Non-master bricks should be dynamic with gravity
                    // They will follow the master via FixedJoints but can still respond to physics
                    groupBrick.GetComponent<Rigidbody>().isKinematic = false;
                    groupBrick.GetComponent<Rigidbody>().useGravity = true;
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
        FindAllConnected(brick, groupBricks);
        
        foreach (var groupBrick in groupBricks)
        {
            // Strengthen the rigidbody properties
            if (groupBrick.GetComponent<Rigidbody>() != null)
            {
                groupBrick.GetComponent<Rigidbody>().mass = Mathf.Max(groupBrick.GetComponent<Rigidbody>().mass, 1.0f);
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