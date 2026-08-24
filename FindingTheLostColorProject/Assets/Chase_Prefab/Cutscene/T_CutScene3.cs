using UnityEngine;
using UnityEngine.Playables;
using System.Collections;

/// <summary>
/// 대화 2개(1부, 2부)만 진행하는 컷씬 트리거.
/// 1부 대화 전, 2부 대화 전에 각각 Timeline을 재생할 수 있다.
/// SceneStartCutsceneTrigger를 참고했으나, 카메라 흔들림/대화창 숨김/이미지 연출 등은 포함하지 않는다.
/// DialogueManager.cs는 수정하지 않는다.
/// </summary>
public class T_CutScene3 : MonoBehaviour
{
    [Header("대화 내용 (1부)")]
    public DialogueLine[] dialogues;

    [Header("대화 내용 (2부)")]
    public DialogueLine[] dialoguesPart2;

    [Header("Timeline 연출")]
    [Tooltip("1부 대화가 시작되기 전에 재생할 Timeline (비워두면 재생하지 않음)")]
    public PlayableDirector timelineBeforeFirstDialogue;

    [Tooltip("2부 대화가 시작되기 전에 재생할 Timeline (비워두면 재생하지 않음)")]
    public PlayableDirector timelineBeforeSecondDialogue;

    [Header("설정")]
    [Tooltip("한 번 실행된 뒤 다시 트리거되지 않게 할지 여부")]
    public bool playOnlyOnce = true;
    private bool hasTriggered = false;

    // 플레이어 이동 잠금 (Rigidbody2D 자체는 비활성화하지 않고, 이동 스크립트만 끄고 속도를 0으로 유지)
    private Rigidbody2D playerRigidbody;
    private MonoBehaviour playerMovementScript;
    private RigidbodyConstraints2D originalPlayerConstraints;
    private bool playerMovementLocked = false;

    private void Start()
    {
        if (playOnlyOnce && hasTriggered) return;
        hasTriggered = true;

        StartCoroutine(RunCutscene());
    }

    private IEnumerator RunCutscene()
    {
        LockPlayerMovement();

        // 1부 대화 전 Timeline
        if (timelineBeforeFirstDialogue != null)
        {
            yield return PlaySingleTimeline(timelineBeforeFirstDialogue);
        }

        // 1부 대화
        yield return PlayDialogueAndWait(dialogues);

        // 2부 대화 전 Timeline
        if (timelineBeforeSecondDialogue != null)
        {
            yield return PlaySingleTimeline(timelineBeforeSecondDialogue);
        }

        // 2부 대화
        yield return PlayDialogueAndWait(dialoguesPart2);

        UnlockPlayerMovement();
    }

    // ================= 대화 재생 대기 =================
    private IEnumerator PlayDialogueAndWait(DialogueLine[] lines)
    {
        if (lines == null || lines.Length == 0) yield break;
        if (DialogueManager.Instance == null) yield break;

        bool finished = false;
        DialogueManager.Instance.StartDialogue(lines, () => finished = true);

        while (!finished)
        {
            yield return null;
        }
    }

    // ================= Timeline 재생 =================
    private IEnumerator PlaySingleTimeline(PlayableDirector director)
    {
        bool finished = false;
        void OnStopped(PlayableDirector d) { finished = true; }

        director.stopped += OnStopped;
        director.Play();

        while (!finished)
        {
            yield return null;
        }

        director.stopped -= OnStopped;
    }

    // ================= 플레이어 이동 잠금 =================
    private void LockPlayerMovement()
    {
        if (playerMovementLocked) return;

        GameObject player = GameObject.FindGameObjectWithTag("Player");
        if (player == null)
        {
            Debug.LogWarning("[SimpleTimelineCutsceneTrigger] Player 태그를 가진 오브젝트를 찾을 수 없습니다.");
            return;
        }

        playerRigidbody = player.GetComponent<Rigidbody2D>();
        if (playerRigidbody == null)
        {
            Debug.LogWarning("[SimpleTimelineCutsceneTrigger] Player에게 Rigidbody2D가 없습니다.");
            return;
        }

        playerMovementScript = player.GetComponent<PlayerMove>();
        if (playerMovementScript != null)
        {
            playerMovementScript.enabled = false;
        }

        playerRigidbody.linearVelocity = Vector2.zero;

        originalPlayerConstraints = playerRigidbody.constraints;
        playerRigidbody.constraints = originalPlayerConstraints
            | RigidbodyConstraints2D.FreezePositionX
            | RigidbodyConstraints2D.FreezePositionY;

        playerMovementLocked = true;
    }

    private void UnlockPlayerMovement()
    {
        if (!playerMovementLocked) return;

        if (playerRigidbody != null)
        {
            playerRigidbody.constraints = originalPlayerConstraints;
            playerRigidbody.linearVelocity = Vector2.zero;
        }

        if (playerMovementScript != null)
        {
            playerMovementScript.enabled = true;
        }

        playerRigidbody = null;
        playerMovementScript = null;
        playerMovementLocked = false;
    }
}