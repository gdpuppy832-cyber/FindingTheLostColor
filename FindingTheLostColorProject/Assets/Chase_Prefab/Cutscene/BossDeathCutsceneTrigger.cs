using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using System.Collections;

/// <summary>
/// 보스 처치 후 재생되는 7단계 엔딩 컷씬 트리거.
/// BossPhase2DialogueTrigger.cs의 구조/사용 방식을 참고했으며, DialogueManager.cs는 수정하지 않는다.
/// 페이드, Timeline, PlayableDirector, 카메라 흔들림, 대화창 숨김/표시 제어는 사용하지 않는다.
/// 이미지는 즉시 켜지고 즉시 꺼진다.
/// </summary>
public class BossDeathCutsceneTrigger : MonoBehaviour
{
    [System.Serializable]
    public class CutsceneStep
    {
        public Image image;
        public DialogueLine[] dialogues;
    }

    [Header("Cutscene Steps")]
    [Tooltip("총 7단계. 7번째(Element 6)는 Dialogues를 사용하지 않는다.")]
    public CutsceneStep[] steps;

    [Header("Ending Scene")]
    [Tooltip("Enter 입력 후 이동할 씬 이름")]
    public string nextSceneName;

    [Header("Timing")]
    [Tooltip("이미지가 표시된 후 대화가 시작되기까지의 대기 시간(초)")]
    public float delayBeforeDialogue = 2f;

    private bool sequenceRunning;
    private bool hasTriggered;

    // 플레이어 조작 잠금 관련
    private Rigidbody2D playerRigidbody;
    private RigidbodyConstraints2D originalConstraints;
    private MonoBehaviour playerMovementScript;
    private bool playerMovementLocked = false;

    private Image currentActiveImage;

    /// <summary>
    /// 외부 보스 스크립트에서 보스가 처치되는 순간 호출한다.
    /// </summary>
    public void StartDeathCutscene()
    {
        if (sequenceRunning || hasTriggered) return;

        sequenceRunning = true;
        hasTriggered = true;

        StartCoroutine(RunCutsceneSequence());
    }

    private IEnumerator RunCutsceneSequence()
    {
        LockPlayerMovement();

        if (steps == null || steps.Length == 0)
        {
            Debug.LogWarning("[BossDeathCutsceneTrigger] steps가 비어 있습니다.");
            UnlockPlayerMovement();
            sequenceRunning = false;
            yield break;
        }

        // 1~6단계: 이미지 ON -> 대화 -> 이미지 OFF
        for (int i = 0; i < steps.Length && i < 6; i++)
        {
            yield return RunDialogueStep(steps[i]);
        }

        // 7단계: 이미지만 ON, 대화 없음, Enter 대기
        if (steps.Length >= 7)
        {
            yield return RunFinalStep(steps[6]);
        }
        else
        {
            Debug.LogWarning("[BossDeathCutsceneTrigger] 7번째 step이 존재하지 않아 Enter 대기 단계를 건너뜁니다.");
        }

        // Enter 입력 완료 후: 조작 잠금 해제 -> 씬 이동
        UnlockPlayerMovement();

        if (!string.IsNullOrEmpty(nextSceneName))
        {
            SceneManager.LoadScene(nextSceneName);
        }
        else
        {
            Debug.LogWarning("[BossDeathCutsceneTrigger] nextSceneName이 비어 있어 씬 전환을 수행하지 않습니다.");
        }

        sequenceRunning = false;
    }

    // 1~6단계 공통 처리: 이전 이미지 끄기 -> 현재 이미지 켜기 -> 2초 대기 -> 대화 -> 대화 종료 대기
    private IEnumerator RunDialogueStep(CutsceneStep step)
    {
        if (currentActiveImage != null)
        {
            currentActiveImage.gameObject.SetActive(false);
            currentActiveImage = null;
        }

        if (step == null)
        {
            yield break;
        }

        if (step.image != null)
        {
            step.image.gameObject.SetActive(true);
            currentActiveImage = step.image;
        }

        // 이미지가 표시된 후 대화가 시작되기 전까지 잠깐 대기
        if (delayBeforeDialogue > 0f)
        {
            yield return new WaitForSeconds(delayBeforeDialogue);
        }

        if (step.dialogues != null && step.dialogues.Length > 0 && DialogueManager.Instance != null)
        {
            bool dialogueFinished = false;
            DialogueManager.Instance.StartDialogue(step.dialogues, () => dialogueFinished = true);

            while (!dialogueFinished)
            {
                yield return null;
            }
        }
        // 대사가 없거나 DialogueManager가 없으면 대화 없이 바로 다음 단계로 진행

        if (step.image != null)
        {
            step.image.gameObject.SetActive(false);
            if (currentActiveImage == step.image)
            {
                currentActiveImage = null;
            }
        }
    }

    // 7단계 처리: 이미지 ON 상태 유지, 대화 없음, Enter 입력 대기
    private IEnumerator RunFinalStep(CutsceneStep step)
    {
        if (currentActiveImage != null)
        {
            currentActiveImage.gameObject.SetActive(false);
            currentActiveImage = null;
        }

        if (step == null || step.image == null)
        {
            Debug.LogWarning("[BossDeathCutsceneTrigger] 7단계 이미지가 비어 있습니다. Enter 입력만 대기합니다.");
        }
        else
        {
            step.image.gameObject.SetActive(true);
            currentActiveImage = step.image;
        }

        while (!Input.GetKeyDown(KeyCode.Return) && !Input.GetKeyDown(KeyCode.KeypadEnter))
        {
            yield return null;
        }
    }

    private void LockPlayerMovement()
    {
        if (playerMovementLocked) return;

        GameObject player = GameObject.FindGameObjectWithTag("Player");

        if (player == null)
        {
            Debug.LogWarning("[BossDeathCutsceneTrigger] Player 태그를 가진 오브젝트를 찾을 수 없습니다.");
            return;
        }

        playerRigidbody = player.GetComponent<Rigidbody2D>();

        playerMovementScript = player.GetComponent<PlayerMove>();
        if (playerMovementScript != null)
        {
            playerMovementScript.enabled = false;
        }

        if (playerRigidbody != null)
        {
            // Rigidbody2D 자체는 계속 활성화 상태로 유지. simulated를 끄지 않는다.
            playerRigidbody.linearVelocity = Vector2.zero;

            originalConstraints = playerRigidbody.constraints;
            playerRigidbody.constraints = originalConstraints | RigidbodyConstraints2D.FreezePosition;
        }
        else
        {
            Debug.LogWarning("[BossDeathCutsceneTrigger] Player에게 Rigidbody2D가 없습니다.");
        }

        playerMovementLocked = true;
    }

    private void UnlockPlayerMovement()
    {
        if (!playerMovementLocked) return;

        if (playerRigidbody != null)
        {
            playerRigidbody.constraints = originalConstraints;
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