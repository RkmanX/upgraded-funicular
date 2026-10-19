using UnityEngine;

// Keeps a sprite facing the camera (turning around the Y axis only, so it stays upright)
public class FaceCamera : MonoBehaviour
{
    Transform cam;

    void Start()
    {
        cam = Camera.main.transform;
    }

    void LateUpdate()
    {
        if (cam != null)
            transform.rotation = Quaternion.Euler(0f, cam.eulerAngles.y, 0f);
    }
}
