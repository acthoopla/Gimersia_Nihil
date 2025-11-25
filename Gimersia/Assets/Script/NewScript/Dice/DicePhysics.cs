using UnityEngine;

[RequireComponent(typeof(Rigidbody))]
public class DicePhysics : MonoBehaviour
{
    private Rigidbody rb;
    public float minVelocityToStop = 0.05f;

    void Awake()
    {
        rb = GetComponent<Rigidbody>();
    }

    public void Launch(Vector3 force, Vector3 torque)
    {
        rb.isKinematic = false;
        rb.useGravity = true;
        rb.AddForce(force, ForceMode.Impulse);
        rb.AddTorque(torque, ForceMode.Impulse);
    }

    public void StopAndReset(Vector3 position, Quaternion rotation)
    {
        rb.isKinematic = true;
        rb.useGravity = false;
        rb.velocity = Vector3.zero;
        rb.angularVelocity = Vector3.zero;
        transform.position = position;
        transform.rotation = rotation;
    }

    // Cek apakah benda ini masih bergerak
    public bool IsMoving()
    {
        return rb.velocity.magnitude > minVelocityToStop || rb.angularVelocity.magnitude > minVelocityToStop;
    }
}