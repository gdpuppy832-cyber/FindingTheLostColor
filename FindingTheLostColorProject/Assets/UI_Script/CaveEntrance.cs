using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections;
using UnityEngine.SceneManagement;

public class CaveEntrance : InteractableObject
{
    [Header("동굴 진입 설정")]
    [Tooltip("이동할 보스 스테이지 씬 이름 (기본값: BossStage)")]
    public string bossSceneName = "BossStage";

    [Tooltip("화면 전환 페이드 아웃 연출 시간 (초)")]
    public float fadeDuration = 1.5f;

    [Header("사용자 경고 UI 설정 (비워둘 시 런타임 자동 생성)")]
    [Tooltip("조건 미달 시 활성화할 경고용 부모 패널/오브젝트")]
    public GameObject warningPanelObj;
    
    [Tooltip("경고 메시지를 출력할 레거시 UI Text")]
    public Text warningText;

    [Tooltip("경고 메시지를 출력할 TextMeshProUGUI")]
    public TextMeshProUGUI warningTmpText;

    [Tooltip("경고 메시지를 출력할 3D TextMeshPro")]
    public TextMeshPro warningTmp3DText;

    [Tooltip("경고 메시지를 출력할 3D TextMesh")]
    public Text warningLegacyTextMesh;

    [Header("사용자 선택 대화창(Yes/No) UI 설정 (비워둘 시 런타임 자동 생성)")]
    [Tooltip("조건 충족 시 활성화할 선택 대화창 패널 오브젝트")]
    public GameObject selectionPanelObj;

    [Tooltip("질문 문구('동굴에 입장하시겠습니까?')를 출력할 레거시 UI Text")]
    public Text selectionQuestionText;

    [Tooltip("질문 문구를 출력할 TextMeshProUGUI")]
    public TextMeshProUGUI selectionQuestionTmpText;

    [Tooltip("'예' (Yes) 선택 버튼")]
    public Button yesButton;

    [Tooltip("'아니오' (No) 선택 버튼")]
    public Button noButton;

    [Header("화면 전환 UI 설정 (페이드 아웃 효과용)")]
    [Tooltip("화면 전체를 덮는 HUD Canvas (비워둘 시 월드 스페이스가 아닌 스크린 스페이스 캔버스를 자동 검색합니다)")]
    public Canvas hudCanvas;

    [Tooltip("에디터에서 미리 배치해 둔 전체 화면 검정 이미지 (비워둘 시 검은색 이미지를 런타임에 자동 생성하여 페이드 진행)")]
    public Image customFadeImage;

    private GameObject activeSelectionUI;
    private Coroutine warningCoroutine;
    private bool isTransitioning = false;

    void Start()
    {
        // 씬 내 플레이어 상호작용 프롬프트 안내 문구 설정
        promptMessage = "W키를 눌러 입장 시도";

        // 시작 시 경고창 패널이 지정되어 있으면 꺼두기
        if (warningPanelObj != null) warningPanelObj.SetActive(false);
        // 선택창 패널이 지정되어 있으면 꺼두기
        if (selectionPanelObj != null) selectionPanelObj.SetActive(false);
    }

    /// <summary>
    /// 플레이어가 입장 영역 내에서 W키를 눌러 상호작용을 시작할 때 실행됩니다.
    /// </summary>
    public override void OnInteract()
    {
        // 씬 로딩 전환이 시작되었다면 어떠한 상호작용도 차단
        if (isTransitioning) return;

        // 1. 스테이지 내 모든 무채색 고양이가 정화되었는지 검사
        bool isAllPurified = false;
        if (PurificationManager.Instance != null)
        {
            isAllPurified = PurificationManager.Instance.IsAllPurified;
        }
        else
        {
            // 매니저가 씬에 없다면 기본적으로 정화 대상 몬스터가 없는 것으로 인지해 입장 가능하도록 허용
            NormalMonster[] monsters = FindObjectsByType<NormalMonster>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
            isAllPurified = monsters.Length == 0;
        }

        // 2. 조건에 따른 분기 처리
        if (isAllPurified)
        {
            // 모든 고양이 정화 완료 -> 선택지 UI 노출
            ShowSelectionDialogue();
        }
        else
        {
            // 정화 조건 미달 -> 안내 팝업 출력
            TriggerWarning("남아 있는 고양이를 모두 정화해야 한다");
        }
    }

    /// <summary>
    /// 경고 텍스트를 출력하고 일정 시간 뒤 UI를 가립니다.
    /// </summary>
    private void TriggerWarning(string message)
    {
        // 사용자가 인스펙터에 경고 텍스트 관련 오브젝트를 하나라도 연결해 두었을 때
        if (warningPanelObj != null || warningText != null || warningTmpText != null || warningTmp3DText != null || warningLegacyTextMesh != null)
        {
            if (warningCoroutine != null) StopCoroutine(warningCoroutine);
            warningCoroutine = StartCoroutine(ShowCustomWarningRoutine(message));
        }
        else
        {
            // 비워져 있으면 기존의 동적 런타임 팝업 가동
            ShowWarningPopupFallback(message);
        }
    }

    private IEnumerator ShowCustomWarningRoutine(string message)
    {
        // 1. 지정 텍스트 컴포넌트 값 할당
        if (warningText != null) warningText.text = message;
        if (warningTmpText != null) warningTmpText.text = message;
        if (warningTmp3DText != null) warningTmp3DText.text = message;
        if (warningLegacyTextMesh != null) warningLegacyTextMesh.text = message;

        // 2. 패널 또는 텍스트 오브젝트 활성화
        GameObject targetPanel = warningPanelObj;
        if (targetPanel == null)
        {
            if (warningText != null) targetPanel = warningText.gameObject;
            else if (warningTmpText != null) targetPanel = warningTmpText.gameObject;
            else if (warningTmp3DText != null) targetPanel = warningTmp3DText.gameObject;
            else if (warningLegacyTextMesh != null) targetPanel = warningLegacyTextMesh.gameObject;
        }

        if (targetPanel != null) targetPanel.SetActive(true);

        // 3. 2초 대기 후 비활성화
        yield return new WaitForSeconds(2.0f);

        if (targetPanel != null) targetPanel.SetActive(false);
    }

    /// <summary>
    /// 동굴 입장 의사를 묻는 Yes/No 선택 대화창 UI를 활성화합니다.
    /// </summary>
    private void ShowSelectionDialogue()
    {
        // 사용자가 선택창 패널을 직접 연결해 둔 경우
        if (selectionPanelObj != null)
        {
            // 텍스트 지정
            string question = "동굴에 입장하시겠습니까?";
            if (selectionQuestionText != null) selectionQuestionText.text = question;
            if (selectionQuestionTmpText != null) selectionQuestionTmpText.text = question;

            // 버튼 리스너 바인딩
            if (yesButton != null)
            {
                yesButton.onClick.RemoveAllListeners();
                yesButton.onClick.AddListener(ConfirmEntrance);
            }
            if (noButton != null)
            {
                noButton.onClick.RemoveAllListeners();
                noButton.onClick.AddListener(CloseSelectionDialogue);
            }

            selectionPanelObj.SetActive(true);
        }
        else
        {
            // 비어 있다면 기존의 동적 런타임 팝업 생성 가동
            ShowSelectionDialogueFallback();
        }
    }

    /// <summary>
    /// 동굴 입장 확인 시 플레이어 이동/공격 통제 및 화면 페이드아웃 후 씬 이동을 실행합니다.
    /// </summary>
    private void ConfirmEntrance()
    {
        if (isTransitioning) return;
        isTransitioning = true;

        CloseSelectionDialogue();

        // 1. 플레이어 제어권 영구 박탈 및 인터랙션 락
        PlayerInteraction playerInt = FindFirstObjectByType<PlayerInteraction>();
        if (playerInt != null) playerInt.SetInteractionLock(true);

        PlayerMove playerMove = FindFirstObjectByType<PlayerMove>();
        if (playerMove != null) playerMove.SetControl(false);

        // 2. 공격(붓 그리기) 입력 차단 (마우스 클릭 차단 위해 CursorController 비활성화)
        CursorController cursor = FindFirstObjectByType<CursorController>();
        if (cursor != null) cursor.enabled = false;

        // 3. 검은 화면 전환 연출 시작 및 씬 전환 코루틴 호출
        StartCoroutine(TransitionToBossStage());
    }

    /// <summary>
    /// 선택 대화창을 닫습니다.
    /// </summary>
    private void CloseSelectionDialogue()
    {
        // 커스텀 패널 비활성화
        if (selectionPanelObj != null)
        {
            selectionPanelObj.SetActive(false);
        }

        // 동적 패널 비활성화
        if (activeSelectionUI != null)
        {
            Destroy(activeSelectionUI);
            activeSelectionUI = null;
        }
    }

    /// <summary>
    /// 화면을 페이드 아웃시킨 뒤 보스 씬으로 이동합니다.
    /// </summary>
    private IEnumerator TransitionToBossStage()
    {
        Image targetFadeImage = customFadeImage;
        GameObject spawnedFadeObj = null;

        if (targetFadeImage == null)
        {
            // 사용자가 페이드용 이미지를 직접 연결하지 않은 경우, 런타임에 자동 생성
            Canvas targetCanvas = hudCanvas != null ? hudCanvas : FindFirstObjectByType<Canvas>();

            // 만약 자동 검색된 캔버스가 월드 스페이스(예: 몬스터 체력바)라면, 스크린 스페이스 캔버스를 찾기 위해 필터링
            if (targetCanvas != null && targetCanvas.renderMode == RenderMode.WorldSpace)
            {
                Canvas[] allCanvases = FindObjectsByType<Canvas>(FindObjectsInactive.Include, FindObjectsSortMode.None);
                foreach (var c in allCanvases)
                {
                    if (c.renderMode != RenderMode.WorldSpace)
                    {
                        targetCanvas = c;
                        break;
                    }
                }
            }

            if (targetCanvas != null)
            {
                spawnedFadeObj = new GameObject("CaveEntrance_FadeOverlay");
                spawnedFadeObj.transform.SetParent(targetCanvas.transform, false);

                RectTransform rect = spawnedFadeObj.AddComponent<RectTransform>();
                rect.anchorMin = Vector2.zero;
                rect.anchorMax = Vector2.one;
                rect.sizeDelta = Vector2.zero;

                targetFadeImage = spawnedFadeObj.AddComponent<Image>();
                targetFadeImage.color = new Color(0f, 0f, 0f, 0f);
            }
        }

        if (targetFadeImage != null)
        {
            targetFadeImage.gameObject.SetActive(true);

            // 페이드 아웃 연출 (투명 -> 검정)
            float elapsed = 0f;
            Color baseColor = targetFadeImage.color;
            while (elapsed < fadeDuration)
            {
                elapsed += Time.deltaTime;
                float alpha = Mathf.Clamp01(elapsed / fadeDuration);
                targetFadeImage.color = new Color(baseColor.r, baseColor.g, baseColor.b, alpha);
                yield return null;
            }
            // 최종적으로 완전한 알파 1 확인
            targetFadeImage.color = new Color(baseColor.r, baseColor.g, baseColor.b, 1f);
        }
        else
        {
            // 캔버스를 아예 찾을 수 없는 경우 시간 대기만 처리
            yield return new WaitForSeconds(fadeDuration);
        }

        // 보스 스테이지 씬 로딩
        SceneManager.LoadScene(bossSceneName);
    }

    /// <summary>
    /// [폴백] 동적 선택창 팝업 생성 장치
    /// </summary>
    private void ShowSelectionDialogueFallback()
    {
        if (activeSelectionUI != null) return;

        Canvas canvas = FindFirstObjectByType<Canvas>();
        if (canvas == null) return;

        activeSelectionUI = new GameObject("CaveEntrance_SelectionDialogue");
        activeSelectionUI.transform.SetParent(canvas.transform, false);

        RectTransform bgRect = activeSelectionUI.AddComponent<RectTransform>();
        bgRect.anchorMin = new Vector2(0.5f, 0.5f);
        bgRect.anchorMax = new Vector2(0.5f, 0.5f);
        bgRect.anchoredPosition = Vector2.zero;
        bgRect.sizeDelta = new Vector2(350f, 180f);

        Image bgImage = activeSelectionUI.AddComponent<Image>();
        bgImage.color = new Color(0.08f, 0.08f, 0.12f, 0.95f);

        Outline outline = activeSelectionUI.AddComponent<Outline>();
        outline.effectColor = new Color(0.8f, 0.8f, 0.8f, 0.5f);
        outline.effectDistance = new Vector2(2f, 2f);

        GameObject titleObj = new GameObject("TitleText");
        titleObj.transform.SetParent(activeSelectionUI.transform, false);
        RectTransform titleRect = titleObj.AddComponent<RectTransform>();
        titleRect.anchorMin = new Vector2(0.5f, 0.7f);
        titleRect.anchorMax = new Vector2(0.5f, 0.7f);
        titleRect.anchoredPosition = Vector2.zero;
        titleRect.sizeDelta = new Vector2(300f, 40f);

        Text titleText = titleObj.AddComponent<Text>();
        titleText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        titleText.fontSize = 18;
        titleText.fontStyle = FontStyle.Bold;
        titleText.alignment = TextAnchor.MiddleCenter;
        titleText.color = Color.white;
        titleText.text = "동굴에 입장하시겠습니까?";

        // '예' 버튼
        GameObject yesBtnObj = new GameObject("YesButton");
        yesBtnObj.transform.SetParent(activeSelectionUI.transform, false);
        RectTransform yesRect = yesBtnObj.AddComponent<RectTransform>();
        yesRect.anchorMin = new Vector2(0.3f, 0.3f);
        yesRect.anchorMax = new Vector2(0.3f, 0.3f);
        yesRect.anchoredPosition = Vector2.zero;
        yesRect.sizeDelta = new Vector2(90f, 40f);

        Image yesBg = yesBtnObj.AddComponent<Image>();
        yesBg.color = new Color(0.12f, 0.45f, 0.22f, 1f);
        Button yesBtn = yesBtnObj.AddComponent<Button>();
        yesBtn.onClick.AddListener(() => ConfirmEntrance());

        GameObject yesTextObj = new GameObject("Text");
        yesTextObj.transform.SetParent(yesBtnObj.transform, false);
        Text yesText = yesTextObj.AddComponent<Text>();
        yesText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        yesText.fontSize = 15;
        yesText.fontStyle = FontStyle.Bold;
        yesText.alignment = TextAnchor.MiddleCenter;
        yesText.color = Color.white;
        yesText.text = "예";

        RectTransform yesTextRect = yesTextObj.GetComponent<RectTransform>();
        yesTextRect.anchorMin = Vector2.zero;
        yesTextRect.anchorMax = Vector2.one;
        yesTextRect.sizeDelta = Vector2.zero;

        // '아니오' 버튼
        GameObject noBtnObj = new GameObject("NoButton");
        noBtnObj.transform.SetParent(activeSelectionUI.transform, false);
        RectTransform noRect = noBtnObj.AddComponent<RectTransform>();
        noRect.anchorMin = new Vector2(0.7f, 0.3f);
        noRect.anchorMax = new Vector2(0.7f, 0.3f);
        noRect.anchoredPosition = Vector2.zero;
        noRect.sizeDelta = new Vector2(90f, 40f);

        Image noBg = noBtnObj.AddComponent<Image>();
        noBg.color = new Color(0.6f, 0.15f, 0.15f, 1f);
        Button noBtn = noBtnObj.AddComponent<Button>();
        noBtn.onClick.AddListener(() => CloseSelectionDialogue());

        GameObject noTextObj = new GameObject("Text");
        noTextObj.transform.SetParent(noBtnObj.transform, false);
        Text noText = noTextObj.AddComponent<Text>();
        noText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        noText.fontSize = 15;
        noText.fontStyle = FontStyle.Bold;
        noText.alignment = TextAnchor.MiddleCenter;
        noText.color = Color.white;
        noText.text = "아니오";

        RectTransform noTextRect = noTextObj.GetComponent<RectTransform>();
        noTextRect.anchorMin = Vector2.zero;
        noTextRect.anchorMax = Vector2.one;
        noTextRect.sizeDelta = Vector2.zero;
    }

    /// <summary>
    /// [폴백] 동적 경고 팝업 생성 장치
    /// </summary>
    private void ShowWarningPopupFallback(string message)
    {
        Canvas canvas = FindFirstObjectByType<Canvas>();
        if (canvas == null) return;

        GameObject oldWarning = GameObject.Find("CaveEntrance_WarningPopup");
        if (oldWarning != null) Destroy(oldWarning);

        GameObject warningObj = new GameObject("CaveEntrance_WarningPopup");
        warningObj.transform.SetParent(canvas.transform, false);

        RectTransform rect = warningObj.AddComponent<RectTransform>();
        rect.anchorMin = new Vector2(0.5f, 0.4f);
        rect.anchorMax = new Vector2(0.5f, 0.4f);
        rect.anchoredPosition = Vector2.zero;
        rect.sizeDelta = new Vector2(340f, 50f);

        Image bg = warningObj.AddComponent<Image>();
        bg.color = new Color(0.5f, 0.1f, 0.1f, 0.9f);

        Outline outline = warningObj.AddComponent<Outline>();
        outline.effectColor = new Color(1f, 0.3f, 0.3f, 0.6f);
        outline.effectDistance = new Vector2(1f, 1f);

        GameObject textObj = new GameObject("Text");
        textObj.transform.SetParent(warningObj.transform, false);

        RectTransform textRect = textObj.AddComponent<RectTransform>();
        textRect.anchorMin = Vector2.zero;
        textRect.anchorMax = Vector2.one;
        textRect.sizeDelta = Vector2.zero;

        Text text = textObj.AddComponent<Text>();
        text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        text.fontSize = 15;
        text.fontStyle = FontStyle.Bold;
        text.alignment = TextAnchor.MiddleCenter;
        text.color = Color.white;
        text.text = message;

        Destroy(warningObj, 2f);
    }
}
