using UnityEngine;

public class SnapPoint : MonoBehaviour
{
    public enum Orientation { Stud, Recept }
    public Orientation orientation;

    public Transform snapTransform; // The point to align when snapping
    public GameObject parentBrick; // The brick object this SnapPoint belongs to

    void Awake()
    {
        if (parentBrick == null)
            parentBrick = transform.parent.gameObject;
        if (snapTransform == null)
            snapTransform = this.transform; // default to the transform itself
    }

    public float GetDistanceTo(Vector3 position)
    {
        return Vector3.Distance(snapTransform.position, position);
    }
}