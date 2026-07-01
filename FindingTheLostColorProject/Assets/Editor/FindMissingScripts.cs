using UnityEngine;
using UnityEditor;

public class FindMissingScripts : EditorWindow
{
    [MenuItem("Tools/Find Missing Scripts")]
    public static void FindAll()
    {
        int sceneCount = 0;
        int prefabCount = 0;

        // 1. 현재 씬에서 비활성화된 오브젝트까지 포함하여 탐색
        #if UNITY_2023_1_OR_NEWER
        GameObject[] sceneObjects = GameObject.FindObjectsByType<GameObject>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        #else
        GameObject[] sceneObjects = Resources.FindObjectsOfTypeAll<GameObject>();
        #endif

        foreach (GameObject go in sceneObjects)
        {
            // 씬에 실제로 존재하는 오브젝트인지 확인 (에셋 프리팹 프리뷰 등 제외)
            if (go.hideFlags == HideFlags.None || go.scene.name != null)
            {
                Component[] components = go.GetComponents<Component>();
                for (int i = 0; i < components.Length; i++)
                {
                    if (components[i] == null)
                    {
                        Debug.LogError($"[Scene Missing Script] 오브젝트 이름: '{go.name}' (위치: {GetGameObjectPath(go)})", go);
                        sceneCount++;
                        break;
                    }
                }
            }
        }

        // 2. 프로젝트 폴더 내 모든 프리팹 탐색
        string[] allAssetPaths = AssetDatabase.GetAllAssetPaths();
        foreach (string path in allAssetPaths)
        {
            if (path.EndsWith(".prefab") && path.StartsWith("Assets/"))
            {
                GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                if (prefab != null)
                {
                    Component[] components = prefab.GetComponentsInChildren<Component>(true);
                    foreach (Component c in components)
                    {
                        if (c == null)
                        {
                            Debug.LogError($"[Prefab Missing Script] 프리팹 파일 경로: '{path}'", prefab);
                            prefabCount++;
                            break;
                        }
                    }
                }
            }
        }

        Debug.LogWarning($"[검사 완료] 씬에서 {sceneCount}개, 프리팹에서 {prefabCount}개의 Missing 스크립트를 발견했습니다. (콘솔창의 빨간색 에러 로그를 클릭하면 대상을 즉시 가리킵니다.)");
    }

    private static string GetGameObjectPath(GameObject obj)
    {
        string path = obj.name;
        while (obj.transform.parent != null)
        {
            obj = obj.transform.parent.gameObject;
            path = obj.name + "/" + path;
        }
        return path;
    }
}
