// BrickBehavior.cs
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using System.Collections.Generic;
using System.Collections;

[RequireComponent(typeof(XRGrabInteractable), typeof(Rigidbody))]
public class BrickBehavior : MonoBehaviour
{
    // ========================================
    // CONSTANTS AND ENUMS
    // ========================================
    private enum BrickState
    {
        Idle,
        Grabbing,
        Snapping,
        Snapped
    }

    // ========================================
    // SERIALIZED FIELDS
    // ========================================
    [Header("Snapping Properties")]
    [Tooltip("How close two studs must be to be considered a valid connection (in meters). Lower values require more precision.")]
    public float snapTolerance = 0.01f;

    [Tooltip("How quickly the brick animates into its snapped position.")]
    public float snapSpeed = 15f;

    [Tooltip("Maximum distance (in meters) for valid snap detection between studs. Higher values make snapping easier.")]
    public float maxSnapDistance = 0.05f;

    [Tooltip("Minimum distance (in meters) to start collision detection between studs. Higher values may improve responsiveness but reduce precision.")]
    public float minCollisionDistance = 0.1f;

    [Header("Group Operations")]
    [Tooltip("Distance (in meters) within which two bricks/groups, each grabbed by a different controller, can join. Higher values make group joining easier.")]
    public float groupJoinThreshold = 0.15f;

    [Tooltip("Stricter distance (in meters) for joining any grabbed bricks. Used for more precise group joining.")]
    public float groupJoinStrictThreshold = 0.1f;

    [Tooltip("Distance (in meters) to separate groups after a split to prevent immediate re-snapping.")]
    public float groupSplitSeparation = 0.2f;

    [Header("Physics Properties")]
    [Tooltip("Linear drag coefficient for the brick's rigidbody. Higher values make the brick more stable and less likely to slide.")]
    public float brickDrag = 2.0f;

    [Tooltip("Angular drag coefficient for the brick's rigidbody. Higher values make the brick more stable and less likely to rotate unexpectedly.")]
    public float brickAngularDrag = 2.0f;

    [Tooltip("Physics material for additional friction control. Create a material with high static friction (0.8-1.0) for better stability.")]
    public PhysicsMaterial brickPhysicsMaterial;

    [Header("Performance & Timing")]
    [Tooltip("Cooldown (in seconds) to prevent repeated collision events between studs.")]
    public float collisionCooldown = 0.1f;

    [Tooltip("Delay (in seconds) after release before resetting the snap flag. Controls how long after release a snap can still occur.")]
    public float releaseFlagDelay = 0.2f;

    [Tooltip("The minimum number of stud/anti-stud pairs that must align for a snap to occur.")]
    public int minRequiredConnections = 1;

    // ========================================
    // PRIVATE FIELDS - COMPONENTS
    // ========================================
    private XRGrabInteractable grabInteractable;
    private Rigidbody rb;
    private Transform originalParent;

    // ========================================
    // PRIVATE FIELDS - STATE
    // ========================================
    public bool isSnapping = false;
    private Vector3 targetSnapPosition;
    private Quaternion targetSnapRotation;
    private BrickBehavior snapTargetBrick;
    private bool justReleased = false;
    private BrickState currentState = BrickState.Idle;

    // Snap immunity system to prevent re-snapping after splits
    public float snapImmunityEndTime = 0f;
    private const float SNAP_IMMUNITY_DURATION = 1.0f; // 1 second of immunity after split

    // Potential snap storage system
    private Stud potentialSnapStud = null;
    private Stud potentialSnapTargetStud = null;
    
    // Store the last grab position to avoid XR Grab Interactable interference
    private Vector3 lastGrabPosition;
    private Quaternion lastGrabRotation;

    // ========================================
    // MANAGER REFERENCES
    // ========================================
    private BrickStudManager studManager;
    private BrickSnappingSystem snappingSystem;
    private BrickConnectionManager connectionManager;
    private BrickPhysicsManager physicsManager;
    private BrickGroupOperations groupOperations;

    // ========================================
    // PUBLIC PROPERTIES
    // ========================================
    public List<Stud> TopStuds => studManager?.TopStuds ?? new List<Stud>();
    public List<Stud> BottomStuds => studManager?.BottomStuds ?? new List<Stud>();
    public List<BrickBehavior> ConnectedNeighbors => connectionManager?.ConnectedNeighbors ?? new List<BrickBehavior>();
    public FixedJoint Joint => connectionManager?.Joint;
    public BrickBehavior MasterBrick => connectionManager?.MasterBrick ?? this;
    public BrickBehavior OriginalMaster => connectionManager?.OriginalMaster ?? this;
    public BrickConnectionManager ConnectionManager => connectionManager;

    // ========================================
    // PUBLIC METHODS
    // ========================================

    // Public method for studs to check if the brick is in a snappable state.
    public bool IsReadyForSnap()
    {
        // Check if we're in snap immunity period (after a split)
        if (Time.time < snapImmunityEndTime)
        {
            Debug.Log($"[{name}] IsReadyForSnap() - In snap immunity period, cannot snap until {snapImmunityEndTime:F2}");
            return false;
        }

        // Allow snapping if:
        // 1. Just released (original logic)
        // 2. Currently grabbing (allow snapping while holding)
        // 3. Idle state (allow snapping for free-floating bricks)
        bool canSnap = justReleased ||
                       currentState == BrickState.Grabbing ||
                       currentState == BrickState.Idle;

        Debug.Log($"[{name}] IsReadyForSnap() - justReleased={justReleased}, currentState={currentState}, canSnap={canSnap}");
        return canSnap;
    }

    // Debug method to check brick state
    [ContextMenu("Check Brick State")]
    public void CheckBrickState()
    {
        Debug.Log($"[{name}] CheckBrickState() - Current state: {currentState}");
        Debug.Log($"[{name}] CheckBrickState() - justReleased: {justReleased}");
        Debug.Log($"[{name}] CheckBrickState() - isSnapping: {isSnapping}");
        Debug.Log($"[{name}] CheckBrickState() - IsReadyForSnap: {IsReadyForSnap()}");
        Debug.Log($"[{name}] CheckBrickState() - Connected neighbors: {ConnectedNeighbors.Count}");
        Debug.Log($"[{name}] CheckBrickState() - Master brick: {MasterBrick?.name ?? "null"}");
        Debug.Log($"[{name}] CheckBrickState() - Potential snap: {potentialSnapStud?.name ?? "null"} to {potentialSnapTargetStud?.name ?? "null"}");
    }

    // Public method to manually strengthen all connections in a structure
    [ContextMenu("Strengthen Structure")]
    public void StrengthenStructure()
    {
        Debug.Log($"[{name}] StrengthenStructure() - Manually strengthening structure");
        connectionManager?.StrengthenGroupConnections();
        physicsManager?.StabilizeGroup();
    }

    // ========================================
    // UNITY LIFECYCLE
    // ========================================

    void Awake()
    {
        Debug.Log($"[{name}] Awake() - Initializing BrickBehavior");

        // Get required components
        grabInteractable = GetComponent<XRGrabInteractable>();
        rb = GetComponent<Rigidbody>();
        originalParent = transform.parent;

        Debug.Log($"[{name}] Awake() - Components acquired: XRGrabInteractable={grabInteractable != null}, Rigidbody={rb != null}, OriginalParent={originalParent?.name ?? "null"}");

        // Apply physics material for better friction if assigned
        if (brickPhysicsMaterial != null)
        {
            var collider = GetComponent<Collider>();
            if (collider != null)
            {
                collider.material = brickPhysicsMaterial;
                Debug.Log($"[{name}] Awake() - Applied physics material: {brickPhysicsMaterial.name}");
            }
        }

        // Initialize managers
        InitializeManagers();

        // Set up event listeners
        grabInteractable.selectEntered.AddListener(OnGrabStarted);
        grabInteractable.selectExited.AddListener(OnGrabReleased);

        Debug.Log($"[{name}] Awake() - Event listeners attached");

        // Validate initial physics state
        physicsManager?.ValidatePhysicsState();
        
        Debug.Log($"[{name}] Awake() - Initialization complete");
    }

    void Update()
    {
        // Periodically check for group joining opportunities when grabbed
        if (grabInteractable.isSelected && currentState == BrickState.Grabbing)
        {
            // Check every 10 frames (about 6 times per second at 60fps)
            if (Time.frameCount % 10 == 0)
            {
                groupOperations?.CheckForGroupJoiningOpportunities();
            }
        }
    }
    
    void FixedUpdate()
    {
        if (isSnapping && currentState == BrickState.Snapping)
        {
            // Smooth snap animation using lerp/slerp with fixed timestep
            // This provides more consistent timing than variable frame rate
            float snapSpeed = this.snapSpeed; // Use the serialized snapSpeed property
            
            // Adjust for fixed timestep - Time.fixedDeltaTime is typically 0.02 (50fps)
            // This provides more controlled animation speed
            float lerpFactor = Mathf.Clamp01(snapSpeed * Time.fixedDeltaTime);
            
            // Lerp position towards target with fixed timestep
            transform.position = Vector3.Lerp(transform.position, targetSnapPosition, lerpFactor);
            
            // Slerp rotation towards target with fixed timestep
            transform.rotation = Quaternion.Slerp(transform.rotation, targetSnapRotation, lerpFactor);
            
            // Check if we're close enough to consider the snap complete
            float positionDistance = Vector3.Distance(transform.position, targetSnapPosition);
            float rotationDistance = Quaternion.Angle(transform.rotation, targetSnapRotation);
            
            // Use a threshold that accounts for the snapOffset (0.001f) plus some tolerance
            float completionThreshold = 0.002f; // 2mm threshold to account for offset + tolerance
            float rotationThreshold = 0.1f; // Much tighter rotation threshold (0.1 degrees)
            
            if (positionDistance < completionThreshold && rotationDistance < rotationThreshold) // Within 2mm and 0.1 degrees
            {
                Debug.Log($"[{name}] FixedUpdate() - Snap animation complete - position: {transform.position}, rotation: {transform.rotation.eulerAngles}");
                Debug.Log($"[{name}] FixedUpdate() - Final distance to target: {positionDistance:F6}, threshold: {completionThreshold}");
                
                // Snap to exact target to ensure perfect alignment
                transform.position = targetSnapPosition;
                transform.rotation = targetSnapRotation;
                
                // Finalize the snap
                snappingSystem?.FinalizeSnap();
            }
        }
    }
    
    void LateUpdate()
    {
        // Update the last grab position continuously while grabbing
        // This ensures we have the most recent position before XR Grab Interactable modifies it
        if (currentState == BrickState.Grabbing && grabInteractable.isSelected)
        {
            lastGrabPosition = transform.position;
            lastGrabRotation = transform.rotation;
        }
    }

    void OnDestroy()
    {
        Debug.Log($"[{name}] OnDestroy() - Cleaning up BrickBehavior");
        
        // Remove event listeners
        if (grabInteractable != null)
        {
            grabInteractable.selectEntered.RemoveListener(OnGrabStarted);
            grabInteractable.selectExited.RemoveListener(OnGrabReleased);
            Debug.Log($"[{name}] OnDestroy() - Event listeners removed");
        }
        
        // Clean up managers
        studManager?.Cleanup();
        snappingSystem?.Cleanup();
        connectionManager?.Cleanup();
        physicsManager?.Cleanup();
        groupOperations?.Cleanup();
        
        Debug.Log($"[{name}] OnDestroy() - Cleanup complete");
    }

    // ========================================
    // PRIVATE METHODS
    // ========================================
    
    private void InitializeManagers()
    {
        studManager = new BrickStudManager(this);
        snappingSystem = new BrickSnappingSystem(this, studManager);
        connectionManager = new BrickConnectionManager(this);
        physicsManager = new BrickPhysicsManager(this);
        groupOperations = new BrickGroupOperations(this);
        
        Debug.Log($"[{name}] InitializeManagers() - All managers initialized");
    }

    private void OnGrabStarted(SelectEnterEventArgs args)
    {
        Debug.Log($"[{name}] OnGrabStarted() - Brick grabbed, previous state: {currentState}");
        
        // Get the interactor that grabbed this brick
        UnityEngine.XR.Interaction.Toolkit.Interactors.IXRSelectInteractor interactor = args.interactorObject;
        if (interactor == null)
        {
            Debug.LogWarning($"[{name}] OnGrabStarted() - WARNING: Interactor is null");
            return;
        }
        
        Debug.Log($"[{name}] OnGrabStarted() - Grabbed by interactor: {interactor.transform.name}");

        // Check if this brick is already being grabbed by a different interactor
        if (grabInteractable.isSelected && grabInteractable.firstInteractorSelecting != interactor)
        {
            Debug.LogWarning($"[{name}] OnGrabStarted() - WARNING: Brick already grabbed by different interactor: {grabInteractable.firstInteractorSelecting?.transform.name}");
            return;
        }

        // IMPORTANT: Check if this brick is part of a connected group
        if (ConnectedNeighbors.Count > 0)
        {
            Debug.Log($"[{name}] OnGrabStarted() - Brick is part of connected group with {ConnectedNeighbors.Count} neighbors");
            
            // Find all bricks in the connected group
            List<BrickBehavior> allGroupBricks = new List<BrickBehavior>();
            BrickGroupUtils.FindAllConnectedInGroup(this, allGroupBricks, name);
            Debug.Log($"[{name}] OnGrabStarted() - Found {allGroupBricks.Count} total bricks in group");
            
            // Check if any other brick in the group is already being grabbed by a different interactor
            bool hasOtherGrabbedBrick = false;
            UnityEngine.XR.Interaction.Toolkit.Interactors.IXRSelectInteractor otherInteractor = null;
            
            foreach (var groupBrick in allGroupBricks)
            {
                if (groupBrick != this && groupBrick.GetComponent<UnityEngine.XR.Interaction.Toolkit.Interactables.XRGrabInteractable>().isSelected)
                {
                    hasOtherGrabbedBrick = true;
                    otherInteractor = groupBrick.GetComponent<UnityEngine.XR.Interaction.Toolkit.Interactables.XRGrabInteractable>().firstInteractorSelecting;
                    Debug.Log($"[{name}] OnGrabStarted() - Found other grabbed brick in group: {groupBrick.name} by interactor: {otherInteractor?.transform.name}");
                    break;
                }
            }
            
            // If another brick in the group is grabbed by a different interactor, allow this grab (multi-controller scenario)
            if (hasOtherGrabbedBrick && otherInteractor != interactor)
            {
                Debug.Log($"[{name}] OnGrabStarted() - Multi-controller scenario detected - allowing grab for group manipulation");
            }
            // If another brick is grabbed by the same interactor, prevent duplicate grabs
            else if (hasOtherGrabbedBrick && otherInteractor == interactor)
            {
                Debug.LogWarning($"[{name}] OnGrabStarted() - WARNING: Same interactor already grabbing another brick in group - preventing duplicate grab");
                
                // Force release the grab
                grabInteractable.interactionManager.CancelInteractableSelection((UnityEngine.XR.Interaction.Toolkit.Interactables.IXRSelectInteractable)grabInteractable);
                return;
            }
            // If no other brick is grabbed, allow single-hand grab of the group
            else
            {
                Debug.Log($"[{name}] OnGrabStarted() - Single-hand grab of connected group allowed - no other bricks currently grabbed");
            }
        }
        else
        {
            Debug.Log($"[{name}] OnGrabStarted() - Brick is standalone - allowing grab");
        }

        // Clear any stored potential snap when grabbing starts
        ClearPotentialSnap();

        // Handle grab logic through managers
        connectionManager?.OnGrabStarted(interactor);
        physicsManager?.OnGrabStarted();

        currentState = BrickState.Grabbing;
        isSnapping = false;
        
        // Store the initial grab position for potential snap calculations
        lastGrabPosition = transform.position;
        lastGrabRotation = transform.rotation;
        Debug.Log($"[{name}] OnGrabStarted() - Stored initial grab position: {lastGrabPosition}, rotation: {lastGrabRotation.eulerAngles}");
        
        Debug.Log($"[{name}] OnGrabStarted() - State updated to: {currentState}");

        // Check for unsnap conditions
        groupOperations?.CheckForUnsnapConditions(interactor);
    }

    private void OnGrabReleased(SelectExitEventArgs args)
    {
        Debug.Log($"[{name}] OnGrabReleased() - Brick released, current state: {currentState}");
        
        if (currentState == BrickState.Grabbing)
        {
            // IMPORTANT: Use the last known grab position instead of current position
            // This avoids issues with XR Grab Interactable modifying the position during release
            Vector3 releasePosition = lastGrabPosition;
            Quaternion releaseRotation = lastGrabRotation;
            Debug.Log($"[{name}] OnGrabReleased() - Using last grab position: {releasePosition}, rotation: {releaseRotation.eulerAngles}");
            
            justReleased = true;
            Debug.Log($"[{name}] OnGrabReleased() - Set justReleased flag to true");
        
            // Reset the flag after a short delay to prevent accidental snaps later
            Invoke(nameof(ResetReleaseFlag), releaseFlagDelay);
            Debug.Log($"[{name}] OnGrabReleased() - Scheduled ResetReleaseFlag in {releaseFlagDelay} seconds");

            // Execute any stored potential snap with the captured release position
            ExecuteStoredSnap(releasePosition, releaseRotation);

            // Handle release logic through managers (but delay physics until snap is complete)
            connectionManager?.OnGrabReleased();
            
            // Delay physics manager call to avoid conflicts during snap animation
            StartCoroutine(DelayedPhysicsManagerCall());
        }
        else
        {
            Debug.Log($"[{name}] OnGrabReleased() - Ignored release (not in Grabbing state)");
        }
    }

    // Resets the flag so that snapping can only be initiated immediately after release.
    private void ResetReleaseFlag()
    {
        Debug.Log($"[{name}] ResetReleaseFlag() - Resetting justReleased flag");
        justReleased = false;
    
        // If a snap hasn't started by now, we go back to being idle
        if (currentState != BrickState.Snapping && currentState != BrickState.Snapped)
        {
            currentState = BrickState.Idle;
            Debug.Log($"[{name}] ResetReleaseFlag() - State reset to: {currentState}");
        }
        else
        {
            Debug.Log($"[{name}] ResetReleaseFlag() - State unchanged: {currentState}");
        }
    }

    // Method to store a potential snap connection (called during collision detection)
    public void StorePotentialSnap(Stud ourStud, Stud targetStud)
    {
        Debug.Log($"[{name}] StorePotentialSnap() - Storing potential snap from {ourStud.name} to {targetStud.name}");
        
        // Check if this is a potential group joining scenario
        groupOperations?.CheckForGroupJoiningDuringCollision(ourStud, targetStud);
        
        // Only store if we don't already have a potential snap
        if (potentialSnapStud == null)
        {
            potentialSnapStud = ourStud;
            potentialSnapTargetStud = targetStud;
            Debug.Log($"[{name}] StorePotentialSnap() - Potential snap stored");
        }
        else
        {
            Debug.Log($"[{name}] StorePotentialSnap() - Already have a potential snap, ignoring new one");
        }
    }

    // Method to execute the stored potential snap (called after release)
    private void ExecuteStoredSnap(Vector3 releasePosition, Quaternion releaseRotation)
    {
        if (potentialSnapStud != null && potentialSnapTargetStud != null)
        {
            Debug.Log($"[{name}] ExecuteStoredSnap() - Executing stored snap from {potentialSnapStud.name} to {potentialSnapTargetStud.name}");
            Debug.Log($"[{name}] ExecuteStoredSnap() - Using release position: {releasePosition}, rotation: {releaseRotation.eulerAngles}");
            
            // IMPORTANT: Restore the brick to its release position before calculating snap
            transform.position = releasePosition;
            transform.rotation = releaseRotation;
            Debug.Log($"[{name}] ExecuteStoredSnap() - Restored brick to release position: {transform.position}");
            
            // Temporarily disable collision detection on all studs to prevent multiple collisions during snap
            studManager?.DisableStudCollisions();
            
            // Execute the actual snap
            snappingSystem?.RequestSnap(potentialSnapStud, potentialSnapTargetStud);
            
            // Clear the stored snap
            potentialSnapStud = null;
            potentialSnapTargetStud = null;
        }
    }

    // Method to clear stored potential snap
    private void ClearPotentialSnap()
    {
        // Clear stored snap references
        potentialSnapStud = null;
        potentialSnapTargetStud = null;
        
        // Reset all stud states to idle
        foreach (var stud in TopStuds)
        {
            stud.ClearSnapRangeState();
        }
        foreach (var stud in BottomStuds)
        {
            stud.ClearSnapRangeState();
        }
        
        Debug.Log($"[{name}] ClearPotentialSnap() - Cleared potential snap and reset stud states");
    }

    // ========================================
    // PUBLIC INTERFACE FOR MANAGERS
    // ========================================
    
    public void SetSnappingState(bool snapping, Vector3 targetPos, Quaternion targetRot, BrickBehavior targetBrick)
    {
        Debug.Log($"[{name}] SetSnappingState() - DEBUG: Setting isSnapping from {isSnapping} to {snapping}");
        isSnapping = snapping;
        targetSnapPosition = targetPos;
        targetSnapRotation = targetRot;
        snapTargetBrick = targetBrick;
        currentState = snapping ? BrickState.Snapping : BrickState.Idle;
        Debug.Log($"[{name}] SetSnappingState() - DEBUG: State changed to {currentState}");
    }

    public void ActivateSnapImmunity()
    {
        snapImmunityEndTime = Time.time + SNAP_IMMUNITY_DURATION;
        Debug.Log($"[{name}] ActivateSnapImmunity() - Snap immunity activated until {snapImmunityEndTime:F2}");
    }

    public void EnableStudCollisions()
    {
        studManager?.EnableStudCollisions();
    }

    public void UpdateMaster(BrickBehavior newMaster)
    {
        connectionManager?.UpdateMaster(newMaster);
    }

    public void RemoveNeighbor(BrickBehavior neighbor)
    {
        connectionManager?.RemoveNeighbor(neighbor);
    }

    public void SetJoint(FixedJoint joint)
    {
        connectionManager?.SetJoint(joint);
    }

    public static bool AreBricksInSameGroup(BrickBehavior brick1, BrickBehavior brick2)
    {
        return BrickGroupUtils.AreBricksInSameGroup(brick1, brick2);
    }

    // Coroutine to delay physics manager call until snap animation is complete
    private System.Collections.IEnumerator DelayedPhysicsManagerCall()
    {
        Debug.Log($"[{name}] DelayedPhysicsManagerCall() - Starting delayed physics manager call");
        Debug.Log($"[{name}] DelayedPhysicsManagerCall() - DEBUG: Initial isSnapping state: {isSnapping}");
        
        // Wait for snap animation to complete with timeout
        int waitCount = 0;
        const int MAX_WAIT_FRAMES = 60; // 1 second at 60fps
        
        while (isSnapping && waitCount < MAX_WAIT_FRAMES)
        {
            waitCount++;
            if (waitCount % 20 == 0) // Log every 20 frames (about 0.3 seconds at 60fps)
            {
                Debug.Log($"[{name}] DelayedPhysicsManagerCall() - DEBUG: Still waiting for snap to complete, frame {waitCount}, isSnapping: {isSnapping}");
            }
            yield return null;
        }
        
        if (waitCount >= MAX_WAIT_FRAMES)
        {
            Debug.LogWarning($"[{name}] DelayedPhysicsManagerCall() - WARNING: Timeout reached! Force completing snap after {waitCount} frames");
            // Force complete the snap only if we have a valid snap system
            if (snappingSystem != null)
            {
                isSnapping = false;
                currentState = BrickState.Idle;
                snappingSystem.FinalizeSnap();
            }
            else
            {
                Debug.LogWarning($"[{name}] DelayedPhysicsManagerCall() - WARNING: Snap system is null, cannot force finalize");
                isSnapping = false;
                currentState = BrickState.Idle;
            }
        }
        
        Debug.Log($"[{name}] DelayedPhysicsManagerCall() - DEBUG: Waited {waitCount} frames for snap to complete");
        
        // Now call physics manager after snap is complete
        Debug.Log($"[{name}] DelayedPhysicsManagerCall() - Snap animation complete, calling physics manager");
        
        var rb = GetComponent<Rigidbody>();
        if (rb != null)
        {
            Debug.Log($"[{name}] DelayedPhysicsManagerCall() - DEBUG: Physics before calling manager - isKinematic: {rb.isKinematic}, useGravity: {rb.useGravity}, velocity: {rb.linearVelocity}");
        }
        
        physicsManager?.OnGrabReleased();
        
        if (rb != null)
        {
            Debug.Log($"[{name}] DelayedPhysicsManagerCall() - DEBUG: Physics after calling manager - isKinematic: {rb.isKinematic}, useGravity: {rb.useGravity}, velocity: {rb.linearVelocity}");
        }
    }
} 