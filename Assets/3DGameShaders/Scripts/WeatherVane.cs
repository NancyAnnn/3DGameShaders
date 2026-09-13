using UnityEngine;

// The original loops a baked "weather-vane-shake" animation on the vane node.
// The OBJ we parse carries no animation, so the vane is driven here instead: a
// slow turn around its own vertical axle with a wobble on top, which is what the
// baked animation reads as.
//
// The vane mesh sits at the world origin, so attach this to a pivot placed on
// the axle (DemoSceneSetup creates one) and the vane turns in place.
[DisallowMultipleComponent]
public class WeatherVane : MonoBehaviour
{
    [Tooltip("Degrees per second of the slow turn.")]
    public float turnSpeed = 12f;

    [Tooltip("Degrees of wobble added on top of the turn.")]
    public float wobbleDegrees = 9f;

    [Tooltip("How fast the wobble swings.")]
    public float wobbleSpeed = 0.8f;

    private float m_Yaw;

    private void Update()
    {
        m_Yaw += turnSpeed * Time.deltaTime;
        float wobble = Mathf.Sin(Time.time * wobbleSpeed) * wobbleDegrees;
        transform.localRotation = Quaternion.Euler(0f, m_Yaw + wobble, 0f);
    }
}
