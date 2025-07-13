// Stud.cs
using UnityEngine;
using System.Collections.Generic;
using System.Linq;

[RequireComponent(typeof(Collider))]
public class Stud : MonoBehaviour
{
   public enum StudType { Top, Bottom }

   public enum StudState
   {
       Idle,
       InSnapRange,
       Snapping,
       Snapped
   }

   [Tooltip("Is this a stud on the top surface or a socket on the bottom?")]
   public StudType Type = StudType.Top;

   [Tooltip("The size/type of this stud (e.g., 1x1, 2x2, etc.)")]
   public string StudSize = "1x1";

   [Tooltip("Whether this stud is currently available for snapping")]
   public bool IsAvailable = true;

   // A reference to the main brick script. This will be set automatically.
   public BrickBehavior ParentBrick { get; set; }

   private float lastCollisionTime = 0f;
   private const float COLLISION_COOLDOWN = 0.1f;

   [Header("Visual Feedback")]
   [Tooltip("Material to show when stud is in snap range")]
   public Material snapRangeMaterial;
   [Tooltip("Material to show when stud is actively snapping")]
   public Material snapActiveMaterial;

   private Material originalMaterial;
   private Renderer studRenderer;

   private StudState currentState = StudState.Idle;

   [System.Serializable]
   public class StudConnection
   {
       public Stud connectedStud;
       public float connectionStrength;
       public float connectionTime;
   }

   private List<StudConnection> activeConnections = new List<StudConnection>();

   public StudState CurrentState
   {
       get => currentState;
       set
       {
           if (currentState != value)
           {
               ParentBrick.LogDebug($"Stud state changed from {currentState} to {value}");
               currentState = value;
           }
       }
   }

   [Header("Performance")]
   [Tooltip("Minimum distance to start collision detection (set by BrickBehavior)")]
   public float minCollisionDistance => ParentBrick != null ? ParentBrick.minCollisionDistance : 0.1f;
   [Tooltip("Maximum distance for valid snap detection (set by BrickBehavior)")]
   public float maxSnapDistance => ParentBrick != null ? ParentBrick.maxSnapDistance : 0.05f;

   [Header("Debug")]
   [Tooltip("Show debug lines for stud connections")]
   public bool showDebugConnections = false;

   private void ValidateStudSetup()
   {
       Collider col = GetComponent<Collider>();
       if (col == null)
       {
           Debug.LogWarning($"Stud '{name}' missing required Collider component!");
       }
       else if (!col.isTrigger)
       {
           Debug.LogWarning($"Stud '{name}' collider should be set to 'Is Trigger'");
       }
   }

   void Awake()
   {
       // Basic setup that doesn't require ParentBrick
       ValidateStudSetup();
       
       // Ensure the collider is set to be a trigger. This is essential for the collision logic.
       // BUT board studs should not have triggers enabled
       Collider col = GetComponent<Collider>();
       if (!col.isTrigger)
       {
           col.isTrigger = true;
       }

       studRenderer = GetComponent<Renderer>();
       if (studRenderer != null)
       {
           originalMaterial = studRenderer.material;
       }
   }

   void Start()
   {
       // Deferred initialization that requires ParentBrick to be assigned
       // This runs after BrickBehavior.Awake() which calls InitializeManagers()
       if (ParentBrick != null)
       {
           // Board studs should have triggers enabled for top studs only
           if (ParentBrick.IsBoard)
           {
               Collider col = GetComponent<Collider>();
               if (col != null)
               {
                   if (Type == StudType.Top)
                   {
                       col.isTrigger = true;
                       col.enabled = true;
                       ParentBrick.LogDebug($"Start() - Set board top stud to trigger: {name}");
                   }
                   else
                   {
                       col.enabled = false;
                       ParentBrick.LogDebug($"Start() - Disabled board bottom stud: {name}");
                   }
               }
           }
           else
           {
               ParentBrick.LogDebug($"Start() - Stud initialized: Type={Type}, StudSize={StudSize}, IsAvailable={IsAvailable}");
               ParentBrick.LogDebug($"Start() - Collider: {GetComponent<Collider>().name}, isTrigger={GetComponent<Collider>().isTrigger}, enabled={GetComponent<Collider>().enabled}");
               ParentBrick.LogDebug($"Start() - Position: {transform.position}");
           }
       }
       else
       {
           Debug.LogWarning($"Stud '{name}' has no ParentBrick assigned in Start() - this should not happen!");
       }
   }


   // This function is called when another trigger collider enters this one.
   void OnTriggerEnter(Collider other)
   {
       // Board bottom studs should never trigger collisions
       if (ParentBrick != null && ParentBrick.IsBoard && Type == StudType.Bottom)
       {
           return;
       }

       // Check if the other object is a stud first
       Stud otherStud = other.GetComponent<Stud>();
       if (otherStud == null)
       {
           // Not a stud, ignore silently
           return;
       }
       
       // Now we know it's a stud, so we can show debug messages
       ParentBrick.LogDebug($"OnTriggerEnter() - Collision detected with stud {other.name}");
       
       // IMPORTANT: Check if these studs belong to the same brick or connected group
       if (ParentBrick == otherStud.ParentBrick)
       {
           ParentBrick.LogDebug($"OnTriggerEnter() - Same brick collision, ignoring");
           return;
       }
       
       // Check if the studs belong to connected groups
       if (ParentBrick != null && otherStud.ParentBrick != null)
       {
           if (AreStudsInSameGroup(ParentBrick, otherStud.ParentBrick))
           {
               ParentBrick.LogDebug($"OnTriggerEnter() - Same group collision, ignoring");
               return;
           }
       }
       
       // Rate limiting
       if (Time.time - lastCollisionTime < (ParentBrick != null ? ParentBrick.collisionCooldown : 0.1f))
       {
           ParentBrick.LogDebug($"OnTriggerEnter() - Rate limited, ignoring collision");
           return;
       }
       lastCollisionTime = Time.time;
       
       // Ignore if our parent brick is not in a state to snap (e.g., not being held and just released).
       if (ParentBrick == null)
       {
           ParentBrick.LogWarning($"OnTriggerEnter() - WARNING: ParentBrick is null!");
           return;
       }
       
       if (!ParentBrick.IsReadyForSnap())
       {
           ParentBrick.LogDebug($"OnTriggerEnter() - Parent brick not ready for snap, ignoring collision");
           return;
       }

       // Crucial Check: Ensure the stud types are compatible (Top can only snap to Bottom).
       if (this.Type == otherStud.Type)
       {
           ParentBrick.LogDebug($"[Stud Collision] IGNORED: Stud '{this.name}' ({this.Type}) on brick '{ParentBrick.name}' detected stud '{otherStud.name}' ({otherStud.Type}) on brick '{otherStud.ParentBrick.name}', but types are the same.");
           return; // Both are Top, or both are Bottom. Invalid connection.
       }
      
       // Check distance for snap tolerance
       float distance = Vector3.Distance(transform.position, other.transform.position);
       float maxSnapDistance = ParentBrick != null ? ParentBrick.maxSnapDistance : 0.05f;
       ParentBrick.LogDebug($"OnTriggerEnter() - Distance to {otherStud.name}: {distance:F6}, snapTolerance: {ParentBrick?.snapTolerance ?? 0.01f:F6}, maxSnapDistance: {maxSnapDistance:F6}");
       if (distance > maxSnapDistance)
       {
           ParentBrick.LogDebug($"[Stud Collision] IGNORED: Stud '{this.name}' too far from '{other.name}' (distance: {distance:F6} > {maxSnapDistance:F6})");
           return;
       }
       
       // Check if these bricks were recently split (prevent immediate re-snapping)
       if (Time.time < ParentBrick.snapImmunityEndTime || 
           Time.time < otherStud.ParentBrick.snapImmunityEndTime)
       {
           ParentBrick.LogDebug($"[Stud Collision] IGNORED: One or both bricks in snap immunity period after split");
           return;
       }
       
       ParentBrick.LogDebug($"<color=cyan>[Stud Collision] VALIDATED: Stud '{this.name}' ({this.Type}) on brick '{ParentBrick.name}' has collided with stud '{otherStud.name}' ({otherStud.Type}) on brick '{otherStud.ParentBrick.name}'. Storing potential snap.</color>");
       
       // Set the stud to InSnapRange state and show visual feedback
       CurrentState = StudState.InSnapRange;
       ShowSnapRange();
       
       // Store the potential snap connection but don't execute it yet
       // The actual snap will be triggered after release
       ParentBrick.StorePotentialSnap(this, otherStud);
   }

   // This function is called when another trigger collider exits this one.
   void OnTriggerExit(Collider other)
   {
       // Board bottom studs should never trigger collisions
       if (ParentBrick != null && ParentBrick.IsBoard && Type == StudType.Bottom)
       {
           return;
       }

       // Check if the other object is a stud
       Stud otherStud = other.GetComponent<Stud>();
       if (otherStud == null)
       {
           return;
       }
       
       // If we were in snap range and the collision ended, reset to idle
       if (CurrentState == StudState.InSnapRange)
       {
           ParentBrick.LogDebug($"OnTriggerExit() - Exiting snap range with {otherStud.name}");
           CurrentState = StudState.Idle;
           ResetVisual();
       }
   }

   private bool IsCompatibleWith(Stud otherStud)
   {
       // Check if both studs are available
       if (!IsAvailable || !otherStud.IsAvailable)
       {
           return false;
       }
       
       // Check if stud sizes are compatible
       if (StudSize != otherStud.StudSize)
       {
           return false;
       }
       
       return true;
   }

   public void ShowSnapRange()
   {
       if (studRenderer != null && snapRangeMaterial != null)
       {
           studRenderer.material = snapRangeMaterial;
       }
   }

   public void ShowSnapActive()
   {
       if (studRenderer != null && snapActiveMaterial != null)
       {
           studRenderer.material = snapActiveMaterial;
       }
   }

   public void ResetVisual()
   {
       if (studRenderer != null && originalMaterial != null)
       {
           studRenderer.material = originalMaterial;
       }
   }

   public bool IsInSnapRange()
   {
       return currentState == StudState.InSnapRange;
   }

   public bool IsSnapping()
   {
       return currentState == StudState.Snapping;
   }

   public bool IsSnapped()
   {
       return currentState == StudState.Snapped;
   }

   public void SetSnapping(bool isSnapping)
   {
       CurrentState = isSnapping ? StudState.Snapping : StudState.Idle;
   }

   public void AddConnection(Stud otherStud, float strength = 1.0f)
   {
       var connection = new StudConnection
       {
           connectedStud = otherStud,
           connectionStrength = strength,
           connectionTime = Time.time
       };
       activeConnections.Add(connection);
       ParentBrick.LogDebug($"Added connection to {otherStud.name} with strength {strength}");
   }

   public void RemoveConnection(Stud otherStud)
   {
       activeConnections.RemoveAll(c => c.connectedStud == otherStud);
       ParentBrick.LogDebug($"Removed connection to {otherStud.name}");
   }

   public bool IsConnectedTo(Stud otherStud)
   {
       return activeConnections.Any(c => c.connectedStud == otherStud);
   }

   void OnDrawGizmos()
   {
       if (!showDebugConnections) return;
       
       // Draw connection lines
       foreach (var connection in activeConnections)
       {
           if (connection.connectedStud != null)
           {
               Gizmos.color = Color.green;
               Gizmos.DrawLine(transform.position, connection.connectedStud.transform.position);
           }
       }
       
       // Draw snap range
       Gizmos.color = Color.yellow;
       Gizmos.DrawWireSphere(transform.position, maxSnapDistance);
   }

   // Helper method to check if two bricks are in the same group
   private bool AreStudsInSameGroup(BrickBehavior brick1, BrickBehavior brick2)
   {
       if (brick1 == null || brick2 == null) return false;
       
       // Boards should not participate in group checks
       if (brick1.IsBoard || brick2.IsBoard)
       {
           return false; // Boards are never considered to be in the same group as other objects
       }
       
       // Use the utility class to check if bricks are in the same group
       return BrickGroupUtils.AreBricksInSameGroup(brick1, brick2);
   }
   
   public void ClearSnapRangeState()
   {
       if (CurrentState == StudState.InSnapRange)
       {
           ParentBrick.LogDebug($"ClearSnapRangeState() - Clearing snap range state");
           CurrentState = StudState.Idle;
           ResetVisual();
       }
   }
}
