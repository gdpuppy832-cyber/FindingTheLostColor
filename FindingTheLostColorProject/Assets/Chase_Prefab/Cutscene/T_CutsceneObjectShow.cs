using UnityEngine;

public class T_CutsceneObjectShow : MonoBehaviour
{
    [Header("¿¬°á")]
    [Tooltip("ÄÆ¾À »óÅÂ¸¦ È®ÀÎÇÒ T_CutScene3")]
    public T_CutScene3 cutscene;

    [Header("¼³Á¤")]
    [Tooltip("ÄÆ¾ÀÀÌ ½ÃÀÛµÇ¸é ÄÑÁö°í, ÄÆ¾ÀÀÌ ³¡³ª¸é ²¨Áú ¿ÀºêÁ§Æ®")]
    public GameObject targetObject;

    private bool previousCutsceneState = false;

    private void Start()
    {
        // Æò¼Ò¿¡´Â ²¨Á® ÀÖÀ½
        if (targetObject != null)
            targetObject.SetActive(false);

        if (cutscene == null)
            cutscene = FindFirstObjectByType<T_CutScene3>();
    }

    private void Update()
    {
        if (cutscene == null || targetObject == null)
            return;

        bool currentCutsceneState = cutscene.IsCutsceneRunning;

        // ÄÆ¾À ½ÃÀÛ
        if (!previousCutsceneState && currentCutsceneState)
        {
            targetObject.SetActive(true);
        }

        // ÄÆ¾À Á¾·á
        if (previousCutsceneState && !currentCutsceneState)
        {
            targetObject.SetActive(false);
        }

        previousCutsceneState = currentCutsceneState;
    }
}