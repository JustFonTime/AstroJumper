using UnityEngine;
using Unity.Cinemachine;

public class SpaceCameraZoom : MonoBehaviour
{
    [Header("Zoom Limits")] [SerializeField]
    private float minZoom = 10f;

    [SerializeField] private float maxZoom = 300f;

    [Header("Zoom Feel")] [SerializeField] private float zoomSpeed = 5f;

    [Header("Refs")] [SerializeField] private CinemachineCamera virtualCamera;

    private void LateUpdate()
    {
        if (virtualCamera == null) return;

        float scrollDelta = Input.GetAxis("Mouse ScrollWheel");
        if (Mathf.Abs(scrollDelta) <= 0.0001f) return;

        // Get current zoom level (orthographic size)
        float current = virtualCamera.Lens.OrthographicSize;
        float target = current - scrollDelta * zoomSpeed;

        target = Mathf.Clamp(target, minZoom, maxZoom);

        virtualCamera.Lens.OrthographicSize = target;

    }
}