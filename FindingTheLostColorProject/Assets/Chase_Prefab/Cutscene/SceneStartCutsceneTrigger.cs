using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Playables;
using System.Collections;

/// <summary>
/// Timeline 배열의 각 항목이 어느 시점에 재생될지를 지정.
/// </summary>
public enum TimelinePlayTiming
{
    CutsceneStart,
    AfterFirstDialogue,
    BeforeImage,
    AfterImage,
    BeforeSecondDialogue,
    AfterSecondDialogue,
    BeforeThirdDialogue,
    AfterThirdDialogue,
    BeforeFourthDialogue,
    AfterFourthDialogue,
    BeforeFifthDialogue,
    CutsceneEnd
}

[System.Serializable]
public class CutsceneTimelineEvent
{
    [Tooltip("이 시점에 재생할 Timeline (PlayableDirector). 비워두면 이 항목은 무시됨")]
    public PlayableDirector timeline;

    [Tooltip("이 Timeline이 재생될 시점")]
    public TimelinePlayTiming timing;
}

/// <summary>
/// 대화창을 자동으로 숨겼다가 다시 보여줄 시점을 지정.
/// (대화 "도중" 정확한 순간은 자동으로 특정할 수 없으므로, During* 타이밍은
///  자동 실행되지 않으며 Timeline Signal이나 다른 스크립트에서 HideDialogueBox()/
///  ShowDialogueBox()/ShakeCamera()를 직접 호출하는 용도로만 사용하는 것을 권장함)
/// </summary>
public enum DialogueBoxTiming
{
    BeforeCutscene,
    DuringFirstDialogue,
    AfterFirstDialogue,
    DuringSecondDialogue,
    AfterSecondDialogue,
    DuringThirdDialogue
}

[System.Serializable]
public class DialogueBoxEvent
{
    [Tooltip("이 시점에 대화창을 숨겼다가 다시 보여줌. During* 타이밍은 자동 실행되지 않고, " +
             "Timeline Signal 등에서 HideDialogueBoxAndShake()/ShowDialogueBox()를 직접 호출하는 용도로 남겨둠")]
    public DialogueBoxTiming timing;

    [Tooltip("대화창이 숨겨진 상태로 유지되는 시간(초). 이 시간이 지나면 자동으로 다시 표시됨")]
    public float duration = 1f;
}

/// <summary>
/// 씬 시작 시 자동 실행되는 컷씬 트리거.
/// 기존 순서(1부 대화 -> 암전 -> 이미지 -> 2부 대화 -> 이미지 종료 -> 복귀 -> 3부 대화 -> 종료)를 유지하면서,
/// Timeline 연출과 "대화창 숨김 + 카메라 흔들림" 연출을 Inspector에서 지정한 시점에 끼워넣을 수 있다.
/// DialogueManager.cs는 전혀 수정하지 않는다.
/// </summary>
public class SceneStartCutsceneTrigger : MonoBehaviour
{
    [Header("대화 내용 (1부: 컷씬 이미지가 나오기 전)")]
    public DialogueLine[] dialogues;

    [Header("컷씬 연출")]
    [Tooltip("화면 전체를 덮는 검은 이미지가 붙어있는 CanvasGroup (알파 0에서 시작)")]
    public CanvasGroup fadeCanvasGroup;
    [Tooltip("검은 상태에서 나타날 컷씬 이미지")]
    public Image cutsceneImage;
    [Tooltip("화면이 검게 덮이거나, 이미지가 나타나고 사라지는 데 걸리는 시간(초)")]
    public float fadeDuration = 1f;

    [Header("대화 내용 (2부: 컷씬 이미지가 나온 뒤)")]
    public DialogueLine[] dialoguesAfterImage;

    [Header("대화 내용 (3부: 원래 화면으로 복귀한 뒤)")]
    public DialogueLine[] dialoguesAfterImageEnded;

    [Header("대화 내용 (4부: 3부 대화 이후)")]
    public DialogueLine[] dialoguesPart4;

    [Header("대화 내용 (5부: 4부 대화 이후)")]
    public DialogueLine[] dialoguesPart5;

    [Header("Timeline 연출")]
    [Tooltip("컷씬 진행 중 특정 시점에 재생할 Timeline 목록. 여러 개를 등록할 수 있으며, " +
             "같은 시점을 가진 항목이 여러 개면 배열 순서대로 순차 재생됨")]
    public CutsceneTimelineEvent[] timelineEvents;

    [Header("대화창 연출")]
    [Tooltip("DialogueManager가 사용하는 대화창 UI 오브젝트 (DialogueManager.dialogueBox와 동일한 오브젝트를 지정)")]
    public GameObject dialogueBox;

    [Tooltip("특정 시점에 대화창을 자동으로 숨겼다가 다시 표시할 이벤트 목록. " +
             "During* 타이밍은 자동 실행되지 않으므로, 그런 연출은 Timeline Signal에서 " +
             "HideDialogueBoxAndShake()/ShowDialogueBox()를 직접 호출해서 구현할 것")]
    public DialogueBoxEvent[] dialogueBoxEvents;

    [Header("카메라 흔들림")]
    [Tooltip("흔들릴 카메라의 Transform")]
    public Transform cameraTransform;
    [Tooltip("흔들림 지속 시간(초)")]
    public float shakeDuration = 0.3f;
    [Tooltip("흔들림 강도 (최대 이동 거리)")]
    public float shakeStrength = 0.2f;


    [Tooltip("한 번 실행된 뒤 다시 트리거되지 않게 할지 여부")]
    public bool playOnlyOnce = true;
    private bool hasTriggered = false;


    [Tooltip("씬 시작 대화가 끝날 때까지 보스 행동을 잠그기 위해 연결. 비워두면 씬에서 자동으로 찾음")]
    public BossAttack bossAttack;
    public BossMove bossMove;
    public BossPortalSpawner bossPortalSpawner;

    // 플레이어 이동 잠금 (Rigidbody2D 자체는 비활성화하지 않고, 이동 스크립트만 끄고 속도를 0으로 유지)
    private Rigidbody2D playerRigidbody;
    private MonoBehaviour playerMovementScript;
    private bool playerMovementLocked = false;

    private Coroutine shakeCoroutine;
    private Vector3 cameraOriginalLocalPosition;
    private bool hasCameraOriginalPosition = false;

    private void Awake()
    {
        if (bossAttack == null) bossAttack = FindFirstObjectByType<BossAttack>();
        if (bossMove == null) bossMove = FindFirstObjectByType<BossMove>();
        if (bossPortalSpawner == null) bossPortalSpawner = FindFirstObjectByType<BossPortalSpawner>();

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

        if (cameraTransform != null)
        {
            cameraOriginalLocalPosition = cameraTransform.localPosition;
            hasCameraOriginalPosition = true;
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
        LockPlayerMovement();

        // 씬이 시작되는 순간, 화면이 검게 덮인 상태에서 2초 동안 서서히 밝아지며 시작함
        yield return PlaySceneStartFadeIn();

        yield return PlayTimelinesForTiming(TimelinePlayTiming.CutsceneStart);

        // 1부 대화
        yield return PlayDialogueAndWait(dialogues);

        yield return PlayTimelinesForTiming(TimelinePlayTiming.AfterFirstDialogue);
        yield return PlayDialogueBoxEventsForTiming(DialogueBoxTiming.AfterFirstDialogue);

        yield return PlayTimelinesForTiming(TimelinePlayTiming.BeforeImage);

        // 암전
        if (fadeCanvasGroup != null)
        {
            fadeCanvasGroup.gameObject.SetActive(true);
            fadeCanvasGroup.blocksRaycasts = true;
            yield return FadeCanvasGroup(fadeCanvasGroup, 0f, 1f, fadeDuration);
        }

        // 컷씬 이미지 등장
        if (cutsceneImage != null)
        {
            cutsceneImage.gameObject.SetActive(true);
            SetImageAlpha(cutsceneImage, 0f);
            yield return FadeImage(cutsceneImage, 0f, 1f, fadeDuration);
        }

        yield return PlayTimelinesForTiming(TimelinePlayTiming.AfterImage);
        yield return PlayTimelinesForTiming(TimelinePlayTiming.BeforeSecondDialogue);

        // 2부 대화
        yield return PlayDialogueAndWait(dialoguesAfterImage);

        yield return PlayTimelinesForTiming(TimelinePlayTiming.AfterSecondDialogue);
        yield return PlayDialogueBoxEventsForTiming(DialogueBoxTiming.AfterSecondDialogue);

        // 컷씬 이미지 사라짐
        if (cutsceneImage != null)
        {
            yield return FadeImage(cutsceneImage, 1f, 0f, fadeDuration);
            cutsceneImage.gameObject.SetActive(false);
        }

        // 암전 해제 -> 원래 화면 복귀
        if (fadeCanvasGroup != null)
        {
            yield return FadeCanvasGroup(fadeCanvasGroup, 1f, 0f, fadeDuration);
            fadeCanvasGroup.alpha = 0f;
            fadeCanvasGroup.blocksRaycasts = false;
            fadeCanvasGroup.gameObject.SetActive(false);
        }

        yield return PlayTimelinesForTiming(TimelinePlayTiming.BeforeThirdDialogue);

        // 3부 대화
        yield return PlayDialogueAndWait(dialoguesAfterImageEnded);

        yield return PlayTimelinesForTiming(TimelinePlayTiming.AfterThirdDialogue);
        yield return PlayTimelinesForTiming(TimelinePlayTiming.BeforeFourthDialogue);

        // 4부 대화
        yield return PlayDialogueAndWait(dialoguesPart4);

        yield return PlayTimelinesForTiming(TimelinePlayTiming.AfterFourthDialogue);
        yield return PlayTimelinesForTiming(TimelinePlayTiming.BeforeFifthDialogue);

        // 5부 대화
        yield return PlayDialogueAndWait(dialoguesPart5);

        yield return PlayTimelinesForTiming(TimelinePlayTiming.CutsceneEnd);

        UnlockPlayerMovement();
        UnlockBossBehavior(); // 씬 시작 대화(마지막 줄)가 완전히 끝난 시점에 보스 행동 잠금 해제
    }

    // 대화 onComplete 시점에 보스의 이동/공격/소환 잠금을 한 번에 해제
    private void UnlockBossBehavior()
    {
        if (bossAttack != null) bossAttack.SetBossBehaviorLocked(false);
        if (bossMove != null) bossMove.SetMovementLocked(false);
        if (bossPortalSpawner != null) bossPortalSpawner.SetSpawnLocked(false);
    }
    [Header("씬 시작 페이드 인")]
    [Tooltip("씬이 시작될 때 화면이 검게 덮인 상태에서 서서히 밝아지는 데 걸리는 시간(초)")]
    public float sceneStartFadeInDuration = 2f;

    // 씬이 처음 시작될 때, 화면을 검게 덮은 상태로 시작해서 sceneStartFadeInDuration 동안 서서히 밝아지게 함.
    // fadeCanvasGroup을 그대로 재사용하므로 별도의 UI를 새로 만들 필요가 없음.
    private IEnumerator PlaySceneStartFadeIn()
    {
        if (fadeCanvasGroup == null) yield break;

        fadeCanvasGroup.gameObject.SetActive(true);
        fadeCanvasGroup.blocksRaycasts = true;
        fadeCanvasGroup.alpha = 1f; // 완전히 검은 상태에서 시작

        yield return FadeCanvasGroup(fadeCanvasGroup, 1f, 0f, sceneStartFadeInDuration);

        fadeCanvasGroup.alpha = 0f;
        fadeCanvasGroup.blocksRaycasts = false;
        fadeCanvasGroup.gameObject.SetActive(false);
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
    private IEnumerator PlayTimelinesForTiming(TimelinePlayTiming targetTiming)
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

    // ================= 대화창 자동 숨김/표시 이벤트 =================
    private IEnumerator PlayDialogueBoxEventsForTiming(DialogueBoxTiming targetTiming)
    {
        if (dialogueBoxEvents == null) yield break;

        foreach (var evt in dialogueBoxEvents)
        {
            if (evt == null) continue;
            if (evt.timing != targetTiming) continue;

            HideDialogueBoxAndShake();
            yield return new WaitForSeconds(Mathf.Max(0f, evt.duration));
            ShowDialogueBox();
        }
    }

    // ================= Timeline Signal에서 호출 가능한 public 함수들 =================

    /// <summary>대화창을 즉시 숨김. Timeline Signal Receiver에서 직접 연결 가능.</summary>
    public void HideDialogueBox()
    {
        if (dialogueBox != null)
            dialogueBox.SetActive(false);
    }

    /// <summary>대화창을 즉시 다시 표시함. Timeline Signal Receiver에서 직접 연결 가능.</summary>
    public void ShowDialogueBox()
    {
        if (dialogueBox != null)
            dialogueBox.SetActive(true);
    }

    /// <summary>카메라를 짧게 흔듦. Timeline Signal Receiver에서 직접 연결 가능.</summary>
    public void ShakeCamera()
    {
        if (cameraTransform == null) return;

        // 이미 흔들리는 중이면 먼저 원래 위치로 복원한 뒤 새로 시작 (중첩 흔들림으로 위치가 어긋나는 것 방지)
        if (shakeCoroutine != null)
        {
            StopCoroutine(shakeCoroutine);
            if (hasCameraOriginalPosition)
                cameraTransform.localPosition = cameraOriginalLocalPosition;
        }

        shakeCoroutine = StartCoroutine(ShakeCameraRoutine());
    }

    /// <summary>대화창을 숨기고 동시에 카메라를 흔듦. Timeline Signal Receiver에서 직접 연결 가능.</summary>
    public void HideDialogueBoxAndShake()
    {
        HideDialogueBox();
        ShakeCamera();
    }

    private IEnumerator ShakeCameraRoutine()
    {
        if (!hasCameraOriginalPosition)
        {
            cameraOriginalLocalPosition = cameraTransform.localPosition;
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
            cameraTransform.localPosition = basePos + new Vector3(randomOffset.x, randomOffset.y, 0f);

            yield return null;
        }

        // 흔들림이 끝나면 정확하게 원래 위치로 복귀
        cameraTransform.localPosition = basePos;
        shakeCoroutine = null;
    }

    // ================= 플레이어 이동 잠금 =================
    private void LockPlayerMovement()
    {
        if (playerMovementLocked) return;

        GameObject player = GameObject.FindGameObjectWithTag("Player");
        if (player == null)
        {
            Debug.LogWarning("[SceneStartCutsceneTrigger] Player 태그를 가진 오브젝트를 찾을 수 없습니다.");
            return;
        }

        playerRigidbody = player.GetComponent<Rigidbody2D>();
        if (playerRigidbody == null)
        {
            Debug.LogWarning("[SceneStartCutsceneTrigger] Player에게 Rigidbody2D가 없습니다.");
            return;
        }

        playerMovementScript = player.GetComponent<PlayerMove>();
        if (playerMovementScript != null)
        {
            playerMovementScript.enabled = false;
        }

        // Rigidbody2D 자체는 끄지 않고 속도만 0으로 고정
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

        if (playerMovementScript != null)
        {
            playerMovementScript.enabled = true;
        }

        playerRigidbody = null;
        playerMovementScript = null;
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