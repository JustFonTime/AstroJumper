using UnityEngine;

[RequireComponent(typeof(Renderer))]
public class InfiniteTiledBackground2D : MonoBehaviour
{
    [Header("Targets")]
    [SerializeField] private Camera cam;     
    [SerializeField] private Transform scrollTarget; 

    [Header("Parallax")]
    [SerializeField] private float parallax = 0.05f;

    [Header("Depth / Safety")]
    [SerializeField] private float zOffset = 10f;     
    [SerializeField] private float extraScale = 1.2f; 

    private Renderer rend;
    private Material mat;

    private void Awake()
    {
        rend = GetComponent<Renderer>();
        mat = rend.material;

        if (!cam) cam = Camera.main;
    }

    private void LateUpdate()
    {
        if (!cam) return;

        Vector3 camPos = cam.transform.position;
        transform.position = new Vector3(camPos.x, camPos.y, camPos.z + zOffset);

        if (cam.orthographic)
        {
            float height = cam.orthographicSize * 2f;
            float width = height * cam.aspect;

            transform.localScale = new Vector3(width * extraScale, height * extraScale, 1f);
        }
        else
        {
            float distance = Mathf.Abs(zOffset);
            float height = 2f * distance * Mathf.Tan(cam.fieldOfView * 0.5f * Mathf.Deg2Rad);
            float width = height * cam.aspect;

            transform.localScale = new Vector3(width * extraScale, height * extraScale, 1f);
        }

        if (scrollTarget)
        {
            Vector2 offset = new Vector2(scrollTarget.position.x, scrollTarget.position.y) * parallax;
            mat.mainTextureOffset = offset;
        }
    }
}
