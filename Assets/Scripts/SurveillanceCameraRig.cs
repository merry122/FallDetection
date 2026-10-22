using UnityEngine;

[RequireComponent(typeof(Camera))]
public class SurveillanceCameraRig : MonoBehaviour
{
    public Transform lookAtTarget;
    public Vector3 fixedPosition = new Vector3(3.7f, 2.7f, -2.8f);
    public Vector3 fallbackLookAtPoint = new Vector3(0f, 1f, 0f);
    public float fieldOfView = 70f;
    public bool lockCameraEveryFrame = true;

    Camera cameraComponent;

    void Awake()
    {
        cameraComponent = GetComponent<Camera>();
        ApplySetup();
    }

    void LateUpdate()
    {
        if (lockCameraEveryFrame)
        {
            ApplySetup();
        }
    }

    public void ApplySetup()
    {
        if (cameraComponent == null)
        {
            cameraComponent = GetComponent<Camera>();
        }

        transform.position = fixedPosition;

        Vector3 target = lookAtTarget != null ? lookAtTarget.position : fallbackLookAtPoint;
        Vector3 direction = target - transform.position;

        if (direction.sqrMagnitude > 0.001f)
        {
            transform.rotation = Quaternion.LookRotation(direction.normalized, Vector3.up);
        }

        cameraComponent.fieldOfView = fieldOfView;
        cameraComponent.nearClipPlane = 0.1f;
        cameraComponent.farClipPlane = 30f;
    }
}
