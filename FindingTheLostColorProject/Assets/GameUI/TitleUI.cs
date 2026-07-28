using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class TitleUI : MonoBehaviour
{
    [Header("Scenes to Load")]
    [SerializeField] private string nextSceneName = "GameScene"; // 전환할 씬 이름

    [Header("Panels")]
    [SerializeField] private GameObject optionPanel; // 옵션 패널 UI

    [Header("Fade Settings")]
    [SerializeField] private Image fadeImage; // 페이드 효과용 검은색 Image 컴포넌트
    [SerializeField] private float fadeDuration = 1.0f; // 페이드 시간
    [SerializeField] private bool useFadeInOnStart = false; // [추가] 씬이 처음 켜질 때 페이드인(검은화면->밝아짐)을 사용할지 여부

    [Header("Full Screen Background Settings (신규)")]
    [Tooltip("모든 해상도에서 화면을 꽉 채울 타이틀 배경 Image (비워 둘 시 런타임 자동 검색)")]
    [SerializeField] private Image backgroundImage;

    private bool isTransitioning = false;

    private void Start()
    {
        // 모든 해상도(16:9, 16:10, 21:9 등)에서 화면을 꽉 채우도록 레이아웃 및 캔버스 자동 세팅
        SetupFullScreenLayout();

        // 시작할 때 옵션 패널은 닫아둡니다.
        if (optionPanel != null)
        {
            optionPanel.SetActive(false);
        }

        if (fadeImage != null)
        {
            if (useFadeInOnStart)
            {
                // 씬 시작 시 화면을 밝히는 페이드인 효과 (알파값 1 -> 0으로 투명하게)
                Color tempColor = fadeImage.color;
                tempColor.a = 1f;
                fadeImage.color = tempColor;
                
                StartCoroutine(FadeRoutine(0f)); 
            }
            else
            {
                // 페이드인을 안 쓰면 이미지를 바로 투명하게(알파 0) 만들어 화면을 가리지 않게 합니다.
                Color tempColor = fadeImage.color;
                tempColor.a = 0f;
                fadeImage.color = tempColor;
                fadeImage.raycastTarget = false;
            }
        }
    }

    /// <summary>
    /// 16:10 및 모든 해상도 모니터에서 위아래 파란 여백 없이 화면 전체가 꽉 차도록 카메라 및 CanvasScaler를 자동 조율합니다.
    /// </summary>
    private void SetupFullScreenLayout()
    {
        // 1. 메인 카메라 기본 파란색 여백 박멸
        Camera mainCam = Camera.main;
        if (mainCam != null)
        {
            mainCam.clearFlags = CameraClearFlags.SolidColor;
            mainCam.backgroundColor = Color.black; // 파란색 비침 원천 차단!
        }

        // 2. CanvasScaler -> Expand (확장) 모드로 설정하여 16:10 화면에서도 위아래 여백을 100% 가득 덮음
        Canvas canvas = GetComponentInParent<Canvas>();
        if (canvas == null) canvas = FindFirstObjectByType<Canvas>();

        if (canvas != null)
        {
            CanvasScaler scaler = canvas.GetComponent<CanvasScaler>();
            if (scaler == null) scaler = canvas.gameObject.AddComponent<CanvasScaler>();

            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.Expand; // Expand 모드는 16:10 해상도의 여백을 가득 덮어버림
        }

        // 3. 씬 내 모든 배경/패널 Image 검색하여 Preserve Aspect(비율 유지) 강제 해제 후 Full Stretch 적용
        Image[] allImages = FindObjectsByType<Image>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        foreach (var img in allImages)
        {
            if (img == null) continue;
            string objName = img.gameObject.name.ToLower();

            // 배경, 패널, 캔버스 관련 이미지의 경우 비율 유지 옵션을 꺼서 16:10 모니터 전체에 쫙 늘려줌
            if (objName.Contains("bg") || objName.Contains("background") || objName.Contains("title") || objName.Contains("panel") || img == backgroundImage)
            {
                img.preserveAspect = false; // 비율 유지 강제 해제 (여백 방지!)
                RectTransform rect = img.rectTransform;
                rect.anchorMin = Vector2.zero;
                rect.anchorMax = Vector2.one;
                rect.offsetMin = Vector2.zero;
                rect.offsetMax = Vector2.zero;
            }
        }

        // 4. 페이드 이미지도 Full Stretch로 꽉 채움 보장
        if (fadeImage != null)
        {
            fadeImage.preserveAspect = false;
            RectTransform rect = fadeImage.rectTransform;
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
        }
    }

    // 1. 게임 시작 버튼 클릭 시 호출할 함수
    public void OnStartButtonClick()
    {
        if (isTransitioning) return;
        
        StartCoroutine(StartGameRoutine());
    }

    private IEnumerator StartGameRoutine()
    {
        isTransitioning = true;

        // 1. 자체 페이드아웃 효과가 존재하면 먼저 적용
        if (fadeImage != null)
        {
            yield return StartCoroutine(FadeRoutine(1f));
        }

        // 2. 씬을 넘어갈 때 ScreenFader에 씬 전환을 위임하여 다음 씬 페이드인까지 자연스럽게 연동!
        if (ScreenFader.Instance != null)
        {
            // 이미 타이틀에서 페이드 아웃을 마친 상태이므로, 0초 딜레이로 즉시 어두운 상태에서 로드 후 페이드인하도록 전달합니다.
            ScreenFader.Instance.FadeToScene(nextSceneName, 0f);
        }
        else
        {
            SceneManager.LoadScene(nextSceneName);
        }

        yield return null;
    }

    // 2. 옵션 버튼 클릭 시 호출할 함수
    public void OpenOptionPanel()
    {
        // 1순위: TitleUI 인스펙터에 지정된 옵션 패널 활성화
        if (optionPanel != null)
        {
            optionPanel.SetActive(true);
            return;
        }

        // 2순위: PauseManager 컴포넌트가 씬에 존재하는 경우 검색 후 OpenOption 호출
        PauseManager pauseMgr = FindFirstObjectByType<PauseManager>();
        if (pauseMgr != null)
        {
            pauseMgr.OpenOption();
            return;
        }

        // 3순위: 씬 전체에서 OptionPanel / SettingsPanel 패널 자동 검색
        GameObject foundPanel = GameObject.Find("OptionPanel");
        if (foundPanel == null) foundPanel = GameObject.Find("SettingsPanel");
        if (foundPanel == null) foundPanel = GameObject.Find("OptionCanvas");
        
        if (foundPanel != null)
        {
            foundPanel.SetActive(true);
        }
        else
        {
            Debug.LogWarning("[TitleUI] 옵션 패널(optionPanel)을 찾을 수 없습니다. 타이틀 씬 Canvas 하위에 OptionPanel 프리팹을 배치하거나 TitleUI 인스펙터에 연결해 주세요.");
        }
    }

    public void CloseOptionPanel()
    {
        if (optionPanel != null)
        {
            optionPanel.SetActive(false);
            return;
        }

        PauseManager pauseMgr = FindFirstObjectByType<PauseManager>();
        if (pauseMgr != null)
        {
            pauseMgr.CloseOption();
            return;
        }

        GameObject foundPanel = GameObject.Find("OptionPanel");
        if (foundPanel == null) foundPanel = GameObject.Find("SettingsPanel");
        if (foundPanel == null) foundPanel = GameObject.Find("OptionCanvas");

        if (foundPanel != null)
        {
            foundPanel.SetActive(false);
        }
    }

    // 3. 종료 버튼 클릭 시 호출할 함수
    public void OnQuitButtonClick()
    {
        #if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
        #else
            Application.Quit();
        #endif
    }

    // 페이드 효과 코루틴 (targetAlpha로 변경)
    private IEnumerator FadeRoutine(float targetAlpha)
    {
        if (fadeImage == null) yield break;

        // 페이드가 시작되면 클릭 방지를 위해 Raycast Target 활성화
        fadeImage.raycastTarget = true;

        Color originalColor = fadeImage.color;
        float startAlpha = originalColor.a;
        float elapsedTime = 0f;

        while (elapsedTime < fadeDuration)
        {
            elapsedTime += Time.deltaTime;
            float newAlpha = Mathf.Lerp(startAlpha, targetAlpha, elapsedTime / fadeDuration);
            
            fadeImage.color = new Color(originalColor.r, originalColor.g, originalColor.b, newAlpha);
            yield return null;
        }

        fadeImage.color = new Color(originalColor.r, originalColor.g, originalColor.b, targetAlpha);

        // 페이드가 완전히 끝나서 화면이 투명해졌다면(알파 0) 뒤의 버튼들을 누를 수 있게 Raycast Target 비활성화
        if (targetAlpha <= 0f)
        {
            fadeImage.raycastTarget = false;
        }
    }
}
