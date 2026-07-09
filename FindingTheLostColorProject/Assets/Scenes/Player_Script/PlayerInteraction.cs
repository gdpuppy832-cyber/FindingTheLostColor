using UnityEngine;
using UnityEngine.UI;
using TMPro;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

public class PlayerInteraction : MonoBehaviour
{
    [Header("상호작용 안내 UI 연결 (가지고 계신 텍스트 컴포넌트 중 하나를 여기에 연결해 주세요)")]
    [Tooltip("기존 레거시 UI Text 컴포넌트")]
    public Text promptText;

    [Tooltip("TextMeshPro - Text(UI) 컴포넌트 (UGUI Canvas 전용)")]
    public TextMeshProUGUI promptTmpText;

    [Tooltip("TextMeshPro - 3D Text 컴포넌트 (3D 월드 공간용)")]
    public TextMeshPro promptTmp3DText;

    [Tooltip("유니티 기본 3D TextMesh 컴포넌트 (3D 월드 공간용)")]
    public TextMesh promptLegacyTextMesh;

    [Header("안내창 게임 오브젝트 (연결하지 않으면 텍스트가 달린 부모 오브젝트를 자동 활성화/비활성화함)")]
    [Tooltip("상호작용 안내 UI 오브젝트")]
    public GameObject promptUIObj;

    [Header("임시 UI 글꼴 설정 (비워둘 시 기본 글꼴 적용)")]
    [Tooltip("상호작용 안내창이 자동 생성될 때 사용할 폰트 에셋")]
    public Font customFont;

    [Tooltip("상호작용 안내창용 TextMeshPro 폰트 에셋")]
    public TMP_FontAsset customTMPFont;

    [Tooltip("Resources 폴더 내부의 TMPro 폰트 에셋 파일명")]
    public string customTMPFontResourceName = "Hakgyoansim Nadeuri TTF L SDF";

    private bool isInteractionLocked = false;
    private bool isDynamicUI = false;

    void Start()
    {
        // 폰트 에셋 슬롯이 누락(None/Missing)된 경우 Resources 폴더에서 자동으로 로드해 옵니다.
        if (customTMPFont == null && !string.IsNullOrEmpty(customTMPFontResourceName))
        {
            customTMPFont = Resources.Load<TMP_FontAsset>(customTMPFontResourceName);
        }

        // 사용자가 아무 UI 텍스트 컴포넌트도 지정하지 않았을 때만 임시 안내 UI를 동적 생성
        if (promptText == null && promptTmpText == null && promptTmp3DText == null && promptLegacyTextMesh == null)
        {
            CreateDefaultPromptUI();
            isDynamicUI = true;
        }
        else
        {
            // 사용자가 수동으로 연결한 텍스트 컴포넌트가 있을 때
            // promptUIObj가 비어있다면 해당 텍스트 컴포넌트가 달린 오브젝트를 UI 패널로 인식하고 할당
            if (promptUIObj == null)
            {
                if (promptText != null) promptUIObj = promptText.gameObject;
                else if (promptTmpText != null) promptUIObj = promptTmpText.gameObject;
                else if (promptTmp3DText != null) promptUIObj = promptTmp3DText.gameObject;
                else if (promptLegacyTextMesh != null) promptUIObj = promptLegacyTextMesh.gameObject;
            }

            // 시작할 때는 꺼두기
            if (promptUIObj != null)
            {
                promptUIObj.SetActive(false);
            }
        }
    }

    void Update()
    {
        // 씬 전환 도중 등 락 상태인 경우 안내창 비활성화 및 반응 차단
        if (isInteractionLocked)
        {
            if (promptUIObj != null) promptUIObj.SetActive(false);
            return;
        }

        // 1. 활성화된 모든 상호작용 대상(InteractableObject) 탐색
        InteractableObject[] interactables = FindObjectsByType<InteractableObject>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
        
        InteractableObject closestInteractable = null;
        float minDistance = float.MaxValue;
        Vector3 playerPos = transform.position;

        // 2. 가장 가까운 상호작용 반경 이내의 대상 색출
        foreach (var interactable in interactables)
        {
            if (interactable.IsInRange(playerPos))
            {
                float dist = interactable.GetDistanceTo(playerPos);
                if (dist < minDistance)
                {
                    minDistance = dist;
                    closestInteractable = interactable;
                }
            }
        }

        // 3. 대상 감지 시 UI 텍스트 갱신 및 키 입력 확인
        if (closestInteractable != null)
        {
            // 안내 UI 활성화
            if (promptUIObj != null) promptUIObj.SetActive(true);

            // 지정한 텍스트메쉬 종류에 맞추어 텍스트 데이터 갱신
            string message = closestInteractable.promptMessage;
            if (promptText != null) promptText.text = message;
            if (promptTmpText != null) promptTmpText.text = message;
            if (promptTmp3DText != null) promptTmp3DText.text = message;
            if (promptLegacyTextMesh != null) promptLegacyTextMesh.text = message;

            // W키 입력 감지 (WasPressedThisFrame 검사로 꾹 눌러도 1회만 트리거되게 설정)
            bool wPressed = false;
#if ENABLE_INPUT_SYSTEM
            if (Keyboard.current != null)
            {
                wPressed = Keyboard.current.wKey.wasPressedThisFrame;
            }
#else
            wPressed = Input.GetKeyDown(KeyCode.W);
#endif

            if (wPressed)
            {
                closestInteractable.OnInteract();
            }
        }
        else
        {
            // 범위 내에 상호작용 대상이 없으면 UI 꺼주기
            if (promptUIObj != null) promptUIObj.SetActive(false);
        }
    }

    /// <summary>
    /// 상호작용 활성화/비활성화 락을 겁니다.
    /// </summary>
    public void SetInteractionLock(bool value)
    {
        isInteractionLocked = value;
        if (isInteractionLocked && promptUIObj != null)
        {
            promptUIObj.SetActive(false);
        }
    }

    /// <summary>
    /// 동적 프롬프트 UI 생성 로직 (사용자가 인스펙터에 슬롯을 아예 연결해두지 않았을 때의 예외 방어책)
    /// </summary>
    private void CreateDefaultPromptUI()
    {
        Canvas canvas = FindFirstObjectByType<Canvas>();
        if (canvas == null)
        {
            GameObject canvasObj = new GameObject("InteractionCanvas");
            canvas = canvasObj.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvasObj.AddComponent<CanvasScaler>();
            canvasObj.AddComponent<GraphicRaycaster>();
        }

        promptUIObj = new GameObject("Default_InteractionPromptPanel");
        promptUIObj.transform.SetParent(canvas.transform, false);

        RectTransform rect = promptUIObj.AddComponent<RectTransform>();
        rect.anchorMin = new Vector2(0.5f, 0.25f);
        rect.anchorMax = new Vector2(0.5f, 0.25f);
        rect.anchoredPosition = Vector2.zero;
        rect.sizeDelta = new Vector2(250f, 45f);

        Image bg = promptUIObj.AddComponent<Image>();
        bg.color = new Color(0f, 0f, 0f, 0.8f);

        GameObject textObj = new GameObject("Default_PromptText");
        textObj.transform.SetParent(promptUIObj.transform, false);

        RectTransform textRect = textObj.AddComponent<RectTransform>();
        textRect.anchorMin = Vector2.zero;
        textRect.anchorMax = Vector2.one;
        textRect.sizeDelta = Vector2.zero;

        promptText = textObj.AddComponent<Text>();
        promptText.font = customFont != null ? customFont : Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        promptText.fontSize = 18;
        promptText.fontStyle = FontStyle.Bold;
        promptText.alignment = TextAnchor.MiddleCenter;
        promptText.color = Color.yellow;

        promptUIObj.SetActive(false);
    }
}
