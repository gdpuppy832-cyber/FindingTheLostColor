using UnityEngine;

public class CutsceneTrigger : MonoBehaviour
{
    [Header("트리거 설정")]
    [Tooltip("플레이어가 영역에 들어왔을 때 단 1번만 컷씬을 실행할지 여부")]
    public bool triggerOnce = true;

    [Tooltip("감지할 플레이어 태그 (기본값: Player)")]
    public string playerTag = "Player";

    private bool hasTriggered = false;

    private void OnTriggerEnter2D(Collider2D collision)
    {
        if (hasTriggered && triggerOnce) return;

        if (collision.CompareTag(playerTag))
        {
            hasTriggered = true;
            Debug.Log($"[CutsceneTrigger] {gameObject.name} 컷씬 발동 영역 진입! 하단 대화창 컷씬을 개시합니다.");

            // CutsceneDialogueManager에 등록된 인스펙터 대사 1~8번을 실행!
            if (CutsceneDialogueManager.Instance != null)
            {
                CutsceneDialogueManager.Instance.PlayInspectorDialogues();
            }
        }
    }
}
