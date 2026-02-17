using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.Rendering.Universal;

public class GreyscalePostProcess : MonoBehaviour
{
    public bool isGrey = false;

    void Update()
    {
        ChangeRender();
    }

    void ChangeRender()
    {
        Camera camera = this.gameObject.GetComponent<Camera>();
        var cameraData = camera.GetUniversalAdditionalCameraData();
        if (isGrey)
        {
            cameraData.SetRenderer(1);
        }
        else
        {
            cameraData.SetRenderer(0);
        }
    }
}
