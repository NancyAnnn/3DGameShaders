using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

public static class CameraAlignSetup
{
    // Original demo camera (from main.cxx):
    //   lookAt = (1.00839, 1.20764, 5.85055)  [Panda3D z-up]
    //   phi = 67.5095, theta = 231.721, radius = 1100.83, fov = 1
    // Converted to Unity y-up and scaled to this scene's size / Unity FOV.
    private static readonly Vector3 LookAt = new Vector3(1.008f, 5.851f, -1.208f);
    private static readonly Vector3 Direction = new Vector3(-0.5723f, 0.3822f, 0.7255f);
    private const float Distance = 12.5f;

    [MenuItem("3DGameShaders/Align Camera To Original Demo")]
    public static void AlignCamera()
    {
        Camera camera = Object.FindObjectOfType<Camera>();
        if (camera == null)
        {
            Debug.LogError("3DGameShaders: no camera found in the scene.");
            return;
        }

        GameObject pivot = GameObject.Find("Pivot");
        if (pivot == null)
        {
            pivot = new GameObject("Pivot");
        }
        pivot.transform.position = LookAt;

        OrbitCamera orbit = camera.GetComponent<OrbitCamera>();
        if (orbit == null)
        {
            orbit = camera.gameObject.AddComponent<OrbitCamera>();
        }
        orbit.target = pivot.transform;

        camera.transform.position = LookAt + Direction * Distance;
        camera.transform.rotation = Quaternion.LookRotation(LookAt - camera.transform.position);
        orbit.SyncFromTransform();

        EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
        Debug.Log("3DGameShaders: camera aligned to the original demo view. Position="
            + camera.transform.position.ToString("F2") + " LookAt=" + LookAt.ToString("F2"));
    }
}