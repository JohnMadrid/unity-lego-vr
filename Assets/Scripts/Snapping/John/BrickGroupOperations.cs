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
        // Boards should not participate in group joining
        if (brick.IsBoard)
        {
            brick.LogDebug($"CheckForGroupJoiningOpportunities() - Skipping group joining for board {brick.name}");
            return;
        }

        // Find all other grabbed bricks (excluding boards)
        BrickBehavior[] allBricks = UnityEngine.Object.FindObjectsOfType<BrickBehavior>();
        float joinThreshold = brick.groupJoinThreshold; // Use BrickBehavior's groupJoinThreshold
        foreach (var otherBrick in allBricks)
        {
            // Skip boards and non-grabbable objects
            if (otherBrick.IsBoard || !otherBrick.IsGrabbable)
            {
                continue;
            }

            if (otherBrick != brick && otherBrick.IsGrabbed)
            {
                // Check if grabbed by different controller
                var thisInteractor = brick.IsGrabbable ? brick.GetComponent<UnityEngine.XR.Interaction.Toolkit.Interactables.XRGrabInteractable>().firstInteractorSelecting : null;
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
                            brick.LogDebug($"CheckForGroupJoiningOpportunities() - Close proximity detected with {otherBrick.name} (distance: {distance})");
                            
                            // Clear snap immunity and enable collisions
                            brick.snapImmunityEndTime = 0f;
                            otherBrick.snapImmunityEndTime = 0f;
                            brick.EnableStudCollisions();
                            otherBrick.EnableStudCollisions();
                            
                            brick.LogDebug($"CheckForGroupJoiningOpportunities() - Enabled joining for {brick.name} and {otherBrick.name}");
                        }
                    }
                }
            }
        }
    }

    public void CheckForUnsnapConditions(IXRSelectInteractor interactor)
    {
        // Boards should not participate in unsnap conditions
        if (brick.IsBoard)
        {
            brick.LogDebug($"CheckForUnsnapConditions() - Skipping unsnap conditions for board {brick.name}");
            return;
        }

        brick.LogDebug($"CheckForUnsnapConditions() - Checking for unsnap conditions with {brick.ConnectedNeighbors.Count} neighbors");
        
        // Check if we are part of a larger group
        if (brick.ConnectedNeighbors.Count > 0)
        {
            // Find all bricks in the connected group
            List<BrickBehavior> allGroupBricks = new List<BrickBehavior>();
            BrickGroupUtils.FindAllConnectedInGroup(brick, allGroupBricks, brick.name);
            brick.LogDebug($"CheckForUnsnapConditions() - Found {allGroupBricks.Count} total bricks in group");
            
            // Find all grabbed bricks in the group (excluding boards)
            List<BrickBehavior> grabbedBricks = new List<BrickBehavior>();
            List<IXRSelectInteractor> grabbedInteractors = new List<IXRSelectInteractor>();
            
            foreach (var groupBrick in allGroupBricks)
            {
                if (!groupBrick.IsBoard && groupBrick.IsGrabbed)
                {
                    grabbedBricks.Add(groupBrick);
                    grabbedInteractors.Add(groupBrick.GetComponent<UnityEngine.XR.Interaction.Toolkit.Interactables.XRGrabInteractable>().firstInteractorSelecting);
                    brick.LogDebug($"CheckForUnsnapConditions() - Found grabbed brick: {groupBrick.name} by interactor: {groupBrick.GetComponent<UnityEngine.XR.Interaction.Toolkit.Interactables.XRGrabInteractable>().firstInteractorSelecting?.transform.name}");
                }
            }
            
            brick.LogDebug($"CheckForUnsnapConditions() - Found {grabbedBricks.Count} grabbed bricks in group");
            
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
                            brick.LogDebug($"CheckForUnsnapConditions() - Different interactors detected: {grabbedInteractors[i]?.transform.name} vs {grabbedInteractors[j]?.transform.name}");
                            break;
                        }
                    }
                    if (differentInteractors) break;
                }
                
                if (differentInteractors)
                {
                    brick.LogDebug($"CheckForUnsnapConditions() - MULTI-CONTROLLER SPLIT CONDITION MET! Splitting group with {grabbedBricks.Count} grabbed bricks");
                    
                    // Perform the split
                    SplitConnectedGroup(grabbedBricks);
                    return;
                }
                else
                {
                    brick.LogDebug($"CheckForUnsnapConditions() - Multiple bricks grabbed by same interactor, no split needed");
                }
            }
            else
            {
                brick.LogDebug($"CheckForUnsnapConditions() - Only one brick grabbed, no split needed");
            }
        }
        else
        {
            brick.LogDebug($"CheckForUnsnapConditions() - No connected neighbors to check");
        }
        
        // Check for potential joining of separate groups
        CheckForGroupJoining(interactor);
    }
    
    // Method to check if separate groups should be joined
    private void CheckForGroupJoining(IXRSelectInteractor interactor)
    {
        // Boards should not participate in group joining
        if (brick.IsBoard)
        {
            brick.LogDebug($"CheckForGroupJoining() - Skipping group joining for board {brick.name}");
            return;
        }

        brick.LogDebug($"CheckForGroupJoining() - Checking for group joining opportunities");
        
        // Find all bricks in the current group
        List<BrickBehavior> allGroupBricks = new List<BrickBehavior>();
        BrickGroupUtils.FindAllConnectedInGroup(brick, allGroupBricks, brick.name);
        brick.LogDebug($"CheckForGroupJoining() - Found {allGroupBricks.Count} bricks in current group");
        
        // Find all other grabbed bricks in the scene (excluding boards)
        List<BrickBehavior> otherGrabbedBricks = new List<BrickBehavior>();
        foreach (var groupBrick in allGroupBricks)
        {
            if (groupBrick != brick && !groupBrick.IsBoard && groupBrick.IsGrabbed)
            {
                otherGrabbedBricks.Add(groupBrick);
            }
        }
        
        brick.LogDebug($"CheckForGroupJoining() - Found {otherGrabbedBricks.Count} other grabbed bricks in group");
        
        // Check each other grabbed brick for potential joining
        foreach (var otherGrabbedBrick in otherGrabbedBricks)
        {
            CheckForGroupJoiningWithBrick(otherGrabbedBrick, interactor);
        }
    }
    
    // Helper method to check for group joining with a specific brick
    private void CheckForGroupJoiningWithBrick(BrickBehavior otherBrick, IXRSelectInteractor interactor)
    {
        // Boards should not participate in group joining
        if (brick.IsBoard || otherBrick.IsBoard)
        {
            brick.LogDebug($"CheckForGroupJoiningWithBrick() - Skipping group joining (one or both are boards)");
            return;
        }

        brick.LogDebug($"CheckForGroupJoiningWithBrick() - Checking for group joining with {otherBrick.name}");
        
        // Check if the other brick is in a different group
        if (!BrickGroupUtils.AreBricksInSameGroup(brick, otherBrick))
        {
            brick.LogDebug($"CheckForGroupJoiningWithBrick() - {otherBrick.name} is in a different group, checking distance");
            
            // Check distance between the groups
            float distance = Vector3.Distance(brick.transform.position, otherBrick.transform.position);
            float joinThreshold = brick.groupJoinThreshold;
            
            if (distance < joinThreshold)
            {
                brick.LogDebug($"CheckForGroupJoiningWithBrick() - Groups are close enough for potential joining (distance: {distance} < {joinThreshold})");
                
                // Clear snap immunity to allow joining
                brick.snapImmunityEndTime = 0f;
                otherBrick.snapImmunityEndTime = 0f;
                
                // Enable collision detection for both bricks
                brick.EnableStudCollisions();
                otherBrick.EnableStudCollisions();
                
                brick.LogDebug($"CheckForGroupJoiningWithBrick() - Cleared snap immunity and enabled collisions for potential joining");
            }
            else
            {
                brick.LogDebug($"CheckForGroupJoiningWithBrick() - Groups too far apart for joining (distance: {distance} >= {joinThreshold})");
            }
        }
        else
        {
            brick.LogDebug($"CheckForGroupJoiningWithBrick() - {otherBrick.name} is already in the same group");
        }
    }
    
    // Helper method to check if two bricks are in the same group
    public bool AreInSameGroup(BrickBehavior brick1, BrickBehavior brick2)
    {
        List<BrickBehavior> group1 = new List<BrickBehavior>();
        List<BrickBehavior> group2 = new List<BrickBehavior>();
        
        BrickGroupUtils.FindAllConnectedInGroup(brick1, group1, brick1.name);
        BrickGroupUtils.FindAllConnectedInGroup(brick2, group2, brick2.name);
        
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
        brick.LogDebug($"CheckForGroupJoiningDuringCollision() - Checking for group joining during collision");
        
        // Check if these studs belong to different groups
        if (ourStud.ParentBrick != null && targetStud.ParentBrick != null)
        {
            // Boards should not participate in group joining
            if (ourStud.ParentBrick.IsBoard || targetStud.ParentBrick.IsBoard)
            {
                brick.LogDebug($"CheckForGroupJoiningDuringCollision() - Skipping group joining (one or both are boards)");
                return;
            }

            if (!BrickGroupUtils.AreBricksInSameGroup(ourStud.ParentBrick, targetStud.ParentBrick))
            {
                brick.LogDebug($"CheckForGroupJoiningDuringCollision() - Studs belong to different groups, checking for joining");
                
                // Check if the bricks are close enough for potential joining
                float distance = Vector3.Distance(ourStud.ParentBrick.transform.position, targetStud.ParentBrick.transform.position);
                float joinThreshold = brick.groupJoinThreshold;
                
                if (distance < joinThreshold)
                {
                    brick.LogDebug($"CheckForGroupJoiningDuringCollision() - Bricks are close enough for potential joining (distance: {distance} < {joinThreshold})");
                    
                    // Clear snap immunity to allow joining
                    ourStud.ParentBrick.snapImmunityEndTime = 0f;
                    targetStud.ParentBrick.snapImmunityEndTime = 0f;
                    
                    // Enable collision detection for both bricks
                    ourStud.ParentBrick.EnableStudCollisions();
                    targetStud.ParentBrick.EnableStudCollisions();
                    
                    brick.LogDebug($"CheckForGroupJoiningDuringCollision() - Cleared snap immunity and enabled collisions for potential joining");
                }
                else
                {
                    brick.LogDebug($"CheckForGroupJoiningDuringCollision() - Bricks too far apart for joining (distance: {distance} >= {joinThreshold})");
                }
            }
            else
            {
                brick.LogDebug($"CheckForGroupJoiningDuringCollision() - Studs belong to same group, no joining needed");
            }
        }
    }

    private void SplitConnectedGroup(List<BrickBehavior> grabbedBricks)
    {
        brick.LogDebug($"SplitConnectedGroup() - Starting group split with {grabbedBricks.Count} grabbed bricks");
        
        // Find all bricks in the original group
        List<BrickBehavior> allGroupBricks = new List<BrickBehavior>();
        BrickGroupUtils.FindAllConnectedInGroup(brick, allGroupBricks, brick.name);
        brick.LogDebug($"SplitConnectedGroup() - Original group has {allGroupBricks.Count} total bricks");
        
        // Create separate groups for each grabbed brick
        List<List<BrickBehavior>> newGroups = new List<List<BrickBehavior>>();
        
        foreach (var grabbedBrick in grabbedBricks)
        {
            brick.LogDebug($"SplitConnectedGroup() - Creating group for grabbed brick: {grabbedBrick.name}");
            
            // Find all bricks that should be in this group
            List<BrickBehavior> groupForThisBrick = new List<BrickBehavior>();
            FindBricksForGroup(grabbedBrick, grabbedBricks, groupForThisBrick);
            
            brick.LogDebug($"SplitConnectedGroup() - Group for {grabbedBrick.name} has {groupForThisBrick.Count} bricks");
            newGroups.Add(groupForThisBrick);
        }
        
        // Now break all connections between different groups
        for (int i = 0; i < newGroups.Count; i++)
        {
            for (int j = i + 1; j < newGroups.Count; j++)
            {
                brick.LogDebug($"SplitConnectedGroup() - Breaking connections between group {i} and group {j}");
                BreakConnectionsBetweenGroups(newGroups[i], newGroups[j]);
            }
        }
        
        // Update masters for each new group
        for (int i = 0; i < newGroups.Count; i++)
        {
            var group = newGroups[i];
            var master = grabbedBricks[i]; // Each grabbed brick becomes master of its group
            
            brick.LogDebug($"SplitConnectedGroup() - Setting {master.name} as master for group {i} with {group.Count} bricks");
            
            foreach (var groupBrick in group)
            {
                groupBrick.UpdateMaster(master);
                
                // Activate snap immunity for all bricks in the split groups
                groupBrick.ActivateSnapImmunity();
                
                // Restore original mass to prevent weight accumulation after separation
                if (groupBrick.GetComponent<Rigidbody>() != null)
                {
                    groupBrick.GetComponent<Rigidbody>().mass = 1.0f;
                    brick.LogDebug($"SplitConnectedGroup() - Restored mass for {groupBrick.name}: mass=1.0f");
                    
                    // IMPORTANT: Restore physics for ALL bricks in the group, not just the master
                    // BUT only if the brick is not a board and not currently grabbed
                    if (!groupBrick.IsBoard && !groupBrick.IsGrabbed)
                    {
                        groupBrick.GetComponent<Rigidbody>().isKinematic = false;
                        groupBrick.GetComponent<Rigidbody>().useGravity = true;
                        brick.LogDebug($"SplitConnectedGroup() - Restored physics for {groupBrick.name}: isKinematic=false, useGravity=true");
                    }
                    else if (groupBrick.IsBoard)
                    {
                        brick.LogDebug($"SplitConnectedGroup() - Skipping physics change for {groupBrick.name} (it's a board)");
                    }
                    else
                    {
                        brick.LogDebug($"SplitConnectedGroup() - Skipping physics change for {groupBrick.name} (currently grabbed by XRGrabInteractable)");
                    }
                }
            }
            
            // Explicitly restore physics for the master brick ONLY if it's not currently grabbed AND is not a board
            if (!master.IsBoard && !master.IsGrabbed)
            {
                master.GetComponent<Rigidbody>().isKinematic = false;
                master.GetComponent<Rigidbody>().useGravity = true;
                brick.LogDebug($"SplitConnectedGroup() - Restored physics for master {master.name}: isKinematic=false, useGravity=true");
            }
            else if (master.IsBoard)
            {
                brick.LogDebug($"SplitConnectedGroup() - Skipping physics change for master {master.name} (it's a board)");
            }
            else
            {
                brick.LogDebug($"SplitConnectedGroup() - Skipping physics change for master {master.name} (currently grabbed by XRGrabInteractable)");
            }
        }
        
        // Move the groups apart to prevent immediate re-snapping
        brick.StartCoroutine(MoveGroupsApart(newGroups));
        
        brick.LogDebug($"SplitConnectedGroup() - Group split complete");
    }
    
    // Coroutine to move split groups apart
    private System.Collections.IEnumerator MoveGroupsApart(List<List<BrickBehavior>> groups)
    {
        brick.LogDebug($"MoveGroupsApart() - Moving {groups.Count} groups apart");
        
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
                brick.LogDebug($"MoveGroupsApart() - Group center: {center}");
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
                    // Skip boards - they should not be moved
                    if (groupBrick.IsBoard)
                    {
                        brick.LogDebug($"MoveGroupsApart() - Skipping movement for board {groupBrick.name}");
                        continue;
                    }

                    if (!groupBrick.IsGrabbed)
                    {
                        Vector3 newPosition = groupBrick.transform.position + (direction * separationDistance);
                        groupBrick.transform.position = newPosition;
                        brick.LogDebug($"MoveGroupsApart() - Moved {groupBrick.name} to {newPosition}");
                    }
                }
            }
            
            brick.LogDebug($"MoveGroupsApart() - Groups moved apart successfully");
        }
    }
    
    private void FindBricksForGroup(BrickBehavior grabbedBrick, List<BrickBehavior> allGrabbedBricks, List<BrickBehavior> groupBricks)
    {
        brick.LogDebug($"FindBricksForGroup() - Finding bricks for group starting from {grabbedBrick.name}");
        
        // Use a breadth-first search to find all bricks that should be in this group
        Queue<BrickBehavior> toVisit = new Queue<BrickBehavior>();
        HashSet<BrickBehavior> visited = new HashSet<BrickBehavior>();
        
        toVisit.Enqueue(grabbedBrick);
        visited.Add(grabbedBrick);
        groupBricks.Add(grabbedBrick);
        
        while (toVisit.Count > 0)
        {
            var currentBrick = toVisit.Dequeue();
            brick.LogDebug($"FindBricksForGroup() - Visiting brick: {currentBrick.name}");
            
            if (currentBrick == null || currentBrick.ConnectedNeighbors == null)
            {
                brick.LogWarning($"FindBricksForGroup() - WARNING: Current brick or neighbors list is null!");
                continue;
            }
            
            foreach (var neighbor in currentBrick.ConnectedNeighbors)
            {
                if (neighbor == null)
                {
                    brick.LogWarning($"FindBricksForGroup() - WARNING: Neighbor is null!");
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
                            brick.LogDebug($"FindBricksForGroup() - Excluding {neighbor.name} (directly grabbed by different interactor)");
                            shouldInclude = false;
                        }
                    }
                    // Boards should not be included in group splitting
                    else if (neighbor.IsBoard)
                    {
                        brick.LogDebug($"FindBricksForGroup() - Excluding {neighbor.name} (it's a board)");
                        shouldInclude = false;
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
                                        brick.LogDebug($"FindBricksForGroup() - Excluding {neighbor.name} (connected to {grabbedBrickCheck.name} which is grabbed by different interactor)");
                                        shouldInclude = false;
                                        connectedToDifferentController = true;
                                        break;
                                    }
                                }
                            }
                        }
                        
                        if (!connectedToDifferentController)
                        {
                            brick.LogDebug($"FindBricksForGroup() - Including {neighbor.name} (not connected to different controller)");
                        }
                    }
                    
                    if (shouldInclude)
                    {
                        brick.LogDebug($"FindBricksForGroup() - Including {neighbor.name} in group");
                        visited.Add(neighbor);
                        groupBricks.Add(neighbor);
                        toVisit.Enqueue(neighbor);
                    }
                }
            }
        }
        
        brick.LogDebug($"FindBricksForGroup() - Group for {grabbedBrick.name} contains {groupBricks.Count} bricks");
    }
    
    private void BreakConnectionsBetweenGroups(List<BrickBehavior> group1, List<BrickBehavior> group2)
    {
        brick.LogDebug($"BreakConnectionsBetweenGroups() - Breaking connections between {group1.Count} and {group2.Count} bricks");
        
        foreach (var brick1 in group1)
        {
            foreach (var brick2 in group2)
            {
                // Check if these bricks are directly connected
                if (brick1.ConnectedNeighbors.Contains(brick2))
                {
                    brick.LogDebug($"BreakConnectionsBetweenGroups() - Breaking connection between {brick1.name} and {brick2.name}");
                    
                    // Remove the connection
                    brick1.RemoveNeighbor(brick2);
                    brick2.RemoveNeighbor(brick1);
                    
                    // Destroy the joint
                    if (brick1.Joint != null && brick1.Joint.connectedBody == brick2.GetComponent<Rigidbody>())
                    {
                        brick.LogDebug($"BreakConnectionsBetweenGroups() - Destroying joint on {brick1.name}");
                        Object.DestroyImmediate(brick1.Joint);
                        // The joint destruction will handle clearing the reference
                    }
                    else if (brick2.Joint != null && brick2.Joint.connectedBody == brick1.GetComponent<Rigidbody>())
                    {
                        brick.LogDebug($"BreakConnectionsBetweenGroups() - Destroying joint on {brick2.name}");
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
                                brick.LogDebug($"BreakConnectionsBetweenGroups() - Destroying untracked joint on {brick1.name}");
                                Object.DestroyImmediate(joint);
                                break;
                            }
                        }
                        
                        FixedJoint[] joints2 = brick2.GetComponents<FixedJoint>();
                        foreach (var joint in joints2)
                        {
                            if (joint.connectedBody == brick1.GetComponent<Rigidbody>())
                            {
                                brick.LogDebug($"BreakConnectionsBetweenGroups() - Destroying untracked joint on {brick2.name}");
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
        
        brick.LogDebug($"BreakConnectionsBetweenGroups() - Connection breaking complete");
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
                brick.LogWarning($"ValidateJointDestruction() - WARNING: Joint still exists on {brick1.name}!");
                jointStillExists = true;
                break;
            }
        }
        
        foreach (var joint in joints2)
        {
            if (joint.connectedBody == brick1.GetComponent<Rigidbody>())
            {
                brick.LogWarning($"ValidateJointDestruction() - WARNING: Joint still exists on {brick2.name}!");
                jointStillExists = true;
                break;
            }
        }
        
        if (!jointStillExists)
        {
            brick.LogDebug($"ValidateJointDestruction() - Joint destruction validated successfully");
        }
    }

    public void Cleanup()
    {
        // No specific cleanup needed for group operations
        brick.LogDebug($"Cleanup() - Group operations cleanup complete");
    }
} 
