using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Playables;
using System.Collections;

/// <summary>
/// S2_SceneStartCutsceneController의 대화 타이밍. "1번 대화"=dialoguesBeforeCutscene,
/// "2번 대화"=dialoguesDuringImage, "3번 대화"=dialoguesAfterCutscene 기준.
/// </summary>
public enum S2TimelinePlayTiming
{
    BeforeFirstDialogue,
    BeforeSecondDialogue,
    AfterSecondDialogue,
    AfterThirdDialogue
}

[System.Serializable]
public class S2CutsceneTimelineEvent
{
    [Tooltip("이 시점에 재생할 Timeline (PlayableDirector). 비워두면 이 항목은 무시됨")]
    public PlayableDirector timeline;

    [Tooltip("이 Timeline이 재생될 시점")]
    public S2TimelinePlayTiming timing;
}

/// <summary>
/// 씬이 시작되면 자동으로 실행되는 컷씬 컨트롤러.
/// 흐름: 플레이어 잠금 -> 시작 대화 -> Timeline 순차 재생 -> 컷씬 이미지(+이미지 위 대화) ->
///       원래 화면 복귀 -> 종료 후 대화 -> 플레이어 잠금 해제.
///
/// DialogueManager.cs, DialogueLine 클래스는 전혀 수정하지 않으며,
/// 기존 BossPhase2DialogueTrigger와 동일한 코드 스타일(FadeCanvasGroup/FadeImage/SetImageAlpha 등)을 따른다.
/// </summary>
public class S2_SceneStartCutsceneController : MonoBehaviour
{
    [Header("시작 대화")]
    [Tooltip("씬이 시작되면 가장 먼저 출력되는 대사 목록")]
    public DialogueLine[] dialoguesBeforeCutscene;

    [Header("페이드")]
    [Tooltip("화면 전체를 덮는 검은 이미지가 붙어있는 CanvasGroup (알파 0에서 시작)")]
    public CanvasGroup fadeCanvasGroup;
    [Tooltip("화면이 검게 덮이거나 걷히는 데 걸리는 시간(초)")]
    public float fadeDuration = 2f;

    [Header("Timeline")]
    [Tooltip("순서대로 재생할 Timeline 목록. 비어있는 항목이나 배열 자체가 비어있으면 자동으로 건너뜀")]
    public PlayableDirector[] timelines;

    [Tooltip("1번/2번/3번 대화 기준 특정 시점에 재생할 Timeline 목록. 여러 개를 등록할 수 있으며, " +
             "같은 시점을 가진 항목이 여러 개면 배열 순서대로 순차 재생됨")]
    public S2CutsceneTimelineEvent[] timelineEvents;

    [Header("컷씬 이미지")]
    [Tooltip("컷씬 중간에 보여줄 UI 이미지. 비워두면 이미지 단계 전체를 건너뜀")]
    public Image cutsceneImage;
    [Tooltip("이미지가 서서히 나타나는 시간(초)")]
    public float imageFadeInDuration = 1f;
    [Tooltip("이미지가 완전히 나타난 뒤, 대화가 시작되기 전까지 그대로 유지되는 시간(초)")]
    public float imageStayDuration = 3f;
    [Tooltip("이미지가 서서히 사라지는 시간(초)")]
    public float imageFadeOutDuration = 1f;

    [Header("이미지 대화")]
    [Tooltip("컷씬 이미지가 보이는 상태에서 출력할 대사 목록. 비어있으면 이미지만 유지되다가 바로 사라짐")]
    public DialogueLine[] dialoguesDuringImage;

    [Header("컷씬 전용 카메라")]
    [Tooltip("컷씬이 진행되는 동안에만 나타났다가, 컷씬이 끝나면 다시 사라지는 카메라(오브젝트)")]
    public CutsceneOnlyCamera cutsceneOnlyCamera;

    [Header("컷씬 종료 시 사라지는 오브젝트")]
    [Tooltip("컷씬이 끝나는 순간 자동으로 비활성화될 오브젝트 목록. 여러 개 등록 가능")]
    public HideAfterCutscene[] objectsToHideAfterCutscene;

    [Header("카메라 흔들림")]
    [Tooltip("흔들릴 카메라. 비워두면 흔들림 관련 함수 호출을 무시함")]
    public Camera targetCamera;

    [Tooltip("흔들림 지속 시간(초)")]
    public float shakeDuration = 0.3f;
    [Tooltip("흔들림 강도 (최대 이동 거리)")]
    public float shakeStrength = 0.2f;

    [Header("컷씬 종료 후 대화")]
    [Tooltip("이미지가 완전히 사라지고 원래 화면으로 돌아온 뒤 출력할 대사 목록")]
    public DialogueLine[] dialoguesAfterCutscene;

    [Header("보스/카메라/씬 전환 잠금")]
    [Tooltip("컷씬 동안 공격/이동을 멈춰둘 보스 스크립트")]
    public BossChaseAttack bossChaseAttack;
    public BossChaseMove bossChaseMove;
    [Tooltip("컷씬 동안 자동 스크롤을 멈춰둘 카메라 스크립트")]
    public AutoCameraMove autoCameraMove;
    [Tooltip("컷씬 동안 다음 씬 전환 카운트다운을 멈춰둘 스크립트")]
    public BossChaseSceneTransition bossChaseSceneTransition;

    [Header("플레이어")]
    [Tooltip("컷씬 동안 비활성화했다가 컷씬이 끝나면 다시 활성화할 플레이어 이동 스크립트. " +
             "Rigidbody2D 자체는 절대 건드리지 않고, 이 스크립트의 enabled만 제어함")]
    public MonoBehaviour playerMovementScript;

    [Header("설정")]
    [Tooltip("한 번 실행된 뒤 다시 자동 실행되지 않게 할지 여부")]
    public bool playOnlyOnce = true;
    private bool hasTriggered = false;

    // 플레이어 잠금 상태 (Rigidbody2D는 끄지 않고 속도만 0으로 유지)
    private Rigidbody2D playerRigidbody;
    private bool playerMovementLocked = false;

    // 카메라 흔들림 복구용 원래 위치
    private Vector3 cameraOriginalLocalPosition;
    private bool hasCameraOriginalPosition = false;
    private Coroutine shakeCoroutine;

    private void Awake()
    {
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

        if (targetCamera != null)
        {
            cameraOriginalLocalPosition = targetCamera.transform.localPosition;
            hasCameraOriginalPosition = true;
        }

        // 씬이 처음 시작될 때는 자동 이동 카메라 오브젝트 자체를 꺼둠.
        // 컷씬이 완전히 끝난 뒤(EndCutscene)에 다시 켜짐.
        if (autoCameraMove != null)
        {
            autoCameraMove.gameObject.SetActive(false);
        }
    }

    private void Start()
    {
        if (playOnlyOnce && hasTriggered) return;
        hasTriggered = true;

        StartCoroutine(RunCutscene());
    }

    // ================= 전체 진행 순서 =================
    private IEnumerator RunCutscene()
    {
        LockAllSystems();

        // 컷씬이 시작되는 시점에 컷씬 전용 카메라를 켬
        if (cutsceneOnlyCamera != null)
        {
            cutsceneOnlyCamera.ShowForCutscene();
        }

        // 컷씬 도중 예외가 발생하더라도 플레이어가 영구히 잠긴 채로 남지 않도록,
        // 진행 전체를 try/finally로 감싸서 마지막에 반드시 잠금 해제 및 정리가 실행되게 함
        try
        {
            // 1번 대화 이전
            yield return PlayTimelinesForTiming(S2TimelinePlayTiming.BeforeFirstDialogue);

            // 1. 시작 대화
            yield return PlayDialogueAndWait(dialoguesBeforeCutscene);

            // 2. Timeline 순차 재생 (대화창이 자동으로 닫힌 뒤)
            yield return PlayAllTimelines();

            // 3~5. 컷씬 이미지 등장 -> 이미지 위 대화(2번 대화) -> 이미지 퇴장
            yield return PlayCutsceneImageSequence();

            // 3번 대화 이후
            // 6. 컷씬 종료 후 대화 (원래 화면으로 돌아온 뒤)
            yield return PlayDialogueAndWait(dialoguesAfterCutscene);

            yield return PlayTimelinesForTiming(S2TimelinePlayTiming.AfterThirdDialogue);
        }
        finally
        {
            // 정상 종료든, 도중에 문제가 생겼든 항상 마지막에 실행되어야 하는 정리 작업
            EndCutscene();
        }
    }

    private void EndCutscene()
    {
        // 컷씬이 끝난 시점에 자동 이동 카메라 오브젝트를 켬.
        // UnlockAllSystems()보다 먼저 켜야, 켜지는 순간 AutoCameraMove.Start()가 실행되며
        // 위치를 초기화한 뒤에 movementLocked가 false로 풀리는 순서가 보장됨
        if (autoCameraMove != null)
        {
            autoCameraMove.gameObject.SetActive(true);
        }

        // 컷씬이 완전히 끝났으므로, 컷씬 전용 카메라는 다시 숨김
        if (cutsceneOnlyCamera != null)
        {
            cutsceneOnlyCamera.HideAfterCutscene();
        }

        // 컷씬 동안만 보여야 했던 오브젝트들도 이 시점에 함께 숨김
        if (objectsToHideAfterCutscene != null)
        {
            foreach (var obj in objectsToHideAfterCutscene)
            {
                if (obj != null) obj.HideNow();
            }
        }

        UnlockAllSystems();

        // 카메라가 흔들리는 중이었다면 강제로 멈추고 원래 위치로 복구
        if (shakeCoroutine != null)
        {
            StopCoroutine(shakeCoroutine);
            shakeCoroutine = null;
        }
        if (targetCamera != null && hasCameraOriginalPosition)
        {
            targetCamera.transform.localPosition = cameraOriginalLocalPosition;
        }

        if (cutsceneImage != null)
        {
            SetImageAlpha(cutsceneImage, 0f);
            cutsceneImage.gameObject.SetActive(false);
        }

        if (fadeCanvasGroup != null)
        {
            fadeCanvasGroup.alpha = 0f;
            fadeCanvasGroup.blocksRaycasts = false;
            fadeCanvasGroup.gameObject.SetActive(false);
        }
    }

    // ================= 대화 재생 대기 =================
    private IEnumerator PlayDialogueAndWait(DialogueLine[] lines)
    {
        // 대화가 없거나 DialogueManager가 없으면 이 단계를 조용히 건너뜀
        if (lines == null || lines.Length == 0) yield break;
        if (DialogueManager.Instance == null) yield break;

        bool finished = false;
        DialogueManager.Instance.StartDialogue(lines, () => finished = true);

        while (!finished)
        {
            yield return null;
        }
    }

    // ================= Timeline 순차 재생 =================
    private IEnumerator PlayAllTimelines()
    {
        if (timelines == null) yield break;

        foreach (var director in timelines)
        {
            if (director == null) continue; // 비어있는 항목은 건너뜀
            yield return PlaySingleTimeline(director);
        }
    }

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

    // ================= 시점 지정 Timeline 재생 =================
    private IEnumerator PlayTimelinesForTiming(S2TimelinePlayTiming targetTiming)
    {
        if (timelineEvents == null) yield break;

        foreach (var evt in timelineEvents)
        {
            if (evt == null) continue;
            if (evt.timing != targetTiming) continue;
            if (evt.timeline == null) continue; // Timeline이 비어있으면 건너뜀

            yield return PlaySingleTimeline(evt.timeline);
        }
    }

    // ================= 컷씬 이미지 + 이미지 위 대화 =================
    private IEnumerator PlayCutsceneImageSequence()
    {
        // 이미지가 비어있으면 화면 암전/이미지 연출만 건너뛰고,
        // 2번 대화(dialoguesDuringImage)와 그 앞뒤 타임라인 시점은 그대로 재생함
        bool hasImage = cutsceneImage != null;

        if (hasImage)
        {
            // 1. 화면이 검게 덮임 (fadeCanvasGroup이 있을 때만)
            if (fadeCanvasGroup != null)
            {
                fadeCanvasGroup.gameObject.SetActive(true);
                fadeCanvasGroup.blocksRaycasts = true;
                yield return FadeCanvasGroup(fadeCanvasGroup, 0f, 1f, fadeDuration);
            }

            // 2. 이미지 Fade In
            cutsceneImage.gameObject.SetActive(true);
            SetImageAlpha(cutsceneImage, 0f);
            yield return FadeImage(cutsceneImage, 0f, 1f, imageFadeInDuration);

            // 3. 이미지 유지
            if (imageStayDuration > 0f)
            {
                yield return new WaitForSeconds(imageStayDuration);
            }
        }

        // 2번 대화 이전
        yield return PlayTimelinesForTiming(S2TimelinePlayTiming.BeforeSecondDialogue);

        // 4. 이미지가 보이는 상태에서 대화 (이미지가 없으면 그냥 현재 화면 위에서 재생)
        yield return PlayDialogueAndWait(dialoguesDuringImage);

        // 2번 대화 이후
        yield return PlayTimelinesForTiming(S2TimelinePlayTiming.AfterSecondDialogue);

        if (hasImage)
        {
            // 5. 이미지 Fade Out
            yield return FadeImage(cutsceneImage, 1f, 0f, imageFadeOutDuration);
            cutsceneImage.gameObject.SetActive(false);

            // 6. 검은 화면 걷힘 -> 원래 게임 화면 복귀
            if (fadeCanvasGroup != null)
            {
                yield return FadeCanvasGroup(fadeCanvasGroup, 1f, 0f, fadeDuration);
                fadeCanvasGroup.alpha = 0f;
                fadeCanvasGroup.blocksRaycasts = false;
                fadeCanvasGroup.gameObject.SetActive(false);
            }
        }
    }

    // ================= 카메라 흔들림 + 대화창 숨김 (Timeline Signal 등에서 직접 호출 가능) =================

    /// <summary>DialogueManager의 대화창을 즉시 숨김. 대화 진행 상태 자체는 유지됨.</summary>
    public void HideDialogueBox()
    {
        if (DialogueManager.Instance != null && DialogueManager.Instance.dialogueBox != null)
        {
            DialogueManager.Instance.dialogueBox.SetActive(false);
        }
    }

    /// <summary>DialogueManager의 대화창을 다시 표시함.</summary>
    public void ShowDialogueBox()
    {
        if (DialogueManager.Instance != null && DialogueManager.Instance.dialogueBox != null)
        {
            DialogueManager.Instance.dialogueBox.SetActive(true);
        }
    }

    /// <summary>카메라를 짧게 흔듦. 플레이어 위치/Rigidbody2D는 전혀 건드리지 않고 카메라 Transform만 움직임.</summary>
    public void ShakeCamera()
    {
        if (targetCamera == null) return;

        // 이미 흔들리는 중이면 먼저 원래 위치로 복원한 뒤 새로 시작 (중첩 흔들림으로 위치가 어긋나는 것 방지)
        if (shakeCoroutine != null)
        {
            StopCoroutine(shakeCoroutine);
            if (hasCameraOriginalPosition)
                targetCamera.transform.localPosition = cameraOriginalLocalPosition;
        }

        shakeCoroutine = StartCoroutine(ShakeCameraRoutine());
    }

    /// <summary>대화창을 숨기고 동시에 카메라를 흔듦.</summary>
    public void HideDialogueBoxAndShake()
    {
        HideDialogueBox();
        ShakeCamera();
    }

    private IEnumerator ShakeCameraRoutine()
    {
        if (!hasCameraOriginalPosition)
        {
            cameraOriginalLocalPosition = targetCamera.transform.localPosition;
            hasCameraOriginalPosition = true;
        }

        Vector3 basePos = cameraOriginalLocalPosition;
        float elapsed = 0f;

        while (elapsed < shakeDuration)
        {
            elapsed += Time.deltaTime;

            // 남은 시간 비율에 따라 강도를 점점 줄여 자연스럽게 잦아들도록 함
            float remainingRatio = 1f - Mathf.Clamp01(elapsed / shakeDuration);
            float currentStrength = shakeStrength * remainingRatio;

            Vector2 randomOffset = Random.insideUnitCircle * currentStrength;
            targetCamera.transform.localPosition = basePos + new Vector3(randomOffset.x, randomOffset.y, 0f);

            yield return null;
        }

        // 흔들림이 끝나면 정확하게 원래 위치로 복귀
        targetCamera.transform.localPosition = basePos;
        shakeCoroutine = null;
    }
    private void LockAllSystems()
    {
        LockPlayerMovement();

        if (bossChaseAttack != null) bossChaseAttack.SetAttackBehaviorLocked(true);
        if (bossChaseMove != null) bossChaseMove.SetMovementLocked(true);
        if (autoCameraMove != null) autoCameraMove.SetMovementLocked(true);
        if (bossChaseSceneTransition != null) bossChaseSceneTransition.SetTransitionLocked(true);
    }

    private void UnlockAllSystems()
    {
        UnlockPlayerMovement();

        if (bossChaseAttack != null) bossChaseAttack.SetAttackBehaviorLocked(false);
        if (bossChaseMove != null) bossChaseMove.SetMovementLocked(false);
        if (autoCameraMove != null) autoCameraMove.SetMovementLocked(false);
        if (bossChaseSceneTransition != null) bossChaseSceneTransition.SetTransitionLocked(false);
    }

    // ================= 플레이어 이동 잠금 =================
    private void LockPlayerMovement()
    {
        if (playerMovementLocked) return;

        GameObject player = GameObject.FindGameObjectWithTag("Player");
        if (player == null)
        {
            Debug.LogWarning("[SceneStartCutsceneController] Player 태그를 가진 오브젝트를 찾을 수 없습니다.");
            return;
        }

        playerRigidbody = player.GetComponent<Rigidbody2D>();
        if (playerRigidbody == null)
        {
            Debug.LogWarning("[SceneStartCutsceneController] Player에게 Rigidbody2D가 없습니다.");
        }

        if (playerMovementScript != null)
        {
            playerMovementScript.enabled = false;
        }

        // Rigidbody2D 자체는 절대 비활성화하지 않고, 속도만 0으로 고정
        if (playerRigidbody != null)
        {
            playerRigidbody.linearVelocity = Vector2.zero;
        }

        playerMovementLocked = true;
    }

    private void UnlockPlayerMovement()
    {
        if (!playerMovementLocked) return;

        if (playerRigidbody != null)
        {
            playerRigidbody.linearVelocity = Vector2.zero;
        }

        if (playerMovementScript != null)
        {
            playerMovementScript.enabled = true;
        }

        playerRigidbody = null;
        playerMovementLocked = false;
    }

    // ================= 페이드 유틸 =================
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