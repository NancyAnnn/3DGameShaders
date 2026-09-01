using UnityEngine;

public sealed class OrbitCamera : MonoBehaviour
{
    public Transform target;
    public float distance = 8f;
    public float orbitSpeed = 0.3f;
    public float panSpeed = 0.02f;
    public Vector2 minMaxDistance = new Vector2(2f, 50f);

    private float yaw;
    private float pitch = 20f;

    private void OnEnable()
    {
        if (target != null)
        {
            transform.LookAt(target);
        }
        Vector3 euler = transform.eulerAngles;
        yaw = euler.y;
        pitch = euler.x;
    }

    /// <summary>
    /// Pick up the current transform (position/rotation/distance) so LateUpdate
    /// keeps using it instead of the previously stored orbit values.
    /// </summary>
    public void SyncFromTransform()
    {
        Vector3 euler = transform.eulerAngles;
        yaw = euler.y;
        pitch = euler.x;
        if (target != null)
        {
            distance = Vector3.Distance(transform.position, target.position);
        }
    }

    private void LateUpdate()
    {
        if (target == null)
        {
            return;
        }

        if (Input.GetMouseButton(0))
        {
            yaw += Input.GetAxis("Mouse X") * orbitSpeed * 10f;
            pitch -= Input.GetAxis("Mouse Y") * orbitSpeed * 10f;
            pitch = Mathf.Clamp(pitch, -89f, 89f);
        }

        if (Input.GetMouseButton(1))
        {
            Vector3 right = transform.right;
            Vector3 up = transform.up;
            target.position += (-right * Input.GetAxis("Mouse X") + up * Input.GetAxis("Mouse Y")) * panSpeed * distance;
        }

        float scroll = Input.GetAxis("Mouse ScrollWheel");
        if (Mathf.Abs(scroll) > 0.0001f)
        {
            distance = Mathf.Clamp(distance - scroll * distance * 0.1f, minMaxDistance.x, minMaxDistance.y);
        }

        Quaternion rotation = Quaternion.Euler(pitch, yaw, 0f);
        transform.position = target.position - rotation * Vector3.forward * distance;
        transform.rotation = rotation;
    }
}