using System;
using UnityEngine;

public class SpaceCameraFollow : MonoBehaviour
{
    public Transform playerTransform;
    public Vector3 offset;
    public float smoothSpeed = 0.125f; // For smooth following
    private float z;

    private void Start()
    {
        z = transform.position.z;
        if (playerTransform == null)
            playerTransform = GameObject.FindGameObjectWithTag("Player").transform;
    }

    // LateUpdate is called after all Update functions have been called
    void LateUpdate()
    {
        if (playerTransform != null)
        {
            // Calculate the desired position with an offset
            Vector3 desiredPosition = playerTransform.position + offset;
            desiredPosition.z = z;
            // Use Lerp for smooth movement
            Vector3 smoothedPosition = Vector3.Lerp(transform.position, desiredPosition, smoothSpeed);
            transform.position = smoothedPosition;
        }
    }
}