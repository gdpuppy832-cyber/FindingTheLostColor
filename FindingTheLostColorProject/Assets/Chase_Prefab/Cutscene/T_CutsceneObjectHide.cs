using UnityEngine;

public class T_CutsceneObjectHide : MonoBehaviour
{
    [Header("연결")]
    [Tooltip("컷씬 상태를 확인할 T_CutScene3")]
    public T_CutScene3 cutscene;

    [Header("설정")]
    [Tooltip("컷씬이 시작되면 꺼지고, 컷씬이 끝나면 켜질 오브젝트")]
    public GameObject targetObject;

    private bool previousCutsceneState = false;

    private void Start()
    {
        // 평소에는 켜져 있음
        if (targetObject != null)
            targetObject.SetActive(true);

        if (cutscene == null)
            cutscene = FindFirstObjectByType<T_CutScene3>();
    }

    private void Update()
    {
        if (cutscene == null || targetObject == null)
            return;

        bool currentCutsceneState = cutscene.IsCutsceneRunning;

        // 컷씬 시작
        if (!previousCutsceneState && currentCutsceneState)
        {
            targetObject.SetActive(false);
        }

        // 컷씬 종료
        if (previousCutsceneState && !currentCutsceneState)
        {
            targetObject.SetActive(true);
        }

        previousCutsceneState = currentCutsceneState;
    }
}