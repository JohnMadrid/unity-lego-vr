using UnityEngine;
using System.Collections.Generic;
using System.Collections;

public class BrickSnappingSystem
{
    private readonly BrickBehavior brick;
    private readonly BrickStudManager studManager;
    
    // Missing variables that were in the original BrickBehavior
    private Vector3 targetSnapPosition;
    private Quaternion targetSnapRotation;
    private BrickBehavior snapTargetBrick;

    public BrickSnappingSystem(BrickBehavior brick, BrickStudManager studManager)
    {
        this.brick = brick;
        this.studManager = studManager;
    }

    // This is the core method, called by a Stud when it collides with another valid stud.
    public void RequestSnap(Stud ourStud, Stud targetStud)
    {
        Debug.Log($"[{brick.name}] RequestSnap() - Request from stud '{ourStud.name}' to target stud '{targetStud.name}'");
        
        if (brick.GetComponent<BrickBehavior>().isSnapping)
        {
            Debug.Log($"[{brick.name}] RequestSnap() - Already snapping, ignoring request");
            return;
        }

        BrickBehavior targetBrick = targetStud.ParentBrick;
        if (targetBrick == null)
        {
            Debug.LogWarning($"[{brick.name}] RequestSnap() - WARNING: Target stud has no parent brick");
            return;
        }
        
        if (targetBrick == brick)
        {
            Debug.Log($"[{brick.name}] RequestSnap() - Ignoring snap to self");
            return;
        }

        // Check if we're already connected to this brick
        if (IsAlreadyConnectedTo(targetBrick))
        {
            Debug.Log($"[{brick.name}] RequestSnap() - Already connected to target brick, ignoring");
            return;
        }

        Debug.Log($"[{brick.name}] RequestSnap() - Target brick: {targetBrick.name}");

        // DEBUG: Log positions of both bricks and studs
        Debug.Log($"[{brick.name}] RequestSnap() - DEBUG: Grabbed brick position: {brick.transform.position}, rotation: {brick.transform.rotation.eulerAngles}");
        Debug.Log($"[{brick.name}] RequestSnap() - DEBUG: Target brick position: {targetBrick.transform.position}, rotation: {targetBrick.transform.rotation.eulerAngles}");
        Debug.Log($"[{brick.name}] RequestSnap() - DEBUG: Our stud world position: {ourStud.transform.position}");
        Debug.Log($"[{brick.name}] RequestSnap() - DEBUG: Target stud world position: {targetStud.transform.position}");
        Debug.Log($"[{brick.name}] RequestSnap() - DEBUG: Distance between studs: {Vector3.Distance(ourStud.transform.position, targetStud.transform.position):F6}");
        Debug.Log($"[{brick.name}] RequestSnap() - DEBUG: Our stud local position: {ourStud.transform.localPosition}");
        Debug.Log($"[{brick.name}] RequestSnap() - DEBUG: Target stud local position: {targetStud.transform.localPosition}");

        // Validate stud compatibility
        if (ourStud.Type == targetStud.Type)
        {
            Debug.LogWarning($"[{brick.name}] RequestSnap() - WARNING: Cannot snap {ourStud.Type} to {targetStud.Type}!");
            return;
        }

        // Check if there are multiple potential snap points
        List<Stud> potentialSnapPoints = FindPotentialSnapPoints(ourStud, targetBrick);
        Debug.Log($"[{brick.name}] RequestSnap() - Found {potentialSnapPoints.Count} potential snap points");

        if (potentialSnapPoints.Count == 0)
        {
            Debug.Log($"[{brick.name}] RequestSnap() - No valid snap points found");
            return;
        }

        // For multiple stud connections (like 2x1 bricks), we need to find the best overall alignment
        if (potentialSnapPoints.Count > 1)
        {
            Debug.Log($"[{brick.name}] RequestSnap() - Multiple snap points detected, finding best overall alignment");
            
            // Find the best alignment that maximizes the number of connecting studs
            Stud bestTargetStud = FindBestMultiStudAlignment(ourStud, potentialSnapPoints, targetBrick);
            if (bestTargetStud != null)
            {
                targetStud = bestTargetStud;
                Debug.Log($"[{brick.name}] RequestSnap() - Selected best multi-stud alignment: {targetStud.name}");
            }
            else
            {
                Debug.LogWarning($"[{brick.name}] RequestSnap() - WARNING: Could not find good multi-stud alignment, falling back to single stud");
                targetStud = ChooseBestSnapPoint(ourStud, potentialSnapPoints);
            }
        }
        else
        {
            targetStud = potentialSnapPoints[0];
            Debug.Log($"[{brick.name}] RequestSnap() - Using single snap point: {targetStud.name}");
        }

        // Calculate snap position and rotation
        CalculateSnapTransform(ourStud, targetStud, targetBrick);

        // Disable physics during snap
        if (brick.GetComponent<Rigidbody>() != null)
        {
            brick.GetComponent<Rigidbody>().isKinematic = true;
            brick.GetComponent<Rigidbody>().useGravity = false;
            Debug.Log($"[{brick.name}] RequestSnap() - Disabled physics for snap animation");
        }

        // Set snapping state
        brick.SetSnappingState(true, targetSnapPosition, targetSnapRotation, targetBrick);
        
        // IMPORTANT: Store the target brick reference for finalization
        snapTargetBrick = targetBrick;

        // Update stud states to show snapping
        ourStud.SetSnapping(true);
        targetStud.SetSnapping(true);

        Debug.Log($"[{brick.name}] RequestSnap() - Snap initiated. Target position: {targetSnapPosition}, Target rotation: {targetSnapRotation.eulerAngles}");
    }

    private void CalculateSnapTransform(Stud ourStud, Stud targetStud, BrickBehavior targetBrick)
    {
        Debug.Log($"[{brick.name}] CalculateSnapTransform() - Calculating snap transform for {ourStud.Type} to {targetStud.Type}");
        Debug.Log($"[{brick.name}] CalculateSnapTransform() - Our brick position: {brick.transform.position}");
        Debug.Log($"[{brick.name}] CalculateSnapTransform() - Target brick position: {targetBrick.transform.position}");

        // Validate stud compatibility
        if (ourStud.Type == targetStud.Type)
        {
            Debug.LogWarning($"[{brick.name}] CalculateSnapTransform() - WARNING: Cannot snap {ourStud.Type} to {targetStud.Type}!");
            return;
        }

        // Get the world positions of the studs
        Vector3 ourStudWorldPos = ourStud.transform.position;
        Vector3 targetStudWorldPos = targetStud.transform.position;
        
        Debug.Log($"[{brick.name}] CalculateSnapTransform() - Stud positions: Our={ourStudWorldPos}, Target={targetStudWorldPos}");

        // For rotation, we want our brick to have the same rotation as the target brick
        // This ensures proper alignment of all studs
        Quaternion targetBrickRotation = targetBrick.transform.rotation;
        Debug.Log($"[{brick.name}] CalculateSnapTransform() - Target brick rotation (euler): {targetBrickRotation.eulerAngles}");

        // Calculate the offset from our brick's center to our stud (in world space)
        Vector3 ourBrickToStudOffset = ourStudWorldPos - brick.transform.position;
        Debug.Log($"[{brick.name}] CalculateSnapTransform() - Our brick to stud world offset: {ourBrickToStudOffset}");

        // Apply the target rotation to our offset to get the final position
        Vector3 rotatedOffset = targetBrickRotation * ourBrickToStudOffset;
        Debug.Log($"[{brick.name}] CalculateSnapTransform() - Rotated offset: {rotatedOffset}");
        
        // Calculate the final position: target stud position minus the rotated offset
        // This will position our brick so that our stud aligns with the target stud
        Vector3 finalPosition = targetStudWorldPos - rotatedOffset;
        Debug.Log($"[{brick.name}] CalculateSnapTransform() - Final position calculation: {targetStudWorldPos} - {rotatedOffset} = {finalPosition}");
        
        // IMPORTANT: Use the proven approach from ValidateAndCorrectAlignment
        // Calculate the correction needed to align our stud with the target stud
        Vector3 correction = targetStudWorldPos - ourStudWorldPos;
        
        // Apply the correction to the final position
        finalPosition += correction;
        
        Debug.Log($"[{brick.name}] CalculateSnapTransform() - Our stud world position: {ourStudWorldPos}");
        Debug.Log($"[{brick.name}] CalculateSnapTransform() - Target stud world position: {targetStudWorldPos}");
        Debug.Log($"[{brick.name}] CalculateSnapTransform() - Applying correction: {correction}");
        Debug.Log($"[{brick.name}] CalculateSnapTransform() - Final position after correction: {finalPosition}");
        
        // Add a small offset to prevent exact overlap that might cause physics issues
        Vector3 snapOffset = Vector3.up * 0.001f; // 1mm offset upward
        finalPosition += snapOffset;
        Debug.Log($"[{brick.name}] CalculateSnapTransform() - Added snap offset: {snapOffset}");
        Debug.Log($"[{brick.name}] CalculateSnapTransform() - Final position with offset: {finalPosition}");
        
        Debug.Log($"[{brick.name}] CalculateSnapTransform() - Final position: {finalPosition}, Rotation: {targetBrickRotation.eulerAngles}");

        targetSnapPosition = finalPosition;
        targetSnapRotation = targetBrickRotation;
        
        Debug.Log($"[{brick.name}] CalculateSnapTransform() - Set targetSnapPosition: {targetSnapPosition}");
        Debug.Log($"[{brick.name}] CalculateSnapTransform() - Set targetSnapRotation: {targetSnapRotation.eulerAngles}");
        
        // Debug visualization - draw lines to show the snap alignment
        Debug.DrawLine(ourStudWorldPos, targetStudWorldPos, Color.green, 2f);
        Debug.DrawLine(brick.transform.position, finalPosition, Color.red, 2f);
        Debug.Log($"[{brick.name}] CalculateSnapTransform() - Debug lines drawn: Green=stud alignment, Red=brick movement");
    }
    
    // Method to snap rotation to the closest 90-degree alignment around the brick's own Z-axis
    private Quaternion SnapToClosest90DegreeRotation(Quaternion currentRotation, BrickBehavior targetBrick)
    {
        // Get the target brick's rotation
        Quaternion targetRotation = targetBrick.transform.rotation;
        
        // For LEGO-like bricks, we want to:
        // 1. Align the brick's X and Y axes with the target brick (so they're on the same plane)
        // 2. Preserve the brick's own Z-axis rotation and snap it to 90-degree increments
        
        // First, extract the Z-axis rotation from the current brick (this is what we want to preserve and snap)
        Vector3 currentEuler = currentRotation.eulerAngles;
        float currentZ = currentEuler.z;
        
        // Snap the Z rotation to the closest 90 degrees
        float snappedZ = Mathf.Round(currentZ / 90f) * 90f;
        
        // Create a rotation that aligns with the target brick's X and Y axes, but uses our snapped Z rotation
        Vector3 targetEuler = targetRotation.eulerAngles;
        Quaternion finalRotation = Quaternion.Euler(targetEuler.x, targetEuler.y, snappedZ);
        
        Debug.Log($"[{brick.name}] SnapToClosest90DegreeRotation() - Current Z: {currentZ:F1}° → {snappedZ:F1}°");
        Debug.Log($"[{brick.name}] SnapToClosest90DegreeRotation() - Target rotation: {targetEuler}, Final rotation: {finalRotation.eulerAngles}");
        
        return finalRotation;
    }

    public void FinalizeSnap()
    {
        Debug.Log($"[{brick.name}] FinalizeSnap() - Finalizing snap connection");
        Debug.Log($"[{brick.name}] FinalizeSnap() - DEBUG: isSnapping before finalization: {brick.isSnapping}");
        
        // DEBUG: Log final position and physics state
        Debug.Log($"[{brick.name}] FinalizeSnap() - DEBUG: Final brick position: {brick.transform.position}");
        Debug.Log($"[{brick.name}] FinalizeSnap() - DEBUG: Final brick rotation: {brick.transform.rotation.eulerAngles}");
        
        // ADDITIONAL DEBUG: Check if the position matches what was calculated
        Debug.Log($"[{brick.name}] FinalizeSnap() - DEBUG: Expected target position was: {targetSnapPosition}");
        Debug.Log($"[{brick.name}] FinalizeSnap() - DEBUG: Position difference: {Vector3.Distance(brick.transform.position, targetSnapPosition):F6}");
        
        var rb = brick.GetComponent<Rigidbody>();
        if (rb != null)
        {
            Debug.Log($"[{brick.name}] FinalizeSnap() - DEBUG: Physics before finalization - isKinematic: {rb.isKinematic}, useGravity: {rb.useGravity}");
        }
        
        brick.SetSnappingState(false, Vector3.zero, Quaternion.identity, null);
        Debug.Log($"[{brick.name}] FinalizeSnap() - DEBUG: isSnapping after finalization: {brick.isSnapping}");

        // Reset stud states after snap is complete
        foreach (var stud in studManager.TopStuds)
        {
            stud.SetSnapping(false);
        }
        foreach (var stud in studManager.BottomStuds)
        {
            stud.SetSnapping(false);
        }
        
        Debug.Log($"[{brick.name}] FinalizeSnap() - Final position: {brick.transform.position}, Rotation: {brick.transform.rotation.eulerAngles}");

        // Store target brick reference before clearing it
        BrickBehavior targetBrick = snapTargetBrick;

        // Check if we have a valid target brick
        if (targetBrick == null)
        {
            Debug.LogWarning($"[{brick.name}] FinalizeSnap() - WARNING: Target brick is null, cannot finalize snap");
            return;
        }

        // Determine the ultimate master of the target brick's group
        BrickBehavior targetMaster = targetBrick.MasterBrick;

        // Create a Fixed Joint to connect this brick to the target
        FixedJoint joint = brick.gameObject.AddComponent<FixedJoint>();
        joint.connectedBody = targetBrick.GetComponent<Rigidbody>();
        joint.breakForce = float.PositiveInfinity;
        joint.breakTorque = float.PositiveInfinity;
        
        // Configure joint for better stability with dynamic objects
        joint.enableCollision = false; // Prevent collision between connected objects
        joint.enablePreprocessing = true; // Enable preprocessing for better stability
        
        // IMPORTANT: Configure joint for maximum rigidity
        joint.anchor = Vector3.zero; // Anchor at the center of the joint
        joint.axis = Vector3.zero; // No specific axis constraint
        
        // IMPORTANT: Store the joint in the connection manager for proper group tracking
        brick.SetJoint(joint);
        Debug.Log($"[{brick.name}] FinalizeSnap() - Stored joint in connection manager: {joint} connecting to {targetBrick.name}");
        
        // Set mass properties to make the connection more rigid
        if (brick.GetComponent<Rigidbody>() != null)
        {
            var brickRb = brick.GetComponent<Rigidbody>();
            Debug.Log($"[{brick.name}] FinalizeSnap() - DEBUG: Physics before joint creation - isKinematic: {brickRb.isKinematic}, useGravity: {brickRb.useGravity}");
            
            // Increase mass slightly to make the brick more stable
            brickRb.mass = Mathf.Max(brickRb.mass, 1.0f);
            // Set drag and angular drag from BrickBehavior
            brickRb.linearDamping = brick.brickDrag;
            brickRb.angularDamping = brick.brickAngularDrag;
            
            // IMPORTANT: Restore physics after snap animation
            brickRb.isKinematic = false;
            brickRb.useGravity = true;
            Debug.Log($"[{brick.name}] FinalizeSnap() - Restored physics: isKinematic=false, useGravity=true");
            Debug.Log($"[{brick.name}] FinalizeSnap() - DEBUG: Physics after restoration - isKinematic: {brickRb.isKinematic}, useGravity: {brickRb.useGravity}, mass: {brickRb.mass}");
        }
        
        // Also adjust the connected body's properties
        if (targetBrick.GetComponent<Rigidbody>() != null)
        {
            targetBrick.GetComponent<Rigidbody>().mass = Mathf.Max(targetBrick.GetComponent<Rigidbody>().mass, 1.0f);
            targetBrick.GetComponent<Rigidbody>().linearDamping = brick.brickDrag;
            targetBrick.GetComponent<Rigidbody>().angularDamping = brick.brickAngularDrag;
        }

        // This brick is no longer its own master. It's now a "slave" to the other group
        brick.UpdateMaster(targetMaster);

        // Update the logical connection graph
        brick.ConnectedNeighbors.Add(targetBrick);
        targetBrick.ConnectedNeighbors.Add(brick);

        // Clear the reference
        snapTargetBrick = null;

        // Validate and correct final alignment (now using stored reference)
        ValidateAndCorrectAlignment(targetBrick);

        // Clear any residual velocities to prevent oscillation
        if (brick.GetComponent<Rigidbody>() != null)
        {
            brick.GetComponent<Rigidbody>().linearVelocity = Vector3.zero;
            brick.GetComponent<Rigidbody>().angularVelocity = Vector3.zero;
            Debug.Log($"[{brick.name}] FinalizeSnap() - Cleared velocities to prevent oscillation");
        }
        
        // IMPORTANT: Don't call physics managers during snap finalization
        // They will be called by the normal OnGrabReleased flow after snap is complete
        // This prevents conflicts between snap physics and normal physics management
        
        // Start a coroutine to stabilize the group after a short delay
        brick.StartCoroutine(StabilizeGroupAfterSnap());
        
        Debug.Log($"[{brick.name}] FinalizeSnap() - Snap finalization complete");
    }

    // Method to validate and correct alignment after snapping
    private void ValidateAndCorrectAlignment(BrickBehavior targetBrick)
    {
        Debug.Log($"[{brick.name}] ValidateAndCorrectAlignment() - Validating final alignment");
        
        // Find the closest stud pair to validate alignment
        float minDistance = float.MaxValue;
        Stud closestOurStud = null;
        Stud closestTargetStud = null;
        
        // Check all our studs against all target studs
        foreach (var ourStud in studManager.TopStuds)
        {
            foreach (var targetStud in targetBrick.BottomStuds)
            {
                float distance = Vector3.Distance(ourStud.transform.position, targetStud.transform.position);
                if (distance < minDistance)
                {
                    minDistance = distance;
                    closestOurStud = ourStud;
                    closestTargetStud = targetStud;
                }
            }
        }
        
        foreach (var ourStud in studManager.BottomStuds)
        {
            foreach (var targetStud in targetBrick.TopStuds)
            {
                float distance = Vector3.Distance(ourStud.transform.position, targetStud.transform.position);
                if (distance < minDistance)
                {
                    minDistance = distance;
                    closestOurStud = ourStud;
                    closestTargetStud = targetStud;
                }
            }
        }
        
        Debug.Log($"[{brick.name}] ValidateAndCorrectAlignment() - Closest stud pair: {closestOurStud?.name} to {closestTargetStud?.name}, distance: {minDistance}");
        
        // REMOVED: Position correction logic that was causing incorrect positioning
        // The snap calculation already positions the brick correctly for stud alignment
        // Adding additional corrections was causing the bricks to end up in wrong positions
        // Instead, just log the alignment status for debugging
        
        if (closestOurStud != null && closestTargetStud != null)
        {
            if (minDistance > brick.snapTolerance * 2f)
            {
                Debug.LogWarning($"[{brick.name}] ValidateAndCorrectAlignment() - WARNING: Alignment significantly off by {minDistance}, but not correcting to avoid position issues");
            }
            else
            {
                Debug.Log($"[{brick.name}] ValidateAndCorrectAlignment() - Alignment is good, distance: {minDistance}");
            }
        }
    }

    private List<Stud> FindPotentialSnapPoints(Stud ourStud, BrickBehavior targetBrick)
    {
        List<Stud> potentialPoints = new List<Stud>();
        
        // Get all studs on the target brick that are compatible with our stud
        List<Stud> targetStuds = (ourStud.Type == Stud.StudType.Top) ? targetBrick.BottomStuds : targetBrick.TopStuds;
        
        Debug.Log($"[{brick.name}] FindPotentialSnapPoints() - Looking for {ourStud.Type} studs on target brick ({targetStuds.Count} found)");
        
        // First, find all studs within a reasonable detection range
        List<Stud> nearbyStuds = new List<Stud>();
        float snapTolerance = brick.snapTolerance; // Use BrickBehavior's snapTolerance
        foreach (var targetStud in targetStuds)
        {
            float distance = Vector3.Distance(ourStud.transform.position, targetStud.transform.position);
            
            // Use a tighter detection range to avoid false positives
            if (distance < snapTolerance * 1.5f) // Reduced from 2x to 1.5x
            {
                nearbyStuds.Add(targetStud);
            }
        }
        
        Debug.Log($"[{brick.name}] FindPotentialSnapPoints() - Found {nearbyStuds.Count} nearby studs");
        
        // Now check if any of these nearby studs are already occupied
        foreach (var nearbyStud in nearbyStuds)
        {
            bool isOccupied = IsStudOccupied(nearbyStud, targetBrick);
            if (!isOccupied)
            {
                potentialPoints.Add(nearbyStud);
            }
        }
        
        // For partial connections (like 1x2 overlap), we need to check if this creates a valid connection
        if (potentialPoints.Count > 0)
        {
            Debug.Log($"[{brick.name}] FindPotentialSnapPoints() - Found {potentialPoints.Count} available snap points");
            
            // Check if this would create a valid partial connection
            if (potentialPoints.Count < brick.minRequiredConnections)
            {
                Debug.LogWarning($"[{brick.name}] FindPotentialSnapPoints() - WARNING: Only {potentialPoints.Count} snap points found, minimum required: {brick.minRequiredConnections}");
                // Don't clear the list, let the calling method decide
            }
        }
        else
        {
            Debug.Log($"[{brick.name}] FindPotentialSnapPoints() - No available snap points found (all nearby studs are occupied)");
        }
        
        return potentialPoints;
    }
    
    // Helper method to check if a stud is already occupied by another brick
    private bool IsStudOccupied(Stud stud, BrickBehavior targetBrick)
    {
        // Check if any other brick is already connected to this target brick
        // and if that connection uses this specific stud
        foreach (var connectedBrick in targetBrick.ConnectedNeighbors)
        {
            // Check if the connected brick has any studs that are very close to this stud
            List<Stud> connectedBrickStuds = new List<Stud>();
            connectedBrickStuds.AddRange(connectedBrick.TopStuds);
            connectedBrickStuds.AddRange(connectedBrick.BottomStuds);
            
            foreach (var connectedStud in connectedBrickStuds)
            {
                float distance = Vector3.Distance(stud.transform.position, connectedStud.transform.position);
                if (distance < brick.snapTolerance * 0.5f) // Very close - likely the same stud
                {
                    Debug.Log($"[{brick.name}] IsStudOccupied() - Stud {stud.name} is occupied by {connectedBrick.name}");
                    return true;
                }
            }
        }
        
        return false;
    }
    
    // Helper method to check if this brick is already connected to a target brick
    private bool IsAlreadyConnectedTo(BrickBehavior targetBrick)
    {
        return brick.ConnectedNeighbors.Contains(targetBrick);
    }
    
    private Stud ChooseBestSnapPoint(Stud ourStud, List<Stud> potentialPoints)
    {
        Stud bestStud = null;
        float bestDistance = float.MaxValue;
        
        Debug.Log($"[{brick.name}] ChooseBestSnapPoint() - Choosing from {potentialPoints.Count} potential points");
        
        if (potentialPoints.Count == 0)
        {
            Debug.LogWarning($"[{brick.name}] ChooseBestSnapPoint() - WARNING: No potential points provided!");
            return null;
        }
        
        if (potentialPoints.Count == 1)
        {
            bestStud = potentialPoints[0];
            bestDistance = Vector3.Distance(ourStud.transform.position, bestStud.transform.position);
            Debug.Log($"[{brick.name}] ChooseBestSnapPoint() - Only one potential point, selecting {bestStud.name}");
            return bestStud;
        }
        
        // Multiple points - find the closest one
        foreach (var potentialStud in potentialPoints)
        {
            float distance = Vector3.Distance(ourStud.transform.position, potentialStud.transform.position);
            
            if (distance < bestDistance)
            {
                bestDistance = distance;
                bestStud = potentialStud;
            }
        }
        
        if (bestStud != null)
        {
            Debug.Log($"[{brick.name}] ChooseBestSnapPoint() - Selected {bestStud.name} with distance {bestDistance:F3}");
            
            // Additional validation: ensure the selected point is within reasonable distance
            if (bestDistance > brick.snapTolerance * 2f)
            {
                Debug.LogWarning($"[{brick.name}] ChooseBestSnapPoint() - WARNING: Best snap point is quite far: {bestDistance:F3} > {brick.snapTolerance * 2f:F3}");
            }
        }
        else
        {
            Debug.LogWarning($"[{brick.name}] ChooseBestSnapPoint() - WARNING: No valid snap point found!");
        }
        
        return bestStud;
    }

    // Method to find the best alignment for multiple stud connections
    private Stud FindBestMultiStudAlignment(Stud ourStud, List<Stud> potentialPoints, BrickBehavior targetBrick)
    {
        Debug.Log($"[{brick.name}] FindBestMultiStudAlignment() - Finding best alignment for {potentialPoints.Count} potential points");
        
        Stud bestTargetStud = null;
        int maxConnectingStuds = 0;
        float bestDistance = float.MaxValue;
        
        // Try each potential target stud as the primary connection point
        foreach (var primaryTargetStud in potentialPoints)
        {
            Debug.Log($"[{brick.name}] FindBestMultiStudAlignment() - Testing primary target: {primaryTargetStud.name}");
            
            // Calculate the position our brick would be in if we connected to this stud
            Vector3 testPosition = CalculateTestPosition(ourStud, primaryTargetStud, targetBrick);
            Quaternion testRotation = targetBrick.transform.rotation;
            
            // Count how many of our studs would connect to target studs at this position
            int connectingStuds = CountConnectingStuds(testPosition, testRotation, targetBrick);
            
            Debug.Log($"[{brick.name}] FindBestMultiStudAlignment() - Position {testPosition} would connect {connectingStuds} studs");
            
            // Prefer more connecting studs, then closer distance
            if (connectingStuds > maxConnectingStuds || 
                (connectingStuds == maxConnectingStuds && Vector3.Distance(ourStud.transform.position, primaryTargetStud.transform.position) < bestDistance))
            {
                maxConnectingStuds = connectingStuds;
                bestDistance = Vector3.Distance(ourStud.transform.position, primaryTargetStud.transform.position);
                bestTargetStud = primaryTargetStud;
                Debug.Log($"[{brick.name}] FindBestMultiStudAlignment() - New best: {primaryTargetStud.name} with {connectingStuds} connections");
            }
        }
        
        if (bestTargetStud != null)
        {
            Debug.Log($"[{brick.name}] FindBestMultiStudAlignment() - Selected {bestTargetStud.name} with {maxConnectingStuds} connecting studs");
        }
        else
        {
            Debug.LogWarning($"[{brick.name}] FindBestMultiStudAlignment() - WARNING: No good multi-stud alignment found");
        }
        
        return bestTargetStud;
    }
    
    // Helper method to calculate test position for alignment checking
    private Vector3 CalculateTestPosition(Stud ourStud, Stud targetStud, BrickBehavior targetBrick)
    {
        // Use the same logic as CalculateSnapTransform but return the position without setting it
        Vector3 ourStudLocalPos = ourStud.transform.localPosition;
        Vector3 targetStudWorldPos = targetStud.transform.position;
        Quaternion targetBrickRotation = targetBrick.transform.rotation;
        
        Vector3 ourStudInTargetSpace = targetBrickRotation * ourStudLocalPos;
        Vector3 testPosition = targetStudWorldPos - ourStudInTargetSpace;
        
        return testPosition;
    }
    
    // Helper method to count how many studs would connect at a given position
    private int CountConnectingStuds(Vector3 testPosition, Quaternion testRotation, BrickBehavior targetBrick)
    {
        int connectingCount = 0;
        
        // Check all our studs against all target studs
        foreach (var ourStud in studManager.TopStuds)
        {
            // Calculate where this stud would be at the test position
            Vector3 ourStudLocalPos = ourStud.transform.localPosition;
            Vector3 ourStudInTestSpace = testRotation * ourStudLocalPos;
            Vector3 ourStudWorldPos = testPosition + ourStudInTestSpace;
            
            foreach (var targetStud in targetBrick.BottomStuds)
            {
                float distance = Vector3.Distance(ourStudWorldPos, targetStud.transform.position);
                if (distance < brick.snapTolerance)
                {
                    connectingCount++;
                }
            }
        }
        
        foreach (var ourStud in studManager.BottomStuds)
        {
            // Calculate where this stud would be at the test position
            Vector3 ourStudLocalPos = ourStud.transform.localPosition;
            Vector3 ourStudInTestSpace = testRotation * ourStudLocalPos;
            Vector3 ourStudWorldPos = testPosition + ourStudInTestSpace;
            
            foreach (var targetStud in targetBrick.TopStuds)
            {
                float distance = Vector3.Distance(ourStudWorldPos, targetStud.transform.position);
                if (distance < brick.snapTolerance)
                {
                    connectingCount++;
                }
            }
        }
        
        Debug.Log($"[{brick.name}] CountConnectingStuds() - Found {connectingCount} connecting studs");
        return connectingCount;
    }

    public void Cleanup()
    {
        // Clean up any references
        studManager?.Cleanup();
    }

    // Coroutine to stabilize the group after snap is complete
    private IEnumerator StabilizeGroupAfterSnap()
    {
        // Wait a few frames to let physics settle
        yield return new WaitForSeconds(0.1f);
        
        Debug.Log($"[{brick.name}] StabilizeGroupAfterSnap() - Stabilizing group after snap");
        
        // Find all bricks in the connected group
        List<BrickBehavior> groupBricks = new List<BrickBehavior>();
        FindAllConnectedInGroup(brick, groupBricks);
        
        Debug.Log($"[{brick.name}] StabilizeGroupAfterSnap() - DEBUG: Found {groupBricks.Count} bricks in group");
        
        foreach (var groupBrick in groupBricks)
        {
            if (groupBrick.GetComponent<Rigidbody>() != null)
            {
                var rb = groupBrick.GetComponent<Rigidbody>();
                Debug.Log($"[{brick.name}] StabilizeGroupAfterSnap() - DEBUG: {groupBrick.name} physics before stabilization - isKinematic: {rb.isKinematic}, useGravity: {rb.useGravity}, velocity: {rb.linearVelocity}");
                
                // Ensure proper physics state
                rb.isKinematic = false;
                rb.useGravity = true;
                
                // Clear any residual velocities
                rb.linearVelocity = Vector3.zero;
                rb.angularVelocity = Vector3.zero;
                
                Debug.Log($"[{brick.name}] StabilizeGroupAfterSnap() - DEBUG: {groupBrick.name} physics after stabilization - isKinematic: {rb.isKinematic}, useGravity: {rb.useGravity}, velocity: {rb.linearVelocity}");
                Debug.Log($"[{brick.name}] StabilizeGroupAfterSnap() - Stabilized {groupBrick.name}");
            }
        }
        
        Debug.Log($"[{brick.name}] StabilizeGroupAfterSnap() - Group stabilization complete");
    }

    // Helper method to find all connected bricks in a group
    private void FindAllConnectedInGroup(BrickBehavior brick, List<BrickBehavior> visited)
    {
        if (brick == null || visited.Contains(brick))
        {
            return;
        }
        
        visited.Add(brick);
        
        foreach (var neighbor in brick.ConnectedNeighbors)
        {
            FindAllConnectedInGroup(neighbor, visited);
        }
    }
} 