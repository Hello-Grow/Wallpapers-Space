using System;
using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(Rigidbody))]
public class RocketEngine : MonoBehaviour
{
    public enum RefSpace { Ship, Nozzle, World }
    public enum GizmoOrigin { Auto, COM, Nozzle }

    [Serializable]
    public class Engine
    {
        [Header("Placement")]
        public Transform nozzle;

        [Header("Direction & Space")]
        [Tooltip("The ship is pushed TOWARD this vector (not opposite).")]
        public Vector3 direction = Vector3.forward; // push toward +Z by default
        public RefSpace referenceSpace = RefSpace.Ship;

        [Header("Force")]
        [Tooltip("Newtons at power = 8.")]
        public float maxThrust = 5000f;
        [Tooltip("Apply at nozzle position (creates torque) or through center of mass (no torque).")]
        public bool applyAtNozzle = false;

        [Header("Input")]
        [Tooltip("Hold key to fire this engine.")]
        public KeyCode key = KeyCode.None;

        [Header("VFX (optional)")]
        public ParticleSystem particles;
        [Tooltip("Emission rate when power = 8.")]
        public float emissionAt8 = 200f;
        [Tooltip("Start speed when power = 8. 0 = unchanged.")]
        public float startSpeedAt8 = 8f;

        // runtime
        [HideInInspector] public float targetPower;   // 0..8
        [HideInInspector] public float currentPower;  // 0..8
        [HideInInspector] public Vector3 lastWorldDir;
        [HideInInspector] public Vector3 lastWorldPos;
    }

    [Header("Physics")]
    public Rigidbody rb;
    [Tooltip("Optional COM override (local space). Leave (0,0,0) to keep Unity’s default.")]
    public Vector3 centerOfMassLocal = Vector3.zero;

    [Header("Throttle Smoothing")]
    [Tooltip("Levels/sec rising toward target (0..8).")]
    public float rampUp = 12f;
    [Tooltip("Levels/sec falling toward target (0..8).")]
    public float rampDown = 16f;
    [Range(0f, 0.2f)]
    public float deadband01 = 0.02f;

    [Header("Gizmos")]
    public bool drawGizmos = true;
    [Tooltip("Also draw when the object is not selected in the Scene view.")]
    public bool drawWhenUnselected = false;
    [Tooltip("Arrow length multiplier.")]
    public float gizmoLength = 1.6f;
    [Tooltip("Arrow head size multiplier.")]
    public float gizmoHeadSize = 0.14f;
    public Color gizmoColor = new Color(0f, 1f, 1f, 1f); // cyan
    public Color comColor = new Color(1f, 0.92f, 0.02f, 1f); // yellow
    [Tooltip("Where arrows start from (purely visual). Auto = Nozzle if set, else COM.")]
    public GizmoOrigin gizmoOrigin = GizmoOrigin.Auto;

    [Header("Engines")]
    public List<Engine> engines = new List<Engine>();

    void Awake()
    {
        if (!rb) rb = GetComponent<Rigidbody>();
        if (centerOfMassLocal != Vector3.zero) rb.centerOfMass = centerOfMassLocal;

        rb.interpolation = RigidbodyInterpolation.Interpolate;
        rb.collisionDetectionMode = CollisionDetectionMode.ContinuousSpeculative;
        rb.ResetInertiaTensor();
    }

    void Update()
    {
        foreach (var e in engines)
        {
            e.targetPower = (e.key != KeyCode.None && Input.GetKey(e.key)) ? 8f : 0f;
            float rate = e.targetPower > e.currentPower ? rampUp : rampDown;
            e.currentPower = Mathf.MoveTowards(e.currentPower, e.targetPower, rate * Time.deltaTime);
            UpdateVFX(e);
        }
    }

    void FixedUpdate()
    {
        if (!rb) rb = GetComponent<Rigidbody>();
        Vector3 com = (rb != null) ? rb.worldCenterOfMass : transform.position;

        foreach (var e in engines)
        {
            Vector3 worldDir = ResolveWorldDirection(e);
            if (worldDir.sqrMagnitude < 1e-10f) continue;
            worldDir.Normalize();

            float t01 = Mathf.Clamp01(e.currentPower / 8f);
            if (t01 <= deadband01) continue;

            float force = e.maxThrust * t01;
            Vector3 pos = (e.applyAtNozzle && e.nozzle) ? e.nozzle.position : com;

            rb.AddForceAtPosition(worldDir * force, pos, ForceMode.Force);

            e.lastWorldDir = worldDir;
            e.lastWorldPos = (gizmoOrigin == GizmoOrigin.Nozzle && e.nozzle) ? e.nozzle.position :
                              (gizmoOrigin == GizmoOrigin.COM ? com :
                               (gizmoOrigin == GizmoOrigin.Auto ? (e.nozzle ? e.nozzle.position : com) : com));
        }
    }

    Vector3 ResolveWorldDirection(Engine e)
    {
        Vector3 d = e.direction;
        if (d.sqrMagnitude < 1e-8f) return Vector3.zero;

        switch (e.referenceSpace)
        {
            case RefSpace.Ship:   return transform.TransformDirection(d);
            case RefSpace.Nozzle: return (e.nozzle ? e.nozzle : transform).TransformDirection(d);
            case RefSpace.World:  return d; // already world space
        }
        return transform.TransformDirection(d);
    }

    void UpdateVFX(Engine e)
    {
        if (!e.particles) return;

        float t01 = Mathf.Clamp01(e.currentPower / 8f);
        var emission = e.particles.emission;
        emission.rateOverTime = new ParticleSystem.MinMaxCurve(e.emissionAt8 * t01);

        if (e.startSpeedAt8 > 0f)
        {
            var main = e.particles.main;
            var ss = main.startSpeed;
            ss.mode = ParticleSystemCurveMode.Constant;
            ss.constant = e.startSpeedAt8 * t01;
            main.startSpeed = ss;
        }

        if (t01 > deadband01)
        {
            if (!e.particles.isPlaying) e.particles.Play();
        }
        else
        {
            if (e.particles.isPlaying) e.particles.Stop(true, ParticleSystemStopBehavior.StopEmitting);
        }
    }

    // -------- GIZMOS (always visible & scalable) --------
    void OnDrawGizmos()
    {
        if (!drawGizmos || !drawWhenUnselected) return;
        DrawEngineGizmos();
    }

    void OnDrawGizmosSelected()
    {
        if (!drawGizmos) return;
        DrawEngineGizmos();
    }

    void DrawEngineGizmos()
    {
        if (!rb) rb = GetComponent<Rigidbody>(); // works in edit mode too

        // Draw COM
        Gizmos.color = comColor;
        Vector3 com = rb ? rb.worldCenterOfMass : transform.position;
        Gizmos.DrawSphere(com, 0.06f * Mathf.Max(0.5f, gizmoHeadSize));

        if (engines == null) return;

        foreach (var e in engines)
        {
            // Always preview world direction
            Vector3 dir = ResolveWorldDirection(e);
            if (dir.sqrMagnitude < 1e-8f) continue;
            dir.Normalize();

            // 🔸 NEW: gizmo start is independent of physics setting
            Vector3 start =
                (gizmoOrigin == GizmoOrigin.Nozzle && e.nozzle) ? e.nozzle.position :
                (gizmoOrigin == GizmoOrigin.COM ? com :
                 (gizmoOrigin == GizmoOrigin.Auto ? (e.nozzle ? e.nozzle.position : com) : com));

            float length = Mathf.Max(0.2f, gizmoLength);
            float head = Mathf.Max(0.04f, gizmoHeadSize);

            Gizmos.color = gizmoColor;
            DrawArrow(start, dir, length, head);
        }
    }

    void DrawArrow(Vector3 start, Vector3 dir, float length, float headSize)
    {
        Vector3 end = start + dir * length;
        Gizmos.DrawLine(start, end);

        // Arrow head
        Vector3 right = Vector3.Cross(dir, Vector3.up);
        if (right.sqrMagnitude < 1e-6f) right = Vector3.right;
        right.Normalize();
        Vector3 up = Vector3.Cross(right, dir).normalized;

        Gizmos.DrawLine(end, end - dir * headSize + up * headSize);
        Gizmos.DrawLine(end, end - dir * headSize - up * headSize);
        Gizmos.DrawLine(end, end - dir * headSize + right * headSize);
        Gizmos.DrawLine(end, end - dir * headSize - right * headSize);
    }

    void OnValidate()
    {
        if (!rb) rb = GetComponent<Rigidbody>();
        foreach (var e in engines)
        {
            if (!float.IsFinite(e.direction.x) || !float.IsFinite(e.direction.y) || !float.IsFinite(e.direction.z))
                e.direction = Vector3.forward;

            e.maxThrust = Mathf.Max(0f, e.maxThrust);
            e.emissionAt8 = Mathf.Max(0f, e.emissionAt8);
            e.startSpeedAt8 = Mathf.Max(0f, e.startSpeedAt8);
        }
        rampUp = Mathf.Max(0f, rampUp);
        rampDown = Mathf.Max(0f, rampDown);
        deadband01 = Mathf.Clamp01(deadband01);

        gizmoLength = Mathf.Max(0.05f, gizmoLength);
        gizmoHeadSize = Mathf.Max(0.02f, gizmoHeadSize);
    }
}
