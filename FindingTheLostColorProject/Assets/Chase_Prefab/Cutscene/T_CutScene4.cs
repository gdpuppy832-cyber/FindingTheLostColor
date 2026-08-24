using UnityEngine;
using UnityEngine.UI;
using System.Collections;

/// <summary>
/// 이 스크립트가 붙어있는 몬스터(NormalMonster)가 정화되는 순간 자동으로 시작되는 컷씬 트리거.
/// 컷씬 시작 = 화면이 즉시 검게 덮인 상태 -> 2초 동안 페이드 인 -> 대사 1개 그룹 출력 -> 종료.
/// Timeline은 사용하지 않는다.
///
/// NormalMonster.cs는 전혀 수정하지 않고, IsPurified 값을 매 프레임 폴링해서 정화 시점을 감지한다.
/// DialogueManager.cs, DialogueLine도 수정하지 않는다.
/// </summary>
public class T_CutScene4 : MonoBehaviour
{
    [Header("감시할 몬스터")]
    [Tooltip("이 몬스터가 정화되면 컷씬이 시작됨. Inspector에서 직접 연결")]
    public NormalMonster targetMonster;

    [Header("대화 내용")]
    [Tooltip("컷씬 시작 시 출력할 대사 목록")]
    public DialogueLine[] dialogues;

    [Header("페이드")]
    [Tooltip("화면 전체를 덮는 검은 이미지가 붙어있는 CanvasGroup. 컷씬 시작 시 즉시 알파 1(완전히 검음)로 세팅됨")]
    public CanvasGroup fadeCanvasGroup;
    [Tooltip("검은 화면에서 서서히 밝아지는 데 걸리는 시간(초)")]
    public float fadeInDuration = 2f;

    [Header("씬 전환")]
    [Tooltip("컷씬(대사)이 끝난 뒤 이동할 씬 이름. 비워두면 씬 전환을 하지 않음")]
    public string nextSceneName;
    [Tooltip("씬 전환 시 페이드 아웃/인에 걸리는 시간(초)")]
    public float sceneTransitionFadeDuration = 1f;

    [Header("설정")]
    [Tooltip("한 번 실행된 뒤 다시 트리거되지 않게 할지 여부")]
    public bool triggerOnlyOnce = true;
    private bool hasTriggered = false;

    // 플레이어 이동 잠금
    private Rigidbody2D playerRigidbody;
    private MonoBehaviour playerMovementScript;
    private RigidbodyConstraints2D originalPlayerConstraints;
    private bool playerMovementLocked = false;

    private void Awake()
    {
        if (targetMonster == null)
        {
            Debug.LogWarning("[T_CutScene4] Target Monster가 Inspector에 연결되지 않았습니다.");
        }

        // 컷씬 시작 전까지는 화면에 영향이 없도록 꺼둠
        if (fadeCanvasGroup != null)
        {
            fadeCanvasGroup.alpha = 0f;
            fadeCanvasGroup.blocksRaycasts = false;
            fadeCanvasGroup.gameObject.SetActive(false);
        }
    }

    private void Update()
    {
        if (triggerOnlyOnce && hasTriggered) return;
        if (targetMonster == null) return;

        // NormalMonster.cs를 수정하지 않고, 정화 여부를 매 프레임 직접 확인해서 감지함
        if (targetMonster.IsPurified)
        {
            hasTriggered = true;
            StartCoroutine(RunCutscene());
        }
    }

    private IEnumerator RunCutscene()
    {
        LockPlayerMovement();

        // 1. 화면을 즉시 검게 덮음
        if (fadeCanvasGroup != null)
        {
            fadeCanvasGroup.gameObject.SetActive(true);
            fadeCanvasGroup.blocksRaycasts = true;
            fadeCanvasGroup.alpha = 1f;

            // 2. 2초 동안 서서히 밝아짐
            yield return FadeCanvasGroup(fadeCanvasGroup, 1f, 0f, fadeInDuration);

            fadeCanvasGroup.alpha = 0f;
            fadeCanvasGroup.blocksRaycasts = false;
            fadeCanvasGroup.gameObject.SetActive(false);
        }

        // 3. 대사 출력
        yield return PlayDialogueAndWait(dialogues);

        UnlockPlayerMovement();

        // 4. 다음 씬으로 전환
        GoToNextScene();
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

    // ================= 씬 전환 =================
    private void GoToNextScene()
    {
        if (string.IsNullOrEmpty(nextSceneName)) return;

        if (ScreenFader.Instance != null)
        {
            // 프로젝트에 이미 있는 페이드/씬전환 시스템을 그대로 사용
            ScreenFader.Instance.FadeToScene(nextSceneName, sceneTransitionFadeDuration);
        }
        else
        {
            Debug.LogWarning("[T_CutScene4] ScreenFader.Instance가 없습니다. " +
                "씬에 ScreenFader가 배치되어 있는지 확인해주세요.");
        }
    }

    // ================= 플레이어 이동 잠금 =================
    private void LockPlayerMovement()
    {
        if (playerMovementLocked) return;

        GameObject player = GameObject.FindGameObjectWithTag("Player");
        if (player == null)
        {
            Debug.LogWarning("[MonsterPurifiedCutsceneTrigger] Player 태그를 가진 오브젝트를 찾을 수 없습니다.");
            return;
        }

        playerRigidbody = player.GetComponent<Rigidbody2D>();
        if (playerRigidbody == null)
        {
            Debug.LogWarning("[MonsterPurifiedCutsceneTrigger] Player에게 Rigidbody2D가 없습니다.");
            return;
        }

        playerMovementScript = player.GetComponent<PlayerMove>();
        if (playerMovementScript != null)
        {
            playerMovementScript.enabled = false;
        }

        playerRigidbody.linearVelocity = Vector2.zero;

        // 위치(X, Y) 자체를 물리적으로 고정 -> 컷씬 동안 중력/외력이 있어도 절대 움직이지 않음
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
}