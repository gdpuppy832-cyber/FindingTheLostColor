using UnityEngine;
using UnityEngine.UI;
using System.Collections;

/// <summary>
/// 보스 오브젝트에 붙이는 대화 트리거.
/// BossAttack의 OnPhase2Started 이벤트(크리스탈이 모두 파괴되어 2페이즈로 전환되는 시점)를 구독해서
/// 그 순간 자동으로 대화 -> 화면 암전 -> 컷씬 이미지 등장 -> 대화 -> 다시 암전 -> 원래 화면 순서로 연출을 재생한다.
/// DialogueManager.cs는 전혀 수정하지 않고, 이 스크립트에서 페이드 연출만 독립적으로 처리한다.
/// </summary>
public class BossPhase2DialogueTrigger : MonoBehaviour
{
    [Header("대화 내용 (1부: 컷씬 이미지가 나오기 전)")]
    [Tooltip("2페이즈 진입 시 가장 먼저 순서대로 말할 대사 목록")]
    public DialogueLine[] dialogues;

    [Header("컷씬 연출")]
    [Tooltip("화면 전체를 덮는 검은 이미지가 붙어있는 CanvasGroup (알파 0에서 시작, 항상 화면 최상단에 배치되어 있어야 함)")]
    public CanvasGroup fadeCanvasGroup;
    [Tooltip("암전 상태에서 서서히 나타날 컷씬 이미지 (fadeCanvasGroup보다 위에 배치되어야 함)")]
    public Image cutsceneImage;
    [Tooltip("화면이 검게 변하거나, 이미지가 나타나고 사라지는 데 걸리는 시간(초)")]
    public float fadeDuration = 1f;

    [Header("대화 내용 (2부: 컷씬 이미지가 나온 뒤)")]
    [Tooltip("컷씬 이미지가 완전히 나타난 뒤 이어서 말할 대사 목록")]
    public DialogueLine[] dialoguesAfterImage;

    [Header("대화 내용 (3부: 원래 게임 화면으로 돌아온 후)")]
    [Tooltip("컷씬이 완전히 끝나고 원래 게임 화면으로 돌아온 후 출력되는 대사")]
    public DialogueLine[] dialoguesAfterImageEnded;

    [Header("연결")]
    [Tooltip("비워두면 같은 오브젝트 또는 부모에서 자동으로 찾음")]
    public BossAttack bossAttack;

    [Header("설정")]
    [Tooltip("한 번 2페이즈 대화가 나온 뒤, 혹시 다시 호출되어도 중복 재생을 막을지 여부")]
    public bool triggerOnlyOnce = true;
    private bool hasTriggered = false;
    private bool sequenceRunning = false;
    private bool phase2Activated = false;

    // 대화 중 플레이어 이동 잠금
    private Rigidbody2D playerRigidbody;
    private Vector2 originalPlayerVelocity;
    private bool playerMovementLocked = false;
    private MonoBehaviour playerMovementScript;

    private void Awake()
    {
        if (bossAttack == null) bossAttack = GetComponent<BossAttack>();
        if (bossAttack == null) bossAttack = GetComponentInParent<BossAttack>();

        // 시작 시점엔 완전히 투명/비활성 상태로 초기화 (평소 게임 화면을 가리지 않도록)
        if (fadeCanvasGroup != null)
        {
            fadeCanvasGroup.alpha = 0f;
            fadeCanvasGroup.blocksRaycasts = false;
            fadeCanvasGroup.gameObject.SetActive(false);
        }
        if (cutsceneImage != null)
        {
            SetImageAlpha(cutsceneImage, 0f);
            cutsceneImage.gameObject.SetActive(false);
        }
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
        if (sequenceRunning || phase2Activated) return;
        if (triggerOnlyOnce && hasTriggered) return;

        // 대화 연출이 시작되었다는 표시
        sequenceRunning = true;
        hasTriggered = true;

        // 1부 대화가 시작되는 순간부터 플레이어 이동 잠금
        LockPlayerMovement();

        // 대화가 없거나 DialogueManager가 없으면 안전하게 바로 2페이즈 시작
        if (dialogues == null || dialogues.Length == 0 || DialogueManager.Instance == null)
        {
            UnlockPlayerMovement();
            ActivatePhase2Once();
            return;
        }

        DialogueManager.Instance.StartDialogue(dialogues, HandleFirstDialogueEnded);
    }

    // 1부 대사가 끝난 직후: 컷씬 연출(암전 -> 이미지 등장) 시작
    private void HandleFirstDialogueEnded()
    {
        StartCoroutine(PlayCutsceneImageThenSecondDialogue());
    }

    private IEnumerator PlayCutsceneImageThenSecondDialogue()
    {
        // 1. 화면이 서서히 검게 변함
        if (fadeCanvasGroup != null)
        {
            fadeCanvasGroup.gameObject.SetActive(true);
            fadeCanvasGroup.blocksRaycasts = true;
            yield return FadeCanvasGroup(fadeCanvasGroup, 0f, 1f, fadeDuration);
        }

        // 2. 암전된 화면 위로 컷씬 이미지가 서서히 나타남
        if (cutsceneImage != null)
        {
            cutsceneImage.gameObject.SetActive(true);
            SetImageAlpha(cutsceneImage, 0f);
            yield return FadeImage(cutsceneImage, 0f, 1f, fadeDuration);
        }

        // 3. 이미지가 보이는 상태에서 2부 대사 진행 (2부 대사가 없으면 바로 컷씬 종료)
        if (dialoguesAfterImage != null && dialoguesAfterImage.Length > 0 && DialogueManager.Instance != null)
        {
            DialogueManager.Instance.StartDialogue(dialoguesAfterImage, HandleSecondDialogueEnded);
        }
        else
        {
            HandleSecondDialogueEnded();
        }
    }

    // 2부 대사가 끝난 직후: 이미지 -> 검은 화면 -> 원래 화면 순서로 되돌리고 2페이즈 발동
    private void HandleSecondDialogueEnded()
    {
        StartCoroutine(EndCutsceneRoutine());
    }
    private void HandleThirdDialogueEnded()
    {
        // 3부 대화가 끝난 뒤 플레이어 이동 잠금 해제
        UnlockPlayerMovement();

        // 3부 대화가 끝난 뒤 최종적으로 2페이즈 시작
        ActivatePhase2Once();
    }
    private void LockPlayerMovement()
    {
        if (playerMovementLocked) return;

        GameObject player = GameObject.FindGameObjectWithTag("Player");

        if (player == null)
        {
            Debug.LogWarning("[BossPhase2DialogueTrigger] Player 태그를 가진 오브젝트를 찾을 수 없습니다.");
            return;
        }

        playerRigidbody = player.GetComponent<Rigidbody2D>();

        if (playerRigidbody == null)
        {
            Debug.LogWarning("[BossPhase2DialogueTrigger] Player에게 Rigidbody2D가 없습니다.");
            return;
        }

        // 플레이어 이동 스크립트 가져오기
        playerMovementScript = player.GetComponent<PlayerMove>();

        if (playerMovementScript != null)
        {
            playerMovementScript.enabled = false;
        }

        // 기존 속도만 저장
        originalPlayerVelocity = playerRigidbody.linearVelocity;

        // 이동 스크립트만 비활성화
        playerRigidbody.linearVelocity = Vector2.zero;

        playerMovementLocked = true;
    }

    private void UnlockPlayerMovement()
    {
        if (!playerMovementLocked) return;

        if (playerRigidbody != null)
        {
            playerRigidbody.linearVelocity = Vector2.zero;
        }

        // 플레이어 이동 스크립트 다시 활성화
        if (playerMovementScript != null)
        {
            playerMovementScript.enabled = true;
        }

        playerRigidbody = null;
        playerMovementScript = null;
        playerMovementLocked = false;
    }
    private void ActivatePhase2Once()
    {
        if (phase2Activated) return;

        phase2Activated = true;
        sequenceRunning = false;

        if (bossAttack != null)
        {
            bossAttack.ActivatePhase2();
        }
    }
    private IEnumerator EndCutsceneRoutine()
    {
        // 1. 컷씬 이미지가 서서히 사라짐
        if (cutsceneImage != null)
        {
            yield return FadeImage(cutsceneImage, 1f, 0f, fadeDuration);
            cutsceneImage.gameObject.SetActive(false);
        }

        // 2. 검은 화면이 서서히 걷히며 원래 게임 화면으로 복귀
        if (fadeCanvasGroup != null)
        {
            yield return FadeCanvasGroup(fadeCanvasGroup, 1f, 0f, fadeDuration);

            // 화면이 완전히 밝아진 뒤에만 비활성화
            fadeCanvasGroup.alpha = 0f;
            fadeCanvasGroup.blocksRaycasts = false;
            fadeCanvasGroup.gameObject.SetActive(false);
        }

        // 3. 여기까지 오면 원래 게임 화면이 완전히 복구된 상태
        //    이제 3부 대화를 시작한다.
        if (dialoguesAfterImageEnded != null &&
            dialoguesAfterImageEnded.Length > 0 &&
            DialogueManager.Instance != null)
        {
            DialogueManager.Instance.StartDialogue(
                dialoguesAfterImageEnded,
                HandleThirdDialogueEnded
            );
        }
        else
        {
            // 3부 대화가 없으면 플레이어 이동 잠금 해제
            UnlockPlayerMovement();

            // 바로 2페이즈 시작
            ActivatePhase2Once();
        }
    }

    private IEnumerator FadeCanvasGroup(CanvasGroup group, float from, float to, float duration)
    {
        float elapsed = 0f;
        group.alpha = from;
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            group.alpha = Mathf.Lerp(from, to, Mathf.Clamp01(elapsed / duration));
            yield return null;
        }
        group.alpha = to;
    }

    private IEnumerator FadeImage(Image image, float from, float to, float duration)
    {
        float elapsed = 0f;
        SetImageAlpha(image, from);
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            SetImageAlpha(image, Mathf.Lerp(from, to, Mathf.Clamp01(elapsed / duration)));
            yield return null;
        }
        SetImageAlpha(image, to);
    }

    private void SetImageAlpha(Image image, float alpha)
    {
        Color c = image.color;
        c.a = alpha;
        image.color = c;
    }
}