using UnityEngine;
using UnityEditor;

namespace LuminariaDevelopment.GasPlanetShader
{
    public class CustomPrefabMenu : MonoBehaviour
    {
        [MenuItem("GameObject/Gas Planet", false, 10)]
        static void CreateCustomPrefab(MenuCommand menuCommand)
        {
            // Load your custom prefab
            GameObject prefab = Resources.Load<GameObject>("Prefabs/Planet");

            if (prefab != null)
            {
                // Get the active scene view camera
                SceneView sceneView = SceneView.lastActiveSceneView;
                if (sceneView != null)
                {
                    // Calculate spawn position in front of the scene view camera
                    Vector3 spawnPosition = sceneView.camera.transform.position + sceneView.camera.transform.forward * 5f;

                    // Instantiate the prefab at the calculated spawn position
                    GameObject obj = Instantiate(prefab, spawnPosition, Quaternion.identity);

                    // Remove "(Clone)" from the name
                    obj.name = ("Gas Planet");

                    // Ensure the instantiated object is properly placed in the scene hierarchy
                    GameObjectUtility.SetParentAndAlign(obj, menuCommand.context as GameObject);

                    // Register the creation in the Undo system
                    Undo.RegisterCreatedObjectUndo(obj, "Create " + obj.name);
                }
                else
                {
                    Debug.LogError("Scene view not found.");
                }
            }
            else
            {
                Debug.LogError("Prefab not found.");
            }
        }
    }
}
