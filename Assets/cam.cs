using UnityEngine;

// SimpleOrbitCamera.cs
// SUPER SIMPLE orbit camera: always orbits with mouse move, no RMB, no gamepad, no limits
// Attach to your Camera, assign your spaceship as Target.

public class SimpleOrbitCamera : MonoBehaviour
{
    [Header("Target")]
    public Transform Target;
    public Vector3 TargetOffset = Vector3.zero;

    [Header("Orbit & Zoom")]
    public float Distance = 10f;
    public float MinDistance = 2f;
    public float MaxDistance = 60f;
    public float LookSensitivity = 120f; // deg/sec per mouse unit
    public float ZoomSpeed = 5f;         // scroll sensitivity

    private float _yaw;   // world up
    private float _pitch; // local right

    void Start()
    {
        if (Target == null)
            Debug.LogWarning("SimpleOrbitCamera: Assign a Target (your spaceship).");

        var e = transform.rotation.eulerAngles;
        _yaw = e.y;
        _pitch = e.x;
    }

    void LateUpdate()
    {
        if (Target == null) return;

        // Always read mouse deltas (no button gating)
        float lookX = Input.GetAxisRaw("Mouse X") * LookSensitivity * Time.deltaTime;
        float lookY = -Input.GetAxisRaw("Mouse Y") * LookSensitivity * Time.deltaTime; // invert so up moves camera up

        _yaw = WrapAngle(_yaw + lookX);
        _pitch = WrapAngle(_pitch + lookY);

        // Build rotation (unbounded)
        Quaternion rot = Quaternion.Euler(_pitch, _yaw, 0f);

        // Zoom with wheel
        float scroll = Input.GetAxisRaw("Mouse ScrollWheel");
        if (Mathf.Abs(scroll) > Mathf.Epsilon)
        {
            float factor = 1f - scroll * ZoomSpeed * 0.1f; // gentle exponential feel
            Distance = Mathf.Clamp(Distance * factor, MinDistance, MaxDistance);
        }

        // Position camera and look at target center
        Vector3 focus = Target.position + TargetOffset;
        Vector3 camPos = focus - rot * Vector3.forward * Distance;
        transform.SetPositionAndRotation(camPos, rot);
        transform.LookAt(focus, Vector3.up);
    }

    static float WrapAngle(float a)
    {
        if (a > 180f) a -= 360f;
        if (a < -180f) a += 360f;
        return a;
    }
}
