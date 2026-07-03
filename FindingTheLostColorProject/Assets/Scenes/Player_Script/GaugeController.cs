using UnityEngine;
using UnityEngine.UI;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

[RequireComponent(typeof(Image))]
public class GaugeController : MonoBehaviour
{
    [Header("Paint Settings")]
    [Tooltip("물감의 최대 용량")]
    public float maxPaint = 1f;

    [Tooltip("물감의 현재 남은 용량")]
    public float currentPaint = 1f;

    [Tooltip("좌클릭 시 초당 감소량 (기본값: 0.05)")]
    public float decreaseSpeed = 0.05f;

    [Tooltip("좌클릭을 뗐을 때 초당 재생량 (기본값: 0.25)")]
    public float regenSpeed = 0.25f;

    [Tooltip("그리기를 시작하기 위해 필요한 최소 물감 양 (기본값: 0.02)")]
    public float minPaintToDraw = 0.02f;

    [Tooltip("물감 게이지바가 부드럽게 움직이는 속도 (높을수록 빠름, 기본값: 5.0)")]
    public float paintLerpSpeed = 5f;

    private Image gaugeImage;
    private PlayerHealth playerHealth; // 플레이어 체력 스크립트 참조
    private bool needsReclick = false; // 물감 고갈 시 마우스 재클릭 필요 여부
    private float baseRegenSpeed;

    public bool NeedsReclick => needsReclick;

    public void SetRegenMultiplier(float multiplier)
    {
        regenSpeed = baseRegenSpeed * multiplier;
    }

    void Awake()
    {
        gaugeImage = GetComponent<Image>();
    }

    void Start()
    {
        // 씬에서 플레이어 체력 컨트롤러 검색
        playerHealth = FindFirstObjectByType<PlayerHealth>();
        
        // 원본 재생 속도 저장
        baseRegenSpeed = regenSpeed;

        // 시작 시 즉시 가득 채우기 및 컴포넌트 활성화
        if (gaugeImage != null)
        {
            gaugeImage.fillAmount = currentPaint / maxPaint;
            gaugeImage.enabled = currentPaint > 0.005f;
        }
    }

    void Update()
    {
        bool isLeftClickHeld = false;
        bool isDead = playerHealth != null && playerHealth.IsDead;
        bool isDrawBlocked = playerHealth != null && playerHealth.IsDrawBlocked;

#if ENABLE_INPUT_SYSTEM
        // New Input System 사용 시
        if (Mouse.current != null)
        {
            isLeftClickHeld = Mouse.current.leftButton.isPressed;
        }
#else
        // Legacy Input Manager 사용 시
        isLeftClickHeld = Input.GetMouseButton(0);
#endif

        // 마우스 클릭을 떼면 재클릭 요구 상태 해제
        if (!isLeftClickHeld)
        {
            needsReclick = false;
        }

        // 물감이 바닥나거나 설정한 임계치 이하로 떨어지면 재클릭 활성화
        if (currentPaint <= minPaintToDraw)
        {
            needsReclick = true;
        }

        // 피격으로 인해 그리기가 일시 끊겨있는 상태인 경우 게이지 변동 없이 고정 (Freeze)
        if (isDrawBlocked)
        {
            // 아무 작업도 하지 않아 소모 및 재생 차단
        }
        // 좌클릭을 누르고 있고 + 물감 잔량이 최소 설정치보다 많으며 + 재클릭 대기 중이 아니고 + 플레이어가 죽지 않았을 때만 소모
        else if (isLeftClickHeld && currentPaint > minPaintToDraw && !needsReclick && !isDead)
        {
            currentPaint -= decreaseSpeed * Time.deltaTime;
        }
        else
        {
            // 그 외에는 물감 재생 (1이상으로 늘어나지 않음)
            currentPaint += regenSpeed * Time.deltaTime;
        }

        // 최솟값 0, 최댓값 1로 제한
        currentPaint = Mathf.Clamp(currentPaint, 0f, maxPaint);

        // UI Image의 fillAmount에 반영 및 잔상 방지 처리 (Lerp 적용)
        if (gaugeImage != null)
        {
            float targetFillAmount = currentPaint / maxPaint;

            if (Mathf.Abs(gaugeImage.fillAmount - targetFillAmount) > 0.005f)
            {
                gaugeImage.fillAmount = Mathf.Lerp(gaugeImage.fillAmount, targetFillAmount, Time.deltaTime * paintLerpSpeed);
            }
            else
            {
                gaugeImage.fillAmount = targetFillAmount;
            }

            // 게이지가 0 이하로 떨어졌을 때 이미지 컴포넌트를 비활성화하여 테두리/잔상 제거
            gaugeImage.enabled = gaugeImage.fillAmount > 0.005f;
        }
    }
}