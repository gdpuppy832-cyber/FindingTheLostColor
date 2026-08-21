using UnityEngine;

/// <summary>
/// NPC 등 대화를 가진 오브젝트에 붙이는 스크립트.
/// Inspector에서 dialogues 배열을 채우고, 각 DialogueLine마다
/// 캐릭터 이미지/입 열림/입 닫힘 이미지를 지정하면 된다.
/// </summary>
public class NPCDialogueTrigger : MonoBehaviour
{
    [Header("대화 내용")]
    [Tooltip("이 NPC가 순서대로 말할 대사 목록. 각 요소마다 캐릭터 이미지, 입 열림/닫힘 이미지, 이름, 텍스트를 지정한다.")]
    public DialogueLine[] dialogues;

    [Header("상호작용 설정")]
    [Tooltip("플레이어가 이 오브젝트와 트리거 충돌했을 때 자동으로 대화를 시작할지 여부. 끄면 외부에서 TriggerDialogue()를 직접 호출해야 함.")]
    public bool startOnPlayerTrigger = true;

    [Tooltip("플레이어를 식별할 태그")]
    public string playerTag = "Player";

    [Tooltip("한 번 대화가 끝난 뒤 다시 트리거로 대화를 시작할 수 있는지 여부")]
    public bool canRepeat = true;

    private bool hasTriggered = false;

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (!startOnPlayerTrigger) return;
        if (!other.CompareTag(playerTag)) return;

        TriggerDialogue();
    }

    /// <summary>
    /// 외부(상호작용 키 입력 등)에서 직접 호출해서 대화를 시작할 수도 있다.
    /// 예: npcDialogueTrigger.TriggerDialogue();
    /// </summary>
    public void TriggerDialogue()
    {
        if (hasTriggered && !canRepeat) return;
        if (dialogues == null || dialogues.Length == 0) return;
        if (DialogueManager.Instance == null) return;

        hasTriggered = true;
        DialogueManager.Instance.StartDialogue(dialogues);
    }
}