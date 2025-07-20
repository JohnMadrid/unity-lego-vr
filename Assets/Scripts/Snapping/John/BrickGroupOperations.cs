using UnityEngine;
using System.Collections.Generic;
using UnityEngine.XR.Interaction.Toolkit.Interactors;
using System.Linq;

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

    // Refactored: Use exclude list for correct group splitting
    public void SplitConnectedGroup(List<BrickBehavior> grabbedBricks)
    {
        brick.LogDebug($"SplitConnectedGroup() - Starting group split with {grabbedBricks.Count} grabbed bricks");
        List<List<BrickBehavior>> newGroups = new List<List<BrickBehavior>>();
        HashSet<BrickBehavior> claimedBricks = new HashSet<BrickBehavior>();
        foreach (var grabbed in grabbedBricks)
        {
            if (claimedBricks.Contains(grabbed))
            {
                continue;
            }
            var exclude = grabbedBricks.Where(b => b != grabbed).ToList();
            List<BrickBehavior> group = new List<BrickBehavior>();
            FindBricksForGroup(grabbed, exclude, claimedBricks, group);
            brick.LogDebug($"SplitConnectedGroup() - Group for {grabbed.name} has {group.Count} bricks");
            newGroups.Add(group);
            foreach (var newBrick in group)
            {
                claimedBricks.Add(newBrick);
            }
        }
        for (int i = 0; i < newGroups.Count; i++)
        {
            for (int j = i + 1; j < newGroups.Count; j++)
            {
                BreakConnectionsBetweenGroups(newGroups[i], newGroups[j]);
            }
        }

        // --- NEW FIX ---
        // Before assigning new masters, reset the original master of the bricks that will BECOME masters.
        // This prevents them from carrying over the old group's original master.
        foreach (var master in grabbedBricks)
        {
            if (master.connectionManager != null)
            {
                master.connectionManager.m_OriginalMaster = master;
                brick.LogDebug($"SplitConnectedGroup() - Resetting original master for new master: {master.name}");
            }
        }

        for (int i = 0; i < newGroups.Count; i++)
        {
            var master = grabbedBricks[i];
            brick.LogDebug($"SplitConnectedGroup() - Setting {master.name} as master for group {i} with {newGroups[i].Count} bricks");
            foreach (var b in newGroups[i])
            {
                b.UpdateMaster(master);
            }
        }
        MoveGroupsApart(newGroups);
        brick.LogDebug($"SplitConnectedGroup() - Group split complete");
    }
    
    public void MoveGrabbedGroupsApart()
    {
        var allBricks = UnityEngine.Object.FindObjectsOfType<BrickBehavior>();
        var grabbedBricks = allBricks.Where(b => b.IsGrabbed).ToList();
        if (grabbedBricks.Count < 2) return;

        var groups = new List<List<BrickBehavior>>();
        var claimed = new HashSet<BrickBehavior>();
        foreach (var grabbed in grabbedBricks)
        {
            if (claimed.Contains(grabbed)) continue;
            var group = new List<BrickBehavior>();
            BrickGroupUtils.FindAllConnectedInGroup(grabbed, group, brick.name);
            groups.Add(group);
            foreach (var b in group)
            {
                claimed.Add(b);
            }
        }
        
        if (groups.Count > 1)
        {
            MoveGroupsApart(groups);
        }
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
    
    // Refactored: Add excludeBricks parameter
    private void FindBricksForGroup(BrickBehavior grabbedBrick, List<BrickBehavior> excludeBricks, HashSet<BrickBehavior> claimedBricks, List<BrickBehavior> groupBricks)
    {
        brick.LogDebug($"FindBricksForGroup() - Finding bricks for group starting from {grabbedBrick.name}");
        Queue<BrickBehavior> toVisit = new Queue<BrickBehavior>();
        HashSet<BrickBehavior> visited = new HashSet<BrickBehavior>();
        toVisit.Enqueue(grabbedBrick);
        visited.Add(grabbedBrick);
        groupBricks.Add(grabbedBrick);
        while (toVisit.Count > 0)
        {
            var current = toVisit.Dequeue();
            foreach (var neighbor in current.ConnectedNeighbors)
            {
                // Stop traversal if neighbor is another grabbed brick
                if (excludeBricks.Contains(neighbor))
                {
                    brick.LogDebug($"FindBricksForGroup() - Skipping {neighbor.name} (in exclude list)");
                    continue;
                }
                // Stop traversal if neighbor has already been claimed by another group
                if (claimedBricks.Contains(neighbor))
                {
                    brick.LogDebug($"FindBricksForGroup() - Skipping {neighbor.name} (already claimed)");
                    continue;
                }
                if (!visited.Contains(neighbor))
                {
                    toVisit.Enqueue(neighbor);
                    visited.Add(neighbor);
                    groupBricks.Add(neighbor);
                    brick.LogDebug($"FindBricksForGroup() - Including {neighbor.name} in group");
                }
            }
        }
        brick.LogDebug($"FindBricksForGroup() - Group for {grabbedBrick.name} contains {groupBricks.Count} bricks");
    }
    
    private void BreakConnectionsBetweenGroups(List<BrickBehavior> groupA, List<BrickBehavior> groupB)
    {
        brick.LogDebug($"--- BreakConnectionsBetweenGroups START ---");
        brick.LogDebug($"Group A ({groupA.Count} bricks): [{string.Join(", ", groupA.Select(b => b.name))}]");
        brick.LogDebug($"Group B ({groupB.Count} bricks): [{string.Join(", ", groupB.Select(b => b.name))}]");

        foreach (var brickA in groupA)
        {
            // Create a copy for safe iteration while modifying the original list
            foreach (var neighborOfA in brickA.ConnectedNeighbors.ToList())
            {
                brick.LogDebug($"Checking connection: {brickA.name} -> {neighborOfA.name}");

                if (groupB.Contains(neighborOfA))
                {
                    brick.LogDebug($"  >>> Found connection to break: {brickA.name} (in Group A) is connected to {neighborOfA.name} (in Group B)");

                    // Clear the logical stud connections before removing neighbors
                    foreach (var studA in brickA.studManager.AllStuds)
                    {
                        if (studA.ConnectedStud != null && studA.ConnectedStud.ParentBrick == neighborOfA)
                        {
                            brick.LogDebug($"  Clearing stud connection between {brickA.name}'s stud {studA.name} and {neighborOfA.name}'s stud {studA.ConnectedStud.name}");
                            studA.ConnectedStud.ConnectedStud = null;
                            studA.ConnectedStud = null;
                            break; // Assume only one connection path between two bricks
                        }
                    }

                    // Remove logical neighbor connection
                    brickA.RemoveNeighbor(neighborOfA);
                    neighborOfA.RemoveNeighbor(brickA);

                    // Find and destroy the physical FixedJoint, regardless of which brick it's on
                    bool jointDestroyed = false;
                    
                    // Check if joint is on brickA
                    foreach (var joint in brickA.GetComponents<FixedJoint>())
                    {
                        if (joint.connectedBody != null)
                        {
                            var connectedBehavior = joint.connectedBody.GetComponent<BrickBehavior>();
                            if (connectedBehavior != null && groupB.Contains(connectedBehavior))
                            {
                                brick.LogDebug($"  Destroying joint on {brickA.name} connected to {connectedBehavior.name} in other group");
                                UnityEngine.Object.Destroy(joint);
                                jointDestroyed = true;
                                break;
                            }
                        }
                    }

                    // If not found, check if joint is on neighborOfA
                    if (!jointDestroyed)
                    {
                        foreach (var joint in neighborOfA.GetComponents<FixedJoint>())
                        {
                            if (joint.connectedBody != null)
                            {
                                var connectedBehavior = joint.connectedBody.GetComponent<BrickBehavior>();
                                if (connectedBehavior != null && groupA.Contains(connectedBehavior))
                                {
                                    brick.LogDebug($"  Destroying joint on {neighborOfA.name} connected to {connectedBehavior.name} in other group");
                                    UnityEngine.Object.Destroy(joint);
                                    break;
                                }
                            }
                        }
                    }
                }
                else
                {
                    brick.LogDebug($"  Connection OK: {neighborOfA.name} is not in Group B.");
                }
            }
        }
        brick.LogDebug($"--- BreakConnectionsBetweenGroups END ---");
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
