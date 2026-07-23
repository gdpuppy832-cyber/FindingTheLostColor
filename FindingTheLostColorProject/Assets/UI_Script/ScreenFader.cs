using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using System.Collections;

public class ScreenFader : MonoBehaviour
{
    public static ScreenFader Instance { get; private set; }

    [Header("Fade Settings")]
    [Tooltip("기본 페이드 속도 (초 단위, 기본값: 1.0s)")]
    public float defaultDuration = 1.0f;

    [Tooltip("페이드에 사용할 커스텀 이미지 (비워두면 동적 자동 생성)")]
    public Image fadeImage;

    [Tooltip("페이드 캔버스 (비워두면 동적 자동 생성)")]
    public Canvas fadeCanvas;

    private bool isFading = false;
    private float currentDuration = 1.0f;

    private void Awake()
    {
        // DontDestroyOnLoad 싱글톤 유지
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
            
            // 씬 로드 이벤트 등록
            SceneManager.sceneLoaded += OnSceneLoaded;
        }
        else
        {
            Destroy(gameObject);
            return;
        }

        // UI 요소 검증 및 동적 생성 백업
        InitializeUI();
    }

    private void OnDestroy()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
    }

    /// <summary>
    /// 페이드 캔버스와 이미지를 동적으로 셋업하거나 기존 연결 컴포넌트 정리
    /// </summary>
    private void InitializeUI()
    {
        if (fadeCanvas == null)
        {
            // 동적 캔버스 생성 및 DontDestroyOnLoad 하위 배치
            GameObject canvasObj = new GameObject("ScreenFader_Canvas");
            canvasObj.transform.SetParent(transform, false);
            
            fadeCanvas = canvasObj.AddComponent<Canvas>();
            fadeCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
            fadeCanvas.sortingOrder = 9999; // 모든 UI보다 최상단에 표기하도록 레이어 정렬

            canvasObj.AddComponent<CanvasScaler>();
            canvasObj.AddComponent<GraphicRaycaster>();
        }

        if (fadeImage == null)
        {
            // 동적 이미지 생성
            GameObject imgObj = new GameObject("FadeImage");
            imgObj.transform.SetParent(fadeCanvas.transform, false);

            RectTransform rect = imgObj.AddComponent<RectTransform>();
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.sizeDelta = Vector2.zero;

            fadeImage = imgObj.AddComponent<Image>();
            // 초기 상태는 투명한 검정색
            fadeImage.color = new Color(0f, 0f, 0f, 0f);
            fadeImage.raycastTarget = false; // 평소에는 터치 차단 해제
            fadeImage.gameObject.SetActive(false);
        }
    }

    /// <summary>
    /// 외부 호출용 API: 특정 씬으로 부드럽게 페이드 아웃 후 로드하고, 로드되면 페이드 인
    /// </summary>
    /// <param name="sceneName">이동할 씬 이름</param>
    /// <param name="duration">페이드 아웃/인 소요 시간 (초)</param>
    public void FadeToScene(string sceneName, float duration = -1f)
    {
        if (isFading) return;
        
        currentDuration = duration > 0f ? duration : defaultDuration;
        StartCoroutine(FadeOutAndLoadScene(sceneName));
    }

    /// <summary>
    /// 1단계: 화면 어둡게 페이드 아웃 ➔ 씬 로드
    /// </summary>
    private IEnumerator FadeOutAndLoadScene(string sceneName)
    {
        isFading = true;

        if (fadeImage != null)
        {
            fadeImage.gameObject.SetActive(true);
            fadeImage.raycastTarget = true; // 페이드 진행 도중에는 오클릭 차단!

            float elapsed = 0f;
            Color baseColor = fadeImage.color;

            // 투명 -> 검정 페이드
            while (elapsed < currentDuration)
            {
                elapsed += Time.unscaledDeltaTime;
                float alpha = Mathf.Clamp01(elapsed / currentDuration);
                fadeImage.color = new Color(baseColor.r, baseColor.g, baseColor.b, alpha);
                yield return null;
            }
            fadeImage.color = new Color(baseColor.r, baseColor.g, baseColor.b, 1f);
        }
        else
        {
            yield return new WaitForSecondsRealtime(currentDuration);
        }

        // 새 씬 로드 진행
        SceneManager.LoadScene(sceneName);
    }

    /// <summary>
    /// 2단계: 씬 로드 완료 이벤트 감지 시 자동으로 페이드 인 작동
    /// </summary>
    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        // 씬 로딩 완료 시 페이드 인 가동
        StartCoroutine(FadeInScene());
    }

    /// <summary>
    /// 3단계: 화면 밝게 페이드 인 ➔ 비활성화
    /// </summary>
    private IEnumerator FadeInScene()
    {
        if (fadeImage != null)
        {
            float elapsed = 0f;
            Color baseColor = fadeImage.color;

            // 검정 -> 투명 페이드
            while (elapsed < currentDuration)
            {
                elapsed += Time.unscaledDeltaTime;
                float alpha = Mathf.Clamp01(1f - (elapsed / currentDuration));
                fadeImage.color = new Color(baseColor.r, baseColor.g, baseColor.b, alpha);
                yield return null;
            }
            fadeImage.color = new Color(baseColor.r, baseColor.g, baseColor.b, 0f);
            fadeImage.raycastTarget = false; // 평소 터치 차단 해제
            fadeImage.gameObject.SetActive(false);
        }

        isFading = false;
    }
}
