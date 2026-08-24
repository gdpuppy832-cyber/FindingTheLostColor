using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Events;

/// <summary>
/// 스테이지 3 (Map_C) 진입 시 실행되는 인트로 컷씬 컨트롤러 (네로가 동굴로 도망치는 연출).
/// 타임라인 없이 코루틴 시퀀스로 직관적이고 안정적으로 동작합니다.
/// 
/// [연출 순서]
/// 1. 화면 페이드 인 (2초)
/// 2. 대사 1 출력 ("어? 어디갔지?")
/// 3. 동굴 및 네로 연출:
///    - 카메라가 동굴 방향으로 고속 패닝
///    - 네로가 둥둥 뜬 상태로 동굴 안으로 이동
///    - 네로가 서서히 투명화되며 소멸
///    - 카메라가 플레이어 위치로 복귀
/// 4. 대사 2 & 3 출력 ("벌써 저기까지!?", "동굴 안으로 들어갔네. 빨리 쫒아가야겠어!")
/// 5. 조작 잠금 해제 및 정상 게임 시작
/// </summary>
public class Stage3IntroCutscene : MonoBehaviour
{
    [Header("1. 페이드 설정")]
    [Tooltip("씬 진입 시 화면이 밝아지는 페이드 인 시간 (초 단위, 기본값: 2.0s)")]
    public float fadeInDuration = 2.0f;

    [Tooltip("페이드 인이 완전히 끝난 후 첫 대사가 출력되기까지의 안정 대기 시간 (초 단위, 기본값: 0.3s)")]
    public float delayAfterFadeIn = 0.3f;

    [Header("2. 대사 설정 1 (네로 발견 전)")]
    [Tooltip("페이드 인 직후 출력할 첫 번째 대사 목록 (예: 치즈 - '어? 어디갔지?')")]
    public DialogueLine[] firstDialogues = new DialogueLine[]
    {
        new DialogueLine
        {
            speakerName = "치즈",
            text = "어? 어디갔지?"
        }
    };

    [Header("3. 동굴 & 네로 A ➔ B 이동 연출 설정")]
    [Tooltip("카메라가 비출 동굴 목표 위치 (비워둘 시 A와 B의 중간 지점을 비추어 네로를 화면 중앙에 포커싱)")]
    public Transform caveCameraTarget;

    [Tooltip("동굴 앞에 나타날 네로 오브젝트")]
    public GameObject neroObject;

    [Tooltip("네로의 출발 위치 A 지점 Transform (비워둘 시 customPointA 또는 네로 현재 위치 사용)")]
    public Transform neroPointA;

    [Tooltip("neroPointA가 비어있을 때 사용할 A 지점 직접 입력 좌표 (선택사항)")]
    public Vector3 customPointA;

    [Tooltip("네로의 도착 위치 B 지점 Transform (동굴 안쪽 등)")]
    public Transform neroPointB;

    [Tooltip("neroPointB가 비어있을 때 사용할 B 지점 직접 입력 좌표 (선택사항)")]
    public Vector3 customPointB;

    [Tooltip("네로 스프라이트의 Sorting Order를 강제로 앞으로 올릴지 여부 (기본값: true)")]
    public bool forceSortingOrder = true;

    [Tooltip("강제 적용할 Sorting Order 값 (기본값: 50 - 배경/지형 위에 표시)")]
    public int neroSortingOrder = 50;

    [Tooltip("카메라가 동굴로 이동하는 시간 (초 단위, 기본값: 1.0s)")]
    public float cameraPanToCaveDuration = 1.0f;

    [Tooltip("네로가 A 지점에서 B 지점으로 이동하는 총 시간 (초 단위, 기본값: 2.0s - 인스펙터에서 변경 가능)")]
    public float neroMoveDuration = 2.0f;

    [Tooltip("네로가 둥둥 뜨는 상하 움직임 속도 (Frequency)")]
    public float neroFloatFrequency = 5.0f;

    [Tooltip("네로가 둥둥 뜨는 상하 진폭 거리 (Amplitude)")]
    public float neroFloatAmplitude = 0.25f;

    [Tooltip("B 지점에 다다르며 네로가 서서히 투명해지는 시간 (초 단위, 기본값: 0.8s)")]
    public float neroFadeDuration = 0.8f;

    [Tooltip("카메라가 플레이어로 돌아오는 시간 (초 단위, 기본값: 1.0s)")]
    public float cameraPanBackDuration = 1.0f;

    [Tooltip("카메라 복귀 완료 후 두 번째 대사가 출력되기까지의 대기 시간 (초)")]
    public float delayBeforeSecondDialogue = 0.3f;

    [Header("4. 대사 설정 2 (네로 발견 후)")]
    [Tooltip("네로가 동굴로 들어간 후 출력할 후속 대사 목록")]
    public DialogueLine[] secondDialogues = new DialogueLine[]
    {
        new DialogueLine
        {
            speakerName = "치즈",
            text = "벌써 저기까지!?"
        },
        new DialogueLine
        {
            speakerName = "치즈",
            text = "동굴 안으로 들어갔네. 빨리 쫒아가야겠어!"
        }
    };

    [Header("5. 컷씬 종료 이벤트")]
    [Tooltip("컷씬이 모두 끝나고 플레이어 조작이 복구될 때 호출할 이벤트 (필요 시 연결)")]
    public UnityEvent onCutsceneEnded;

    private PlayerMove cachedPlayerMove;
    private CursorController cachedCursorController;
    private CameraFollow cachedCameraFollow;

    private void Awake()
    {
        // 씬 시작 직후 화면을 암전(Black) 상태로 즉시 초기화하여 깜빡임 및 조기 대사 노출 방지
        if (ScreenFader.Instance != null && ScreenFader.Instance.fadeImage != null)
        {
            ScreenFader.Instance.fadeImage.gameObject.SetActive(true);
            ScreenFader.Instance.fadeImage.color = new Color(0f, 0f, 0f, 1f);
        }

        // 플레이어 조작 잠금 & 카메라 팔로우 일시 비활성화
        cachedPlayerMove = FindFirstObjectByType<PlayerMove>();
        if (cachedPlayerMove != null)
        {
            cachedPlayerMove.SetControl(false);
        }

        cachedCursorController = FindFirstObjectByType<CursorController>();
        if (cachedCursorController != null)
        {
            cachedCursorController.enabled = false;
        }

        cachedCameraFollow = FindFirstObjectByType<CameraFollow>();
        if (cachedCameraFollow != null)
        {
            cachedCameraFollow.enabled = false;
        }

        // 네로 오브젝트 초기 비활성화
        if (neroObject != null)
        {
            neroObject.SetActive(false);
        }
    }

    private void Start()
    {
        StartCoroutine(PlayStage3CutsceneSequence());
    }

    /// <summary>
    /// 전체 컷씬 메인 시퀀스 코루틴 (각 단계가 100% 완료된 후 다음 단계로 순차 진행하여 겹침 방지)
    /// </summary>
    private IEnumerator PlayStage3CutsceneSequence()
    {
        // -------------------------------------------------------------
        // [0단계] 시작 프레임 안전 대기 및 위치 초기화
        // -------------------------------------------------------------
        yield return null;

        // -------------------------------------------------------------
        // [1단계] 화면 페이드 인 (2.0초 동안 부드럽게 밝아짐 - 대사 절대 겹침 없음)
        // -------------------------------------------------------------
        if (ScreenFader.Instance != null && fadeInDuration > 0f)
        {
            yield return StartCoroutine(ScreenFader.Instance.FadeInOnly(fadeInDuration));
        }
        else if (fadeInDuration > 0f)
        {
            yield return new WaitForSeconds(fadeInDuration);
        }

        // 페이드 인이 100% 끝난 후 잠시 대기하여 시각적 안정성 확보
        if (delayAfterFadeIn > 0f)
        {
            yield return new WaitForSeconds(delayAfterFadeIn);
        }

        // -------------------------------------------------------------
        // [2단계] 대사 1 출력 ("어? 어디갔지?") - 페이드 인이 완전히 끝난 후 등장
        // -------------------------------------------------------------
        if (firstDialogues != null && firstDialogues.Length > 0 && DialogueManager.Instance != null)
        {
            bool isDialogue1Done = false;
            DialogueManager.Instance.StartDialogue(firstDialogues, () => isDialogue1Done = true);
            yield return new WaitUntil(() => isDialogue1Done);
        }

        yield return new WaitForSeconds(0.3f);

        // -------------------------------------------------------------
        // [3단계] 동굴 및 네로 A ➔ B 도망 연출
        // -------------------------------------------------------------
        // 1. 카메라 시작점 계산
        Vector3 playerPos = cachedPlayerMove != null ? cachedPlayerMove.transform.position : Camera.main.transform.position;
        float yOff = cachedCameraFollow != null ? cachedCameraFollow.yOffset : 2.0f;
        Vector3 startCamPos = new Vector3(playerPos.x, playerPos.y + yOff, Camera.main.transform.position.z);

        // 2. 네로 A좌표 (출발점) 및 B좌표 (도착점) 확정
        Vector3 startPosA = Vector3.zero;
        if (neroPointA != null) startPosA = neroPointA.position;
        else if (customPointA != Vector3.zero) startPosA = customPointA;
        else if (neroObject != null) startPosA = neroObject.transform.position;

        Vector3 endPosB = Vector3.zero;
        if (neroPointB != null) endPosB = neroPointB.position;
        else if (customPointB != Vector3.zero) endPosB = customPointB;
        else endPosB = startPosA + new Vector3(6f, 0f, 0f);

        // Z축 보정: 2D 배경 뒤로 숨겨지지 않도록 Z값 동기화
        float defaultZ = (neroObject != null) ? neroObject.transform.position.z : 0f;
        startPosA.z = (neroPointA != null) ? neroPointA.position.z : defaultZ;
        endPosB.z = startPosA.z;

        // 카메라 목표 지점 확정 (기본값: A와 B의 중간 지점을 비추어 네로가 화면 중앙에 오도록 함)
        Vector3 targetCavePos = caveCameraTarget != null ? caveCameraTarget.position : (startPosA + endPosB) * 0.5f;
        Vector3 targetCamPos = new Vector3(targetCavePos.x, targetCavePos.y + yOff, Camera.main.transform.position.z);

        // 3. A 지점에 네로 배치 + 활성화 + 알파값 100%(선명함) 강제 복구
        if (neroObject != null)
        {
            neroObject.transform.position = startPosA;
            neroObject.SetActive(true);

            // SpriteRenderer 알파값 1.0f 복구 및 Sorting Order 보정
            SpriteRenderer[] srs = neroObject.GetComponentsInChildren<SpriteRenderer>(true);
            foreach (var sr in srs)
            {
                if (sr != null)
                {
                    Color c = sr.color;
                    sr.color = new Color(c.r, c.g, c.b, 1f);

                    if (forceSortingOrder && sr.sortingOrder < neroSortingOrder)
                    {
                        sr.sortingOrder = neroSortingOrder;
                    }
                }
            }

            // Image 컴포넌트 알파 복구
            Image[] imgs = neroObject.GetComponentsInChildren<Image>(true);
            foreach (var img in imgs)
            {
                if (img != null)
                {
                    Color c = img.color;
                    img.color = new Color(c.r, c.g, c.b, 1f);
                }
            }

            CanvasGroup cg = neroObject.GetComponent<CanvasGroup>();
            if (cg != null) cg.alpha = 1f;
        }

        // 4. 카메라가 동굴 방향으로 빠르게 패닝 이동
        float elapsedCam = 0f;
        while (elapsedCam < cameraPanToCaveDuration)
        {
            elapsedCam += Time.deltaTime;
            float t = Mathf.Clamp01(elapsedCam / cameraPanToCaveDuration);
            t = Mathf.SmoothStep(0f, 1f, t);
            Camera.main.transform.position = Vector3.Lerp(startCamPos, targetCamPos, t);
            yield return null;
        }
        Camera.main.transform.position = targetCamPos;

        // 5. 네로가 A ➔ B 좌표로 2.0초 동안 둥둥 뜨며 이동 + 서서히 투명화
        if (neroObject != null)
        {
            SpriteRenderer[] neroRenderers = neroObject.GetComponentsInChildren<SpriteRenderer>(true);
            Image[] neroImages = neroObject.GetComponentsInChildren<Image>(true);

            float elapsedNero = 0f;
            while (elapsedNero < neroMoveDuration)
            {
                elapsedNero += Time.deltaTime;
                float t = Mathf.Clamp01(elapsedNero / neroMoveDuration);

                // A -> B 부드러운 위치 이동
                Vector3 currentBasePos = Vector3.Lerp(startPosA, endPosB, t);

                // 둥둥 뜨는 상하(Y축) 사인파 오프셋
                float floatY = Mathf.Sin(Time.time * neroFloatFrequency) * neroFloatAmplitude;
                neroObject.transform.position = new Vector3(currentBasePos.x, currentBasePos.y + floatY, currentBasePos.z);

                // 도착 지점에 다다를 때 서서히 투명화 (Fade Out)
                float fadeStartTime = Mathf.Max(0f, neroMoveDuration - neroFadeDuration);
                if (elapsedNero >= fadeStartTime && neroFadeDuration > 0f)
                {
                    float fadeT = Mathf.Clamp01((elapsedNero - fadeStartTime) / neroFadeDuration);
                    float currentAlpha = Mathf.Lerp(1f, 0f, fadeT);

                    for (int i = 0; i < neroRenderers.Length; i++)
                    {
                        if (neroRenderers[i] != null)
                        {
                            Color c = neroRenderers[i].color;
                            c.a = currentAlpha;
                            neroRenderers[i].color = c;
                        }
                    }

                    for (int i = 0; i < neroImages.Length; i++)
                    {
                        if (neroImages[i] != null)
                        {
                            Color c = neroImages[i].color;
                            c.a = currentAlpha;
                            neroImages[i].color = c;
                        }
                    }
                }

                yield return null;
            }

            // 네로 완전 소멸(비활성화)
            neroObject.SetActive(false);
        }
        else
        {
            yield return new WaitForSeconds(neroMoveDuration);
        }

        yield return new WaitForSeconds(0.4f);

        // 5. 카메라가 플레이어 위치로 복귀
        elapsedCam = 0f;
        while (elapsedCam < cameraPanBackDuration)
        {
            elapsedCam += Time.deltaTime;
            float t = Mathf.Clamp01(elapsedCam / cameraPanBackDuration);
            t = Mathf.SmoothStep(0f, 1f, t);
            Camera.main.transform.position = Vector3.Lerp(targetCamPos, startCamPos, t);
            yield return null;
        }
        Camera.main.transform.position = startCamPos;

        // -------------------------------------------------------------
        // [4단계] 대사 2 & 3 출력 ("벌써 저기까지!?", "동굴 안으로 들어갔네...")
        // -------------------------------------------------------------
        if (secondDialogues != null && secondDialogues.Length > 0 && DialogueManager.Instance != null)
        {
            bool isDialogue2Done = false;
            DialogueManager.Instance.StartDialogue(secondDialogues, () => isDialogue2Done = true);
            yield return new WaitUntil(() => isDialogue2Done);
        }

        // -------------------------------------------------------------
        // [5단계] 조작 복구 및 카메라 팔로우 복원 (게임 시작)
        // -------------------------------------------------------------
        if (cachedCameraFollow != null)
        {
            cachedCameraFollow.enabled = true;
        }

        if (cachedPlayerMove != null)
        {
            cachedPlayerMove.SetControl(true);
        }

        if (cachedCursorController != null)
        {
            cachedCursorController.enabled = true;
        }

        if (onCutsceneEnded != null)
        {
            onCutsceneEnded.Invoke();
        }

        Debug.Log("[Stage3IntroCutscene] 스테이지 3 인트로 컷씬 완료! 게임 플레이를 시작합니다.");
    }
}
