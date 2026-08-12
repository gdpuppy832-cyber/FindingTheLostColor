using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class TitleUI : MonoBehaviour
{
    [Header("Difficulty Select Panel & First Game Scene")]
    [Tooltip("게임 시작 클릭 시 열릴 난이도 선택 패널 UI 오브젝트")]
    [SerializeField] private GameObject difficultySelectPanel;
    [Tooltip("이지/하드 난이도 선택 후 첫 게임 진행을 개시할 메인 맵 씬 이름")]
    [SerializeField] private string startGameSceneName = "Map_a";

    [Header("Panels")]
    [SerializeField] private GameObject optionPanel; // 옵션 패널 UI

    [Header("Fade Settings")]
    [SerializeField] private Image fadeImage; // 페이드 효과용 검은색 Image 컴포넌트
    [SerializeField] private float fadeDuration = 1.0f; // 페이드 시간

    [Header("Full Screen Background Settings (신규)")]
    [Tooltip("모든 해상도에서 화면을 꽉 채울 타이틀 배경 Image (비워 둘 시 런타임 자동 검색)")]
    [SerializeField] private Image backgroundImage;

    private bool isTransitioning = false;

    private void Start()
    {
        // 모든 해상도(16:9, 16:10, 21:9 등)에서 화면을 꽉 채우도록 레이아웃 및 캔버스 자동 세팅
        SetupFullScreenLayout();

        // 시작할 때 옵션 패널 및 난이도 선택 패널은 닫아둡니다.
        if (optionPanel != null)
        {
            optionPanel.SetActive(false);
        }

        if (difficultySelectPanel != null)
        {
            difficultySelectPanel.SetActive(false);
        }

        // 시작 시 자체 페이드 이미지는 비활성화하여 전역 ScreenFader만 깔끔하게 단일 페이드를 처리하도록 합니다.
        if (fadeImage != null)
        {
            fadeImage.raycastTarget = false;
            fadeImage.gameObject.SetActive(false);
        }
    }

    private void Update()
    {
        // ESC 키로 난이도 패널 또는 옵션 패널 닫기 지원
        if (Input.GetKeyDown(KeyCode.Escape))
        {
            if (difficultySelectPanel != null && difficultySelectPanel.activeSelf)
            {
                CloseDifficultySelectPanel();
            }
            else if (optionPanel != null && optionPanel.activeSelf)
            {
                CloseOptionPanel();
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
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 0.5f; // 가로/세로 모든 해상도 비율 변경 시 패널 및 자식 버튼 크기가 반응형으로 확대/축소됨!
        }

        // 3. 씬 내 모든 배경/패널 Image 검색하여 Preserve Aspect(비율 유지) 강제 해제 후 Full Stretch 적용
        Image[] allImages = FindObjectsByType<Image>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        foreach (var img in allImages)
        {
            if (img == null) continue;

            // 난이도 선택 패널, 옵션 패널 등의 팝업 윈도우 패널은 크기를 강제로 확장하지 않도록 예외 처리
            if (difficultySelectPanel != null && (img.gameObject == difficultySelectPanel || img.transform.IsChildOf(difficultySelectPanel.transform)))
            {
                continue;
            }
            if (optionPanel != null && (img.gameObject == optionPanel || img.transform.IsChildOf(optionPanel.transform)))
            {
                continue;
            }

            string objName = img.gameObject.name.ToLower();
            if (objName.Contains("difficulty") || objName.Contains("popup") || objName.Contains("dialog") || objName.Contains("select"))
            {
                continue;
            }

            // 배경, 메인 캔버스 이미지의 경우 비율 유지 옵션을 꺼서 화면 전체에 쫙 늘려줌
            if (objName.Contains("bg") || objName.Contains("background") || objName.Contains("title") || img == backgroundImage)
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

    // 1. 게임 시작 버튼 클릭 시 호출할 함수 (난이도 선택 패널 열기)
    public void OnStartButtonClick()
    {
        if (isTransitioning) return;

        if (difficultySelectPanel != null)
        {
            difficultySelectPanel.SetActive(true);
        }
        else
        {
            // 난이도 패널이 비어있으면 기본 지정된 첫 게임 맵(Map_a)으로 이동
            PlayerPrefs.SetString("SelectedDifficulty", "Easy");
            PlayerPrefs.Save();
            StartCoroutine(StartGameRoutine(startGameSceneName));
        }
    }

    // 난이도 선택 패널 닫기 버튼용
    public void CloseDifficultySelectPanel()
    {
        if (difficultySelectPanel != null)
        {
            difficultySelectPanel.SetActive(false);
        }
    }

    // 2. 이지 모드 선택 버튼 클릭 시 호출할 함수 (난이도: Easy 저장 후 Map_a 씬으로 로드)
    public void OnEasyModeButtonClick()
    {
        if (isTransitioning) return;
        PlayerPrefs.SetString("SelectedDifficulty", "Easy");
        PlayerPrefs.Save();
        Debug.Log("[TitleUI] 선택된 난이도: Easy ➔ 첫 번째 게임 맵('Map_a')으로 진입합니다.");

        StartCoroutine(StartGameRoutine(startGameSceneName));
    }

    // 3. 하드 모드 선택 버튼 클릭 시 호출할 함수 (난이도: Hard 저장 후 Map_a 씬으로 로드)
    public void OnHardModeButtonClick()
    {
        if (isTransitioning) return;
        PlayerPrefs.SetString("SelectedDifficulty", "Hard");
        PlayerPrefs.Save();
        Debug.Log("[TitleUI] 선택된 난이도: Hard ➔ 첫 번째 게임 맵('Map_a')으로 진입합니다.");

        StartCoroutine(StartGameRoutine(startGameSceneName));
    }

    private IEnumerator StartGameRoutine(string targetSceneName)
    {
        if (string.IsNullOrEmpty(targetSceneName))
        {
            Debug.LogError("[TitleUI] 이동할 targetSceneName이 비어 있습니다!");
            isTransitioning = false;
            yield break;
        }

        isTransitioning = true;

        // 씬 전환 및 페이드 아웃/인 처리를 전역 ScreenFader 단일 시스템으로 위임
        Debug.Log($"[TitleUI] '{targetSceneName}' 씬으로 전역 ScreenFader 페이드 전환을 개시합니다.");
        if (ScreenFader.Instance != null)
        {
            ScreenFader.Instance.FadeToScene(targetSceneName, fadeDuration);
        }
        else
        {
            SceneManager.LoadScene(targetSceneName);
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
