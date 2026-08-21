using UnityEngine;

/// <summary>
/// 보스 오브젝트에 붙이는 대화 트리거.
/// BossAttack의 OnPhase2Started 이벤트(크리스탈이 모두 파괴되어 2페이즈로 전환되는 시점)를 구독해서
/// 그 순간 자동으로 대화를 시작시킨다.
/// </summary>
public class BossPhase2DialogueTrigger : MonoBehaviour
{
    [Header("대화 내용")]
    [Tooltip("2페이즈 진입 시 순서대로 말할 대사 목록. 각 요소마다 캐릭터 이미지, 입 열림/닫힘 이미지, 이름, 텍스트를 지정한다.")]
    public DialogueLine[] dialogues;

    [Header("연결")]
    [Tooltip("비워두면 같은 오브젝트 또는 부모에서 자동으로 찾음")]
    public BossAttack bossAttack;

    [Header("설정")]
    [Tooltip("한 번 2페이즈 대화가 나온 뒤, 혹시 다시 호출되어도 중복 재생을 막을지 여부")]
    public bool triggerOnlyOnce = true;

    private bool hasTriggered = false;

    private void Awake()
    {
        if (bossAttack == null) bossAttack = GetComponent<BossAttack>();
        if (bossAttack == null) bossAttack = GetComponentInParent<BossAttack>();
    }

    private void OnEnable()
    {
        if (bossAttack != null)
        {
            bossAttack.OnPhase2Started += HandlePhase2Started;
        }
    }

    private void OnDisable()
    {
        if (bossAttack != null)
        {
            bossAttack.OnPhase2Started -= HandlePhase2Started;
        }
    }

    private void HandlePhase2Started()
    {
        if (triggerOnlyOnce && hasTriggered) return;

        // 대화가 없거나 DialogueManager가 없으면, 보스가 동결된 채로 영원히 멈춰있으면 안 되므로
        // 안전장치로 대화 없이 바로 2페이즈를 발동시킴
        if (dialogues == null || dialogues.Length == 0 || DialogueManager.Instance == null)
        {
            if (bossAttack != null) bossAttack.ActivatePhase2();
            return;
        }

        hasTriggered = true;
        DialogueManager.Instance.StartDialogue(dialogues, HandleDialogueEnded);
    }

    private void HandleDialogueEnded()
    {
        if (bossAttack != null)
        {
            bossAttack.ActivatePhase2();
        }
    }
}