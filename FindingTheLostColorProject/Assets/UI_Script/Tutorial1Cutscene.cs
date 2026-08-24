using System.Collections;
using System.Collections.Generic; // List 사용을 위해 추가
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Events;
using UnityEngine.SceneManagement;

/// <summary>
/// 2-2. 튜토리얼 1 인트로 컷씬 컨트롤러
/// "자고 있는 치즈를 샴이 깨우고 함께 색채 구슬을 지키러 간다."
/// 
/// [연출 순서]
/// 1. 화면 페이드 인 (2초)
/// 2. 대사 1 출력 ("쿨쿨…")
/// 3. 대사 2 출력 ("음냐음냐…")
/// 4. 샴이 문을 열고 들어온다 (문 위치 생성 ➔ 2초 대기 ➔ 1초 고정 ➔ 3초 걸어옴 ➔ 치즈 앞 2초 정지)
/// 5. 대사 3 출력 ("...아직도 자?")
/// 6. 대사 4 출력 ("쿨쿨…")
/// 7. 대사 5 출력 ("일어나!")
/// 8. 대사 6 출력 ("으악!")
/// 9. 치즈가 침대에서 일어남 (부스럭 소리 ➔ 빈 침대 전환 ➔ 치즈 기본 애니메이션 재생 ➔ 2초 대기)
/// 10. 대사 7 출력 ("이른 아침부터 무슨 일이야?")
/// 11. 대사 8 출력 ("오늘 우리가 색채 구슬 지키는 날이잖아.")
/// 12. 치즈 머리 위에 '!' 출력 (2초)
/// 13. 대사 9 출력 ("아!")
/// 14. 대사 10 출력 ("그러네. 완전히 까먹고 있었어!")
/// 15. 대사 11 출력 ("하아... 그럴 줄 알았어,")
/// 16. 대사 12 출력 ("아무튼 다들 기다리고 있으니까 준비되면 나와.")
/// 17. 샴이 문을 닫고 나감 (2초 대기 ➔ 3초 문으로 이동 ➔ 문 앞 2초 정지 ➔ 샴 삭제 ➔ 2초 대기)
/// 18. 대사 13 출력 ("아니, 어떻게 이걸 까먹었지.")
/// 19. 대사 14 출력 ("붓이랑 망토까지 다 챙겼으니 이제 나가볼까?")
/// 20. 화면 페이드 아웃 (2초) ➔ 다음 씬 전환 또는 게임 시작
/// </summary>
public class Tutorial1Cutscene : MonoBehaviour
{
    [Header("1. 화면 페이드 설정")]
    [Tooltip("시작 시 화면 페이드 인 시간 (초 단위, 기본값: 2.0s)")]
    public float fadeInDuration = 2.0f;

    [Tooltip("종료 시 화면 페이드 아웃 시간 (초 단위, 기본값: 2.0s)")]
    public float fadeOutDuration = 2.0f;

    [Tooltip("페이드 아웃 완료 후 이동할 다음 씬 이름 (비워둘 시 씬 전환 없이 조작 해제)")]
    public string nextSceneName = "";

    [Header("2. 샴(Siam) 오브젝트 및 수동 프레임 설정 (애니메이터 사용 안 함)")]
    [Tooltip("샴 고양이 게임오브젝트")]
    public GameObject siamObject;

    [Tooltip("샴 SpriteRenderer (비워둘 시 siamObject에서 자동 검색)")]
    public SpriteRenderer siamSpriteRenderer;

    [Tooltip("샴이 멈춰있을 때 표시할 단일 스프라이트 이미지 (배열이 비었을 때 사용)")]
    public Sprite siamIdleSprite;

    [Tooltip("샴이 이동할 때 표시할 단일 스프라이트 이미지 (배열이 비었을 때 사용)")]
    public Sprite siamWalkSprite;

    [Tooltip("샴이 멈춰있을 때 재생할 스프라이트 프레임 배열 (인스펙터에 수동 할당)")]
    public Sprite[] siamIdleFrames;

    [Tooltip("샴이 이동할 때 재생할 스프라이트 프레임 배열 (인스펙터에 수동 할당)")]
    public Sprite[] siamWalkFrames;

    [Tooltip("샴 스프라이트 프레임 재생 속도 (FPS, 기본값: 8)")]
    public float siamAnimFPS = 8.0f;

    [Header("  [샴 이동 경로 위치]")]
    [Tooltip("샴이 들어오고 나가는 문 위치 Transform")]
    public Transform doorTransform;

    [Tooltip("샴이 치즈 앞에서 멈출 목표 위치 Transform")]
    public Transform siamStopTargetTransform;

    [Header("3. 치즈(Cheese) & 침대 기상 연출 설정")]
    [Tooltip("치즈 캐릭터 / 플레이어 게임오브젝트")]
    public GameObject cheesePlayerObj;

    [Tooltip("치즈 SpriteRenderer (비워둘 시 cheesePlayerObj에서 자동 검색)")]
    public SpriteRenderer cheeseSpriteRenderer;

    [Header("  [치즈 처음 수면(Sleeping) 상태 설정]")]
    [Tooltip("처음 씬 시작 시 치즈의 '수면' 상태 단일 스프라이트 (깨어나기 전까지 고정)")]
    public Sprite cheeseSleepingSprite;

    [Tooltip("처음 씬 시작 시 치즈의 수면 전용 게임오브젝트 (오브젝트 자체를 분리한 경우 사용, 깨어나면 꺼짐)")]
    public GameObject bedWithSleepingCheeseObj;

    [Header("  [치즈 깨어날 때 프레임 애니메이션 설정 (애니메이터 사용 안 함)]")]
    [Tooltip("치즈가 깨어났을 때 순환 재생할 기본 IDLE 스프라이트 프레임 배열")]
    public Sprite[] cheeseAwakeIdleFrames;

    [Tooltip("치즈 스프라이트 프레임 재생 속도 (FPS, 기본값: 8)")]
    public float cheeseAnimFPS = 8.0f;

    [Header("  [빈 침대 및 부스럭 소리 설정]")]
    [Tooltip("치즈가 일어난 후의 빈 침대 오브젝트 (기상 시 켜짐)")]
    public GameObject emptyBedObj;

    [Tooltip("침대 SpriteRenderer (오브젝트 대신 스프라이트 교체 방식을 쓸 경우)")]
    public SpriteRenderer bedSpriteRenderer;

    [Tooltip("치즈가 일어난 후 교체할 빈 침대 스프라이트 (선택사항)")]
    public Sprite emptyBedSprite;

    [Tooltip("치즈가 일어날 때 재생할 부스럭 효과음 (비워둘 시 Resources의 grass_rustling 자동 로드)")]
    public AudioClip rustleSoundClip;

    [Header("4. 치즈 '!' 느낌표 연출 설정")]
    [Tooltip("치즈 머리 위에 띄울 '!' 느낌표 오브젝트")]
    public GameObject exclamationMarkObj;

    [Header("5. 컷씬 대사 설정 (1단계: 잠꼬대)")]
    public DialogueLine[] dialogues1_2 = new DialogueLine[]
    {
        new DialogueLine { speakerName = "치즈", text = "쿨쿨…" },
        new DialogueLine { speakerName = "치즈", text = "음냐음냐…" }
    };

    [Header("6. 컷씬 대사 설정 (2단계: 샴의 기상 재촉)")]
    public DialogueLine[] dialogues3 = new DialogueLine[]
    {
        new DialogueLine { speakerName = "샴", text = "...아직도 자?" }
    };

    public DialogueLine[] dialogues4 = new DialogueLine[]
    {
        new DialogueLine { speakerName = "치즈", text = "쿨쿨…" }
    };

    public DialogueLine[] dialogues5 = new DialogueLine[]
    {
        new DialogueLine { speakerName = "샴", text = "일어나!" }
    };

    public DialogueLine[] dialogues6 = new DialogueLine[]
    {
        new DialogueLine { speakerName = "치즈", text = "으악!" }
    };

    [Header("7. 컷씬 대사 설정 (3단계: 기상 후 대화)")]
    public DialogueLine[] dialogues7_8 = new DialogueLine[]
    {
        new DialogueLine { speakerName = "치즈", text = "이른 아침부터 무슨 일이야?" },
        new DialogueLine { speakerName = "샴", text = "오늘 우리가 색채 구슬 지키는 날이잖아." }
    };

    public DialogueLine[] dialogues9_10 = new DialogueLine[]
    {
        new DialogueLine { speakerName = "치즈", text = "아!" },
        new DialogueLine { speakerName = "치즈", text = "그러네. 완전히 까먹고 있었어!" }
    };

    public DialogueLine[] dialogues11_12 = new DialogueLine[]
    {
        new DialogueLine { speakerName = "샴", text = "하아... 그럴 줄 알았어," },
        new DialogueLine { speakerName = "샴", text = "아무튼 다들 기다리고 있으니까 준비되면 나와." }
    };

    [Header("8. 컷씬 대사 설정 (4단계: 샴 퇴장 후 독백)")]
    public DialogueLine[] dialogues13_14 = new DialogueLine[]
    {
        new DialogueLine { speakerName = "치즈", text = "아니, 어떻게 이걸 까먹었지." },
        new DialogueLine { speakerName = "치즈", text = "붓이랑 망토까지 다 챙겼으니 이제 나가볼까?" }
    };

    [Header("9. 컷씬 종료 이벤트")]
    public UnityEvent onCutsceneEnded;

    private PlayerMove cachedPlayerMove;
    private CursorController cachedCursorController;
    private CameraFollow cachedCameraFollow;

    private Coroutine siamFrameAnimRoutine;
    private Coroutine cheeseFrameAnimRoutine;

    // --- [치즈(네로) 원본 상태 백업 변수] ---
    private Sprite originalCheeseSprite;
    private bool originalCheeseAnimatorState = false;
    private Animator internalCheeseAnimator; // 내부적으로만 기억해둘 용도
    // ----------------------------------------

    private void Awake()
    {
        if (ScreenFader.Instance != null && ScreenFader.Instance.fadeImage != null)
        {
            ScreenFader.Instance.fadeImage.gameObject.SetActive(true);
            ScreenFader.Instance.fadeImage.color = new Color(0f, 0f, 0f, 1f);
        }

        cachedPlayerMove = FindFirstObjectByType<PlayerMove>();
        if (cachedPlayerMove != null) cachedPlayerMove.SetControl(false);

        cachedCursorController = FindFirstObjectByType<CursorController>();
        if (cachedCursorController != null) cachedCursorController.enabled = false;

        cachedCameraFollow = FindFirstObjectByType<CameraFollow>();
        if (cachedCameraFollow != null) cachedCameraFollow.enabled = false;

        // 1. 샴 초기화 및 "애니메이터 강제 박멸"
        if (siamObject != null)
        {
            if (siamSpriteRenderer == null) siamSpriteRenderer = siamObject.GetComponentInChildren<SpriteRenderer>(true);

            // 프리팹에 붙어있을지도 모르는 Animator를 찾아 강제로 꺼버립니다. (방해 원천 차단)
            Animator siamAnim = siamObject.GetComponentInChildren<Animator>(true);
            if (siamAnim != null) siamAnim.enabled = false;

            siamObject.SetActive(false);
        }

        // 2. 치즈(플레이어) 초기화 및 원본 백업
        if (cheesePlayerObj != null)
        {
            if (cheeseSpriteRenderer == null) cheeseSpriteRenderer = cheesePlayerObj.GetComponentInChildren<SpriteRenderer>(true);
            internalCheeseAnimator = cheesePlayerObj.GetComponentInChildren<Animator>(true);

            // [원본 백업 로직] 치즈 플레이어 프리팹 원래 스프라이트와 애니메이터 활성 상태 복사
            if (cheeseSpriteRenderer != null) originalCheeseSprite = cheeseSpriteRenderer.sprite;
            if (internalCheeseAnimator != null) originalCheeseAnimatorState = internalCheeseAnimator.enabled;

            if (cheeseSleepingSprite != null && cheeseSpriteRenderer != null)
            {
                // 코루틴과 싸우지 않게 Animator 강제 종료
                if (internalCheeseAnimator != null) internalCheeseAnimator.enabled = false;
                cheesePlayerObj.SetActive(true);
                cheeseSpriteRenderer.sprite = cheeseSleepingSprite;
            }
            else if (bedWithSleepingCheeseObj != null)
            {
                cheesePlayerObj.SetActive(false);
            }
        }

        if (bedWithSleepingCheeseObj != null) bedWithSleepingCheeseObj.SetActive(true);
        if (emptyBedObj != null) emptyBedObj.SetActive(false);
        if (exclamationMarkObj != null) exclamationMarkObj.SetActive(false);

        if (rustleSoundClip == null)
        {
            rustleSoundClip = Resources.Load<AudioClip>("cheese_sound/grass_rustling");
        }
    }

    private void Start()
    {
        StartCoroutine(PlayTutorial1Sequence());
    }

    private IEnumerator PlayTutorial1Sequence()
    {
        yield return null;

        if (ScreenFader.Instance != null && fadeInDuration > 0f)
        {
            yield return StartCoroutine(ScreenFader.Instance.FadeInOnly(fadeInDuration));
        }
        else if (fadeInDuration > 0f)
        {
            yield return new WaitForSeconds(fadeInDuration);
        }

        yield return new WaitForSeconds(0.3f);

        yield return StartCoroutine(RunDialogueAndWait(dialogues1_2));

        yield return new WaitForSeconds(0.5f);

        // -------------------------------------------------------------
        // [4단계] 샴이 문을 열고 들어온다.
        // -------------------------------------------------------------
        Vector3 doorPos = doorTransform != null ? doorTransform.position : (siamObject != null ? siamObject.transform.position : Vector3.zero);

        Vector3 cheeseFrontPos;
        if (siamStopTargetTransform != null)
        {
            cheeseFrontPos = siamStopTargetTransform.position;
        }
        else if (cheesePlayerObj != null)
        {
            float dir = Mathf.Sign(cheesePlayerObj.transform.position.x - doorPos.x);
            cheeseFrontPos = doorPos + new Vector3(dir * 5f, 0f, 0f);
        }
        else
        {
            cheeseFrontPos = doorPos + new Vector3(-5f, 0f, 0f);
        }

        if (siamObject != null)
        {
            // SetActive 되기 전에 미리 전부 셋팅해둡니다.
            SetSpriteFlip(siamObject, cheeseFrontPos.x < doorPos.x);
            siamObject.transform.position = doorPos;
            SetSpriteAlpha(siamObject, 1f);

            siamObject.SetActive(true);
            PlaySiamIdle();
        }

        yield return new WaitForSeconds(2.0f);
        yield return new WaitForSeconds(1.0f);

        if (siamObject != null)
        {
            PlaySiamWalk();

            float walkTime = 3.0f;
            float elapsed = 0f;
            while (elapsed < walkTime)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / walkTime);
                siamObject.transform.position = Vector3.Lerp(doorPos, cheeseFrontPos, t);
                yield return null;
            }
            siamObject.transform.position = cheeseFrontPos;
            PlaySiamIdle();
        }
        else
        {
            yield return new WaitForSeconds(3.0f);
        }

        yield return new WaitForSeconds(2.0f);

        yield return StartCoroutine(RunDialogueAndWait(dialogues3));
        yield return StartCoroutine(RunDialogueAndWait(dialogues4));
        yield return StartCoroutine(RunDialogueAndWait(dialogues5));
        yield return StartCoroutine(RunDialogueAndWait(dialogues6));

        yield return new WaitForSeconds(0.2f);

        // -------------------------------------------------------------
        // [9단계] 치즈가 침대에서 일어남 (기상 및 원본 복구 타이밍)
        // -------------------------------------------------------------
        PlayRustleSound();

        if (bedWithSleepingCheeseObj != null) bedWithSleepingCheeseObj.SetActive(false);
        if (emptyBedObj != null) emptyBedObj.SetActive(true);
        if (bedSpriteRenderer != null && emptyBedSprite != null) bedSpriteRenderer.sprite = emptyBedSprite;

        if (cheesePlayerObj != null)
        {
            // [네로 원상 복구] 일어나는 순간에 아까 저장해둔 원래 스프라이트와 원래 Animator 상태를 복구시킵니다.
            if (cheeseSpriteRenderer != null && originalCheeseSprite != null)
            {
                cheeseSpriteRenderer.sprite = originalCheeseSprite;
            }
            if (internalCheeseAnimator != null)
            {
                internalCheeseAnimator.enabled = originalCheeseAnimatorState;
            }

            cheesePlayerObj.SetActive(true);
            SetSpriteAlpha(cheesePlayerObj, 1f);

            // 기상 시 배열을 수동으로 넣어뒀을 때만 프레임 코루틴 재생
            PlayCheeseAwakeIdle();
        }

        yield return new WaitForSeconds(2.0f);

        yield return StartCoroutine(RunDialogueAndWait(dialogues7_8));
        yield return new WaitForSeconds(0.3f);

        if (exclamationMarkObj != null)
        {
            exclamationMarkObj.SetActive(true);
            yield return new WaitForSeconds(2.0f);
            exclamationMarkObj.SetActive(false);
        }
        else
        {
            yield return new WaitForSeconds(2.0f);
        }

        yield return StartCoroutine(RunDialogueAndWait(dialogues9_10));
        yield return StartCoroutine(RunDialogueAndWait(dialogues11_12));

        yield return new WaitForSeconds(0.3f);
        yield return new WaitForSeconds(2.0f);

        // -------------------------------------------------------------
        // [17단계] 샴이 문을 닫고 나간다.
        // -------------------------------------------------------------
        if (siamObject != null)
        {
            SetSpriteFlip(siamObject, doorPos.x < cheeseFrontPos.x);
            PlaySiamWalk();

            float walkTime = 3.0f;
            float elapsed = 0f;
            while (elapsed < walkTime)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / walkTime);
                siamObject.transform.position = Vector3.Lerp(cheeseFrontPos, doorPos, t);
                yield return null;
            }
            siamObject.transform.position = doorPos;
            PlaySiamIdle();
        }
        else
        {
            yield return new WaitForSeconds(3.0f);
        }

        yield return new WaitForSeconds(2.0f);

        if (siamObject != null)
        {
            siamObject.SetActive(false);
        }

        yield return new WaitForSeconds(2.0f);

        yield return StartCoroutine(RunDialogueAndWait(dialogues13_14));
        yield return new WaitForSeconds(0.5f);

        if (!string.IsNullOrEmpty(nextSceneName))
        {
            if (ScreenFader.Instance != null)
            {
                ScreenFader.Instance.FadeToScene(nextSceneName, fadeOutDuration);
            }
            else
            {
                SceneManager.LoadScene(nextSceneName);
            }
        }
        else
        {
            if (ScreenFader.Instance != null && fadeOutDuration > 0f)
            {
                yield return StartCoroutine(ScreenFader.Instance.FadeOutOnly(fadeOutDuration));
            }

            if (cachedCameraFollow != null) cachedCameraFollow.enabled = true;
            if (cachedPlayerMove != null) cachedPlayerMove.SetControl(true);
            if (cachedCursorController != null) cachedCursorController.enabled = true;

            if (onCutsceneEnded != null) onCutsceneEnded.Invoke();
        }

        Debug.Log("[Tutorial1Cutscene] 튜토리얼 1 컷씬 연출 완료!");
    }

    /// <summary>
    /// 오직 인스펙터 배열만 사용하는 샴 정지 재생
    /// </summary>
    public void PlaySiamIdle()
    {
        if (siamFrameAnimRoutine != null) StopCoroutine(siamFrameAnimRoutine);

        if (siamIdleFrames != null && siamIdleFrames.Length > 0 && siamSpriteRenderer != null)
        {
            siamFrameAnimRoutine = StartCoroutine(LoopSpriteFrames(siamSpriteRenderer, siamIdleFrames, siamAnimFPS));
        }
        else if (siamIdleSprite != null && siamSpriteRenderer != null)
        {
            siamSpriteRenderer.sprite = siamIdleSprite;
        }
    }

    /// <summary>
    /// 오직 인스펙터 배열만 사용하는 샴 걷기 재생
    /// </summary>
    public void PlaySiamWalk()
    {
        if (siamFrameAnimRoutine != null) StopCoroutine(siamFrameAnimRoutine);

        if (siamWalkFrames != null && siamWalkFrames.Length > 0 && siamSpriteRenderer != null)
        {
            siamFrameAnimRoutine = StartCoroutine(LoopSpriteFrames(siamSpriteRenderer, siamWalkFrames, siamAnimFPS));
        }
        else if (siamWalkSprite != null && siamSpriteRenderer != null)
        {
            siamSpriteRenderer.sprite = siamWalkSprite;
        }
    }

    /// <summary>
    /// 치즈(네로) 기상 시 수동 배열 애니메이션
    /// </summary>
    public void PlayCheeseAwakeIdle()
    {
        if (cheeseFrameAnimRoutine != null) StopCoroutine(cheeseFrameAnimRoutine);

        if (cheeseAwakeIdleFrames != null && cheeseAwakeIdleFrames.Length > 0 && cheeseSpriteRenderer != null)
        {
            // 치즈의 경우, 수동 애니메이션이 들어있으면 켜져있던 애니메이터를 끄고 직접 돌립니다.
            if (internalCheeseAnimator != null) internalCheeseAnimator.enabled = false;
            cheeseFrameAnimRoutine = StartCoroutine(LoopSpriteFrames(cheeseSpriteRenderer, cheeseAwakeIdleFrames, cheeseAnimFPS));
        }
    }

    /// <summary>
    /// 애니메이터 대신 수동 프레임을 순환 재생 (빈칸 예외 처리 완벽 포함)
    /// </summary>
    private IEnumerator LoopSpriteFrames(SpriteRenderer sr, Sprite[] frames, float fps)
    {
        if (sr == null || frames == null || frames.Length == 0) yield break;

        // [핵심] 배열 안에 빈 칸(null)이 섞여 있어 깜빡임이 생기는 것을 완벽히 방지합니다.
        List<Sprite> validFrames = new List<Sprite>();
        foreach (var frame in frames)
        {
            if (frame != null) validFrames.Add(frame);
        }

        // 유효한 이미지가 1장도 없으면 취소
        if (validFrames.Count == 0) yield break;

        float interval = 1f / Mathf.Max(1f, fps);
        int index = 0;

        while (true)
        {
            sr.sprite = validFrames[index];
            index = (index + 1) % validFrames.Count;
            yield return new WaitForSeconds(interval);
        }
    }

    private IEnumerator RunDialogueAndWait(DialogueLine[] lines)
    {
        if (lines != null && lines.Length > 0 && DialogueManager.Instance != null)
        {
            bool isDone = false;
            DialogueManager.Instance.StartDialogue(lines, () => isDone = true);
            yield return new WaitUntil(() => isDone);
        }
        else
        {
            yield return new WaitForSeconds(1.0f);
        }
    }

    private void SetSpriteFlip(GameObject targetObj, bool flipX)
    {
        if (targetObj == null) return;
        SpriteRenderer[] srs = targetObj.GetComponentsInChildren<SpriteRenderer>(true);
        foreach (var sr in srs)
        {
            if (sr != null) sr.flipX = flipX;
        }
    }

    private void SetSpriteAlpha(GameObject targetObj, float alpha)
    {
        if (targetObj == null) return;
        SpriteRenderer[] srs = targetObj.GetComponentsInChildren<SpriteRenderer>(true);
        foreach (var sr in srs)
        {
            if (sr != null)
            {
                Color c = sr.color;
                c.a = alpha;
                sr.color = c;
            }
        }
    }

    private void PlayRustleSound()
    {
        if (rustleSoundClip != null)
        {
            AudioSource.PlayClipAtPoint(rustleSoundClip, Camera.main != null ? Camera.main.transform.position : Vector3.zero, 1.0f);
        }
    }
}