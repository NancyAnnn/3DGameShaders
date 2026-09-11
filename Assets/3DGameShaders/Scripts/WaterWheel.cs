using UnityEngine;

// Spins the mill wheel the way the tutorial does: main.cxx advances the wheel
// node -90 degrees per second around its P axis, which after the Panda3D z-up to
// Unity y-up conversion is the local X axis.
//
// The meshes in the mill scene are parsed straight out of the OBJ, so their
// vertices sit at their world position and the transform origin is not the wheel
// axle. Attach this to a pivot that sits on the axle (WaterSurfaceSetup creates
// one) and the wheel turns in place.
[DisallowMultipleComponent]
public class WaterWheel : MonoBehaviour
{
    [Tooltip("Degrees per second around the wheel's axle. Negative matches the tutorial.")]
    public float degreesPerSecond = -90f;

    private void Update()
    {
        transform.Rotate(Vector3.right, degreesPerSecond * Time.deltaTime, Space.Self);
    }
}
