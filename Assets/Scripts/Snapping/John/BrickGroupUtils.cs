// BrickGroupUtils.cs
using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// Static utility class for brick group traversal operations.
/// Provides shared implementations for finding connected bricks in groups.
/// </summary>
public static class BrickGroupUtils
{
    /// <summary>
    /// Finds all connected bricks in a group using recursive traversal.
    /// This is the basic implementation used by most managers.
    /// </summary>
    /// <param name="brick">The starting brick</param>
    /// <param name="visited">List to store all connected bricks</param>
    public static void FindAllConnected(BrickBehavior brick, List<BrickBehavior> visited)
    {
        if (brick == null || visited.Contains(brick))
        {
            return;
        }
        
        visited.Add(brick);
        
        // Recursively check all connected neighbors
        foreach (var neighbor in brick.ConnectedNeighbors)
        {
            FindAllConnected(neighbor, visited);
        }
    }
    
    /// <summary>
    /// Finds all connected bricks in a group with additional validation.
    /// This version includes null checks and group validation.
    /// </summary>
    /// <param name="brick">The starting brick</param>
    /// <param name="visited">List to store all connected bricks</param>
    /// <param name="context">Optional context string for debugging</param>
    public static void FindAllConnectedInGroup(BrickBehavior brick, List<BrickBehavior> visited, string context = "")
    {
        // Use a queue for non-recursive traversal to avoid stack overflow on large structures.
        Queue<BrickBehavior> toVisit = new Queue<BrickBehavior>();
        if (brick == null || visited.Contains(brick))
        {
            return;
        }

        // Start traversal from the initial brick. Boards cannot be the starting point of a group.
        if (brick.IsBoard)
        {
            brick.LogDebug($"FindAllConnectedInGroup() - DEBUG: Cannot start group traversal from a board.", false);
            return;
        }

        toVisit.Enqueue(brick);
        visited.Add(brick);
        brick.LogDebug($"FindAllConnectedInGroup() - START: Queued initial brick {brick.name}", false);

        while (toVisit.Count > 0)
        {
            BrickBehavior current = toVisit.Dequeue();
            brick.LogDebug($"FindAllConnectedInGroup() - Visiting: {current.name}", false);

            foreach (var neighbor in current.ConnectedNeighbors)
            {
                if (neighbor == null || visited.Contains(neighbor))
                {
                    continue;
                }

                visited.Add(neighbor);
                brick.LogDebug($"FindAllConnectedInGroup() - Found neighbor: {neighbor.name}. Added to group.", false);

                // CRITICAL LOGIC: If the neighbor is a board, add it to the group but DO NOT
                // continue traversal from it. This prevents merging separate groups on the same board.
                if (neighbor.IsBoard)
                {
                    brick.LogDebug($"FindAllConnectedInGroup() - Neighbor is a board ({neighbor.name}). Stopping traversal along this path.", false);
                }
                else
                {
                    // If it's a regular brick, add it to the queue to visit its neighbors.
                    toVisit.Enqueue(neighbor);
                    brick.LogDebug($"FindAllConnectedInGroup() - Queued neighbor brick: {neighbor.name}", false);
                }
            }
        }
        brick.LogDebug($"FindAllConnectedInGroup() - END: Traversal complete. Group size: {visited.Count}", false);
    }
    
    /// <summary>
    /// Checks if two bricks are in the same connected group.
    /// </summary>
    /// <param name="brick1">First brick</param>
    /// <param name="brick2">Second brick</param>
    /// <returns>True if bricks are in the same group</returns>
    public static bool AreBricksInSameGroup(BrickBehavior brick1, BrickBehavior brick2)
    {
        if (brick1 == null || brick2 == null) return false;
        if (brick1 == brick2) return true;

        // If one of the bricks is a board, the only way they are in the "same group"
        // is if the other brick is directly connected to it or part of a group that is.
        // We can check this by traversing from the non-board brick and seeing if the board is found.
        if (brick2.IsBoard)
        {
            List<BrickBehavior> group1 = new List<BrickBehavior>();
            FindAllConnectedInGroup(brick1, group1, "IsSameGroupCheck");
            return group1.Contains(brick2);
        }
        if (brick1.IsBoard)
        {
            List<BrickBehavior> group2 = new List<BrickBehavior>();
            FindAllConnectedInGroup(brick2, group2, "IsSameGroupCheck");
            return group2.Contains(brick1);
        }

        // For two regular bricks, we find the entire group of the first brick
        // and check if the second brick is a member of it. The traversal handles
        // finding bricks connected through intermediaries.
        List<BrickBehavior> groupOfBrick1 = new List<BrickBehavior>();
        FindAllConnectedInGroup(brick1, groupOfBrick1, "IsSameGroupCheck");
        return groupOfBrick1.Contains(brick2);
    }
} 
