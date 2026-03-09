using UnityEngine;
using System.Collections.Generic;
using System.Collections;

public class BrickSnappingSystem
{
    private readonly BrickBehavior brick;
    private readonly BrickStudManager studManager;
    private BrickBehavior frozenBrick = null;
    private BrickBehavior snapInitiator; // To track who started the multi-snap process
    
    // Missing variables that were in the original BrickBehavior
    private Vector3 targetSnapPosition;
    private Quaternion targetSnapRotation;
    private BrickBehavior snapTargetBrick;

    public BrickSnappingSystem(BrickBehavior brick, BrickStudManager studManager)
    {
        this.brick = brick;
        this.studManager = studManager;
    }

    // ========================================

    private struct StoredSnap
    {
        public Stud fromStud;
        public Stud toStud;
    }
    private StoredSnap? storedSnap;

    public void StorePotentialSnap(Stud fromStud, Stud toStud)
    {
        storedSnap = new StoredSnap { fromStud = fromStud, toStud = toStud };
    }

    // Called from BrickBehavior.OnGrabReleased()
    public void ExecuteStoredSnap(Vector3 releasePosition, Quaternion releaseRotation)
    {
        if (storedSnap.HasValue)
        {
            var snap = storedSnap.Value;
            brick.LogDebug($"ExecuteStoredSnap() - Executing stored snap from {snap.fromStud.name} to {snap.toStud.name}");
            RequestSnap(snap.fromStud, snap.toStud, releasePosition, releaseRotation, null);
            storedSnap = null;
        }
    }

    public bool HasStoredSnap => storedSnap.HasValue;

    // Overload for calls without a specific release pose
    public void RequestSnap(Stud myStud, Stud targetStud)
    {
        RequestSnap(myStud, targetStud, brick.transform.position, brick.transform.rotation, null);
    }

    // This is the core method, called by a Stud when it collides with another valid stud.
    public void RequestSnap(Stud myStud, Stud targetStud, Vector3 releasePosition, Quaternion releaseRotation, BrickBehavior initiator = null)
    {
        // The initiator is the brick that manages the multi-snap queue.
        // If null, this brick is initiating its own snap.
        this.snapInitiator = initiator ?? brick;
        
        // Validate that snapInitiator is not null
        if (this.snapInitiator == null)
        {
            brick.LogWarning($"RequestSnap() - WARNING: snapInitiator is null after assignment!");
            return;
        }
        
        brick.LogDebug($"RequestSnap() - Initiator is {this.snapInitiator.name}");

        brick.LogDebug($"RequestSnap() - Request from stud '{myStud.name}' to target stud '{targetStud.name}'");
        
        if (brick.GetComponent<BrickBehavior>().isSnapping)
        {
            brick.LogDebug($"RequestSnap() - Already snapping, ignoring request");
            return;
        }

        BrickBehavior targetBrick = targetStud.ParentBrick;
        if (targetBrick == null)
        {
            brick.LogWarning($"RequestSnap() - WARNING: Target stud has no parent brick");
            return;
        }
        
        if (targetBrick == brick)
        {
            brick.LogDebug($"RequestSnap() - Ignoring snap to self");
            return;
        }

        // Check if we're already connected to this brick
        if (IsAlreadyConnectedTo(targetBrick))
        {
            brick.LogDebug($"RequestSnap() - Already connected to target brick, ignoring");
            return;
        }

        brick.LogDebug($"RequestSnap() - Target brick: {targetBrick.name}");

        // DEBUG: Log positions of both bricks and studs
        brick.LogDebug($"RequestSnap() - DEBUG: Grabbed brick position: {brick.transform.position}, rotation: {brick.transform.rotation.eulerAngles}", false);
        brick.LogDebug($"RequestSnap() - DEBUG: Target brick position: {targetBrick.transform.position}, rotation: {targetBrick.transform.rotation.eulerAngles}", false);
        
        // Calculate and log the relative rotation at release time
        // Use Vector3.SignedAngle to get the true relative rotation around the shared local Z-axis
        Vector3 brickRight = brick.transform.rotation * Vector3.right;  // The "+X" direction of released brick
        Vector3 targetRight = targetBrick.transform.rotation * Vector3.right;  // The "+X" direction of target brick
        
        // Calculate the signed angle from target's right-vector to released's right-vector around world Z
        float relativeZ = Vector3.SignedAngle(targetRight, brickRight, Vector3.forward);
        
        brick.LogDebug($"RequestSnap() - DEBUG: Relative rotation at release - Z: {relativeZ:F1}°", false);
        brick.LogDebug($"RequestSnap() - DEBUG: Grabbed brick rotation: {brick.transform.rotation.eulerAngles}", false);
        brick.LogDebug($"RequestSnap() - DEBUG: Target brick rotation: {targetBrick.transform.rotation.eulerAngles}", false);
        brick.LogDebug($"RequestSnap() - DEBUG: Grabbed brick right vector: {brickRight}", false);
        brick.LogDebug($"RequestSnap() - DEBUG: Target brick right vector: {targetRight}", false);
        
        // ADDITIONAL DEBUG: Check if the relative rotation makes sense for LEGO bricks
        // For LEGO bricks at 90° relative rotation, we expect the relative Z to be close to 90°, -90°, 180°, or -180°
        float absRelativeZ = Mathf.Abs(relativeZ);
        if (absRelativeZ < 10f)
        {
            brick.LogDebug($"RequestSnap() - DEBUG: WARNING: Relative Z rotation is very small ({relativeZ:F1}°), bricks appear to be aligned!", false);
        }
        else if (absRelativeZ > 80f && absRelativeZ < 100f)
        {
            brick.LogDebug($"RequestSnap() - DEBUG: Relative Z rotation is close to 90° ({relativeZ:F1}°), this is expected for LEGO bricks!", false);
        }
        else if (absRelativeZ > 170f && absRelativeZ < 190f)
        {
            brick.LogDebug($"RequestSnap() - DEBUG: Relative Z rotation is close to 180° ({relativeZ:F1}°), this is expected for LEGO bricks!", false);
        }
        else
        {
            brick.LogDebug($"RequestSnap() - DEBUG: Relative Z rotation is {relativeZ:F1}°, not a standard LEGO alignment!", false);
        }

        brick.LogDebug($"RequestSnap() - DEBUG: Our stud world position: {myStud.transform.position}", false);
        brick.LogDebug($"RequestSnap() - DEBUG: Target stud world position: {targetStud.transform.position}", false);
        brick.LogDebug($"RequestSnap() - DEBUG: Distance between studs: {Vector3.Distance(myStud.transform.position, targetStud.transform.position):F6}", false);
        brick.LogDebug($"RequestSnap() - DEBUG: Our stud local position: {myStud.transform.localPosition}", false);
        brick.LogDebug($"RequestSnap() - DEBUG: Target stud local position: {targetStud.transform.localPosition}", false);

        // Validate stud compatibility
        if (myStud.Type == targetStud.Type)
        {
            brick.LogWarning($"RequestSnap() - WARNING: Cannot snap {myStud.Type} to {targetStud.Type}!");
            return;
        }

        // Check if there are multiple potential snap points
        List<Stud> potentialSnapPoints = FindPotentialSnapPoints(myStud, targetBrick);
        brick.LogDebug($"RequestSnap() - Found {potentialSnapPoints.Count} potential snap points", true);

        if (potentialSnapPoints.Count == 0)
        {
            brick.LogDebug($"RequestSnap() - No valid snap points found", true);
            return;
        }

        // For multiple stud connections (like 2x1 bricks), we need to find the best overall alignment
        if (potentialSnapPoints.Count > 1)
        {
            brick.LogDebug($"RequestSnap() - Multiple snap points detected, finding best overall alignment", true);
            
            // Find the best alignment that maximizes the number of connecting studs
            Stud bestTargetStud = FindBestMultiStudAlignment(myStud, potentialSnapPoints, targetBrick);
            if (bestTargetStud != null)
            {
                targetStud = bestTargetStud;
                brick.LogDebug($"RequestSnap() - Selected best multi-stud alignment: {targetStud.name}", true);
            }
            else
            {
                brick.LogWarning($"RequestSnap() - WARNING: Could not find good multi-stud alignment, falling back to single stud", true);
                targetStud = ChooseBestSnapPoint(myStud, potentialSnapPoints);
            }
        }
        else
        {
            targetStud = potentialSnapPoints[0];
            brick.LogDebug($"RequestSnap() - Using single snap point: {targetStud.name}", true);
        }

        // Store target brick's initial rotation for debugging
        Vector3 initialTargetRotation = targetBrick.transform.rotation.eulerAngles;
        brick.LogDebug($"RequestSnap() - DEBUG: Target brick initial rotation: {initialTargetRotation}", false);

        // Calculate snap position and rotation using the release pose
        brick.LogDebug($"RequestSnap() - About to call CalculateSnapTransform", true);
        CalculateSnapTransform(myStud, targetStud, targetBrick, releasePosition, releaseRotation);
        brick.LogDebug($"RequestSnap() - CalculateSnapTransform completed", true);

        // --- Log snap event to BricksRelationTracker ---
        var bricksRelationTracker = UnityEngine.Object.FindObjectOfType<BricksRelationTracker>();
        if (bricksRelationTracker != null && bricksRelationTracker.trackingEnabled)
        {
            // Minimal implementation: log the main stud pair.
            var snappedStuds = new List<Stud> { myStud };
            var targetStuds = new List<Stud> { targetStud };

            bricksRelationTracker.RecordSnapEvent(
                snappedBrick: brick,
                targetBrickOrBoard: targetBrick,
                snappedStuds: snappedStuds,
                targetStuds: targetStuds
            );
        }

        // Check if target brick rotation changed during calculation
        Vector3 finalTargetRotation = targetBrick.transform.rotation.eulerAngles;
        if (Vector3.Distance(initialTargetRotation, finalTargetRotation) > 0.1f)
        {
            brick.LogWarning($"RequestSnap() - WARNING: Target brick rotation changed during snap calculation!", true);
            brick.LogWarning($"RequestSnap() - Initial: {initialTargetRotation}, Final: {finalTargetRotation}", true);
        }

        // Disable physics during snap for both bricks to prevent interference
        // BUT only for non-board bricks
        if (!brick.IsBoard && brick.GetComponent<Rigidbody>() != null)
        {
            brick.GetComponent<Rigidbody>().isKinematic = true;
            brick.GetComponent<Rigidbody>().useGravity = false;
            brick.LogDebug($"RequestSnap() - Disabled physics for snap animation", true);
        }
        else if (brick.IsBoard)
        {
            brick.LogDebug($"RequestSnap() - Skipping physics change for board during snap", true);
        }
        
        // IMPORTANT: Also disable physics on target brick to prevent it from moving during snap
        // BUT only for non-board bricks
        if (!targetBrick.IsBoard && targetBrick.GetComponent<Rigidbody>() != null)
        {
            targetBrick.GetComponent<Rigidbody>().isKinematic = true;
            targetBrick.GetComponent<Rigidbody>().useGravity = false;
            brick.LogDebug($"RequestSnap() - Disabled physics on target brick to prevent interference", true);
        }
        else if (targetBrick.IsBoard)
        {
            brick.LogDebug($"RequestSnap() - Skipping physics change for target board during snap", true);
        }

        // --- NEW: Freeze the grabbed brick if this is a two-controller snap ---
        // Find if any brick in the target group is grabbed, and freeze that specific one.
        BrickBehavior grabbedBrickInTargetGroup = null;
        List<BrickBehavior> targetGroupBricks = new List<BrickBehavior>();
        BrickGroupUtils.FindAllConnectedInGroup(targetBrick, targetGroupBricks, brick.name);

        foreach (var brickInGroup in targetGroupBricks)
        {
            if (brickInGroup.IsGrabbed)
            {
                grabbedBrickInTargetGroup = brickInGroup;
                break;
            }
        }

        if (grabbedBrickInTargetGroup != null)
        {
            brick.LogDebug($"RequestSnap() - Freezing grabbed brick in target group: {grabbedBrickInTargetGroup.name}");
            grabbedBrickInTargetGroup.Freeze(true);
            frozenBrick = grabbedBrickInTargetGroup;
        }

        // Set snapping state
        brick.SetSnappingState(true, targetSnapPosition, targetSnapRotation, targetBrick);
        
        // IMPORTANT: Store the target brick reference for finalization
        snapTargetBrick = targetBrick;

        // Update stud states to show snapping
        myStud.SetSnapping(true);
        targetStud.SetSnapping(true);

        brick.LogDebug($"RequestSnap() - Snap initiated. Target position: {targetSnapPosition}, Target rotation: {targetSnapRotation.eulerAngles}", true);
        brick.LogDebug($"RequestSnap() - DEBUG: Current brick position: {brick.transform.position}, Current rotation: {brick.transform.rotation.eulerAngles}", false);
        brick.LogDebug($"RequestSnap() - DEBUG: Position difference: {Vector3.Distance(brick.transform.position, targetSnapPosition):F6}", false);
        brick.LogDebug($"RequestSnap() - DEBUG: Rotation difference: {Quaternion.Angle(brick.transform.rotation, targetSnapRotation):F2}°", false);
    }

    private void CalculateSnapTransform(Stud myStud, Stud targetStud, BrickBehavior targetBrick, Vector3 releasePosition, Quaternion releaseRotation)
    {
        brick.LogDebug($"CalculateSnapTransform() - Calculating snap transform for {myStud.Type} to {targetStud.Type}", true);
        brick.LogDebug($"CalculateSnapTransform() - DEBUG: Our brick position: {brick.transform.position}", false);
        brick.LogDebug($"CalculateSnapTransform() - DEBUG: Target brick position: {targetBrick.transform.position}", false);

        // Validate stud compatibility
        if (myStud.Type == targetStud.Type)
        {
            brick.LogWarning($"CalculateSnapTransform() - WARNING: Cannot snap {myStud.Type} to {targetStud.Type}!");
            return;
        }

        // Get the world positions of the studs
        Vector3 myStudWorldPos = myStud.transform.position;
        Vector3 targetStudWorldPos = targetStud.transform.position;
        
        brick.LogDebug($"CalculateSnapTransform() - DEBUG: Stud positions: Our={myStudWorldPos}, Target={targetStudWorldPos}", false);

        // STEP 1: Calculate final position assuming brick matches target brick rotation on all axes
        // Get our stud's local position relative to our brick
        Vector3 myStudLocalPos = myStud.transform.localPosition;
        brick.LogDebug($"CalculateSnapTransform() - DEBUG: Our stud local position: {myStudLocalPos}", false);

        // First, assume the brick will have the same rotation as the target brick (for position calculation)
        Quaternion targetBrickRotation = targetBrick.transform.rotation;
        brick.LogDebug($"CalculateSnapTransform() - DEBUG: Using target brick rotation for position calculation: {targetBrickRotation.eulerAngles}", false);

        // Calculate the final position using a simpler, more direct approach
        // We want our stud to end up at the target stud's world position
        // So our brick center needs to be: target stud position - (our stud's position in final rotation)
        
        // First calculate the final rotation
        brick.LogDebug($"CalculateSnapTransform() - About to call AlignXYPlanes", true);
        Quaternion alignedRotation = AlignXYPlanes(brick.transform.rotation, targetBrick);
        brick.LogDebug($"CalculateSnapTransform() - AlignXYPlanes completed, aligned rotation: {alignedRotation.eulerAngles}", true);
        
        // For LEGO-style alignment, find the closest 90° increment relative to aligned rotation
        Quaternion finalRotation = FindClosest90DegreeIncrement(brick.transform.rotation, alignedRotation);
        
        brick.LogDebug($"CalculateSnapTransform() - DEBUG: Target brick rotation: {targetBrick.transform.rotation.eulerAngles}", false);
        brick.LogDebug($"CalculateSnapTransform() - DEBUG: Using closest 90° increment for final alignment: {finalRotation.eulerAngles}", false);
        
        brick.LogDebug($"CalculateSnapTransform() - DEBUG: Original rotation: {brick.transform.rotation.eulerAngles}", false);
        brick.LogDebug($"CalculateSnapTransform() - DEBUG: Target brick rotation: {targetBrick.transform.rotation.eulerAngles}", false);
        brick.LogDebug($"CalculateSnapTransform() - DEBUG: Final rotation (matching target): {finalRotation.eulerAngles}", false);
        
        brick.LogDebug($"CalculateSnapTransform() - DEBUG: Final rotation: {finalRotation.eulerAngles}", false);
        
        // Calculate where our stud will be in the final rotation
        Vector3 myStudInFinalRotation = finalRotation * myStudLocalPos;
        
        // Calculate final position: target stud position - our stud position in final rotation
        Vector3 finalPosition = targetStudWorldPos - myStudInFinalRotation;
        
        brick.LogDebug($"CalculateSnapTransform() - DEBUG: Our stud local position: {myStudLocalPos}", false);
        brick.LogDebug($"CalculateSnapTransform() - DEBUG: Our stud in final rotation: {myStudInFinalRotation}", false);
        brick.LogDebug($"CalculateSnapTransform() - DEBUG: Initial position calculation: {targetStudWorldPos} - {myStudInFinalRotation} = {finalPosition}", false);
        
        // Calculate offset using the direction from relative target brick center to target stud
        // This creates a "relative target brick center" that works for any brick size
        Vector3 targetStudLocalPos = targetStud.transform.localPosition;
        Vector3 relativeTargetBrickCenter = targetStudWorldPos - (targetBrick.transform.rotation * targetStudLocalPos);
        Vector3 relativeTargetBrickToStud = (targetStudWorldPos - relativeTargetBrickCenter).normalized;
        Vector3 snapOffset = relativeTargetBrickToStud * 0.001f; // 1mm offset in stud direction
        finalPosition += snapOffset;
        
        brick.LogDebug($"CalculateSnapTransform() - DEBUG: Target stud local position: {targetStudLocalPos}", false);
        brick.LogDebug($"CalculateSnapTransform() - DEBUG: Relative target brick center: {relativeTargetBrickCenter}", false);
        brick.LogDebug($"CalculateSnapTransform() - DEBUG: Relative target brick to stud direction: {relativeTargetBrickToStud}", false);
        brick.LogDebug($"CalculateSnapTransform() - DEBUG: Added snap offset: {snapOffset}", false);
        brick.LogDebug($"CalculateSnapTransform() - DEBUG: Position after offset: {finalPosition}", false);
        
        // STEP 2: Calculate optimal distance based on actual brick and stud geometry
        Vector3 currentBrickCenter = finalPosition;
        float distanceFromTargetStudToOurBrick = Vector3.Distance(currentBrickCenter, targetStudWorldPos);
        
        brick.LogDebug($"CalculateSnapTransform() - DEBUG: Distance from target stud to our brick center: {distanceFromTargetStudToOurBrick:F3}", false);
        
        // Calculate the optimal distance: target stud to our brick center = our stud to our brick center + tolerance
        float myStudToBrickCenterDistance = Vector3.Distance(myStud.transform.position, brick.transform.position);
        float optimalDistance = myStudToBrickCenterDistance + 0.001f; // 1mm tolerance
        
        brick.LogDebug($"CalculateSnapTransform() - DEBUG: Our stud to brick center distance: {myStudToBrickCenterDistance:F3}", false);
        brick.LogDebug($"CalculateSnapTransform() - DEBUG: Optimal distance: {optimalDistance:F3}", false);
        
        // If the distance from target stud to our brick center is too large, adjust the position to bring them closer
        // while still maintaining the stud alignment
        if (distanceFromTargetStudToOurBrick > optimalDistance)
        {
            // Calculate the direction from target stud to our brick center
            Vector3 directionFromTargetStudToOurBrick = (currentBrickCenter - targetStudWorldPos).normalized;
            
            // Calculate how much we need to move our brick closer
            float excessDistance = distanceFromTargetStudToOurBrick - optimalDistance;
            
            // Move our brick closer by the excess distance
            Vector3 proximityAdjustment = directionFromTargetStudToOurBrick * excessDistance;
            finalPosition -= proximityAdjustment;
            
            brick.LogDebug($"CalculateSnapTransform() - DEBUG: Applied proximity adjustment: {proximityAdjustment}", false);
            brick.LogDebug($"CalculateSnapTransform() - DEBUG: New distance from target stud to our brick center: {Vector3.Distance(finalPosition, targetStudWorldPos):F3}", false);
        }
        else
        {
            brick.LogDebug($"CalculateSnapTransform() - Distance is already optimal, no proximity adjustment needed", true);
        }
        
        // STEP 3: Log the final rotation (already calculated in Step 1)
        // This step just logs the result for debugging purposes
        brick.LogDebug($"CalculateSnapTransform() - DEBUG: Final rotation after 90° increment selection: {finalRotation.eulerAngles}", false);
        
        brick.LogDebug($"CalculateSnapTransform() - Final position: {finalPosition}, Rotation: {finalRotation.eulerAngles}", true);

        // ADD DEBUGGING: Show expected snap point positions after transformation
        // Calculate where our stud will be after the brick is moved and rotated
        Vector3 expectedMyStudPosition = finalPosition + (finalRotation * myStudLocalPos);
        
        brick.LogDebug($"CalculateSnapTransform() - DEBUG: EXPECTED SNAP POINT ALIGNMENT:", false);
        brick.LogDebug($"CalculateSnapTransform() - DEBUG: Expected our stud position: {expectedMyStudPosition}", false);
        brick.LogDebug($"CalculateSnapTransform() - DEBUG: Target stud position: {targetStudWorldPos}", false);
        brick.LogDebug($"CalculateSnapTransform() - DEBUG: Alignment difference: {expectedMyStudPosition - targetStudWorldPos}", false);
        brick.LogDebug($"CalculateSnapTransform() - DEBUG: Alignment distance: {Vector3.Distance(expectedMyStudPosition, targetStudWorldPos):F6}", false);

        targetSnapPosition = finalPosition;
        targetSnapRotation = finalRotation;
        
        brick.LogDebug($"CalculateSnapTransform() - Set targetSnapPosition: {targetSnapPosition}", true);
        brick.LogDebug($"CalculateSnapTransform() - Set targetSnapRotation: {targetSnapRotation.eulerAngles}", true);
        
        // Debug visualization - draw lines to show the snap alignment
        Debug.DrawLine(myStudWorldPos, targetStudWorldPos, Color.green, 2f);
        Debug.DrawLine(brick.transform.position, finalPosition, Color.red, 2f);
        brick.LogDebug($"CalculateSnapTransform() - DEBUG: Debug lines drawn: Green=stud alignment, Red=brick movement", false);
        
        // Add a crucial flow message for Lite Debug level
        brick.LogDebug($"CalculateSnapTransform() - Snap transform calculated successfully");
    }
    

    // Method to align the released brick's local X-Y plane to the target brick's local X-Y plane
    private Quaternion AlignXYPlanes(Quaternion releasedBrickRotation, BrickBehavior targetBrick)
    {
        // Get the target brick's rotation
        Quaternion targetRotation = targetBrick.transform.rotation;
        
        // Get the up vectors (Y-axis) of both bricks in world space
        Vector3 releasedUp = releasedBrickRotation * Vector3.up;
        Vector3 targetUp = targetRotation * Vector3.up;
        
        // Get the forward vectors (Z-axis) of both bricks in world space
        Vector3 releasedForward = releasedBrickRotation * Vector3.forward;
        Vector3 targetForward = targetRotation * Vector3.forward;
        
        brick.LogDebug($"AlignXYPlanes() - DEBUG: Released brick up vector: {releasedUp}", false);
        brick.LogDebug($"AlignXYPlanes() - DEBUG: Target brick up vector: {targetUp}", false);
        brick.LogDebug($"AlignXYPlanes() - DEBUG: Released brick forward vector: {releasedForward}", false);
        brick.LogDebug($"AlignXYPlanes() - DEBUG: Target brick forward vector: {targetForward}", false);
        
        // Calculate the rotation needed to align the up vectors
        // This ensures both bricks are oriented in the same direction
        Quaternion upAlignment = Quaternion.FromToRotation(releasedUp, targetUp);
        
        // Apply the up alignment to the released brick's rotation
        Quaternion alignedRotation = upAlignment * releasedBrickRotation;
        
        // Now align the forward vectors in the plane perpendicular to the up vector
        Vector3 tempReleasedForward = alignedRotation * Vector3.forward;
        Vector3 newReleasedRight = alignedRotation * Vector3.right;
        
        // Project the forward vectors onto the plane perpendicular to the up vector
        Vector3 projectedReleasedForward = Vector3.ProjectOnPlane(tempReleasedForward, targetUp).normalized;
        Vector3 projectedTargetForward = Vector3.ProjectOnPlane(targetForward, targetUp).normalized;
        
        // Calculate the rotation needed to align the projected forward vectors
        Quaternion forwardAlignment = Quaternion.FromToRotation(projectedReleasedForward, projectedTargetForward);
        
        // Apply both alignments
        Quaternion finalAlignedRotation = forwardAlignment * alignedRotation;
        
        brick.LogDebug($"AlignXYPlanes() - DEBUG: Up alignment quaternion: {upAlignment.eulerAngles}", false);
        brick.LogDebug($"AlignXYPlanes() - DEBUG: Forward alignment quaternion: {forwardAlignment.eulerAngles}", false);
        brick.LogDebug($"AlignXYPlanes() - DEBUG: Final aligned rotation: {finalAlignedRotation.eulerAngles}", false);
        
        // Validate the alignment
        Vector3 newReleasedUp = finalAlignedRotation * Vector3.up;
        Vector3 newReleasedForward = finalAlignedRotation * Vector3.forward;
        
        float upAlignmentError = Vector3.Angle(newReleasedUp, targetUp);
        float forwardAlignmentError = Vector3.Angle(newReleasedForward, targetForward);
        
        brick.LogDebug($"AlignXYPlanes() - DEBUG: Up alignment error: {upAlignmentError:F1}°", false);
        brick.LogDebug($"AlignXYPlanes() - DEBUG: Forward alignment error: {forwardAlignmentError:F1}°", false);
        
        if (upAlignmentError > 0.5f)
        {
            brick.LogWarning($"AlignXYPlanes() - WARNING: Up alignment error is {upAlignmentError:F1}° (should be < 0.5°)", true);
        }
        else
        {
            brick.LogDebug($"AlignXYPlanes() - Up alignment successful (error: {upAlignmentError:F1}°)", true);
        }
        
        if (forwardAlignmentError > 0.5f)
        {
            brick.LogWarning($"AlignXYPlanes() - WARNING: Forward alignment error is {forwardAlignmentError:F1}° (should be < 0.5°)", true);
        }
        else
        {
            brick.LogDebug($"AlignXYPlanes() - Forward alignment successful (error: {forwardAlignmentError:F1}°)", true);
        }
        
        return finalAlignedRotation;
    }

    // Method to find the closest 90° increment relative to aligned rotation using quaternions and SignedAngle
    private Quaternion FindClosest90DegreeIncrement(Quaternion originalRotation, Quaternion alignedRotation)
    {
        brick.LogDebug($"FindClosest90DegreeIncrement() - DEBUG: Original rotation: {originalRotation.eulerAngles}", false);
        brick.LogDebug($"FindClosest90DegreeIncrement() - DEBUG: Aligned rotation: {alignedRotation.eulerAngles}", false);
        
        // Get the aligned rotation's local Z-axis (forward direction)
        Vector3 alignedZAxis = alignedRotation * Vector3.forward;
        
        // Create the 4 possible 90° increment rotations around the aligned rotation's Z-axis
        Quaternion option1 = alignedRotation; // 0° (original aligned rotation)
        Quaternion option2 = alignedRotation * Quaternion.AngleAxis(90f, Vector3.forward); // +90° around Z
        Quaternion option3 = alignedRotation * Quaternion.AngleAxis(-90f, Vector3.forward); // -90° around Z
        Quaternion option4 = alignedRotation * Quaternion.AngleAxis(180f, Vector3.forward); // 180° around Z
        
        brick.LogDebug($"FindClosest90DegreeIncrement() - DEBUG: Option 1 (0°): {option1.eulerAngles}", false);
        brick.LogDebug($"FindClosest90DegreeIncrement() - DEBUG: Option 2 (+90°): {option2.eulerAngles}", false);
        brick.LogDebug($"FindClosest90DegreeIncrement() - DEBUG: Option 3 (-90°): {option3.eulerAngles}", false);
        brick.LogDebug($"FindClosest90DegreeIncrement() - DEBUG: Option 4 (180°): {option4.eulerAngles}", false);
        
        // Calculate signed angles between original rotation and each option
        float angle1 = Quaternion.Angle(originalRotation, option1);
        float angle2 = Quaternion.Angle(originalRotation, option2);
        float angle3 = Quaternion.Angle(originalRotation, option3);
        float angle4 = Quaternion.Angle(originalRotation, option4);
        
        brick.LogDebug($"FindClosest90DegreeIncrement() - DEBUG: Angle to option 1: {angle1:F1}°", false);
        brick.LogDebug($"FindClosest90DegreeIncrement() - DEBUG: Angle to option 2: {angle2:F1}°", false);
        brick.LogDebug($"FindClosest90DegreeIncrement() - DEBUG: Angle to option 3: {angle3:F1}°", false);
        brick.LogDebug($"FindClosest90DegreeIncrement() - DEBUG: Angle to option 4: {angle4:F1}°", false);
        
        float[] angles = { angle1, angle2, angle3, angle4 };
        Quaternion[] options = { option1, option2, option3, option4 };
        string[] labels = { "option 1 (0°)", "option 2 (+90°)", "option 3 (-90°)", "option 4 (180°)" };

        int minIndex = 0;
        for (int i = 1; i < angles.Length; i++)
        {
            if (angles[i] < angles[minIndex]) minIndex = i;
        }

        Quaternion closestRotation = options[minIndex];
        brick.LogDebug($"FindClosest90DegreeIncrement() - Selected {labels[minIndex]} with angle: {angles[minIndex]:F1}°", true);
        brick.LogDebug($"FindClosest90DegreeIncrement() - Final closest rotation: {closestRotation.eulerAngles}", true);
        
        return closestRotation;
    }

    public void FinalizeSnap()
    {
        brick.LogDebug($"FinalizeSnap() - Finalizing snap connection");
        brick.LogDebug($"FinalizeSnap() - DEBUG: isSnapping before finalization: {brick.isSnapping}", false);
        
        // DEBUG: Log final position and physics state
        brick.LogDebug($"FinalizeSnap() - DEBUG: Final brick position: {brick.transform.position}", false);
        brick.LogDebug($"FinalizeSnap() - DEBUG: Final brick rotation: {brick.transform.rotation.eulerAngles}", false);
        
        // ADDITIONAL DEBUG: Check if the position matches what was calculated
        brick.LogDebug($"FinalizeSnap() - DEBUG: Expected target position was: {targetSnapPosition}", false);
        brick.LogDebug($"FinalizeSnap() - DEBUG: Position difference: {Vector3.Distance(brick.transform.position, targetSnapPosition):F6}", false);
        
        var rb = brick.GetComponent<Rigidbody>();
        if (rb != null)
        {
            brick.LogDebug($" FinalizeSnap() - DEBUG: Physics before finalization - isKinematic: {rb.isKinematic}, useGravity: {rb.useGravity}", false);
        }
        
        brick.SetSnappingState(false, Vector3.zero, Quaternion.identity, null);
        brick.LogDebug($" FinalizeSnap() - DEBUG: isSnapping after finalization: {brick.isSnapping}", false);

        // Reset stud states after snap is complete
        foreach (var stud in studManager.TopStuds)
        {
            stud.SetSnapping(false);
        }
        foreach (var stud in studManager.BottomStuds)
        {
            stud.SetSnapping(false);
        }
        
        brick.LogDebug($" FinalizeSnap() - Final position: {brick.transform.position}, Rotation: {brick.transform.rotation.eulerAngles}");

        // Store target brick reference before clearing it
        BrickBehavior targetBrick = snapTargetBrick;

        // Check if we have a valid target brick
        if (targetBrick == null)
        {
            brick.LogWarning($"[{brick.name}] FinalizeSnap() - WARNING: Target brick is null, cannot finalize snap");
            return;
        }

        // --- JOINT, MASTER, AND CONNECTION LOGIC ---
        Rigidbody jointTargetRigidbody;
        if (targetBrick.IsBoard)
        {
            // Snapping to a board: Connect directly to the board's Rigidbody.
            // The brick's group master remains unchanged.
            jointTargetRigidbody = targetBrick.GetComponent<Rigidbody>();
            brick.LogDebug($"FinalizeSnap() - Board snap: Target Rigidbody is {targetBrick.name}. Brick master remains {brick.MasterBrick.name}.");
        }
        else
        {
            // Snapping to another brick/group:
            BrickBehavior targetMaster = targetBrick.MasterBrick;

            // Get all bricks in the target's group to check for two-controller grabs
            List<BrickBehavior> groupBricks = new List<BrickBehavior>();
            BrickGroupUtils.FindAllConnectedInGroup(targetBrick, groupBricks, targetBrick.name);
            BrickBehavior grabbedBrickInTargetGroup = groupBricks.Find(b => b.IsGrabbed);

            if (grabbedBrickInTargetGroup != null && grabbedBrickInTargetGroup.OriginalMaster != null)
            {
                // Two-controller scenario: Connect to the ORIGINAL master of the other held group for stability.
                var originalMaster = grabbedBrickInTargetGroup.OriginalMaster;
                jointTargetRigidbody = originalMaster.GetComponent<Rigidbody>();
                brick.LogDebug($"FinalizeSnap() - Two-controller snap: Connecting joint to held group's ORIGINAL master: {originalMaster.name}");
            }
            else
            {
                // Normal brick-to-brick snap: Connect to the target brick's Rigidbody.
                jointTargetRigidbody = targetBrick.GetComponent<Rigidbody>();
                brick.LogDebug($"FinalizeSnap() - Normal snap: Connecting joint to target brick: {targetBrick.name}");
            }

            // Update this brick's group to join the target's group.
            brick.UpdateMaster(targetMaster);
            brick.LogDebug($"FinalizeSnap() - Brick joined target's group, new master: {targetMaster.name}");
        }

        // A joint is now always created, including for boards.
        ConfigurableJoint joint = brick.gameObject.AddComponent<ConfigurableJoint>();
        joint.connectedBody = jointTargetRigidbody;
        joint.xMotion = ConfigurableJointMotion.Locked;
        joint.yMotion = ConfigurableJointMotion.Locked;
        joint.zMotion = ConfigurableJointMotion.Locked;
        joint.angularXMotion = ConfigurableJointMotion.Locked;
        joint.angularYMotion = ConfigurableJointMotion.Locked;
        joint.angularZMotion = ConfigurableJointMotion.Locked;
        JointDrive drive = new JointDrive();
        drive.positionSpring = float.MaxValue; // Unlimited stiffness
        drive.positionDamper = float.MaxValue; // Unlimited damping
        drive.maximumForce = float.MaxValue;
        joint.xDrive = drive;
        joint.yDrive = drive;
        joint.zDrive = drive;
        joint.slerpDrive = drive;
        brick.SetJoint(joint);
        brick.LogDebug($" FinalizeSnap() - Created ConfigurableJoint from {brick.name} to {jointTargetRigidbody.name}");
        
        // Set mass properties to make the connection more rigid
        // BUT only for non-board bricks and only if not currently grabbed
        if (!brick.IsBoard && brick.GetComponent<Rigidbody>() != null && !brick.IsGrabbed)
        {
            var brickRb = brick.GetComponent<Rigidbody>();
            brick.LogDebug($" FinalizeSnap() - DEBUG: Physics before joint creation - isKinematic: {brickRb.isKinematic}, useGravity: {brickRb.useGravity}", false);
            
            // Normalize mass to prevent group weight accumulation
            brickRb.mass = 1.0f; // Fixed mass regardless of group size
            // Set drag and angular drag from BrickBehavior
            brickRb.linearDamping = brick.brickDrag;
            brickRb.angularDamping = brick.brickAngularDrag;
            
            // IMPORTANT: Restore physics after snap animation
            brickRb.isKinematic = false;
            brickRb.useGravity = true;
            brick.LogDebug($" FinalizeSnap() - Restored physics: isKinematic=false, useGravity=true");
            brick.LogDebug($" FinalizeSnap() - DEBUG: Physics after restoration - isKinematic: {brickRb.isKinematic}, useGravity: {brickRb.useGravity}, mass: {brickRb.mass}", false);
        }
        else if (brick.IsBoard)
        {
            brick.LogDebug($" FinalizeSnap() - Skipping physics restoration for board", true);
        }
        else if (brick.IsGrabbed)
        {
            brick.LogDebug($" FinalizeSnap() - Skipping physics restoration for grabbed brick (XR system controls physics)", true);
        }
        
    // IMPORTANT: Also restore physics on target brick (or master brick in two-controller scenario)
    // BUT only for non-board bricks and only if not currently grabbed
        if (!targetBrick.IsBoard && jointTargetRigidbody != null)
        {
            BrickBehavior targetBehavior = jointTargetRigidbody.GetComponent<BrickBehavior>();
            if (targetBehavior != null && !targetBehavior.IsGrabbed)
            {
                targetBehavior.GetComponent<Rigidbody>().isKinematic = false;
                targetBehavior.GetComponent<Rigidbody>().useGravity = true;
                targetBehavior.GetComponent<Rigidbody>().mass = 1.0f; // Normalize mass
                targetBehavior.GetComponent<Rigidbody>().linearDamping = brick.brickDrag;
                targetBehavior.GetComponent<Rigidbody>().angularDamping = brick.brickAngularDrag;
                brick.LogDebug($" FinalizeSnap() - Restored physics on target/master brick: isKinematic=false, useGravity=true");
            }
            else if (targetBehavior != null && targetBehavior.IsGrabbed)
            {
                brick.LogDebug($" FinalizeSnap() - Skipping physics restoration for grabbed target/master brick (XR system controls physics)", true);
            }
        }
        else if (targetBrick.IsBoard)
        {
            brick.LogDebug($" FinalizeSnap() - Skipping physics restoration for target board", true);
        }

        // The connection is now always symmetrical.
        brick.ConnectedNeighbors.Add(targetBrick);
        targetBrick.ConnectedNeighbors.Add(brick);
        brick.LogDebug($" FinalizeSnap() - Created symmetrical connection between {brick.name} and {targetBrick.name}");

        // --- NEW: Un-freeze the brick after snap is complete ---
        if (frozenBrick != null)
        {
            brick.LogDebug($"FinalizeSnap() - Un-freezing brick: {frozenBrick.name}");
            frozenBrick.Freeze(false);
            frozenBrick = null;
        }

        // Clear the reference
        snapTargetBrick = null;

        // Validate and correct final alignment (now using stored reference)
        ValidateAndCorrectAlignment(targetBrick);
        
        // IMPORTANT: Force the brick to the exact target rotation after physics is re-enabled
        // This ensures the brick doesn't drift due to physics calculations
        brick.transform.rotation = targetSnapRotation;
        brick.LogDebug($"FinalizeSnap() - DEBUG: FORCED brick to exact target rotation: {targetSnapRotation.eulerAngles}", false);

        // Clear any residual velocities to prevent oscillation
        // BUT only for non-board bricks
        if (!brick.IsBoard && brick.GetComponent<Rigidbody>() != null)
        {
            brick.GetComponent<Rigidbody>().linearVelocity = Vector3.zero;
            brick.GetComponent<Rigidbody>().angularVelocity = Vector3.zero;
            brick.LogDebug($" FinalizeSnap() - Cleared velocities to prevent oscillation");
        }
        else if (brick.IsBoard)
        {
            brick.LogDebug($" FinalizeSnap() - Skipping velocity clearing for board", true);
        }
        
        // IMPORTANT: Don't call physics managers during snap finalization
        // They will be called by the normal OnGrabReleased flow after snap is complete
        // This prevents conflicts between snap physics and normal physics management
        
        // Start a coroutine to stabilize the group after a short delay
        brick.StartCoroutine(StabilizeGroupAfterSnap());
        
        // --- MULTI-SNAP CONTINUATION ---
        // The call must be made on the brick that initiated the multi-snap sequence,
        // which may not be this brick.
        if (snapInitiator != null)
        {
            brick.LogDebug($"FinalizeSnap() - Calling OnSnapFinalized_MultiSnap on initiator {snapInitiator.name} for multi-snap continuation", true);
            snapInitiator.OnSnapFinalized_MultiSnap();
        }
        else
        {
            brick.LogWarning($"FinalizeSnap() - WARNING: snapInitiator is null, skipping multi-snap continuation");
        }
        
        // Clean up the initiator reference
        snapInitiator = null;
        
        brick.LogDebug($" FinalizeSnap() - Snap finalization complete");
    }

    // Method to validate and correct alignment after snapping
    private void ValidateAndCorrectAlignment(BrickBehavior targetBrick)
    {
        brick.LogDebug($" ValidateAndCorrectAlignment() - Validating final alignment");
        
        // Find the closest stud pair to validate alignment
        float minDistance = float.MaxValue;
        Stud closestMyStud = null;
        Stud closestTargetStud = null;
        
        // Check all our studs against all target studs
        foreach (var myStud in studManager.TopStuds)
        {
            foreach (var targetStud in targetBrick.BottomStuds)
            {
                float distance = Vector3.Distance(myStud.transform.position, targetStud.transform.position);
                if (distance < minDistance)
                {
                    minDistance = distance;
                    closestMyStud = myStud;
                    closestTargetStud = targetStud;
                }
            }
        }
        
        foreach (var myStud in studManager.BottomStuds)
        {
            foreach (var targetStud in targetBrick.TopStuds)
            {
                float distance = Vector3.Distance(myStud.transform.position, targetStud.transform.position);
                if (distance < minDistance)
                {
                    minDistance = distance;
                    closestMyStud = myStud;
                    closestTargetStud = targetStud;
                }
            }
        }
        
        brick.LogDebug($" ValidateAndCorrectAlignment() - Closest stud pair: {closestMyStud?.name} to {closestTargetStud?.name}, distance: {minDistance}");
        
        // ADD DETAILED SNAP POINT ALIGNMENT DEBUGGING
        if (closestMyStud != null && closestTargetStud != null)
        {
            brick.LogDebug($" ValidateAndCorrectAlignment() - DEBUG: DETAILED ALIGNMENT CHECK:", false);
            brick.LogDebug($" ValidateAndCorrectAlignment() - DEBUG: Our stud '{closestMyStud.name}' position: {closestMyStud.transform.position}", false);
            brick.LogDebug($" ValidateAndCorrectAlignment() - DEBUG: Target stud '{closestTargetStud.name}' position: {closestTargetStud.transform.position}", false);
            brick.LogDebug($" ValidateAndCorrectAlignment() - DEBUG: Position difference: {closestMyStud.transform.position - closestTargetStud.transform.position}", false);
            brick.LogDebug($" ValidateAndCorrectAlignment() - DEBUG: Distance: {minDistance:F6}", false);
            brick.LogDebug($" ValidateAndCorrectAlignment() - DEBUG: Snap tolerance: {brick.snapTolerance:F6}", false);
            brick.LogDebug($" ValidateAndCorrectAlignment() - DEBUG: Our brick position: {brick.transform.position}", false);
            brick.LogDebug($" ValidateAndCorrectAlignment() - DEBUG: Target brick position: {targetBrick.transform.position}", false);
            brick.LogDebug($" ValidateAndCorrectAlignment() - DEBUG: Our brick rotation: {brick.transform.rotation.eulerAngles}", false);
            brick.LogDebug($" ValidateAndCorrectAlignment() - DEBUG: Target brick rotation: {targetBrick.transform.rotation.eulerAngles}", false);
            
            // Check if our brick rotation has drifted from the target snap rotation
            float rotationDrift = Quaternion.Angle(brick.transform.rotation, targetSnapRotation);
            if (rotationDrift > 1f) // More than 1 degree of drift
            {
                brick.LogWarning($"ValidateAndCorrectAlignment() - WARNING: Brick rotation has drifted by {rotationDrift:F2}° from target!", true);
                brick.LogWarning($"ValidateAndCorrectAlignment() - Target was: {targetSnapRotation.eulerAngles}, Current is: {brick.transform.rotation.eulerAngles}", true);
            }
            else
            {
                brick.LogDebug($"ValidateAndCorrectAlignment() - Rotation drift check: {rotationDrift:F2}° (acceptable)", true);
            }
        }
        
        // Check if alignment is within tolerance
        if (minDistance > brick.snapTolerance * 2f)
        {
            brick.LogWarning($"[{brick.name}] ValidateAndCorrectAlignment() - WARNING: Alignment significantly off by {minDistance}, but not correcting to avoid position issues");
        }
        else
        {
            brick.LogDebug($" ValidateAndCorrectAlignment() - Alignment is good, distance: {minDistance}");
        }
    }

    private List<Stud> FindPotentialSnapPoints(Stud myStud, BrickBehavior targetBrick)
    {
        List<Stud> potentialPoints = new List<Stud>();
        
        // Get all studs on the target brick that are compatible with our stud
        List<Stud> targetStuds = (myStud.Type == Stud.StudType.Top) ? targetBrick.BottomStuds : targetBrick.TopStuds;
        
        brick.LogDebug($" FindPotentialSnapPoints() - Looking for {myStud.Type} studs on target brick ({targetStuds.Count} found)");
        
        // First, find all studs within a reasonable detection range
        List<Stud> nearbyStuds = new List<Stud>();
        float snapTolerance = brick.snapTolerance; // Use BrickBehavior's snapTolerance
        foreach (var targetStud in targetStuds)
        {
            float distance = Vector3.Distance(myStud.transform.position, targetStud.transform.position);
            
            // Use a tighter detection range to avoid false positives
            if (distance < snapTolerance * 1.5f) // Reduced from 2x to 1.5x
            {
                nearbyStuds.Add(targetStud);
            }
        }
        
        brick.LogDebug($" FindPotentialSnapPoints() - Found {nearbyStuds.Count} nearby studs");
        
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
            brick.LogDebug($" FindPotentialSnapPoints() - Found {potentialPoints.Count} available snap points");
            
            // Check if this would create a valid partial connection
            if (potentialPoints.Count < brick.minRequiredConnections)
            {
                brick.LogWarning($"[{brick.name}] FindPotentialSnapPoints() - WARNING: Only {potentialPoints.Count} snap points found, minimum required: {brick.minRequiredConnections}");
                // Don't clear the list, let the calling method decide
            }
        }
        else
        {
            brick.LogDebug($" FindPotentialSnapPoints() - No available snap points found (all nearby studs are occupied)");
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
                    brick.LogDebug($" IsStudOccupied() - Stud {stud.name} is occupied by {connectedBrick.name}");
                    return true;
                }
            }
        }
        
        return false;
    }
    
    // Helper method to check if this brick is already connected to a target brick
    private bool IsAlreadyConnectedTo(BrickBehavior targetBrick)
    {
        // For boards, check if there's actually a physical joint connection
        // since boards don't have joints, we should allow re-snapping
        if (targetBrick.IsBoard)
        {
            // Check if there's a joint connecting to this board
            ConfigurableJoint joint = brick.GetComponent<ConfigurableJoint>();
            if (joint != null && joint.connectedBody != null && joint.connectedBody.gameObject == targetBrick.gameObject)
            {
                brick.LogDebug($"IsAlreadyConnectedTo() - Found existing joint to board {targetBrick.name}");
                return true;
            }
            else
            {
                brick.LogDebug($"IsAlreadyConnectedTo() - No joint to board {targetBrick.name}, allowing re-snap");
                return false;
            }
        }
        
        // For regular bricks, check the neighbors list
        return brick.ConnectedNeighbors.Contains(targetBrick);
    }
    
    private Stud ChooseBestSnapPoint(Stud myStud, List<Stud> potentialPoints)
    {
        Stud bestStud = null;
        float bestDistance = float.MaxValue;
        
        brick.LogDebug($" ChooseBestSnapPoint() - Choosing from {potentialPoints.Count} potential points");
        
        if (potentialPoints.Count == 0)
        {
            brick.LogWarning($"[{brick.name}] ChooseBestSnapPoint() - WARNING: No potential points provided!");
            return null;
        }
        
        if (potentialPoints.Count == 1)
        {
            bestStud = potentialPoints[0];
            bestDistance = Vector3.Distance(myStud.transform.position, bestStud.transform.position);
            brick.LogDebug($" ChooseBestSnapPoint() - Only one potential point, selecting {bestStud.name}");
            return bestStud;
        }
        
        // Multiple points - find the closest one
        foreach (var potentialStud in potentialPoints)
        {
            float distance = Vector3.Distance(myStud.transform.position, potentialStud.transform.position);
            
            if (distance < bestDistance)
            {
                bestDistance = distance;
                bestStud = potentialStud;
            }
        }
        
        if (bestStud != null)
        {
            brick.LogDebug($" ChooseBestSnapPoint() - Selected {bestStud.name} with distance {bestDistance:F3}");
            
            // Additional validation: ensure the selected point is within reasonable distance
            if (bestDistance > brick.snapTolerance * 2f)
            {
                brick.LogWarning($"[{brick.name}] ChooseBestSnapPoint() - WARNING: Best snap point is quite far: {bestDistance:F3} > {brick.snapTolerance * 2f:F3}");
            }
        }
        else
        {
            brick.LogWarning($"[{brick.name}] ChooseBestSnapPoint() - WARNING: No valid snap point found!");
        }
        
        return bestStud;
    }

    // Method to find the best alignment for multiple stud connections
    private Stud FindBestMultiStudAlignment(Stud myStud, List<Stud> potentialPoints, BrickBehavior targetBrick)
    {
        brick.LogDebug($" FindBestMultiStudAlignment() - Finding best alignment for {potentialPoints.Count} potential points");
        
        Stud bestTargetStud = null;
        int maxConnectingStuds = 0;
        float bestDistance = float.MaxValue;
        
        // Try each potential target stud as the primary connection point
        foreach (var primaryTargetStud in potentialPoints)
        {
            brick.LogDebug($" FindBestMultiStudAlignment() - Testing primary target: {primaryTargetStud.name}");
            
            // Calculate the position our brick would be in if we connected to this stud
            Vector3 testPosition = CalculateTestPosition(myStud, primaryTargetStud, targetBrick);
            Quaternion testRotation = targetBrick.transform.rotation;
            
            // Count how many of our studs would connect to target studs at this position
            int connectingStuds = CountConnectingStuds(testPosition, testRotation, targetBrick);
            
            brick.LogDebug($" FindBestMultiStudAlignment() - Position {testPosition} would connect {connectingStuds} studs");
            
            // Prefer more connecting studs, then closer distance
            if (connectingStuds > maxConnectingStuds || 
                (connectingStuds == maxConnectingStuds && Vector3.Distance(myStud.transform.position, primaryTargetStud.transform.position) < bestDistance))
            {
                maxConnectingStuds = connectingStuds;
                bestDistance = Vector3.Distance(myStud.transform.position, primaryTargetStud.transform.position);
                bestTargetStud = primaryTargetStud;
                brick.LogDebug($" FindBestMultiStudAlignment() - New best: {primaryTargetStud.name} with {connectingStuds} connections");
            }
        }
        
        if (bestTargetStud != null)
        {
            brick.LogDebug($" FindBestMultiStudAlignment() - Selected {bestTargetStud.name} with {maxConnectingStuds} connecting studs");
        }
        else
        {
            brick.LogWarning($"[{brick.name}] FindBestMultiStudAlignment() - WARNING: No good multi-stud alignment found");
        }
        
        return bestTargetStud;
    }
    
    // Helper method to calculate test position for alignment checking
    private Vector3 CalculateTestPosition(Stud myStud, Stud targetStud, BrickBehavior targetBrick)
    {
        // Use the same logic as CalculateSnapTransform but return the position without setting it
        Vector3 myStudLocalPos = myStud.transform.localPosition;
        Vector3 targetStudWorldPos = targetStud.transform.position;
        Quaternion targetBrickRotation = targetBrick.transform.rotation;
        
        Vector3 myStudInTargetSpace = targetBrickRotation * myStudLocalPos;
        Vector3 testPosition = targetStudWorldPos - myStudInTargetSpace;
        
        return testPosition;
    }
    
    // Helper method to count how many studs would connect at a given position
    private int CountConnectingStuds(Vector3 testPosition, Quaternion testRotation, BrickBehavior targetBrick)
    {
        int connectingCount = 0;
        
        // Check all our studs against all target studs
        foreach (var myStud in studManager.TopStuds)
        {
            // Calculate where this stud would be at the test position
            Vector3 myStudLocalPos = myStud.transform.localPosition;
            Vector3 myStudInTestSpace = testRotation * myStudLocalPos;
            Vector3 myStudWorldPos = testPosition + myStudInTestSpace;
            
            foreach (var targetStud in targetBrick.BottomStuds)
            {
                float distance = Vector3.Distance(myStudWorldPos, targetStud.transform.position);
                if (distance < brick.snapTolerance)
                {
                    connectingCount++;
                }
            }
        }
        
        foreach (var myStud in studManager.BottomStuds)
        {
            // Calculate where this stud would be at the test position
            Vector3 myStudLocalPos = myStud.transform.localPosition;
            Vector3 myStudInTestSpace = testRotation * myStudLocalPos;
            Vector3 myStudWorldPos = testPosition + myStudInTestSpace;
            
            foreach (var targetStud in targetBrick.TopStuds)
            {
                float distance = Vector3.Distance(myStudWorldPos, targetStud.transform.position);
                if (distance < brick.snapTolerance)
                {
                    connectingCount++;
                }
            }
        }
        
        brick.LogDebug($" CountConnectingStuds() - Found {connectingCount} connecting studs");
        return connectingCount;
    }

    private IEnumerator StabilizeGroupAfterSnap()
    {
        brick.LogDebug($" StabilizeGroupAfterSnap() - Starting group stabilization after snap");
        
        // Wait a short delay to let physics settle
        yield return new WaitForSeconds(0.1f);
        
        // Find all bricks in the connected group
        List<BrickBehavior> groupBricks = new List<BrickBehavior>();
        BrickGroupUtils.FindAllConnectedInGroup(brick, groupBricks, brick.name);
        
        brick.LogDebug($" StabilizeGroupAfterSnap() - Found {groupBricks.Count} bricks in group to stabilize");
        
        // Stabilize each brick in the group
        foreach (var groupBrick in groupBricks)
        {
            // Skip boards - they should not be stabilized
            if (groupBrick.IsBoard)
            {
                brick.LogDebug($" StabilizeGroupAfterSnap() - Skipping stabilization for board {groupBrick.name}");
                continue;
            }

            if (groupBrick.GetComponent<Rigidbody>() != null && !groupBrick.IsGrabbed)
            {
                // Clear any residual velocities
                groupBrick.GetComponent<Rigidbody>().linearVelocity = Vector3.zero;
                groupBrick.GetComponent<Rigidbody>().angularVelocity = Vector3.zero;
                
                // Ensure proper physics state
                groupBrick.GetComponent<Rigidbody>().isKinematic = false;
                groupBrick.GetComponent<Rigidbody>().useGravity = true;
                
                brick.LogDebug($" StabilizeGroupAfterSnap() - Stabilized {groupBrick.name}");
            }
        }
        
        brick.LogDebug($" StabilizeGroupAfterSnap() - Group stabilization complete");
    }

    public void Cleanup()
    {
        // Clear any stored references
        snapTargetBrick = null;
        targetSnapPosition = Vector3.zero;
        targetSnapRotation = Quaternion.identity;
        
        brick.LogDebug($" Cleanup() - Snap system cleanup complete");
    }
} 
