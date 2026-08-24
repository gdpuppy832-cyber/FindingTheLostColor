using UnityEngine;
using UnityEngine.Playables;
using System.Collections;

/// <summary>
/// ��ȭ 2��(1��, 2��)�� �����ϴ� �ƾ� Ʈ����.
/// 1�� ��ȭ ��, 2�� ��ȭ ���� ���� Timeline�� ����� �� �ִ�.
/// SceneStartCutsceneTrigger�� ����������, ī�޶� ��鸲/��ȭâ ����/�̹��� ���� ���� �������� �ʴ´�.
/// DialogueManager.cs�� �������� �ʴ´�.
/// </summary>
public class T_CutScene3 : MonoBehaviour
{
    [Header("��ȭ ���� (1��)")]
    public DialogueLine[] dialogues;

    [Header("��ȭ ���� (2��)")]
    public DialogueLine[] dialoguesPart2;

    [Header("Timeline ����")]
    [Tooltip("1�� ��ȭ�� ���۵Ǳ� ���� ����� Timeline (����θ� ������� ����)")]
    public PlayableDirector timelineBeforeFirstDialogue;

    [Tooltip("2차 대화가 시작되기 전에 재생할 Timeline (비워두면 재생하지 않음)")]
    public PlayableDirector timelineBeforeSecondDialogue;

    [Tooltip("2차 대화가 끝난 후에 재생할 Timeline (비워두면 재생하지 않음)")]
    public PlayableDirector timelineAfterSecondDialogue;

    [Header("����")]
    [Tooltip("�� �� ����� �� �ٽ� Ʈ���ŵ��� �ʰ� ���� ����")]
    public bool playOnlyOnce = true;
    private bool hasTriggered = false;

    public bool IsCutsceneRunning { get; private set; }

    // �÷��̾� �̵� ��� (Rigidbody2D ��ü�� ��Ȱ��ȭ���� �ʰ�, �̵� ��ũ��Ʈ�� ��� �ӵ��� 0���� ����)
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
        IsCutsceneRunning = true;

        LockPlayerMovement();

        // 1�� ��ȭ �� Timeline
        if (timelineBeforeFirstDialogue != null)
        {
            yield return PlaySingleTimeline(timelineBeforeFirstDialogue);
        }

        // 1�� ��ȭ
        yield return PlayDialogueAndWait(dialogues);

        // 2차 대화 전 Timeline
        if (timelineBeforeSecondDialogue != null)
        {
            yield return PlaySingleTimeline(timelineBeforeSecondDialogue);
        }

        // 2차 대화
        yield return PlayDialogueAndWait(dialoguesPart2);

        // 2차 대화 후 Timeline
        if (timelineAfterSecondDialogue != null)
        {
            yield return PlaySingleTimeline(timelineAfterSecondDialogue);
        }

        UnlockPlayerMovement();

        IsCutsceneRunning = false;
    }

    // ================= ��ȭ ��� ��� =================
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

    // ================= Timeline ��� =================
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

    // ================= �÷��̾� �̵� ��� =================
    private void LockPlayerMovement()
    {
        if (playerMovementLocked) return;

        GameObject player = GameObject.FindGameObjectWithTag("Player");
        if (player == null)
        {
            Debug.LogWarning("[SimpleTimelineCutsceneTrigger] Player �±׸� ���� ������Ʈ�� ã�� �� �����ϴ�.");
            return;
        }

        playerRigidbody = player.GetComponent<Rigidbody2D>();
        if (playerRigidbody == null)
        {
            Debug.LogWarning("[SimpleTimelineCutsceneTrigger] Player���� Rigidbody2D�� �����ϴ�.");
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