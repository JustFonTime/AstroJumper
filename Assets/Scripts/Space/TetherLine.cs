using UnityEngine;

[RequireComponent(typeof(LineRenderer))]
public class TetherLine : MonoBehaviour
{
    [SerializeField] private Transform a;
    [SerializeField] private Transform b;

    private LineRenderer lr;

    private void Awake()
    {
        lr = GetComponent<LineRenderer>();
        lr.positionCount = 2;

        lr.useWorldSpace = true;
        lr.alignment = LineAlignment.TransformZ;

        lr.material = new Material(Shader.Find("Sprites/Default"));
        lr.startColor = Color.cyan;
        lr.endColor = Color.cyan;
        lr.startWidth = 0.05f;
        lr.endWidth = 0.05f;
        lr.sortingOrder = 50;
    }

    public void SetupLine(Transform pointA, Transform pointB)
    {
        a = pointA;
        b = pointB;
    }

    public Transform[] GetEndpoints()
    {
        return new[] { a, b };
    }

    private void LateUpdate()
    {
        if (!a || !b) return;


        Vector3 p0 = a.position;
        Vector3 p1 = b.position;
        p0.z = 0f;
        p1.z = 0f;

        lr.SetPosition(0, p0);
        lr.SetPosition(1, p1);
    }
}