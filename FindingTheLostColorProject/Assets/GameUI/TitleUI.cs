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

    private bool isTransitioning = false;

    private void Start()
    {
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

    // 1. 게임 시작 버튼 클릭 시 호출할 함수
    public void OnStartButtonClick()
    {
        if (isTransitioning) return;
        
        StartCoroutine(StartGameRoutine());
    }

    private IEnumerator StartGameRoutine()
    {
        isTransitioning = true;

        // 페이드아웃 효과 (알파값 0 -> 1로 어두워짐)
        if (fadeImage != null)
        {
            yield return StartCoroutine(FadeRoutine(1f));
        }

        // 씬 로드
        SceneManager.LoadScene(nextSceneName);
    }

    // 2. 옵션 버튼 클릭 시 호출할 함수
    public void OpenOptionPanel()
    {
        if (optionPanel != null)
        {
            optionPanel.SetActive(true);
        }
    }

    public void CloseOptionPanel()
    {
        if (optionPanel != null)
        {
            optionPanel.SetActive(false);
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
