using UnityEngine;
using UnityEngine.UI;
using TMPro;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

public class SuperGaugeController : MonoBehaviour
{
    public static SuperGaugeController Instance { get; private set; }

    [Header("Super Gauge Settings")]
    [Tooltip("궁극기 충전에 필요한 누적 피해량 (정화량)")]
    public float maxSuper = 45f;
    [Tooltip("현재 충전된 누적 피해량 (정화량, 에디터 실행 중 실시간 조절 가능)")]
    public float currentSuper = 0f;

    [Header("UI References")]
    [SerializeField] private Image gaugeImage; // 궁극기 게이지 UI바 이미지
    [SerializeField] private TextMeshProUGUI readyText; // 궁극기 완료 시 표시할 텍스트
    [SerializeField] private GameObject readyVisual; // 궁극기 완료 시 표시할 이미지/오브젝트

    public bool IsFullyCharged => currentSuper >= maxSuper;

    private void Awake()
    {
        // 씬 내 싱글톤 관리
        if (Instance == null)
        {
            Instance = this;
        }
        else
        {
            Destroy(gameObject);
        }
    }

    private void Start()
    {
        UpdateUI();
    }

    private void Update()
    {
        // [치트] 개발자 모드 무한 궁극기 적용 시 실시간으로 최대 충전 상태 유지
        if (PauseManager.IsInfiniteSuper)
        {
            currentSuper = maxSuper;
        }
        // [치트키] 개발 및 테스트 편의를 위해 인게임에서 'T' 키를 누르면 궁극기 게이지 즉시 충전 완료
        bool isTKeyPressed = false;
#if ENABLE_INPUT_SYSTEM
        if (Keyboard.current != null)
        {
            isTKeyPressed = Keyboard.current.tKey.wasPressedThisFrame;
        }
#else
        isTKeyPressed = Input.GetKeyDown(KeyCode.T);
#endif
        if (isTKeyPressed)
        {
            AddSuperGauge(maxSuper);
            Debug.Log("[치트키 발동] 궁극기 게이지가 100% 충전되었습니다!");
        }

        // 인펙터 창에서 currentSuper 수치를 임의로 긁어서 조절할 때도 UI에 실시간 피드백 반영
        UpdateUI();
    }

    // 누적 피해량 가산 함수
    public void AddSuperGauge(float amount)
    {
        if (IsFullyCharged) return;

        currentSuper = Mathf.Clamp(currentSuper + amount, 0f, maxSuper);
        UpdateUI();

        if (currentSuper >= maxSuper)
        {
            OnFullyCharged();
        }
    }

    // 궁극기 사용 시 초기화 함수
    public void UseSuper()
    {
        currentSuper = 0f;
        UpdateUI();
    }

    private void UpdateUI()
    {
        // currentSuper 수치 유효 범위 고정
        currentSuper = Mathf.Clamp(currentSuper, 0f, maxSuper);

        if (gaugeImage != null)
        {
            gaugeImage.fillAmount = currentSuper / maxSuper;
        }

        bool isReady = IsFullyCharged;
        if (readyText != null)
        {
            readyText.gameObject.SetActive(isReady);
        }
        if (readyVisual != null)
        {
            readyVisual.SetActive(isReady);
        }
    }

    private void OnFullyCharged()
    {
        Debug.Log("[SuperGauge] 궁극기 게이지 완충! 마우스 우클릭으로 별똥별을 투하하세요.");
        if (readyText != null)
        {
            readyText.text = "궁극기 준비 완료! (우클릭)";
        }
    }
}
