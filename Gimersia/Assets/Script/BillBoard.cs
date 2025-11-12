using UnityEngine;

public class Billboard : MonoBehaviour
{
    [SerializeField] private bool freezeXZ = false;
    [SerializeField] private bool invert = false;
    [SerializeField] private float smoothSpeed = 10f;

    private Transform cam;

    private void Start()
    {
        if (cam == null && Camera.main != null)
            cam = Camera.main.transform;

        UpdateRotationInstant();
    }

    private void LateUpdate()
    {
        if (cam == null) return;

        Vector3 direction = transform.position - cam.position;

        if (freezeXZ)
            direction.y = 0f;

        if (direction.sqrMagnitude > 0.001f)
        {
            if (invert) direction = -direction;

            Quaternion targetRotation = Quaternion.LookRotation(direction, Vector3.up);
            transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, Time.deltaTime * smoothSpeed);
        }
    }

    private void UpdateRotationInstant()
    {
        if (cam == null) return;

        Vector3 direction = transform.position - cam.position;

        if (freezeXZ)
            direction.y = 0f;

        if (invert) direction = -direction;

        if (direction.sqrMagnitude > 0.001f)
            transform.rotation = Quaternion.LookRotation(direction, Vector3.up);
    }
}