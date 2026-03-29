using UnityEngine;

public class CameraScaler : MonoBehaviour
{
    public float baseAspect = 720f / 1080f;

    private Camera cam;
    private float baseSize;

    void Awake()
    {
        cam = GetComponent<Camera>();
        baseSize = cam.orthographicSize;

        UpdateCamera();
    }

    void UpdateCamera()
    {
        float screenAspect = (float)Screen.width / Screen.height;

        float ratio = baseAspect / screenAspect;

        cam.orthographicSize = baseSize * ratio;
    }
}