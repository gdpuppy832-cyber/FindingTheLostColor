using UnityEngine;
using UnityEngine.UI;
using System.Collections;

/// <summary>
/// 보스 오브젝트에 붙이는 대화 트리거.
/// BossAttack의 OnPhase2Started 이벤트(크리스탈이 모두 파괴되어 2페이즈로 전환되는 시점)를 구독해서
/// 그 순간 자동으로 대화 -> 화면 전환 -> 컷씬 이미지 노출 -> 대화 -> 다시 원래 -> 실제 2페이즈 발동까지 순서대로 진행한다.
/// DialogueManager.cs는 전혀 건드리지 않고, 이 스크립트에서 순서만 제어한다.
/// </summary>
public class BossPhase2DialogueTrigger : MonoBehaviour
{
    [Header("대화 내용 (1차: 컷씬 이미지가 나오기 전)")]
    [Tooltip("2페이즈 진입 시 가장 먼저 순서대로 나올 대사 목록")]
    public DialogueLine[] dialogues;

    [Header("컷씬 연출")]
    [Tooltip("화면 전체를 덮는 검은 이미지가 붙어있는 CanvasGroup (알파 0에서 시작, 항상 화면 최상단에 배치되어 있어야 함)")]
    public CanvasGroup fadeCanvasGroup;
    [Tooltip("검은 상태에서 나타날 컷씬 이미지 (fadeCanvasGroup보다 위에 배치되어야 함)")]
    public Image cutsceneImage;
    [Tooltip("화면이 검게 덮이거나, 이미지가 나타나고 사라지는 데 걸리는 시간(초)")]
    public float fadeDuration = 1f;

    [Header("대화 내용 (2차: 컷씬 이미지가 나온 뒤)")]
    [Tooltip("컷씬 이미지가 나오고 나서 이어서 나올 대사 목록")]
    public DialogueLine[] dialoguesAfterImage;

    [Header("대화 내용 (3차: 다시 원래 화면으로 돌아온 뒤)")]
    [Tooltip("컷씬이 끝나고 다시 원래 게임 화면으로 돌아온 뒤 출력되는 대사 목록")]
    public DialogueLine[] dialoguesAfterImageEnded;

    [Header("연결")]
    [Tooltip("2페이즈 연출을 담당할 보스 스크립트. BossAttack, EZ_BossAttack 등 IBossPhase2Controller를 구현한 스크립트라면 무엇이든 넣을 수 있음. 비워두면 같은 오브젝트 또는 부모에서 자동으로 찾음")]
    public MonoBehaviour bossAttack;

    private IBossPhase2Controller bossController; // bossAttack을 인터페이스로 캐스팅한 실제 사용 참조

    [Header("설정")]
    [Tooltip("한 번 2페이즈 대화가 나온 뒤, 혹시 다시 호출되어도 중복 재생을 막을지 여부")]
    public bool triggerOnlyOnce = true;
    private bool hasTriggered = false;
    private bool sequenceRunning = false;
    private bool phase2Activated = false;

    // 대화 중 플레이어 이동 잠금
    private Rigidbody2D playerRigidbody;
    private Vector2 originalPlayerVelocity;
    private RigidbodyConstraints2D originalPlayerConstraints;
    private bool playerMovementLocked = false;
    private MonoBehaviour playerMovementScript;

    private void Awake()
    {
        // 인스펙터에 연결된 스크립트가 인터페이스를 구현하는지 확인
        if (bossAttack != null)
        {
            bossController = bossAttack as IBossPhase2Controller;
            if (bossController == null)
            {
                Debug.LogWarning($"[BossPhase2DialogueTrigger] 연결된 스크립트 '{bossAttack.GetType().Name}'는 IBossPhase2Controller를 구현하지 않습니다.");
            }
        }

        // 못 찾았으면 같은 오브젝트 -> 부모 순으로 자동 탐색 (BossAttack이든 EZ_BossAttack이든 상관없이 잡힘)
        if (bossController == null) bossController = GetComponent<IBossPhase2Controller>();
        if (bossController == null) bossController = GetComponentInParent<IBossPhase2Controller>();

        // 컷씬 관련 오브젝트들을 초기 숨김 상태로 초기화 (평소 게임 화면에 영향 없도록)
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

        // 대사별로 표시/숨김이 제어되는 오브젝트들은 씬 시작 시점부터 확실히 꺼둠.
        // (Inspector에서 실수로 활성화 상태로 남아있으면 대화가 시작되기도 전에 보여버리는 문제를 방지)
        HideAllDialogueControlledObjects();
    }

    private void OnEnable()
    {
        if (bossController != null)
        {
            bossController.OnPhase2Started += HandlePhase2Started;
        }
    }

    private void OnDisable()
    {
        if (bossController != null)
        {
            bossController.OnPhase2Started -= HandlePhase2Started;
        }
    }

    private void HandlePhase2Started()
    {
        if (sequenceRunning || phase2Activated) return;
        if (triggerOnlyOnce && hasTriggered) return;

        // 대화 연출이 시작되었다는 표시
        sequenceRunning = true;
        hasTriggered = true;

        // 1차 대화가 시작되는 시점부터 플레이어 이동 잠금
        LockPlayerMovement();

        // 대화가 없거나 DialogueManager가 없으면 연출을 생략하고 바로 2페이즈 발동
        if (dialogues == null || dialogues.Length == 0 || DialogueManager.Instance == null)
        {
            UnlockPlayerMovement();
            ActivatePhase2Once();
            return;
        }

        DialogueManager.Instance.StartDialogue(dialogues, HandleFirstDialogueEnded);
    }

    // 1차 대사가 전부 끝남: 컷씬 연출(페이드 -> 이미지 노출) 시작
    private void HandleFirstDialogueEnded()
    {
        StartCoroutine(PlayCutsceneImageThenSecondDialogue());
    }

    private IEnumerator PlayCutsceneImageThenSecondDialogue()
    {
        // ★ 1차 대사와 2차 대사 사이(화면 전환 구간)에는 대사로 켜졌던 오브젝트가 남아있으면 안 되므로,
        // 전환이 시작되기 전에 1차/2차 대사에 등록된 오브젝트를 전부 강제로 꺼버림
        HideAllDialogueControlledObjects();

        // 1. 화면이 검게 덮이며 어두워짐
        if (fadeCanvasGroup != null)
        {
            fadeCanvasGroup.gameObject.SetActive(true);
            fadeCanvasGroup.blocksRaycasts = true;
            yield return FadeCanvasGroup(fadeCanvasGroup, 0f, 1f, fadeDuration);
        }

        // 2. 검게 덮인 화면 위로 컷씬 이미지가 서서히 나타남
        if (cutsceneImage != null)
        {
            cutsceneImage.gameObject.SetActive(true);
            SetImageAlpha(cutsceneImage, 0f);
            yield return FadeImage(cutsceneImage, 0f, 1f, fadeDuration);
        }

        // ★ 이미지가 화면에 뜬 채로 2차 대사가 나오는 이 시점에 2페이즈 "상태"를 발동시킴
        // (보스는 여전히 isFrozenForPhaseTransition == true라서 제자리에 가만히 있고 공격도 안 함 -
        //  실제 이동/공격은 3차 대사가 끝난 뒤 ReleasePhase2MovementFreeze()가 호출돼야 시작됨)
        if (bossController != null)
        {
            bossController.ActivatePhase2();
        }

        // 3. 이미지가 보이는 상태에서 2차 대사 재생 (2차 대사가 끝나면 바로 컷씬 종료)
        if (dialoguesAfterImage != null && dialoguesAfterImage.Length > 0 && DialogueManager.Instance != null)
        {
            DialogueManager.Instance.StartDialogue(dialoguesAfterImage, HandleSecondDialogueEnded);
        }
        else
        {
            HandleSecondDialogueEnded();
        }
    }

    // 2차 대사가 전부 끝남: 이미지 -> 원래 화면 -> 원래 화면 복귀 뒤 3차 대사를 순서대로 발동
    private void HandleSecondDialogueEnded()
    {
        StartCoroutine(EndCutsceneRoutine());
    }
    private void HandleThirdDialogueEnded()
    {
        // 3차 대화가 끝난 뒤 플레이어 이동 잠금 해제
        UnlockPlayerMovement();

        // 3차 대화가 끝난 뒤 최종적으로 2페이즈 발동
        ActivatePhase2Once();
    }

    /// <summary>
    /// dialogues / dialoguesAfterImage / dialoguesAfterImageEnded에 등록된
    /// objectsToShow 오브젝트들을 전부 강제로 비활성화한다. (대화 그룹 사이 전환 구간에 사용)
    /// </summary>
    private void HideAllDialogueControlledObjects()
    {
        HideObjectsFromLines(dialogues);
        HideObjectsFromLines(dialoguesAfterImage);
        HideObjectsFromLines(dialoguesAfterImageEnded);
    }

    private void HideObjectsFromLines(DialogueLine[] lines)
    {
        if (lines == null) return;

        foreach (var line in lines)
        {
            if (line == null) continue;
            if (line.objectsToShow == null) continue;

            foreach (var obj in line.objectsToShow)
            {
                if (obj != null) obj.SetActive(false);
            }
        }
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

        // 플레이어 이동 스크립트 참조 가져오기
        playerMovementScript = player.GetComponent<PlayerMove>();

        if (playerMovementScript != null)
        {
            playerMovementScript.enabled = false;
        }

        // 원래 속도를 저장
        originalPlayerVelocity = playerRigidbody.linearVelocity;

        // 이동 속도를 0으로
        playerRigidbody.linearVelocity = Vector2.zero;

        // 위치(X, Y) 자체를 물리적으로 고정 -> 중력/외력이 있어도 컷씬 동안 절대 움직이지 않음
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
            // 고정했던 위치 제약을 원래대로 복구
            playerRigidbody.constraints = originalPlayerConstraints;
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

        if (bossController != null)
        {
            // 2차 대사 시점에 이미 상태가 발동됐다면 ActivatePhase2()는 내부 가드로 인해 아무 일도 안 하고,
            // 여기서는 실질적으로 동결만 해제됨. (대화 없이 통째로 스킵된 경우엔 상태 발동+동결 해제가 한 번에 처리됨)
            bossController.ActivatePhase2();
            bossController.ReleasePhase2MovementFreeze();

            // 모든 컷씬/대화가 끝난 이 시점에서야 검은 안개가 움직이기 시작함
            // (이전엔 2차 대사 도중에 이미 움직이기 시작해서, 대화가 끝나기도 전에 안개가 전진하는 문제가 있었음)
            bossController.StartBlackFogMovement();
        }
    }
    private IEnumerator EndCutsceneRoutine()
    {
        // ★ 2차 대사와 3차 대사 사이(화면 전환 구간)에도 마찬가지로,
        // 전환이 시작되기 전에 등록된 오브젝트를 전부 강제로 꺼버림
        HideAllDialogueControlledObjects();

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

            // 화면이 완전히 걷힌 뒤에는 비활성화
            fadeCanvasGroup.alpha = 0f;
            fadeCanvasGroup.blocksRaycasts = false;
            fadeCanvasGroup.gameObject.SetActive(false);
        }

        // 3. 원래대로 돌아온 게임 화면 위에서 3차 대사를 재생
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
            // 3차 대화가 없으면 바로 플레이어 이동 잠금 해제 후 2페이즈 발동
            UnlockPlayerMovement();

            // 바로 2페이즈 발동
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