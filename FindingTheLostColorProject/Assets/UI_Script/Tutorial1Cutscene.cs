using System.Collections;
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

    [Header("2. 샴(Siam) 오브젝트 및 애니메이션 설정")]
    [Tooltip("샴 고양이 게임오브젝트")]
    public GameObject siamObject;

    [Tooltip("샴 SpriteRenderer (비워둘 시 siamObject에서 자동 검색)")]
    public SpriteRenderer siamSpriteRenderer;

    [Tooltip("샴 Animator 컴포넌트 (비워둘 시 siamObject에서 자동 검색)")]
    public Animator siamAnimator;

    [Header("  [샴 애니메이션 - 애니메이터 상태/불리언 설정]")]
    [Tooltip("샴이 멈춰있을 때 재생할 Animator State 이름 (기본값: Idle)")]
    public string siamIdleStateName = "Idle";

    [Tooltip("샴이 이동할 때 재생할 Animator State 이름 (기본값: Walk)")]
    public string siamWalkStateName = "Walk";

    [Tooltip("샴 이동 여부를 제어할 Animator Bool 파라미터 이름 (선택사항, 기본: isWalking)")]
    public string siamWalkingBoolParam = "isWalking";

    [Header("  [샴 애니메이션 - 인스펙터 스프라이트 직접 연결 (애니메이터 미사용 시)]")]
    [Tooltip("샴이 멈춰있을 때 표시할 단일 스프라이트 이미지")]
    public Sprite siamIdleSprite;

    [Tooltip("샴이 이동할 때 표시할 단일 스프라이트 이미지")]
    public Sprite siamWalkSprite;

    [Tooltip("샴이 멈춰있을 때 재생할 스프라이트 프레임 배열 (프레임 애니메이션용)")]
    public Sprite[] siamIdleFrames;

    [Tooltip("샴이 이동할 때 재생할 스프라이트 프레임 배열 (프레임 애니메이션용)")]
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

    [Tooltip("치즈 Animator 컴포넌트 (비워둘 시 cheesePlayerObj에서 자동 검색)")]
    public Animator cheeseAnimator;

    [Header("  [치즈 처음 수면(Sleeping) 상태 설정]")]
    [Tooltip("처음 씬 시작 시 치즈의 '수면' 상태 단일 스프라이트 (깨어나기 전까지 고정)")]
    public Sprite cheeseSleepingSprite;

    [Tooltip("처음 씬 시작 시 치즈의 수면 전용 게임오브젝트 (오브젝트 자체를 분리한 경우 사용, 깨어나면 꺼짐)")]
    public GameObject bedWithSleepingCheeseObj;

    [Header("  [치즈 깨어날 때 기본 애니메이션 설정]")]
    [Tooltip("치즈가 깨어났을 때 재생할 Animator State 이름 (기본값: Idle)")]
    public string cheeseAwakeStateName = "Idle";

    [Tooltip("치즈가 깨어났을 때 순환 재생할 기본 IDLE 스프라이트 프레임 배열 (애니메이터 미사용 시)")]
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

    private void Awake()
    {
        // 1. 화면 암전 초기화
        if (ScreenFader.Instance != null && ScreenFader.Instance.fadeImage != null)
        {
            ScreenFader.Instance.fadeImage.gameObject.SetActive(true);
            ScreenFader.Instance.fadeImage.color = new Color(0f, 0f, 0f, 1f);
        }

        // 2. 플레이어 조작 및 카메라 잠금
        cachedPlayerMove = FindFirstObjectByType<PlayerMove>();
        if (cachedPlayerMove != null) cachedPlayerMove.SetControl(false);

        cachedCursorController = FindFirstObjectByType<CursorController>();
        if (cachedCursorController != null) cachedCursorController.enabled = false;

        cachedCameraFollow = FindFirstObjectByType<CameraFollow>();
        if (cachedCameraFollow != null) cachedCameraFollow.enabled = false;

        // 3. 샴 컴포넌트 자동 캐싱
        if (siamObject != null)
        {
            if (siamSpriteRenderer == null) siamSpriteRenderer = siamObject.GetComponentInChildren<SpriteRenderer>(true);
            if (siamAnimator == null) siamAnimator = siamObject.GetComponentInChildren<Animator>(true);
            siamObject.SetActive(false);
        }

        // 4. 치즈 컴포넌트 자동 캐싱 및 초기 수면 상태 세팅
        if (cheesePlayerObj != null)
        {
            if (cheeseSpriteRenderer == null) cheeseSpriteRenderer = cheesePlayerObj.GetComponentInChildren<SpriteRenderer>(true);
            if (cheeseAnimator == null) cheeseAnimator = cheesePlayerObj.GetComponentInChildren<Animator>(true);

            // 처음엔 "수면" 상태 이미지 적용 (깨어나기 전까지 애니메이터 일시 정지)
            if (cheeseSleepingSprite != null && cheeseSpriteRenderer != null)
            {
                cheeseSpriteRenderer.sprite = cheeseSleepingSprite;
                if (cheeseAnimator != null) cheeseAnimator.enabled = false;
                cheesePlayerObj.SetActive(true);
            }
            else if (bedWithSleepingCheeseObj != null)
            {
                // 별도 수면 오브젝트가 있는 경우 깨어나기 전까지 치즈 플레이어 비활성화
                cheesePlayerObj.SetActive(false);
            }
        }

        if (bedWithSleepingCheeseObj != null) bedWithSleepingCheeseObj.SetActive(true);
        if (emptyBedObj != null) emptyBedObj.SetActive(false);
        if (exclamationMarkObj != null) exclamationMarkObj.SetActive(false);

        // 부스럭 사운드 자동 로드 백업
        if (rustleSoundClip == null)
        {
            rustleSoundClip = Resources.Load<AudioClip>("cheese_sound/grass_rustling");
        }
    }

    private void Start()
    {
        StartCoroutine(PlayTutorial1Sequence());
    }

    /// <summary>
    /// 튜토리얼 1 메인 컷씬 코루틴 시퀀스
    /// </summary>
    private IEnumerator PlayTutorial1Sequence()
    {
        // -------------------------------------------------------------
        // [1단계] 화면 페이드 인 (2초)
        // -------------------------------------------------------------
        yield return null; // 1프레임 안전 대기

        if (ScreenFader.Instance != null && fadeInDuration > 0f)
        {
            yield return StartCoroutine(ScreenFader.Instance.FadeInOnly(fadeInDuration));
        }
        else if (fadeInDuration > 0f)
        {
            yield return new WaitForSeconds(fadeInDuration);
        }

        yield return new WaitForSeconds(0.3f); // 안정 버퍼

        // -------------------------------------------------------------
        // [2단계 & 3단계] 대사 1 & 대사 2 출력 ("쿨쿨…", "음냐음냐…")
        // -------------------------------------------------------------
        yield return StartCoroutine(RunDialogueAndWait(dialogues1_2));

        yield return new WaitForSeconds(0.5f);

        // -------------------------------------------------------------
        // [4단계] 샴이 문을 열고 들어온다.
        // -------------------------------------------------------------
        Vector3 doorPos = doorTransform != null ? doorTransform.position : (siamObject != null ? siamObject.transform.position : Vector3.zero);
        Vector3 cheeseFrontPos = siamStopTargetTransform != null ? siamStopTargetTransform.position : doorPos + new Vector3(5f, 0f, 0f);

        // 1. 샴이 문 자리에 생성/활성화 + 정지(Idle) 애니메이션 재생
        if (siamObject != null)
        {
            siamObject.transform.position = doorPos;
            siamObject.SetActive(true);
            SetSpriteAlpha(siamObject, 1f);
            SetSpriteFlip(siamObject, cheeseFrontPos.x < doorPos.x); // 치즈 방향 바라보기
            PlaySiamIdle();
        }

        // 2. 2초 쉬기
        yield return new WaitForSeconds(2.0f);

        // 3. 샴이 문 자리에 고정 (1초)
        yield return new WaitForSeconds(1.0f);

        // 4. 샴이 문 자리에서 걸어옴 (3초 동안 이동 + 걷기 애니메이션 재생)
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

        // 5. 치즈 앞에서 멈춤 (2초)
        yield return new WaitForSeconds(2.0f);

        // -------------------------------------------------------------
        // [5단계] 대사 3 출력 ("...아직도 자?")
        // -------------------------------------------------------------
        yield return StartCoroutine(RunDialogueAndWait(dialogues3));

        // -------------------------------------------------------------
        // [6단계] 대사 4 출력 ("쿨쿨…")
        // -------------------------------------------------------------
        yield return StartCoroutine(RunDialogueAndWait(dialogues4));

        // -------------------------------------------------------------
        // [7단계] 대사 5 출력 ("일어나!")
        // -------------------------------------------------------------
        yield return StartCoroutine(RunDialogueAndWait(dialogues5));

        // -------------------------------------------------------------
        // [8단계] 대사 6 출력 ("으악!")
        // -------------------------------------------------------------
        yield return StartCoroutine(RunDialogueAndWait(dialogues6));

        yield return new WaitForSeconds(0.2f);

        // -------------------------------------------------------------
        // [9단계] 치즈가 침대에 누워있다 일어난다. (수면 ➔ 기본 IDLE 애니메이션 재생)
        // -------------------------------------------------------------
        // 1. 부스럭 소리 출력
        PlayRustleSound();

        // 2. 치즈가 누워있는 침대 이미지에서 빈 침대 이미지로 전환
        if (bedWithSleepingCheeseObj != null) bedWithSleepingCheeseObj.SetActive(false);
        if (emptyBedObj != null) emptyBedObj.SetActive(true);
        if (bedSpriteRenderer != null && emptyBedSprite != null) bedSpriteRenderer.sprite = emptyBedSprite;

        // 3. 치즈 깨어남 및 기본 IDLE 애니메이션 시작
        if (cheesePlayerObj != null)
        {
            cheesePlayerObj.SetActive(true);
            SetSpriteAlpha(cheesePlayerObj, 1f);
            PlayCheeseAwakeIdle();
        }

        // 4. 2초 쉬기
        yield return new WaitForSeconds(2.0f);

        // -------------------------------------------------------------
        // [10단계 & 11단계] 대사 7 & 대사 8 출력 ("이른 아침부터 무슨 일이야?", "오늘 우리가 색채 구슬...")
        // -------------------------------------------------------------
        yield return StartCoroutine(RunDialogueAndWait(dialogues7_8));

        yield return new WaitForSeconds(0.3f);

        // -------------------------------------------------------------
        // [12단계] 치즈 머리 위에 '!'가 뜬다 (2초간 출력)
        // -------------------------------------------------------------
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

        // -------------------------------------------------------------
        // [13단계 & 14단계] 대사 9 & 대사 10 출력 ("아!", "그러네. 완전히 까먹고 있었어!")
        // -------------------------------------------------------------
        yield return StartCoroutine(RunDialogueAndWait(dialogues9_10));

        // -------------------------------------------------------------
        // [15단계 & 16단계] 대사 11 & 대사 12 출력 ("하아... 그럴 줄 알았어,", "아무튼 다들...")
        // -------------------------------------------------------------
        yield return StartCoroutine(RunDialogueAndWait(dialogues11_12));

        yield return new WaitForSeconds(0.3f);

        // -------------------------------------------------------------
        // [17단계] 샴이 문을 닫고 나간다.
        // -------------------------------------------------------------
        // 1. 2초 쉬기
        yield return new WaitForSeconds(2.0f);

        // 2. 샴이 플레이어 자리에서 문 방향으로 걸어감 (3초 이동 + 걷기 애니메이션)
        if (siamObject != null)
        {
            SetSpriteFlip(siamObject, doorPos.x < cheeseFrontPos.x); // 문 방향 바라보기
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

        // 3. 샴이 문 앞에서 멈춤 (2초)
        yield return new WaitForSeconds(2.0f);

        // 4. 샴 이미지 삭제/비활성화
        if (siamObject != null)
        {
            siamObject.SetActive(false);
        }

        // 5. 2초 쉬기
        yield return new WaitForSeconds(2.0f);

        // -------------------------------------------------------------
        // [18단계 & 19단계] 대사 13 & 대사 14 출력 ("아니, 어떻게 이걸 까먹었지.", "붓이랑 망토까지...")
        // -------------------------------------------------------------
        yield return StartCoroutine(RunDialogueAndWait(dialogues13_14));

        yield return new WaitForSeconds(0.5f);

        // -------------------------------------------------------------
        // [20단계] 화면 페이드 아웃 (2초) ➔ 씬 전환 또는 게임 시작
        // -------------------------------------------------------------
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
            // 인게임 씬에서 바로 이어지는 경우 페이드 아웃/인 또는 조작 복구
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
    /// 샴 정지(Idle) 애니메이션/스프라이트 실행
    /// </summary>
    public void PlaySiamIdle()
    {
        if (siamAnimator != null)
        {
            if (!string.IsNullOrEmpty(siamWalkingBoolParam))
            {
                siamAnimator.SetBool(siamWalkingBoolParam, false);
            }
            if (!string.IsNullOrEmpty(siamIdleStateName))
            {
                siamAnimator.Play(siamIdleStateName);
            }
        }

        if (siamIdleFrames != null && siamIdleFrames.Length > 0 && siamSpriteRenderer != null)
        {
            if (siamFrameAnimRoutine != null) StopCoroutine(siamFrameAnimRoutine);
            siamFrameAnimRoutine = StartCoroutine(LoopSpriteFrames(siamSpriteRenderer, siamIdleFrames, siamAnimFPS));
        }
        else if (siamIdleSprite != null && siamSpriteRenderer != null)
        {
            if (siamFrameAnimRoutine != null) StopCoroutine(siamFrameAnimRoutine);
            siamSpriteRenderer.sprite = siamIdleSprite;
        }
    }

    /// <summary>
    /// 샴 걷기(Walk) 애니메이션/스프라이트 실행
    /// </summary>
    public void PlaySiamWalk()
    {
        if (siamAnimator != null)
        {
            if (!string.IsNullOrEmpty(siamWalkingBoolParam))
            {
                siamAnimator.SetBool(siamWalkingBoolParam, true);
            }
            if (!string.IsNullOrEmpty(siamWalkStateName))
            {
                siamAnimator.Play(siamWalkStateName);
            }
        }

        if (siamWalkFrames != null && siamWalkFrames.Length > 0 && siamSpriteRenderer != null)
        {
            if (siamFrameAnimRoutine != null) StopCoroutine(siamFrameAnimRoutine);
            siamFrameAnimRoutine = StartCoroutine(LoopSpriteFrames(siamSpriteRenderer, siamWalkFrames, siamAnimFPS));
        }
        else if (siamWalkSprite != null && siamSpriteRenderer != null)
        {
            if (siamFrameAnimRoutine != null) StopCoroutine(siamFrameAnimRoutine);
            siamSpriteRenderer.sprite = siamWalkSprite;
        }
    }

    /// <summary>
    /// 치즈 기상 시 기본 IDLE 애니메이션/스프라이트 실행
    /// </summary>
    public void PlayCheeseAwakeIdle()
    {
        if (cheeseAnimator != null)
        {
            cheeseAnimator.enabled = true;
            if (!string.IsNullOrEmpty(cheeseAwakeStateName))
            {
                cheeseAnimator.Play(cheeseAwakeStateName, 0, 0f);
            }
        }

        if (cheeseAwakeIdleFrames != null && cheeseAwakeIdleFrames.Length > 0 && cheeseSpriteRenderer != null)
        {
            if (cheeseFrameAnimRoutine != null) StopCoroutine(cheeseFrameAnimRoutine);
            cheeseFrameAnimRoutine = StartCoroutine(LoopSpriteFrames(cheeseSpriteRenderer, cheeseAwakeIdleFrames, cheeseAnimFPS));
        }
    }

    /// <summary>
    /// 스프라이트 배열 순환 재생 루틴 (프레임 애니메이션 헬퍼)
    /// </summary>
    private IEnumerator LoopSpriteFrames(SpriteRenderer sr, Sprite[] frames, float fps)
    {
        if (sr == null || frames == null || frames.Length == 0) yield break;
        float interval = 1f / Mathf.Max(1f, fps);
        int index = 0;
        while (true)
        {
            if (frames[index] != null) sr.sprite = frames[index];
            index = (index + 1) % frames.Length;
            yield return new WaitForSeconds(interval);
        }
    }

    /// <summary>
    /// 대사 출력 및 사용자 클릭 대기 헬퍼 코루틴
    /// </summary>
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
