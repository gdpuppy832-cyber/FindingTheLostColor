using UnityEngine;
using UnityEngine.UI;
using TMPro;

[RequireComponent(typeof(Button))]
public class KeyBindButton : MonoBehaviour
{
    [Header("Action Setup")]
    [Tooltip("연동할 액션 이름 (Left, Right, Jump, Interact, Dash 중 정확히 기입)")]
    public string actionName;

    [Header("UI Text References (둘 중 하나만 연결하면 자동 매칭)")]
    public TextMeshProUGUI tmpText;
    public Text legacyText;

    private Button button;

    private void Start()
    {
        button = GetComponent<Button>();

        // 텍스트 자동 캐싱
        if (tmpText == null && legacyText == null)
        {
            tmpText = GetComponentInChildren<TextMeshProUGUI>();
            if (tmpText == null) legacyText = GetComponentInChildren<Text>();
        }

        // 버튼 클릭 이벤트 리스너 바인딩
        if (button != null)
        {
            button.onClick.AddListener(OnClickRebind);
        }

        // 현재 할당되어 있는 키 이름으로 최초 화면 텍스트 초기화
        UpdateDisplay(GetActionCurrentKey());
    }

    private void OnEnable()
    {
        // 씬 전환 등으로 컴포넌트가 활성화될 때 키 설정 동기화
        if (KeyBindManager.Instance != null)
        {
            UpdateDisplay(GetActionCurrentKey());
        }
    }

    /// <summary>
    /// 버튼 클릭 시 호출: 입력 대기 모드로 화면을 바꾼 뒤 키 바인딩 코루틴 실행
    /// </summary>
    private void OnClickRebind()
    {
        if (KeyBindManager.Instance == null) return;
        if (KeyBindManager.Instance.IsRebinding) return; // 이미 다른 키 리바인딩 대기 중이면 터치 차단

        string koreanActionName = GetKoreanActionName();

        // 1. 대기 화면 출력 (예: 점프 : [입력 대기 중])
        SetTextValue($"{koreanActionName} : [입력 대기 중]");

        // 2. 키 바인딩 연산 시작
        KeyBindManager.Instance.StartRebinding(actionName, (newKey) =>
        {
            // 3. 재매핑 성공 시 UI 텍스트를 새 단축키명으로 업데이트
            UpdateDisplay(newKey);
        });
    }

    /// <summary>
    /// 현재 매핑되어 있는 키코드를 기지국에서 조회
    /// </summary>
    private KeyCode GetActionCurrentKey()
    {
        if (KeyBindManager.Instance != null)
        {
            return KeyBindManager.Instance.GetKey(actionName);
        }
        
        // 아직 기지국 로드가 안 끝난 상태라면 예외 처리로 기본값 매칭
        if (actionName == "Left") return KeyCode.A;
        if (actionName == "Right") return KeyCode.D;
        if (actionName == "Jump") return KeyCode.Space;
        if (actionName == "Interact") return KeyCode.W;
        if (actionName == "Dash") return KeyCode.LeftShift;
        if (actionName == "ChangeAttack") return KeyCode.E;
        if (actionName == "RecoverPaint") return KeyCode.R;
        return KeyCode.None;
    }

    /// <summary>
    /// 영어 액션명을 인게임용 한글 명칭으로 번역 반환
    /// </summary>
    private string GetKoreanActionName()
    {
        if (actionName == "Left") return "왼쪽 이동";
        if (actionName == "Right") return "오른쪽 이동";
        if (actionName == "Jump") return "점프";
        if (actionName == "Interact") return "상호작용";
        if (actionName == "Dash") return "대시";
        if (actionName == "ChangeAttack") return "공격 변경";
        if (actionName == "RecoverPaint") return "물감 충전";
        return actionName;
    }

    /// <summary>
    /// 키코드 상태에 맞춰 보기 좋은 텍스트(예: Space -> SPACE)로 포맷 변환하여 화면에 출력
    /// </summary>
    private void UpdateDisplay(KeyCode key)
    {
        string keyName = key.ToString().ToUpper();
        
        // 특정 주요 특수키들의 인게임 가독성 텍스트 변경
        if (key == KeyCode.LeftShift) keyName = "L_SHIFT";
        else if (key == KeyCode.RightShift) keyName = "R_SHIFT";
        else if (key == KeyCode.Space) keyName = "SPACE";
        else if (key == KeyCode.LeftArrow) keyName = "◀";
        else if (key == KeyCode.RightArrow) keyName = "▶";
        else if (key == KeyCode.UpArrow) keyName = "▲";
        else if (key == KeyCode.DownArrow) keyName = "▼";
        else if (key == KeyCode.Alpha0) keyName = "0";
        else if (key == KeyCode.Alpha1) keyName = "1";
        else if (key == KeyCode.Alpha2) keyName = "2";
        else if (key == KeyCode.Alpha3) keyName = "3";
        else if (key == KeyCode.Alpha4) keyName = "4";
        else if (key == KeyCode.Alpha5) keyName = "5";
        else if (key == KeyCode.Alpha6) keyName = "6";
        else if (key == KeyCode.Alpha7) keyName = "7";
        else if (key == KeyCode.Alpha8) keyName = "8";
        else if (key == KeyCode.Alpha9) keyName = "9";

        string koreanActionName = GetKoreanActionName();
        SetTextValue($"{koreanActionName} : [{keyName}]");
    }

    private void SetTextValue(string text)
    {
        if (tmpText != null) tmpText.text = text;
        if (legacyText != null) legacyText.text = text;
    }
}
