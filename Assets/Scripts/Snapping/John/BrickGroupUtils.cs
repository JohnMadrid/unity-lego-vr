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
        if (brick == null || visited.Contains(brick))
        {
            brick.LogDebug($"FindAllConnectedInGroup() - DEBUG: Brick is null or already visited, returning", false);
            return;
        }
        
        // Boards should not participate in group traversal at all
        if (brick.IsBoard)
        {
            brick.LogDebug($"FindAllConnectedInGroup() - DEBUG: Skipping board {brick.name} - boards don't participate in group management", false);
            return;
        }
        
        brick.LogDebug($"FindAllConnectedInGroup() - DEBUG: Visiting brick: {brick.name}", false);
        
        visited.Add(brick);
        
        brick.LogDebug($"FindAllConnectedInGroup() - DEBUG: Added {brick.name} to visited list. Total visited: {visited.Count}", false);
        
        // Recursively check all connected neighbors
        foreach (var neighbor in brick.ConnectedNeighbors)
        {
            // Skip boards - they should not be part of group traversal
            if (neighbor.IsBoard)
            {
                brick.LogDebug($"FindAllConnectedInGroup() - DEBUG: Skipping board neighbor: {neighbor.name}", false);
                continue;
            }
            
            brick.LogDebug($"FindAllConnectedInGroup() - DEBUG: Recursively checking neighbor of {brick.name}: {neighbor.name}", false);
            FindAllConnectedInGroup(neighbor, visited, context);
        }
        
        brick.LogDebug($"FindAllConnectedInGroup() - DEBUG: Finished visiting all neighbors of {brick.name}", false);
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
        
        List<BrickBehavior> group1 = new List<BrickBehavior>();
        List<BrickBehavior> group2 = new List<BrickBehavior>();
        
        // Find all connected bricks for both groups
        FindAllConnectedInGroup(brick1, group1);
        FindAllConnectedInGroup(brick2, group2);
        
        // Check if there's any overlap between the groups
        foreach (var brick in group1)
        {
            if (group2.Contains(brick))
            {
                return true; // They're in the same group
            }
        }
        
        return false; // They're in different groups
    }
} 
