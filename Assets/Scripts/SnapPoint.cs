using UnityEngine;

public class SnapPoint : MonoBehaviour
{
    public enum Orientation { Stud, Recept }
    public Orientation orientation;

    public Transform snapTransform; // point for aligning
    public GameObject parentBrick;  // auto-assigned in Awake

    void Awake()
    {
        if (parentBrick == null)
            parentBrick = transform.parent.gameObject;
        if (snapTransform == null)
            snapTransform = this.transform; // default
    }

    public float GetDistanceTo(Vector3 position)
    {
        return Vector3.Distance(snapTransform.position, position);
    }
}