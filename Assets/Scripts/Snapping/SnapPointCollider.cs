using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using UnityEngine.XR.Interaction.Toolkit.Interactors;

public class SnapPointCollider : MonoBehaviour
{
    //private XRBaseInteractable grabbedObject;
    private SnapPointJohn _snapPoint;
    private Collider _collider;

    public bool isGrabbed;
    public GameObject brick;
    public GameObject otherBrick;
    public Collider matchingSnapPointCollider;

    void Awake()
    {
        // Tell the collider which is the brick it belongs to
        brick = gameObject.GetComponentInParent<XRBaseInteractable>().gameObject;

        isGrabbed = false;
        Debug.Log(brick.name + $" - Awake: set _isGrabbed to FALSE" );
        
        _snapPoint = GetComponent<SnapPointJohn>();
        Debug.Log(brick.name + $" - Awake: SnapPoint '{_snapPoint.name}' found");

        // Set collider as trigger if it's not
        _collider = GetComponent<Collider>();
        if (_collider != null && !_collider.isTrigger)
        {
            _collider.isTrigger = true;
            Debug.Log(brick.name + $" - Awake: Collider '{_collider.name}' found and ENABLED trigger on it" );
        }
    }

    private void OnTriggerEnter(Collider otherCollider)
    {
        //Debug.Log(brick.name + $" - OnCollisionEnter: {collision.collider}'");
        
        //Collider other = collision.collider;
        // Only process if the collider belongs to a game object tagged "SnapPoint"
        /*if (!other.CompareTag("SnapPoint"))
        {
            Debug.Log($"Ignored {other.gameObject.name} - not tagged 'SnapPoint'");
            return;
        }*/

        if (otherCollider.gameObject.CompareTag("SnapPoint") && isGrabbed)
        {
            GameObject _otherBrick = otherCollider.GetComponent<SnapPointCollider>().brick;
            Debug.Log(brick.name + $" - OnCollisionEnter: Collision start with gameObject '{_otherBrick.name}' via collider '{otherCollider.name}' from collider '{gameObject.name}'");

            var otherSnapPoint = otherCollider.GetComponent<SnapPointJohn>();
            Debug.Log(brick.name + $" - OnCollisionEnter: Found SnapPoint with type '{otherSnapPoint.snapType}'");
            // Check compatibility
            if (IsCompatible(_snapPoint.snapType, otherSnapPoint.snapType))
            {
                Debug.Log(brick.name + $" - OnCollisionEnter: Snap types are compatible: '{_snapPoint.snapType}' -> '{otherSnapPoint.snapType}'");
                // Check if the other object is being held
                // var otherInteractable = other.GetComponentInParent<XRBaseInteractable>();
                // if (otherInteractable != null && otherInteractable.isSelected)
                // {
                //     Debug.Log($"Interaction confirmed: {other.gameObject.name} is grabbed");
                //     grabbedObject = otherInteractable;
                //     Debug.Log($"Calling SnapObjects to align {grabbedObject.gameObject.name} with {other.gameObject.name}");
                //     SnapObjects(other.transform, transform);
                // }
                // else
                // {
                //     SnapObjects(other.transform, transform);
                //     Debug.Log($"Object {other.gameObject.name} not currently grabbed");
                // }
                otherBrick = _otherBrick;
                matchingSnapPointCollider = otherCollider;
                gameObject.GetComponent<Collider>().enabled = false;
                //SnapObjects();
            }
            else
            {
                Debug.Log(brick.name + $" - OnCollisionEnter: Snap types NOT compatible: '{_snapPoint.snapType}' -> '{otherSnapPoint.snapType}'");
            }
        }
    }

    private void OnTriggerExit(Collider other)
    {
        otherBrick = null;
        matchingSnapPointCollider = null;
        gameObject.GetComponent<Collider>().enabled = true;
        //     var otherInteractable = other.GetComponentInParent<XRBaseInteractable>();
        //     if (grabbedObject != null && otherInteractable != null && otherInteractable == grabbedObject)
        //     {
        //         Debug.Log($"Object {other.gameObject.name} exited trigger, clearing grabbedObject");
        //         grabbedObject = null;
        //     }
        //     else
        //     {
        //         Debug.Log($"Object {other.gameObject.name} exited trigger, but no match with grabbedObject");
        //     }
    }

    private bool IsCompatible(SnapPointJohn.SnapType thisBrick, SnapPointJohn.SnapType otherBrick)
    {
        return thisBrick != otherBrick;
    }

    public void SnapObjects()
    {
        Transform myTransform = gameObject.transform;
        Transform otherTransform = matchingSnapPointCollider.transform;
        
        if (otherBrick == null)
        {
            Debug.Log(brick.name + $": No matching brick in reach");
            return;
        }

        Debug.Log(brick.name + $": Snapping '{otherBrick.name}'");

        // Move the grabbed object so the snap points align
        Vector3 offset = myTransform.position - otherTransform.position;
        //Vector3 offset2 = new Vector3(10, 10, 10);
        Debug.Log(otherBrick.name + $": Position before '{otherBrick.transform.position}'");
        otherBrick.transform.position += offset;
        Debug.Log(otherBrick.name + $": Moved by offset '{offset}' to match '{brick.name}'");
        Debug.Log(otherBrick.name + $": Position after '{otherBrick.transform.position}'");

        // Optionally, match rotation if desired
        brick.transform.rotation = otherBrick.transform.rotation;
        Debug.Log(brick.name + $": Rotated to match parent rotation of '{otherBrick.name}'");
        
        // Make rigidbody kinematic during snap
        Rigidbody rb = otherBrick.GetComponent<Rigidbody>();
        if (rb != null)
        {
            rb.isKinematic = true;
            rb.useGravity = false;
            Debug.Log(brick.name + $": Set rigidbody of '{otherBrick.name}' to Kinematic = true");
        }
        
        var interactableObject = otherBrick.GetComponent<IXRSelectInteractable>();
        XRBaseInteractor interactor = FindFirstObjectByType<XRBaseInputInteractor>();
        HashSet<IXRSelectInteractable> grabbedObjects = new HashSet<IXRSelectInteractable>();
        if (interactableObject != null && interactor != null)
        {
            // Check if the object isn't already grabbed
            if (!grabbedObjects.Contains(interactableObject))
            {
                // Force the interactor to select this object as well
                // This depends on your setup; you might need to call:
                ////interactableObject.Select(interactableObject);
                grabbedObjects.Add(interactableObject);
            }
        }

        // Parent the grabbed object to the parent brick to move as one
        //otherTransform.SetParent(myTransform.parent);
        //Debug.Log(brick.name + $": Parented '{otherBrick.name}' to '{myTransform.parent}'");
    }
}