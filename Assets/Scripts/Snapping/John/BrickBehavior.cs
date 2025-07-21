// BrickBehavior.cs
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using System.Collections.Generic;
using System.Collections;

[RequireComponent(typeof(Rigidbody))]
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
    
    public enum DebugLevel
    {
        NoDebug,        // No debug messages
        LiteDebug,      // Only crucial debug messages that help understand the flow
        NormalDebug,    // All current log messages except the ones with DEBUG:
        ExtensiveDebug  // All current log messages including the ones with DEBUG:
    }

    // ========================================
    // SERIALIZED FIELDS
    // ========================================
    [Header("Object Type")]
    [Tooltip("Board detection is automatic based on GameObject tag. Set tag to 'Board' for boards, 'Brick' for bricks.")]
    [SerializeField] private bool isBoard = false; // This will be set automatically based on tag

    [Header("Snapping Properties")]
    [Tooltip("How close two studs must be to be considered a valid connection (in meters). Lower values require more precision.")]
    public float snapTolerance = 0.01f;

    [Tooltip("How quickly the brick animates into its snapped position. Higher values = faster animation. Typical range: 5-50. Lower values prevent overshooting but may feel sluggish.")]
    public float snapSpeed = 30f; // Increased from 8f for faster, more responsive snapping

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

    [Header("Animation Tolerances")]
    [Tooltip("Maximum allowed increase in position distance during snap animation (in meters). Lower values prevent overshooting but may slow animation.")]
    public float positionTolerance = 0.001f; // 1mm

    [Tooltip("Maximum allowed increase in rotation angle during snap animation (in degrees). Lower values prevent overshooting but may slow animation.")]
    public float rotationTolerance = 0.2f; // 0.2°

    [Tooltip("Final position accuracy required for snap completion (in meters). Must be greater than positionTolerance.")]
    public float completionThreshold = 0.002f; // 2mm

    [Tooltip("Final rotation accuracy required for snap completion (in degrees). Must be greater than rotationTolerance.")]
    public float rotationThreshold = 0.5f; // 0.5°

    [Header("Debug Settings")]
    [Tooltip("Controls the level of debug output. NoDebug=no messages, LiteDebug=only crucial flow messages, NormalDebug=all except DEBUG:, ExtensiveDebug=all messages.")]
    public DebugLevel debugLevel = DebugLevel.NormalDebug;

    // ========================================
    // PUBLIC FIELDS - COMPONENTS
    // ========================================
    public BrickSnappingSystem snappingSystem;
    public BrickConnectionManager connectionManager;
    public BrickPhysicsManager physicsManager;
    public BrickGroupOperations groupOperations;
    public BrickStudManager studManager;

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

    // --- NEW: State for freezing grabbed bricks ---
    private bool originalTrackPosition;
    private bool originalTrackRotation;

    // --- MULTI-SNAP STATE ---
    private Queue<(Stud, Stud, Vector3, Quaternion)> pendingMultiSnaps = new Queue<(Stud, Stud, Vector3, Quaternion)>();
    private bool isMultiSnapInProgress = false;

    // ========================================
    // MANAGER REFERENCES
    // ========================================
    // private BrickStudManager studManager; // This line is removed as it's now public
    // private BrickSnappingSystem snappingSystem; // This line is removed as it's now public
    // private BrickConnectionManager connectionManager; // This line is removed as it's now public
    // private BrickPhysicsManager physicsManager; // This line is removed as it's now public
    // private BrickGroupOperations groupOperations; // This line is removed as it's now public

    // ========================================
    // PUBLIC PROPERTIES
    // ========================================
    public List<Stud> TopStuds => studManager?.TopStuds ?? new List<Stud>();
    public List<Stud> BottomStuds => studManager?.BottomStuds ?? new List<Stud>();
    public List<BrickBehavior> ConnectedNeighbors => connectionManager?.ConnectedNeighbors ?? new List<BrickBehavior>();
    public ConfigurableJoint Joint => connectionManager?.Joint;
    public BrickBehavior MasterBrick => connectionManager?.MasterBrick ?? this;
    public BrickBehavior OriginalMaster => connectionManager?.OriginalMaster ?? this;
    public BrickConnectionManager ConnectionManager => connectionManager;

    // ========================================
    // BOARD-SPECIFIC PROPERTIES
    // ========================================
    public bool IsBoard => isBoard;
    public bool IsGrabbable => !isBoard && grabInteractable != null;
    public bool IsGrabbed => IsGrabbable && grabInteractable.isSelected;
    
    // Public property to check the current tag (for debugging)
    public string CurrentTag => gameObject.tag;

    // ========================================
    // DEBUG LOGGING HELPERS
    // ========================================
    
    public void LogDebug(string message, bool isNormalOrExtensiveDebug = false)
    {
        if (debugLevel == DebugLevel.NoDebug) return;
        
        // Check if message contains "DEBUG:" - these should only be logged in ExtensiveDebug mode
        bool containsDebug = message.Contains("DEBUG:");
        
        // Determine if this message should be logged based on debug level and content
        bool shouldLog = false;
        
        switch (debugLevel)
        {
            case DebugLevel.LiteDebug:
                // Only log non-DEBUG messages that aren't normal or extensive
                shouldLog = !containsDebug && !isNormalOrExtensiveDebug;
                break;
                
            case DebugLevel.NormalDebug:
                // Log non-DEBUG messages, and normal/extensive messages if flagged
                shouldLog = !containsDebug || (containsDebug && isNormalOrExtensiveDebug);
                break;
                
            case DebugLevel.ExtensiveDebug:
                // Log everything
                shouldLog = true;
                break;
        }
        
        if (shouldLog)
        {
            // Remove double spaces and trim leading/trailing spaces
            Debug.Log($"[{name}] {message.Trim()}");
        }
    }
    
    public void LogWarning(string message, bool isNormalOrExtensiveDebug = false)
    {
        if (debugLevel == DebugLevel.NoDebug) return;
        
        // Check if message contains "DEBUG:" - these should only be logged in ExtensiveDebug mode
        bool containsDebug = message.Contains("DEBUG:");
        
        // Determine if this message should be logged based on debug level and content
        bool shouldLog = false;
        
        switch (debugLevel)
        {
            case DebugLevel.LiteDebug:
                // Only log non-DEBUG messages that aren't normal or extensive
                shouldLog = !containsDebug && !isNormalOrExtensiveDebug;
                break;
                
            case DebugLevel.NormalDebug:
                // Log non-DEBUG messages, and normal/extensive messages if flagged
                shouldLog = !containsDebug || (containsDebug && isNormalOrExtensiveDebug);
                break;
                
            case DebugLevel.ExtensiveDebug:
                // Log everything
                shouldLog = true;
                break;
        }
        
        if (shouldLog)
        {
            // Remove double spaces and trim leading/trailing spaces
            Debug.LogWarning($"[{name}] {message.Trim()}");
        }
    }

    // ========================================
    // PUBLIC METHODS
    // ========================================

    // Public method for studs to check if the brick is in a snappable state.
    public bool IsReadyForSnap()
    {
        // Boards are always ready for snapping (they can't be grabbed)
        if (isBoard)
        {
            LogDebug($"IsReadyForSnap() - Board is always ready for snapping", true);
            return true;
        }

        // Check if we're in snap immunity period (after a split)
        if (Time.time < snapImmunityEndTime)
        {
            LogDebug($"IsReadyForSnap() - In snap immunity period, cannot snap until {snapImmunityEndTime:F2}", true);
            return false;
        }

        // Allow snapping if:
        // 1. Just released (original logic)
        // 2. Currently grabbing (allow snapping while holding)
        // 3. Idle state (allow snapping for free-floating bricks)
        bool canSnap = justReleased ||
                       currentState == BrickState.Grabbing ||
                       currentState == BrickState.Idle;

        LogDebug($"IsReadyForSnap() - justReleased={justReleased}, currentState={currentState}, canSnap={canSnap}", true);
        return canSnap;
    }

    // Debug method to check brick state
    [ContextMenu("Check Brick State")]
    public void CheckBrickState()
    {
        LogDebug($"CheckBrickState() - Current state: {currentState}");
        LogDebug($"CheckBrickState() - justReleased: {justReleased}", true);
        LogDebug($"CheckBrickState() - isSnapping: {isSnapping}", true);
        LogDebug($"CheckBrickState() - IsReadyForSnap: {IsReadyForSnap()}", true);
        LogDebug($"CheckBrickState() - Connected neighbors: {ConnectedNeighbors.Count}", true);
        LogDebug($"CheckBrickState() - Master brick: {MasterBrick?.name ?? "null"}", true);
        LogDebug($"CheckBrickState() - Potential snap: {potentialSnapStud?.name ?? "null"} to {potentialSnapTargetStud?.name ?? "null"}", true);
    }

    // Public method to manually strengthen all connections in a structure
    [ContextMenu("Strengthen Structure")]
    public void StrengthenStructure()
    {
        LogDebug("StrengthenStructure() - Manually strengthening structure");
        connectionManager?.StrengthenGroupConnections();
        physicsManager?.StabilizeGroup();
    }

    public void Freeze(bool shouldFreeze)
    {
        if (grabInteractable == null || !IsGrabbed) return;

        if (shouldFreeze)
        {
            LogDebug($"Freeze() - Freezing {name}");
            // Store original tracking settings
            originalTrackPosition = grabInteractable.trackPosition;
            originalTrackRotation = grabInteractable.trackRotation;

            // Disable tracking
            grabInteractable.trackPosition = false;
            grabInteractable.trackRotation = false;
        }
        else
        {
            LogDebug($"Freeze() - Un-freezing {name}");
            // Restore original tracking settings
            grabInteractable.trackPosition = originalTrackPosition;
            grabInteractable.trackRotation = originalTrackRotation;
        }
    }

    // ========================================
    // UNITY LIFECYCLE
    // ========================================

    void Awake()
    {
        // Automatically detect if this is a board based on GameObject tag
        isBoard = gameObject.CompareTag("Board");
        LogDebug($"Awake() - Initializing BrickBehavior (isBoard: {isBoard}, tag: {gameObject.tag})");

        // Get required components
        rb = GetComponent<Rigidbody>();
        originalParent = transform.parent;

        // For boards, we don't need XRGrabInteractable
        if (!isBoard)
        {
            grabInteractable = GetComponent<XRGrabInteractable>();
            if (grabInteractable == null)
            {
                LogWarning("Awake() - WARNING: Non-board object missing XRGrabInteractable component!");
            }
        }
        else
        {
            // Boards start with normal physics to fall onto table, then become kinematic after 2 seconds
            if (rb != null)
            {
                rb.isKinematic = false;
                rb.useGravity = true;
                LogDebug("Awake() - Set board physics: isKinematic=false, useGravity=true (will fall, then become immovable)", true);
                
                // Start coroutine to make board kinematic after 2 seconds
                StartCoroutine(MakeBoardKinematicAfterDelay());
            }
        }

        LogDebug($"Awake() - Components acquired: XRGrabInteractable={grabInteractable != null}, Rigidbody={rb != null}, OriginalParent={originalParent?.name ?? "null"}", true);

        // Apply physics material for better friction if assigned (only for bricks)
        if (!isBoard && brickPhysicsMaterial != null)
        {
            var collider = GetComponent<Collider>();
            if (collider != null)
            {
                collider.material = brickPhysicsMaterial;
                LogDebug($"Awake() - Applied physics material: {brickPhysicsMaterial.name}", true);
            }
        }

        // Initialize managers
        InitializeManagers();

        // Set up event listeners (only for bricks)
        if (!isBoard && grabInteractable != null)
        {
            grabInteractable.selectEntered.AddListener(OnGrabStarted);
            grabInteractable.selectExited.AddListener(OnGrabReleased);
            LogDebug("Awake() - Event listeners attached", true);
        }
        else if (isBoard)
        {
            LogDebug("Awake() - Board detected, skipping event listeners", true);
        }

        // Validate initial physics state (only for bricks)
        if (!isBoard)
        {
            physicsManager?.ValidatePhysicsState();
        }
        
        LogDebug("Awake() - Initialization complete");
    }

    void Update()
    {
        // Periodically check for group joining opportunities when grabbed
        if (grabInteractable != null && grabInteractable.isSelected && currentState == BrickState.Grabbing)
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
            // ========================================
            // PHYSICS SAFEGUARD DURING SNAP ANIMATION
            // ========================================
            // Ensure physics remains disabled during the entire snap animation
            // This prevents any physics forces from interfering with the lerp movement
            if (rb != null && (rb.isKinematic == false || rb.useGravity == true))
            {
                LogWarning($"FixedUpdate() - WARNING: Physics was re-enabled during snap animation! Force disabling again.");
                rb.isKinematic = true;
                rb.useGravity = false;
            }
            
            // ========================================
            // ANIMATION TOLERANCE DEFINITIONS
            // ========================================
            // These tolerances define how much the animation is allowed to diverge from the target
            // before the safeguard stops applying the lerp changes.
            // 
            // RELATIONSHIP: Safeguard tolerances must be SMALLER than completion thresholds
            // This ensures the animation can't get stuck too far from the target to ever complete.
            // - positionTolerance < completionThreshold ✅
            // - rotationTolerance < rotationThreshold ✅
            //
            // Position: Safeguard allows positionTolerance increase, completion requires within completionThreshold
            // Rotation: Safeguard allows rotationTolerance increase, completion requires within rotationThreshold
            
            // Store initial distances for comparison
            float initialPositionDistance = Vector3.Distance(transform.position, targetSnapPosition);
            float initialRotationDistance = Quaternion.Angle(transform.rotation, targetSnapRotation);
            
            // Smooth snap animation using lerp/slerp with fixed timestep
            // This provides more consistent timing than variable frame rate
            float snapSpeed = this.snapSpeed; // Use the serialized snapSpeed property
            
            // Adjust for fixed timestep - Time.fixedDeltaTime is typically 0.02 (50fps)
            // This provides more controlled animation speed
            float lerpFactor = Mathf.Clamp01(snapSpeed * Time.fixedDeltaTime);
            
            // ADAPTIVE LERP: Reduce lerp factor when close to target to prevent overshooting
            float distanceToTarget = Vector3.Distance(transform.position, targetSnapPosition);
            if (distanceToTarget < 0.01f) // Within 1cm of target
            {
                // Use much smaller lerp factor when very close to prevent overshooting
                lerpFactor *= 0.3f; // Reduce to 30% of normal speed
            }
            else if (distanceToTarget < 0.05f) // Within 5cm of target
            {
                // Use reduced lerp factor when moderately close
                lerpFactor *= 0.6f; // Reduce to 60% of normal speed
            }
            
            // Calculate new position and rotation
            Vector3 newPosition = Vector3.Lerp(transform.position, targetSnapPosition, lerpFactor);
            Quaternion newRotation = Quaternion.Slerp(transform.rotation, targetSnapRotation, lerpFactor);
            
            // SAFEGUARD: Only apply changes if they don't significantly move us away from the target
            float newPositionDistance = Vector3.Distance(newPosition, targetSnapPosition);
            float newRotationDistance = Quaternion.Angle(newRotation, targetSnapRotation);
            
            if (newPositionDistance <= initialPositionDistance + positionTolerance)
            {
                transform.position = newPosition;
            }
            else
            {
                LogWarning($"FixedUpdate() - WARNING: Position animation diverging significantly! Old distance: {initialPositionDistance:F6}, New distance: {newPositionDistance:F6}");
            }
            
            if (newRotationDistance <= initialRotationDistance + rotationTolerance)
            {
                transform.rotation = newRotation;
            }
            else
            {
                LogWarning($"FixedUpdate() - WARNING: Rotation animation diverging significantly! Old distance: {initialRotationDistance:F2}, New distance: {newRotationDistance:F2}");
            }
            
            // Check if we're close enough to consider the snap complete
            float positionDistance = Vector3.Distance(transform.position, targetSnapPosition);
            float rotationDistance = Quaternion.Angle(transform.rotation, targetSnapRotation);
            
            // Add debug logging every 10 frames to track progress
            if (Time.frameCount % 10 == 0)
            {
                LogDebug($"FixedUpdate() - Snap progress - Position distance: {positionDistance:F6}, Rotation distance: {rotationDistance:F2}, Thresholds: {completionThreshold:F6}, {rotationThreshold:F2}");
                LogDebug($"FixedUpdate() - DEBUG: Adaptive lerp - Distance to target: {distanceToTarget:F6}, Lerp factor: {lerpFactor:F3}", false);
            }
            
            if (positionDistance < completionThreshold && rotationDistance < rotationThreshold) // Within 2mm and 0.1 degrees
            {
                LogDebug($"FixedUpdate() - Snap animation complete - position: {transform.position}, rotation: {transform.rotation.eulerAngles}");
                LogDebug($"FixedUpdate() - Final distance to target: {positionDistance:F6}, threshold: {completionThreshold}");
                LogDebug($"FixedUpdate() - Final rotation difference: {rotationDistance:F2}°, threshold: {rotationThreshold:F2}°");
                
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
        if (grabInteractable != null && currentState == BrickState.Grabbing && grabInteractable.isSelected)
        {
            lastGrabPosition = transform.position;
            lastGrabRotation = transform.rotation;
        }
    }

    void OnDestroy()
    {
        LogDebug("OnDestroy() - Cleaning up BrickBehavior");
        
        // Remove event listeners (only for bricks)
        if (!isBoard && grabInteractable != null)
        {
            grabInteractable.selectEntered.RemoveListener(OnGrabStarted);
            grabInteractable.selectExited.RemoveListener(OnGrabReleased);
            LogDebug("OnDestroy() - Event listeners removed", true);
        }
        
        // Clean up managers
        studManager?.Cleanup();
        snappingSystem?.Cleanup();
        connectionManager?.Cleanup();
        physicsManager?.Cleanup();
        groupOperations?.Cleanup();
        
        LogDebug("OnDestroy() - Cleanup complete");
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
        
        LogDebug("InitializeManagers() - All managers initialized", true);
    }

    // ========================================
    // XR INTERACTION EVENT HANDLERS
    // ========================================

    private void OnGrabStarted(SelectEnterEventArgs args)
    {
        // Boards cannot be grabbed
        if (isBoard)
        {
            LogWarning("OnGrabStarted() - WARNING: Attempted to grab a board, which is not allowed!");
            return;
        }

        LogDebug($"OnGrabStarted() - Brick grabbed, previous state: {currentState}");
        
        // Get the interactor that grabbed this brick
        UnityEngine.XR.Interaction.Toolkit.Interactors.IXRSelectInteractor interactor = args.interactorObject;
        if (interactor == null)
        {
            LogWarning("OnGrabStarted() - WARNING: Interactor is null");
            return;
        }
        
        LogDebug($"OnGrabStarted() - DEBUG: Grabbed by interactor: {interactor.transform.name}", false);

        // Check if this brick is already being grabbed by a different interactor
        if (grabInteractable != null && grabInteractable.isSelected && grabInteractable.firstInteractorSelecting != interactor)
        {
            LogWarning($"OnGrabStarted() - WARNING: Brick already grabbed by different interactor: {grabInteractable.firstInteractorSelecting?.transform.name}");
            return;
        }

        // IMPORTANT: Check if this brick is part of a connected group
        if (ConnectedNeighbors.Count > 0)
        {
            LogDebug($"OnGrabStarted() - Brick is part of connected group with {ConnectedNeighbors.Count} neighbors");
            
            // --- NEW: DETACH FROM BOARD ON GRAB ---
            // Find all bricks in this group first.
            List<BrickBehavior> groupBricks = new List<BrickBehavior>();
            BrickGroupUtils.FindAllConnectedInGroup(this, groupBricks, name);
            LogDebug($"OnGrabStarted() - Grabbed group has {groupBricks.Count} bricks (including boards).");

            // Because joints are components of GameObjects, we need a direct way to destroy them.
            // We'll define a helper using Object.Destroy for clean removal.
            void Destroy(Object obj) => Object.Destroy(obj);

            // Detach the entire group from any boards it's connected to.
            // We iterate through all bricks in the identified group.
            foreach (var brickInGroup in groupBricks)
            {
                // A brick can have multiple joints if it connects to multiple others.
                var joints = brickInGroup.GetComponents<ConfigurableJoint>();
                foreach (var joint in joints)
                {
                    if (joint.connectedBody != null)
                    {
                        var connectedBehavior = joint.connectedBody.GetComponent<BrickBehavior>();
                        if (connectedBehavior != null && connectedBehavior.IsBoard)
                        {
                            LogDebug($"OnGrabStarted() - Detaching {brickInGroup.name} from board {connectedBehavior.name}. Destroying joint.");
                            
                            // Remove logical connection from both sides
                            brickInGroup.ConnectedNeighbors.Remove(connectedBehavior);
                            connectedBehavior.ConnectedNeighbors.Remove(brickInGroup);
                            
                            // Destroy the physical joint component from the brick's GameObject.
                            Destroy(joint);
                        }
                    }
                }
            }
            // --- END DETACH FROM BOARD LOGIC ---
            
            // Find all bricks in the now potentially smaller group
            List<BrickBehavior> allGroupBricks = new List<BrickBehavior>();
            BrickGroupUtils.FindAllConnectedInGroup(this, allGroupBricks, name);
            // Find any other grabbed bricks in the group
            BrickBehavior otherGrabbedBrick = allGroupBricks.Find(b => b.IsGrabbed && b != this);
            if (otherGrabbedBrick != null)
            {
                LogDebug($"OnGrabStarted() - Multi-controller scenario detected - splitting group immediately");
                List<BrickBehavior> grabbedBricks = new List<BrickBehavior> { this, otherGrabbedBrick };
                
                // CRITICAL FIX: Ensure the original grabbed brick is processed first.
                // The 'otherGrabbedBrick' was the original master, so it should be first in the list.
                grabbedBricks.Reverse();
                
                groupOperations.SplitConnectedGroup(grabbedBricks);
                // After splitting, the current brick's group has changed. We need to re-evaluate the grab.
                // The rest of the grab logic will be handled by the normal flow after this.
            }
            else
            {
                // NEW: Check for grabbing a separate group
                groupOperations.MoveGrabbedGroupsApart();
            }
        }
        else
        {
            LogDebug("OnGrabStarted() - DEBUG: Brick is standalone - allowing grab", false);
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
        LogDebug($"OnGrabStarted() - DEBUG: Stored initial grab position: {lastGrabPosition}, rotation: {lastGrabRotation.eulerAngles}", false);
        
        LogDebug($"OnGrabStarted() - State updated to: {currentState}");

        // === DEBUG: Print connection graph and controller splits ===
        // Find all bricks in the current group
        List<BrickBehavior> debugAllGroupBricks = new List<BrickBehavior>();
        BrickGroupUtils.FindAllConnectedInGroup(this, debugAllGroupBricks, name);
        LogDebug($"OnGrabStarted() - Group connection graph after grab:");
        foreach (var brick in debugAllGroupBricks)
        {
            string neighbors = string.Join(", ", brick.ConnectedNeighbors.ConvertAll(b => b.name));
            LogDebug($"OnGrabStarted() -   - {brick.name} (Master: {(brick.MasterBrick == brick ? "YES" : "NO")}) | Neighbors: [{neighbors}]");
        }
        LogDebug($"OnGrabStarted() - Master brick for this group: {MasterBrick.name}");
        // Check for multi-controller scenario
        var debugGrabbedInteractors = new Dictionary<UnityEngine.XR.Interaction.Toolkit.Interactors.IXRSelectInteractor, List<BrickBehavior>>();
        var debugAllGrabbedBricks = new List<BrickBehavior>();
        // Find all grabbed bricks in the current group and their interactors
        foreach (var b in debugAllGroupBricks)
        {
            var interactable = b.GetComponent<UnityEngine.XR.Interaction.Toolkit.Interactables.XRGrabInteractable>();
            if (interactable != null && interactable.isSelected)
            {
                debugAllGrabbedBricks.Add(b);
                var subInteractor = interactable.firstInteractorSelecting;
                if (!debugGrabbedInteractors.ContainsKey(subInteractor))
                {
                    debugGrabbedInteractors[subInteractor] = new List<BrickBehavior>();
                }
            }
        }
    }

    private void OnGrabReleased(SelectExitEventArgs args)
    {
        if (isBoard)
        {
            LogWarning("OnGrabReleased() - WARNING: Attempted to release a board, which is not allowed!");
            return;
        }

        LogDebug($"OnGrabReleased() - Brick released, current state: {currentState}");

        if (currentState == BrickState.Grabbing)
        {
            justReleased = true;
            LogDebug("OnGrabReleased() - Set justReleased flag to true");
            
            Invoke(nameof(ResetReleaseFlag), releaseFlagDelay);
            LogDebug($"OnGrabReleased() - Scheduled ResetReleaseFlag in {releaseFlagDelay} seconds");

            Vector3 releasePosition = lastGrabPosition;
            Quaternion releaseRotation = lastGrabRotation;
            LogDebug($"OnGrabReleased() - Using last grab position: {releasePosition}, rotation: {releaseRotation.eulerAngles}");

            // --- MULTI-SNAP LOGIC ---
            List<BrickBehavior> groupBricks = new List<BrickBehavior>();
            BrickGroupUtils.FindAllConnectedInGroup(this, groupBricks, name);
            var snapPairs = new List<(Stud, Stud, BrickBehavior, BrickBehavior)>();
            var seenPairs = new HashSet<(Stud, Stud)>();

            foreach (var brickInGroup in groupBricks)
            {
                foreach (var stud in brickInGroup.studManager.AllStuds)
                {
                    var target = stud.PotentialSnapTarget;
                    if (target != null && stud.ParentBrick != null && target.ParentBrick != null)
                    {
                        // Only consider snaps between different bricks and different groups
                        if (stud.ParentBrick != target.ParentBrick && !BrickBehavior.AreBricksInSameGroup(stud.ParentBrick, target.ParentBrick))
                        {
                            // Avoid duplicate pairs (A,B) and (B,A)
                            var pair = (stud, target);
                            var reversePair = (target, stud);
                            if (!seenPairs.Contains(pair) && !seenPairs.Contains(reversePair))
                            {
                                snapPairs.Add((stud, target, stud.ParentBrick, target.ParentBrick));
                                seenPairs.Add(pair);
                            }
                        }
                    }
                }
            }

            if (snapPairs.Count == 0)
            {
                LogDebug($"OnGrabReleased() - No potential snap found in group.");
                connectionManager?.OnGrabReleased();
                StartCoroutine(DelayedPhysicsManagerCall());
                return;
            }

            // Sort so the first snap is the one involving this brick if possible
            int firstIdx = snapPairs.FindIndex(p => p.Item3 == this || p.Item4 == this);
            if (firstIdx > 0)
            {
                var first = snapPairs[firstIdx];
                snapPairs.RemoveAt(firstIdx);
                snapPairs.Insert(0, first);
            }

            // Prepare the queue for sequential execution
            pendingMultiSnaps.Clear();
            foreach (var (stud, target, _, _) in snapPairs)
            {
                pendingMultiSnaps.Enqueue((stud, target, releasePosition, releaseRotation));
            }
            isMultiSnapInProgress = true;
            LogDebug($"OnGrabReleased() - Multi-snap: {pendingMultiSnaps.Count} snap(s) queued");
            ExecuteNextMultiSnap();
        }
        else
        {
            LogDebug($"OnGrabReleased() - Ignored release (not in Grabbing state)");
        }
    }

    private void ExecuteNextMultiSnap()
    {
        // Dynamically rebuild the snap queue after each snap
        // 1. Find all bricks in the current group
        List<BrickBehavior> currentGroup = new List<BrickBehavior>();
        BrickGroupUtils.FindAllConnectedInGroup(this, currentGroup, name);
        var newSnapPairs = new List<(Stud, Stud, BrickBehavior, BrickBehavior)>();
        var seenPairs = new HashSet<(Stud, Stud)>();

        foreach (var brickInGroup in currentGroup)
        {
            // Don't check studs on boards, they are passive targets.
            if (brickInGroup.IsBoard) continue;
            
            foreach (var stud in brickInGroup.studManager.AllStuds)
            {
                var target = stud.PotentialSnapTarget;
                if (target != null && stud.ParentBrick != null && target.ParentBrick != null)
                {
                    // Only consider snaps between different bricks that are not already in the same group.
                    // This is the crucial check to ensure we only process external connections.
                    if (!BrickGroupUtils.AreBricksInSameGroup(stud.ParentBrick, target.ParentBrick))
                    {
                        var pair = (stud, target);
                        var reversePair = (target, stud);
                        if (!seenPairs.Contains(pair) && !seenPairs.Contains(reversePair))
                        {
                            newSnapPairs.Add((stud, target, stud.ParentBrick, target.ParentBrick));
                            seenPairs.Add(pair);
                        }
                    }
                }
            }
        }

        if (newSnapPairs.Count == 0)
        {
            isMultiSnapInProgress = false;
            LogDebug($"ExecuteNextMultiSnap() - All multi-snaps complete");
            StartCoroutine(DelayedPhysicsManagerCall());
            return;
        }

        // Pick the first available snap
        (Stud snapStud, Stud snapTarget, BrickBehavior fromBrick, BrickBehavior toBrick) = newSnapPairs[0];
        LogDebug($"ExecuteNextMultiSnap() - Executing snap: {snapStud.name} <-> {snapTarget.name} (from {fromBrick.name} to {toBrick.name})");
        // Clear snap targets to avoid duplicate snaps
        snapStud.PotentialSnapTarget = null;
        snapTarget.PotentialSnapTarget = null;
        
        // --- BUG FIX ---
        // The snap request MUST be initiated by the brick that owns the stud, not necessarily the brick that was grabbed.
        // The 'fromBrick' is the owner of snapStud. Its snapping system will perform the calculation.
        // We pass 'this' as the initiator so the callback returns to this brick, which manages the queue.
        fromBrick.snappingSystem.RequestSnap(snapStud, snapTarget, lastGrabPosition, lastGrabRotation, this);
    }

    // In FinalizeSnap, after each snap, continue the multi-snap sequence if needed
    public void OnSnapFinalized_MultiSnap()
    {
        if (isMultiSnapInProgress)
        {
            // With the improved AreBricksInSameGroup logic, we no longer need to manually
            // halt the process. ExecuteNextMultiSnap will now correctly filter out
            // internal connections on its own.
            
            // --- FIX: Force group/neighbor update before next multi-snap ---
            ForceGroupAndNeighborUpdate();

            LogDebug($"OnSnapFinalized_MultiSnap() - Continuing multi-snap sequence");
            ExecuteNextMultiSnap();
        }
    }

    // --- FIX: Utility to force group/neighbor update after each snap ---
    private void ForceGroupAndNeighborUpdate()
    {
        // Find all bricks in the current group
        List<BrickBehavior> groupBricks = new List<BrickBehavior>();
        BrickGroupUtils.FindAllConnectedInGroup(this, groupBricks, name);
        
        // The master brick for the entire group should be the master of the brick that initiated the snap sequence.
        // This ensures a consistent master after a merge.
        BrickBehavior initiatorMaster = this.MasterBrick;

        foreach (var brick in groupBricks)
        {
            // Force update master for all bricks in the newly formed group.
            brick.UpdateMaster(initiatorMaster);
        }
    }

    // Resets the flag so that snapping can only be initiated immediately after release.
    private void ResetReleaseFlag()
    {
        LogDebug("ResetReleaseFlag() - Resetting justReleased flag");
        justReleased = false;
    
        // If a snap hasn't started by now, we go back to being idle
        if (currentState != BrickState.Snapping && currentState != BrickState.Snapped)
        {
            currentState = BrickState.Idle;
            LogDebug($"ResetReleaseFlag() - State reset to: {currentState}");
        }
        else
        {
            LogDebug($"ResetReleaseFlag() - DEBUG: State unchanged: {currentState}", false);
        }
    }

    // Method to store a potential snap connection (called during collision detection)
    public void StorePotentialSnap(Stud fromStud, Stud toStud)
    {
        // This method is now obsolete. The potential snap is stored on the studs.
        // snappingSystem.StorePotentialSnap(fromStud, toStud);
    }

    // Method to execute the stored potential snap (called after release)
    private void ExecuteStoredSnap(Vector3 releasePosition, Quaternion releaseRotation)
    {
        if (potentialSnapStud != null && potentialSnapTargetStud != null)
        {
            LogDebug($"ExecuteStoredSnap() - Executing stored snap from {potentialSnapStud.name} to {potentialSnapTargetStud.name}");
            LogDebug($"ExecuteStoredSnap() - DEBUG: Using release position: {releasePosition}, rotation: {releaseRotation.eulerAngles}", false);
            
            // IMPORTANT: Restore the brick to its release position before calculating snap
            transform.position = releasePosition;
            transform.rotation = releaseRotation;
            LogDebug($"ExecuteStoredSnap() - DEBUG: Restored brick to release position: {transform.position}", false);
            
            // Temporarily disable collision detection on all studs to prevent multiple collisions during snap
            studManager?.DisableStudCollisions();
            
            // Execute the actual snap
            snappingSystem?.RequestSnap(potentialSnapStud, potentialSnapTargetStud, releasePosition, releaseRotation, this);
            
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
        
        LogDebug("ClearPotentialSnap() - DEBUG: Cleared potential snap and reset stud states", false);
    }

    // ========================================
    // PUBLIC INTERFACE FOR MANAGERS
    // ========================================
    
    public void SetSnappingState(bool snapping, Vector3 targetPos, Quaternion targetRot, BrickBehavior targetBrick)
    {
        LogDebug($"SetSnappingState() - DEBUG: Setting isSnapping from {isSnapping} to {snapping}", false);
        isSnapping = snapping;
        targetSnapPosition = targetPos;
        targetSnapRotation = targetRot;
        snapTargetBrick = targetBrick;
        currentState = snapping ? BrickState.Snapping : BrickState.Idle;
        LogDebug($"SetSnappingState() - DEBUG: State changed to {currentState}", false);
    }

    public void ActivateSnapImmunity()
    {
        snapImmunityEndTime = Time.time + SNAP_IMMUNITY_DURATION;
        LogDebug($"ActivateSnapImmunity() - DEBUG: Snap immunity activated until {snapImmunityEndTime:F2}", false);
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

    public void SetJoint(ConfigurableJoint joint)
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
        LogDebug("DelayedPhysicsManagerCall() - Starting delayed physics manager call");
        LogDebug($"DelayedPhysicsManagerCall() - DEBUG: Initial isSnapping state: {isSnapping}", false);
        
        // Wait for snap animation to complete with timeout
        int waitCount = 0;
        const int MAX_WAIT_FRAMES = 60; // 1 second at 60fps
        
        while (isSnapping && waitCount < MAX_WAIT_FRAMES)
        {
            waitCount++;
            if (waitCount % 20 == 0) // Log every 20 frames (about 0.3 seconds at 60fps)
            {
                LogDebug($"DelayedPhysicsManagerCall() - DEBUG: Still waiting for snap to complete, frame {waitCount}, isSnapping: {isSnapping}", false);
            }
            yield return null;
        }
        
        if (waitCount >= MAX_WAIT_FRAMES)
        {
            LogWarning($"DelayedPhysicsManagerCall() - WARNING: Timeout reached! Force completing snap after {waitCount} frames");
            // Force complete the snap only if we have a valid snap system
            if (snappingSystem != null)
            {
                isSnapping = false;
                currentState = BrickState.Idle;
                snappingSystem.FinalizeSnap();
            }
            else
            {
                LogWarning("DelayedPhysicsManagerCall() - WARNING: Snap system is null, cannot force finalize");
                isSnapping = false;
                currentState = BrickState.Idle;
            }
        }
        
        LogDebug($"DelayedPhysicsManagerCall() - DEBUG: Waited {waitCount} frames for snap to complete", false);
        
        // Now call physics manager after snap is complete
        LogDebug("DelayedPhysicsManagerCall() - Snap animation complete, calling physics manager");
        
        var rb = GetComponent<Rigidbody>();
        if (rb != null)
        {
            LogDebug($"DelayedPhysicsManagerCall() - DEBUG: Physics before calling manager - isKinematic: {rb.isKinematic}, useGravity: {rb.useGravity}, velocity: {rb.linearVelocity}", false);
        }
        
        physicsManager?.OnGrabReleased();
        
        // IMPORTANT: Force restore physics if the brick is not actually grabbed but still has kinematic physics
        if (rb != null && !(grabInteractable?.isSelected ?? false) && rb.isKinematic)
        {
            LogWarning("DelayedPhysicsManagerCall() - WARNING: Brick appears to be kinematic but not grabbed! Force restoring physics.");
            rb.isKinematic = false;
            rb.useGravity = true;
        }
        
        if (rb != null)
        {
            LogDebug($"DelayedPhysicsManagerCall() - DEBUG: Physics after calling manager - isKinematic: {rb.isKinematic}, useGravity: {rb.useGravity}, velocity: {rb.linearVelocity}", false);
        }
    }

    // Coroutine to make board kinematic after a delay to allow initial falling
    private System.Collections.IEnumerator MakeBoardKinematicAfterDelay()
    {
        LogDebug("MakeBoardKinematicAfterDelay() - Starting 2-second delay before making board kinematic");
        
        // Wait for 2 seconds
        yield return new WaitForSeconds(2.0f);
        
        // Make the board kinematic to prevent further movement
        if (rb != null && isBoard)
        {
            rb.isKinematic = true;
            LogDebug("MakeBoardKinematicAfterDelay() - Made board kinematic (now immovable)");
        }
    }

    // Helper for debug subgraph traversal
    private void FindBricksForDebugSubGroup(BrickBehavior start, List<BrickBehavior> debugGrabbedBricks, List<BrickBehavior> result)
    {
        Queue<BrickBehavior> toVisit = new Queue<BrickBehavior>();
        HashSet<BrickBehavior> visited = new HashSet<BrickBehavior>();
        toVisit.Enqueue(start);
        visited.Add(start);
        result.Add(start);
        while (toVisit.Count > 0)
        {
            var current = toVisit.Dequeue();
            foreach (var neighbor in current.ConnectedNeighbors)
            {
                if (neighbor == null || visited.Contains(neighbor)) continue;
                // Don't cross to other grabbed bricks
                if (debugGrabbedBricks.Contains(neighbor) && neighbor != start) continue;
                visited.Add(neighbor);
                result.Add(neighbor);
                toVisit.Enqueue(neighbor);
            }
        }
    }
} 
