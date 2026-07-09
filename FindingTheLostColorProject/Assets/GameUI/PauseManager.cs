using UnityEngine;
using UnityEngine.SceneManagement;

public class PauseManager : MonoBehaviour
{
    [Header("UI Panels")]
    [SerializeField] private GameObject pausePanel;         // 일시정지 UI 패널 오브젝트
    [SerializeField] private GameObject optionPanel;        // 옵션 UI 패널 오브젝트
    [SerializeField] private GameObject quitConfirmPanel;   // 정말 종료하시겠습니까? 팝업 패널 오브젝트
    [SerializeField] private GameObject titleConfirmPanel;  // [추가] 정말 타이틀로 가시겠습니까? 팝업 패널 오브젝트

    [Header("Scenes to Load")]
    [SerializeField] private string titleSceneName = "TitleScene"; // 타이틀 씬 이름

    private bool isPaused = false;

    private void Start()
    {
        // 시작 시 모든 UI 패널은 닫아둔 상태로 시작합니다.
        if (pausePanel != null) pausePanel.SetActive(false);
        if (optionPanel != null) optionPanel.SetActive(false);
        if (quitConfirmPanel != null) quitConfirmPanel.SetActive(false);
        if (titleConfirmPanel != null) titleConfirmPanel.SetActive(false);

        // 씬이 로드될 때 시간 배율을 1로 초기화합니다.
        Time.timeScale = 1f;
    }

    private void Update()
    {
        // ESC 키를 누르면 일시정지 상태를 토글(켜고 끄기)합니다.
        if (Input.GetKeyDown(KeyCode.Escape))
        {
            if (isPaused)
            {
                // 1순위: 종료 확인 팝업이 켜져있다면 그것부터 닫습니다.
                if (quitConfirmPanel != null && quitConfirmPanel.activeSelf)
                {
                    CloseQuitConfirm();
                }
                // 2순위: 타이틀 이동 확인 팝업이 켜져있다면 그것부터 닫습니다.
                else if (titleConfirmPanel != null && titleConfirmPanel.activeSelf)
                {
                    CloseTitleConfirm();
                }
                // 3순위: 옵션 패널이 열려있다면 그것부터 닫습니다.
                else if (optionPanel != null && optionPanel.activeSelf)
                {
                    CloseOption();
                }
                // 4순위: 그 외 일반 일시정지 상태라면 일시정지를 해제합니다.
                else
                {
                    Resume();
                }
            }
            else
            {
                Pause();
            }
        }
    }

    // 1. 게임 계속하기 (일시정지 해제)
    public void Resume()
    {
        if (pausePanel != null) pausePanel.SetActive(false);
        if (optionPanel != null) optionPanel.SetActive(false);
        if (quitConfirmPanel != null) quitConfirmPanel.SetActive(false);
        if (titleConfirmPanel != null) titleConfirmPanel.SetActive(false);

        Time.timeScale = 1f; // 시간 재생
        isPaused = false;
    }

    // 2. 게임 일시정지
    public void Pause()
    {
        if (pausePanel != null) pausePanel.SetActive(true);

        Time.timeScale = 0f; // 시간 정지
        isPaused = true;
    }

    // 3. 옵션 패널 열기
    public void OpenOption()
    {
        if (optionPanel != null) optionPanel.SetActive(true);
    }

    // 4. 옵션 패널 닫기
    public void CloseOption()
    {
        if (optionPanel != null) optionPanel.SetActive(false);
    }

    // 5. 타이틀 화면으로 이동 버튼 클릭 시 호출 (이동 확인 팝업창을 켬)
    public void GoToTitle()
    {
        if (titleConfirmPanel != null)
        {
            titleConfirmPanel.SetActive(true);
        }
        else
        {
            // 만약 확인 팝업 패널을 지정해두지 않았다면 예외 처리로 즉시 타이틀로 이동
            ConfirmGoToTitle();
        }
    }

    // 6. 타이틀 이동 확인 팝업 닫기 (팝업창의 '아니오' 버튼)
    public void CloseTitleConfirm()
    {
        if (titleConfirmPanel != null) titleConfirmPanel.SetActive(false);
    }

    // 7. 실제 타이틀 이동 확정 (팝업창의 '네' 버튼)
    public void ConfirmGoToTitle()
    {
        // [중요] 씬을 이동하기 전에 반드시 시간 배율을 1로 되돌려야 다음 씬이 멈추지 않습니다!
        Time.timeScale = 1f;
        SceneManager.LoadScene(titleSceneName);
    }

    // 8. 게임 종료하기 버튼 클릭 시 호출 (종료 확인 팝업창을 켬)
    public void QuitGame()
    {
        if (quitConfirmPanel != null)
        {
            quitConfirmPanel.SetActive(true);
        }
        else
        {
            // 만약 확인 팝업 패널을 지정해두지 않았다면 예외 처리로 즉시 종료
            ConfirmQuit();
        }
    }

    // 9. 종료 확인 팝업 닫기 (팝업창의 '아니오' 버튼)
    public void CloseQuitConfirm()
    {
        if (quitConfirmPanel != null) quitConfirmPanel.SetActive(false);
    }

    // 10. 실제 게임 종료 확정 (팝업창의 '네' 버튼)
    public void ConfirmQuit()
    {
        #if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
        #else
            Application.Quit();
        #endif
    }
}
