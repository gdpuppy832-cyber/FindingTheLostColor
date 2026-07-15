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

    [Header("Developer Mode Settings (신규)")]
    [Tooltip("숨겨져 있다가 커맨드로 나타날 개발자 모드 진입 버튼")]
    [SerializeField] private GameObject devModeButton;
    [Tooltip("개발자 옵션 패널")]
    [SerializeField] private GameObject devModePanel;

    private int commandIndex = 0; // 히든 키 입력 시퀀스 인덱스 (0: 대기, 1: C 입력됨)

    // 치트 활성화 상태 (전역 정적 변수로 제공하여 타 스크립트에서 쉽게 참조)
    private static bool isGodMode = false;
    private static bool isInfinitePaint = false;
    private static bool isInfiniteSuper = false;

    [Header("Developer Cheat Button Text References (신규)")]
    [Tooltip("체력 무한 버튼 내 TextMeshProUGUI")]
    [SerializeField] private TMPro.TextMeshProUGUI godModeText;
    [Tooltip("물감 무한 버튼 내 TextMeshProUGUI")]
    [SerializeField] private TMPro.TextMeshProUGUI infinitePaintText;
    [Tooltip("궁극기 무한 버튼 내 TextMeshProUGUI")]
    [SerializeField] private TMPro.TextMeshProUGUI infiniteSuperText;

    public static bool IsGodMode => isGodMode;
    public static bool IsInfinitePaint => isInfinitePaint;
    public static bool IsInfiniteSuper => isInfiniteSuper;

    private void Start()
    {
        // 시작 시 모든 UI 패널은 닫아둔 상태로 시작합니다.
        if (pausePanel != null) pausePanel.SetActive(false);
        if (optionPanel != null) optionPanel.SetActive(false);
        if (quitConfirmPanel != null) quitConfirmPanel.SetActive(false);
        if (titleConfirmPanel != null) titleConfirmPanel.SetActive(false);

        // 개발자 패널과 버튼도 기본적으로 숨김
        if (devModeButton != null) devModeButton.SetActive(false);
        if (devModePanel != null) devModePanel.SetActive(false);

        // 초기 치트 텍스트 셋업 (OFF 상태로 초기화)
        UpdateCheatTexts();

        // 씬이 로드될 때 시간 배율을 1로 초기화합니다.
        Time.timeScale = 1f;
    }

    private void Update()
    {
        // [신규] 일시정지 상태에서만 히든 커맨드 C -> H 감지
        if (isPaused)
        {
            if (Input.GetKeyDown(KeyCode.C))
            {
                commandIndex = 1;
            }
            else if (Input.GetKeyDown(KeyCode.H))
            {
                if (commandIndex == 1)
                {
                    if (devModeButton != null)
                    {
                        devModeButton.SetActive(true); // 개발자 모드 진입 버튼 활성화!
                    }
                    if (SoundManager.Instance != null)
                    {
                        SoundManager.Instance.PlaySFX(SoundManager.SFXType.TextInfo, 0.8f);
                    }
                    Debug.Log("[PauseManager] 개발자 모드 히든 커맨드 (C->H) 활성화 성공!");
                    commandIndex = 0;
                }
                else
                {
                    commandIndex = 0;
                }
            }
            else if (Input.anyKeyDown && !Input.GetKeyDown(KeyCode.Escape))
            {
                // 다른 키를 누르면 시퀀스 리셋 (단 ESC 누르는 순간은 제외)
                commandIndex = 0;
            }
        }

        // ESC 키를 누르면 일시정지 상태를 토글(켜고 끄기)합니다.
        if (Input.GetKeyDown(KeyCode.Escape))
        {
            // [방탄 코드] 변수 대신 실제 화면에 UI 패널이 켜져 있는지 실시간 검사
            bool currentlyPaused = (pausePanel != null && pausePanel.activeSelf) ||
                                   (optionPanel != null && optionPanel.activeSelf) ||
                                   (quitConfirmPanel != null && quitConfirmPanel.activeSelf) ||
                                   (titleConfirmPanel != null && titleConfirmPanel.activeSelf);

            if (currentlyPaused)
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

        // [초강력 동기화 감지기 추가]
        // 설정창 속 게임재개 버튼 클릭 등으로 시간 배율은 재생(1f)되었으나,
        // 일시정지창(pausePanel) 등 백그라운드 패널이 여전히 켜져있는 꼬임 상태를 실시간 감지하여 자동 청소합니다.
        if (Time.timeScale > 0f)
        {
            bool anyPanelActive = (pausePanel != null && pausePanel.activeSelf) ||
                                  (optionPanel != null && optionPanel.activeSelf) ||
                                  (quitConfirmPanel != null && quitConfirmPanel.activeSelf) ||
                                  (titleConfirmPanel != null && titleConfirmPanel.activeSelf);

            if (anyPanelActive || isPaused)
            {
                if (pausePanel != null) pausePanel.SetActive(false);
                if (optionPanel != null) optionPanel.SetActive(false);
                if (quitConfirmPanel != null) quitConfirmPanel.SetActive(false);
                if (titleConfirmPanel != null) titleConfirmPanel.SetActive(false);
                isPaused = false;
            }
        }
    }

    // 1. 게임 계속하기 (일시정지 해제)
    public void Resume()
    {
        if (SoundManager.Instance != null) SoundManager.Instance.PlaySFX(SoundManager.SFXType.ButtonClick, 0.8f);

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
        if (SoundManager.Instance != null) SoundManager.Instance.PlaySFX(SoundManager.SFXType.ButtonClick, 0.8f);
        if (optionPanel != null) optionPanel.SetActive(true);
    }

    // 4. 옵션 패널 닫기
    public void CloseOption()
    {
        if (SoundManager.Instance != null) SoundManager.Instance.PlaySFX(SoundManager.SFXType.ButtonClick, 0.8f);
        if (optionPanel != null) optionPanel.SetActive(false);
    }

    // 5. 타이틀 화면으로 이동 버튼 클릭 시 호출 (이동 확인 팝업창을 켬)
    public void GoToTitle()
    {
        if (SoundManager.Instance != null) SoundManager.Instance.PlaySFX(SoundManager.SFXType.ButtonClick, 0.8f);
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
        if (SoundManager.Instance != null) SoundManager.Instance.PlaySFX(SoundManager.SFXType.ButtonClick, 0.8f);
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
        if (SoundManager.Instance != null) SoundManager.Instance.PlaySFX(SoundManager.SFXType.ButtonClick, 0.8f);
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
        if (SoundManager.Instance != null) SoundManager.Instance.PlaySFX(SoundManager.SFXType.ButtonClick, 0.8f);
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

    // ==========================================
    // [신규] 개발자 모드 & 치트 기능 연동 (텍스트 동적 스왑 포함)
    // ==========================================
    public void ToggleDevPanel()
    {
        if (devModePanel != null)
        {
            devModePanel.SetActive(!devModePanel.activeSelf);
            if (SoundManager.Instance != null) SoundManager.Instance.PlaySFX(SoundManager.SFXType.ButtonClick, 0.8f);
        }
    }

    public void ToggleGodMode()
    {
        isGodMode = !isGodMode;
        if (SoundManager.Instance != null) SoundManager.Instance.PlaySFX(SoundManager.SFXType.ButtonClick, 0.8f);
        UpdateCheatTexts(); // 실시간 텍스트 및 칼라 피드백 스왑
        Debug.Log("[DevMode] 무한 체력 상태: " + isGodMode);
    }

    public void ToggleInfinitePaint()
    {
        isInfinitePaint = !isInfinitePaint;
        if (SoundManager.Instance != null) SoundManager.Instance.PlaySFX(SoundManager.SFXType.ButtonClick, 0.8f);
        UpdateCheatTexts(); // 실시간 텍스트 및 칼라 피드백 스왑
        Debug.Log("[DevMode] 무한 물감 상태: " + isInfinitePaint);
    }

    public void ToggleInfiniteSuper()
    {
        isInfiniteSuper = !isInfiniteSuper;
        if (SoundManager.Instance != null) SoundManager.Instance.PlaySFX(SoundManager.SFXType.ButtonClick, 0.8f);
        UpdateCheatTexts(); // 실시간 텍스트 및 칼라 피드백 스왑
        Debug.Log("[DevMode] 무한 궁극기 상태: " + isInfiniteSuper);
    }

    /// <summary>
    /// 치트 버튼의 상태 텍스트를 Rich Text 포맷으로 다이내믹하게 스왑합니다. (ON = 녹색, OFF = 적색)
    /// </summary>
    private void UpdateCheatTexts()
    {
        if (godModeText != null)
        {
            godModeText.text = "체력 무한: " + (isGodMode ? "<color=#00FF00>ON</color>" : "<color=#FF0000>OFF</color>");
        }
        if (infinitePaintText != null)
        {
            infinitePaintText.text = "물감 무한: " + (isInfinitePaint ? "<color=#00FF00>ON</color>" : "<color=#FF0000>OFF</color>");
        }
        if (infiniteSuperText != null)
        {
            infiniteSuperText.text = "궁극기 무한: " + (isInfiniteSuper ? "<color=#00FF00>ON</color>" : "<color=#FF0000>OFF</color>");
        }
    }
}
