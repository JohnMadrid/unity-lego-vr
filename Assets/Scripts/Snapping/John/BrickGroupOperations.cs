using UnityEngine;
using System.Collections.Generic;
using UnityEngine.XR.Interaction.Toolkit.Interactors;

public class BrickGroupOperations
{
    private readonly BrickBehavior brick;

    // Missing properties that are referenced by other classes
    public BrickBehavior m_MasterBrick { get; set; }
    public BrickBehavior m_OriginalMaster { get; set; }
    public FixedJoint m_Joint { get; set; }

    public BrickGroupOperations(BrickBehavior brick)
    {
        this.brick = brick;
    }

    public void CheckForGroupJoiningOpportunities()
    {
        // Find all other grabbed bricks
        BrickBehavior[] allBricks = UnityEngine.Object.FindObjectsOfType<BrickBehavior>();
        float joinThreshold = brick.groupJoinThreshold; // Use BrickBehavior's groupJoinThreshold
        foreach (var otherBrick in allBricks)
        {
            if (otherBrick != brick && otherBrick.GetComponent<UnityEngine.XR.Interaction.Toolkit.Interactables.XRGrabInteractable>().isSelected)
            {
                // Check if grabbed by different controller
                var thisInteractor = brick.GetComponent<UnityEngine.XR.Interaction.Toolkit.Interactables.XRGrabInteractable>().firstInteractorSelecting;
                var otherInteractor = otherBrick.GetComponent<UnityEngine.XR.Interaction.Toolkit.Interactables.XRGrabInteractable>().firstInteractorSelecting;
                
                if (thisInteractor != otherInteractor)
                {
                    // Check if in different groups
                    if (!AreInSameGroup(brick, otherBrick))
                    {
                        // Check distance
                        float distance = Vector3.Distance(brick.transform.position, otherBrick.transform.position);
                        if (distance < joinThreshold) // Adjustable group join threshold
                        {
                            Debug.Log($"[{brick.name}] CheckForGroupJoiningOpportunities() - Close proximity detected with {otherBrick.name} (distance: {distance})");
                            
                            // Clear snap immunity and enable collisions
                            brick.snapImmunityEndTime = 0f;
                            otherBrick.snapImmunityEndTime = 0f;
                            brick.EnableStudCollisions();
                            otherBrick.EnableStudCollisions();
                            
                            Debug.Log($"[{brick.name}] CheckForGroupJoiningOpportunities() - Enabled joining for {brick.name} and {otherBrick.name}");
                        }
                    }
                }
            }
        }
    }

    public void CheckForUnsnapConditions(IXRSelectInteractor interactor)
    {
        Debug.Log($"[{brick.name}] CheckForUnsnapConditions() - Checking for unsnap conditions with {brick.ConnectedNeighbors.Count} neighbors");
        
        // Check if we are part of a larger group
        if (brick.ConnectedNeighbors.Count > 0)
        {
            // Find all bricks in the connected group
            List<BrickBehavior> allGroupBricks = new List<BrickBehavior>();
            BrickGroupUtils.FindAllConnected(brick, allGroupBricks);
            Debug.Log($"[{brick.name}] CheckForUnsnapConditions() - Found {allGroupBricks.Count} total bricks in group");
            
            // Find all grabbed bricks in the group
            List<BrickBehavior> grabbedBricks = new List<BrickBehavior>();
            List<IXRSelectInteractor> grabbedInteractors = new List<IXRSelectInteractor>();
            
            foreach (var groupBrick in allGroupBricks)
            {
                if (groupBrick.GetComponent<UnityEngine.XR.Interaction.Toolkit.Interactables.XRGrabInteractable>().isSelected)
                {
                    grabbedBricks.Add(groupBrick);
                    grabbedInteractors.Add(groupBrick.GetComponent<UnityEngine.XR.Interaction.Toolkit.Interactables.XRGrabInteractable>().firstInteractorSelecting);
                    Debug.Log($"[{brick.name}] CheckForUnsnapConditions() - Found grabbed brick: {groupBrick.name} by interactor: {groupBrick.GetComponent<UnityEngine.XR.Interaction.Toolkit.Interactables.XRGrabInteractable>().firstInteractorSelecting?.transform.name}");
                }
            }
            
            Debug.Log($"[{brick.name}] CheckForUnsnapConditions() - Found {grabbedBricks.Count} grabbed bricks in group");
            
            // If we have multiple grabbed bricks with different interactors, we need to split
            if (grabbedBricks.Count > 1)
            {
                // Check if they're grabbed by different interactors
                bool differentInteractors = false;
                for (int i = 0; i < grabbedInteractors.Count; i++)
                {
                    for (int j = i + 1; j < grabbedInteractors.Count; j++)
                    {
                        if (grabbedInteractors[i] != grabbedInteractors[j])
                        {
                            differentInteractors = true;
                            Debug.Log($"[{brick.name}] CheckForUnsnapConditions() - Different interactors detected: {grabbedInteractors[i]?.transform.name} vs {grabbedInteractors[j]?.transform.name}");
                            break;
                        }
                    }
                    if (differentInteractors) break;
                }
                
                if (differentInteractors)
                {
                    Debug.Log($"[{brick.name}] CheckForUnsnapConditions() - MULTI-CONTROLLER SPLIT CONDITION MET! Splitting group with {grabbedBricks.Count} grabbed bricks");
                    
                    // Perform the split
                    SplitConnectedGroup(grabbedBricks);
                    return;
                }
                else
                {
                    Debug.Log($"[{brick.name}] CheckForUnsnapConditions() - Multiple bricks grabbed by same interactor, no split needed");
                }
            }
            else
            {
                Debug.Log($"[{brick.name}] CheckForUnsnapConditions() - Only one brick grabbed, no split needed");
            }
        }
        else
        {
            Debug.Log($"[{brick.name}] CheckForUnsnapConditions() - No connected neighbors to check");
        }
        
        // Check for potential joining of separate groups
        CheckForGroupJoining(interactor);
    }
    
    // Method to check if separate groups should be joined
    private void CheckForGroupJoining(IXRSelectInteractor interactor)
    {
        Debug.Log($"[{brick.name}] CheckForGroupJoining() - Checking for group joining opportunities");
        
        // Find all bricks in the current group
        List<BrickBehavior> allGroupBricks = new List<BrickBehavior>();
        BrickGroupUtils.FindAllConnected(brick, allGroupBricks);
        Debug.Log($"[{brick.name}] CheckForGroupJoining() - Found {allGroupBricks.Count} bricks in current group");
        
        // Find all other grabbed bricks in the scene
        List<BrickBehavior> otherGrabbedBricks = new List<BrickBehavior>();
        foreach (var groupBrick in allGroupBricks)
        {
            if (groupBrick != brick && groupBrick.GetComponent<UnityEngine.XR.Interaction.Toolkit.Interactables.XRGrabInteractable>().isSelected)
            {
                otherGrabbedBricks.Add(groupBrick);
            }
        }
        
        Debug.Log($"[{brick.name}] CheckForGroupJoining() - Found {otherGrabbedBricks.Count} other grabbed bricks in group");
        
        // Check each other grabbed brick for potential joining
        foreach (var otherGrabbedBrick in otherGrabbedBricks)
        {
            CheckForGroupJoiningWithBrick(otherGrabbedBrick, interactor);
        }
    }
    
    // Helper method to check for group joining with a specific brick
    private void CheckForGroupJoiningWithBrick(BrickBehavior otherBrick, IXRSelectInteractor interactor)
    {
        Debug.Log($"[{brick.name}] CheckForGroupJoiningWithBrick() - Checking for group joining with {otherBrick.name}");
        
        // Check if the other brick is in a different group
        if (!BrickGroupUtils.AreBricksInSameGroup(brick, otherBrick))
        {
            Debug.Log($"[{brick.name}] CheckForGroupJoiningWithBrick() - {otherBrick.name} is in a different group, checking distance");
            
            // Check distance between the groups
            float distance = Vector3.Distance(brick.transform.position, otherBrick.transform.position);
            float joinThreshold = brick.groupJoinThreshold;
            
            if (distance < joinThreshold)
            {
                Debug.Log($"[{brick.name}] CheckForGroupJoiningWithBrick() - Groups are close enough for potential joining (distance: {distance} < {joinThreshold})");
                
                // Clear snap immunity to allow joining
                brick.snapImmunityEndTime = 0f;
                otherBrick.snapImmunityEndTime = 0f;
                
                // Enable collision detection for both bricks
                brick.EnableStudCollisions();
                otherBrick.EnableStudCollisions();
                
                Debug.Log($"[{brick.name}] CheckForGroupJoiningWithBrick() - Cleared snap immunity and enabled collisions for potential joining");
            }
            else
            {
                Debug.Log($"[{brick.name}] CheckForGroupJoiningWithBrick() - Groups too far apart for joining (distance: {distance} >= {joinThreshold})");
            }
        }
        else
        {
            Debug.Log($"[{brick.name}] CheckForGroupJoiningWithBrick() - {otherBrick.name} is already in the same group");
        }
    }
    
    // Helper method to check if two bricks are in the same group
    public bool AreInSameGroup(BrickBehavior brick1, BrickBehavior brick2)
    {
        List<BrickBehavior> group1 = new List<BrickBehavior>();
        List<BrickBehavior> group2 = new List<BrickBehavior>();
        
        BrickGroupUtils.FindAllConnected(brick1, group1);
        BrickGroupUtils.FindAllConnected(brick2, group2);
        
        // Check if there's any overlap between the groups
        foreach (var groupBrick in group1)
        {
            if (group2.Contains(groupBrick))
            {
                return true; // They're in the same group
            }
        }
        
        return false; // They're in different groups
    }

    public void CheckForGroupJoiningDuringCollision(Stud ourStud, Stud targetStud)
    {
        Debug.Log($"[{brick.name}] CheckForGroupJoiningDuringCollision() - Checking for group joining during collision");
        
        // Check if these studs belong to different groups
        if (ourStud.ParentBrick != null && targetStud.ParentBrick != null)
        {
            if (!BrickGroupUtils.AreBricksInSameGroup(ourStud.ParentBrick, targetStud.ParentBrick))
            {
                Debug.Log($"[{brick.name}] CheckForGroupJoiningDuringCollision() - Studs belong to different groups, checking for joining");
                
                // Check if the bricks are close enough for potential joining
                float distance = Vector3.Distance(ourStud.ParentBrick.transform.position, targetStud.ParentBrick.transform.position);
                float joinThreshold = brick.groupJoinThreshold;
                
                if (distance < joinThreshold)
                {
                    Debug.Log($"[{brick.name}] CheckForGroupJoiningDuringCollision() - Bricks are close enough for potential joining (distance: {distance} < {joinThreshold})");
                    
                    // Clear snap immunity to allow joining
                    ourStud.ParentBrick.snapImmunityEndTime = 0f;
                    targetStud.ParentBrick.snapImmunityEndTime = 0f;
                    
                    // Enable collision detection for both bricks
                    ourStud.ParentBrick.EnableStudCollisions();
                    targetStud.ParentBrick.EnableStudCollisions();
                    
                    Debug.Log($"[{brick.name}] CheckForGroupJoiningDuringCollision() - Cleared snap immunity and enabled collisions for potential joining");
                }
                else
                {
                    Debug.Log($"[{brick.name}] CheckForGroupJoiningDuringCollision() - Bricks too far apart for joining (distance: {distance} >= {joinThreshold})");
                }
            }
            else
            {
                Debug.Log($"[{brick.name}] CheckForGroupJoiningDuringCollision() - Studs belong to same group, no joining needed");
            }
        }
    }

    private void SplitConnectedGroup(List<BrickBehavior> grabbedBricks)
    {
        Debug.Log($"[{brick.name}] SplitConnectedGroup() - Starting group split with {grabbedBricks.Count} grabbed bricks");
        
        // Find all bricks in the original group
        List<BrickBehavior> allGroupBricks = new List<BrickBehavior>();
        BrickGroupUtils.FindAllConnectedInGroup(brick, allGroupBricks, brick.name);
        Debug.Log($"[{brick.name}] SplitConnectedGroup() - Original group has {allGroupBricks.Count} total bricks");
        
        // Create separate groups for each grabbed brick
        List<List<BrickBehavior>> newGroups = new List<List<BrickBehavior>>();
        
        foreach (var grabbedBrick in grabbedBricks)
        {
            Debug.Log($"[{brick.name}] SplitConnectedGroup() - Creating group for grabbed brick: {grabbedBrick.name}");
            
            // Find all bricks that should be in this group
            List<BrickBehavior> groupForThisBrick = new List<BrickBehavior>();
            FindBricksForGroup(grabbedBrick, grabbedBricks, groupForThisBrick);
            
            Debug.Log($"[{brick.name}] SplitConnectedGroup() - Group for {grabbedBrick.name} has {groupForThisBrick.Count} bricks");
            newGroups.Add(groupForThisBrick);
        }
        
        // Now break all connections between different groups
        for (int i = 0; i < newGroups.Count; i++)
        {
            for (int j = i + 1; j < newGroups.Count; j++)
            {
                Debug.Log($"[{brick.name}] SplitConnectedGroup() - Breaking connections between group {i} and group {j}");
                BreakConnectionsBetweenGroups(newGroups[i], newGroups[j]);
            }
        }
        
        // Update masters for each new group
        for (int i = 0; i < newGroups.Count; i++)
        {
            var group = newGroups[i];
            var master = grabbedBricks[i]; // Each grabbed brick becomes master of its group
            
            Debug.Log($"[{brick.name}] SplitConnectedGroup() - Setting {master.name} as master for group {i} with {group.Count} bricks");
            
            foreach (var groupBrick in group)
            {
                groupBrick.UpdateMaster(master);
                
                // Activate snap immunity for all bricks in the split groups
                groupBrick.ActivateSnapImmunity();
                
                // Restore original mass to prevent weight accumulation after separation
                if (groupBrick.GetComponent<Rigidbody>() != null)
                {
                    groupBrick.GetComponent<Rigidbody>().mass = 1.0f;
                    Debug.Log($"[{brick.name}] SplitConnectedGroup() - Restored mass for {groupBrick.name}: mass=1.0f");
                }
            }
            
            // Explicitly restore physics for the master brick ONLY if it's not currently grabbed
            if (!master.GetComponent<UnityEngine.XR.Interaction.Toolkit.Interactables.XRGrabInteractable>().isSelected)
            {
                master.GetComponent<Rigidbody>().isKinematic = false;
                master.GetComponent<Rigidbody>().useGravity = true;
                Debug.Log($"[{brick.name}] SplitConnectedGroup() - Restored physics for master {master.name}: isKinematic=false, useGravity=true");
            }
            else
            {
                Debug.Log($"[{brick.name}] SplitConnectedGroup() - Skipping physics change for master {master.name} (currently grabbed by XRGrabInteractable)");
            }
        }
        
        // Move the groups apart to prevent immediate re-snapping
        brick.StartCoroutine(MoveGroupsApart(newGroups));
        
        Debug.Log($"[{brick.name}] SplitConnectedGroup() - Group split complete");
    }
    
    // Coroutine to move split groups apart
    private System.Collections.IEnumerator MoveGroupsApart(List<List<BrickBehavior>> groups)
    {
        Debug.Log($"[{brick.name}] MoveGroupsApart() - Moving {groups.Count} groups apart");
        
        // Wait a frame to let physics settle
        yield return null;
        
        if (groups.Count >= 2)
        {
            // Calculate the center of each group
            List<Vector3> groupCenters = new List<Vector3>();
            
            foreach (var group in groups)
            {
                Vector3 center = Vector3.zero;
                foreach (var groupBrick in group)
                {
                    center += groupBrick.transform.position;
                }
                center /= group.Count;
                groupCenters.Add(center);
                Debug.Log($"[{brick.name}] MoveGroupsApart() - Group center: {center}");
            }
            
            // Calculate the separation direction (away from the center of all groups)
            Vector3 overallCenter = Vector3.zero;
            foreach (var center in groupCenters)
            {
                overallCenter += center;
            }
            overallCenter /= groupCenters.Count;
            
            // Move each group away from the overall center
            for (int i = 0; i < groups.Count; i++)
            {
                Vector3 direction = (groupCenters[i] - overallCenter).normalized;
                float separationDistance = brick.groupSplitSeparation; // Adjustable group split separation
                
                foreach (var groupBrick in groups[i])
                {
                    if (!groupBrick.GetComponent<UnityEngine.XR.Interaction.Toolkit.Interactables.XRGrabInteractable>().isSelected)
                    {
                        Vector3 newPosition = groupBrick.transform.position + (direction * separationDistance);
                        groupBrick.transform.position = newPosition;
                        Debug.Log($"[{brick.name}] MoveGroupsApart() - Moved {groupBrick.name} to {newPosition}");
                    }
                }
            }
            
            Debug.Log($"[{brick.name}] MoveGroupsApart() - Groups moved apart successfully");
        }
    }
    
    private void FindBricksForGroup(BrickBehavior grabbedBrick, List<BrickBehavior> allGrabbedBricks, List<BrickBehavior> groupBricks)
    {
        Debug.Log($"[{brick.name}] FindBricksForGroup() - Finding bricks for group starting from {grabbedBrick.name}");
        
        // Use a breadth-first search to find all bricks that should be in this group
        Queue<BrickBehavior> toVisit = new Queue<BrickBehavior>();
        HashSet<BrickBehavior> visited = new HashSet<BrickBehavior>();
        
        toVisit.Enqueue(grabbedBrick);
        visited.Add(grabbedBrick);
        groupBricks.Add(grabbedBrick);
        
        while (toVisit.Count > 0)
        {
            var currentBrick = toVisit.Dequeue();
            Debug.Log($"[{brick.name}] FindBricksForGroup() - Visiting brick: {currentBrick.name}");
            
            if (currentBrick == null || currentBrick.ConnectedNeighbors == null)
            {
                Debug.LogWarning($"[{brick.name}] FindBricksForGroup() - WARNING: Current brick or neighbors list is null!");
                continue;
            }
            
            foreach (var neighbor in currentBrick.ConnectedNeighbors)
            {
                if (neighbor == null)
                {
                    Debug.LogWarning($"[{brick.name}] FindBricksForGroup() - WARNING: Neighbor is null!");
                    continue;
                }
                
                if (!visited.Contains(neighbor))
                {
                    // Check if this neighbor should be included in this group
                    bool shouldInclude = true;
                    
                    // Check if this neighbor is grabbed by a different controller
                    if (allGrabbedBricks.Contains(neighbor))
                    {
                        var currentInteractor = grabbedBrick.GetComponent<UnityEngine.XR.Interaction.Toolkit.Interactables.XRGrabInteractable>().firstInteractorSelecting;
                        var neighborInteractor = neighbor.GetComponent<UnityEngine.XR.Interaction.Toolkit.Interactables.XRGrabInteractable>().firstInteractorSelecting;
                        
                        if (currentInteractor != neighborInteractor)
                        {
                            Debug.Log($"[{brick.name}] FindBricksForGroup() - Excluding {neighbor.name} (directly grabbed by different interactor)");
                            shouldInclude = false;
                        }
                    }
                    else
                    {
                        // Check if this neighbor is connected to any brick grabbed by a different controller
                        var currentInteractor = grabbedBrick.GetComponent<UnityEngine.XR.Interaction.Toolkit.Interactables.XRGrabInteractable>().firstInteractorSelecting;
                        bool connectedToDifferentController = false;
                        
                        foreach (var grabbedBrickCheck in allGrabbedBricks)
                        {
                            if (grabbedBrickCheck != grabbedBrick) // Skip the current grabbed brick
                            {
                                var otherInteractor = grabbedBrickCheck.GetComponent<UnityEngine.XR.Interaction.Toolkit.Interactables.XRGrabInteractable>().firstInteractorSelecting;
                                if (currentInteractor != otherInteractor)
                                {
                                    // Check if this neighbor is connected to the other grabbed brick
                                    if (neighbor.ConnectedNeighbors.Contains(grabbedBrickCheck))
                                    {
                                        Debug.Log($"[{brick.name}] FindBricksForGroup() - Excluding {neighbor.name} (connected to {grabbedBrickCheck.name} which is grabbed by different interactor)");
                                        shouldInclude = false;
                                        connectedToDifferentController = true;
                                        break;
                                    }
                                }
                            }
                        }
                        
                        if (!connectedToDifferentController)
                        {
                            Debug.Log($"[{brick.name}] FindBricksForGroup() - Including {neighbor.name} (not connected to different controller)");
                        }
                    }
                    
                    if (shouldInclude)
                    {
                        Debug.Log($"[{brick.name}] FindBricksForGroup() - Including {neighbor.name} in group");
                        visited.Add(neighbor);
                        groupBricks.Add(neighbor);
                        toVisit.Enqueue(neighbor);
                    }
                }
            }
        }
        
        Debug.Log($"[{brick.name}] FindBricksForGroup() - Group for {grabbedBrick.name} contains {groupBricks.Count} bricks");
    }
    
    private void BreakConnectionsBetweenGroups(List<BrickBehavior> group1, List<BrickBehavior> group2)
    {
        Debug.Log($"[{brick.name}] BreakConnectionsBetweenGroups() - Breaking connections between {group1.Count} and {group2.Count} bricks");
        
        foreach (var brick1 in group1)
        {
            foreach (var brick2 in group2)
            {
                // Check if these bricks are directly connected
                if (brick1.ConnectedNeighbors.Contains(brick2))
                {
                    Debug.Log($"[{brick.name}] BreakConnectionsBetweenGroups() - Breaking connection between {brick1.name} and {brick2.name}");
                    
                    // Remove the connection
                    brick1.RemoveNeighbor(brick2);
                    brick2.RemoveNeighbor(brick1);
                    
                    // Destroy the joint
                    if (brick1.Joint != null && brick1.Joint.connectedBody == brick2.GetComponent<Rigidbody>())
                    {
                        Debug.Log($"[{brick.name}] BreakConnectionsBetweenGroups() - Destroying joint on {brick1.name}");
                        Object.DestroyImmediate(brick1.Joint);
                        // The joint destruction will handle clearing the reference
                    }
                    else if (brick2.Joint != null && brick2.Joint.connectedBody == brick1.GetComponent<Rigidbody>())
                    {
                        Debug.Log($"[{brick.name}] BreakConnectionsBetweenGroups() - Destroying joint on {brick2.name}");
                        Object.DestroyImmediate(brick2.Joint);
                        // The joint destruction will handle clearing the reference
                    }
                    else
                    {
                        // Search for untracked joints
                        FixedJoint[] joints1 = brick1.GetComponents<FixedJoint>();
                        foreach (var joint in joints1)
                        {
                            if (joint.connectedBody == brick2.GetComponent<Rigidbody>())
                            {
                                Debug.Log($"[{brick.name}] BreakConnectionsBetweenGroups() - Destroying untracked joint on {brick1.name}");
                                Object.DestroyImmediate(joint);
                                break;
                            }
                        }
                        
                        FixedJoint[] joints2 = brick2.GetComponents<FixedJoint>();
                        foreach (var joint in joints2)
                        {
                            if (joint.connectedBody == brick1.GetComponent<Rigidbody>())
                            {
                                Debug.Log($"[{brick.name}] BreakConnectionsBetweenGroups() - Destroying untracked joint on {brick2.name}");
                                Object.DestroyImmediate(joint);
                                break;
                            }
                        }
                    }
                    
                    // Validate that the joint was actually destroyed
                    brick.StartCoroutine(ValidateJointDestruction(brick1, brick2));
                }
            }
        }
        
        Debug.Log($"[{brick.name}] BreakConnectionsBetweenGroups() - Connection breaking complete");
    }
    
    private System.Collections.IEnumerator ValidateJointDestruction(BrickBehavior brick1, BrickBehavior brick2)
    {
        yield return new WaitForEndOfFrame();
        
        // Check if any joints still exist between these bricks
        FixedJoint[] joints1 = brick1.GetComponents<FixedJoint>();
        FixedJoint[] joints2 = brick2.GetComponents<FixedJoint>();
        
        bool jointStillExists = false;
        foreach (var joint in joints1)
        {
            if (joint.connectedBody == brick2.GetComponent<Rigidbody>())
            {
                Debug.LogWarning($"[{brick.name}] ValidateJointDestruction() - WARNING: Joint still exists on {brick1.name}!");
                jointStillExists = true;
                break;
            }
        }
        
        foreach (var joint in joints2)
        {
            if (joint.connectedBody == brick1.GetComponent<Rigidbody>())
            {
                Debug.LogWarning($"[{brick.name}] ValidateJointDestruction() - WARNING: Joint still exists on {brick2.name}!");
                jointStillExists = true;
                break;
            }
        }
        
        if (!jointStillExists)
        {
            Debug.Log($"[{brick.name}] ValidateJointDestruction() - Joint destruction validated successfully");
        }
    }

    public void Cleanup()
    {
        // No specific cleanup needed for group operations
        Debug.Log($"[{brick.name}] Cleanup() - Group operations cleanup complete");
    }
} 