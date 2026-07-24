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
    private CursorController cursorController; // 커서 컨트롤러 참조 추가
    private GaugeVisualFeedback gaugeFeedback; // [추가] 물감 부족 비주얼 피드백 컴포넌트 참조
    private bool needsReclick = false; // 물감 고갈 시 마우스 재클릭 필요 여부
    private float baseRegenSpeed;

    private PlayerMove playerMove;                 // [추가] 대쉬 여부 판단을 위한 참조
    private bool isFocusCharging = false;          // [추가] R키 꾹 눌러 충전하는 중인지 여부
    public bool IsFocusCharging => isFocusCharging; // [추가] 외부 속도 제한기용 Getter

    [Header("집중 충전 설정 (신규)")]
    [Tooltip("집중 충전(R키 꾹 누름) 시 물감 재생 속도 증가 배율 (기본값: 6배)")]
    public float focusChargeRegenMultiplier = 6f;

    private float zoneRegenMultiplier = 1f;        // [추가] 페인트 리젠존에 의한 재생 배율
    private float focusRegenMultiplier = 1f;       // [추가] R키 집중 충전에 의한 재생 배율

    public bool NeedsReclick => needsReclick;

    /// <summary>
    /// 외부(예: PaintRegenZone)에서 물감 재생 속도를 곱해줄 때 호출
    /// </summary>
    public void SetRegenMultiplier(float multiplier)
    {
        zoneRegenMultiplier = multiplier;
        UpdateFinalRegenSpeed();
    }

    /// <summary>
    /// 최종 물감 재생 속도를 갱신합니다. (중첩 금지: 더 빠른 배율 하나만 최종 적용)
    /// (최종 재생속도 = 기본 속도 * Max(리젠존 배율, 집중 충전 배율))
    /// </summary>
    private void UpdateFinalRegenSpeed()
    {
        float finalMultiplier = Mathf.Max(zoneRegenMultiplier, focusRegenMultiplier);
        regenSpeed = baseRegenSpeed * finalMultiplier;
    }

    void Awake()
    {
        gaugeImage = GetComponent<Image>();
    }

    void Start()
    {
        // 씬에서 필요한 컨트롤러 검색 및 참조
        playerHealth = FindFirstObjectByType<PlayerHealth>();
        cursorController = FindFirstObjectByType<CursorController>();
        gaugeFeedback = FindFirstObjectByType<GaugeVisualFeedback>(); // [추가] 캐싱
        playerMove = FindFirstObjectByType<PlayerMove>(); // [추가] 대쉬 체크용 캐싱
        
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
        // [치트] 개발자 모드 무한 물감 적용 시 실시간으로 최대 물감 유지
        if (PauseManager.IsInfinitePaint)
        {
            currentPaint = maxPaint;
        }
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

        // [신규] R키 집중 충전 가동 (KeyBindManager 연동)
        KeyCode recoverPaintKey = (KeyBindManager.Instance != null) ? KeyBindManager.Instance.RecoverPaintKey : KeyCode.R;
        bool rKeyHeld = Input.GetKey(recoverPaintKey);

        bool isDashing = playerMove != null && playerMove.IsDashing;
        bool isAttacking = isLeftClickHeld && currentPaint > minPaintToDraw && !needsReclick;

        // R키를 누르는 상태이고, 캔슬 방해 요인(공격 중, 대쉬 중, 기동 마비)이 전혀 없을 때만 집중 충전 기믹 가동
        if (rKeyHeld && !isAttacking && !isDashing && !isDead && !isDrawBlocked)
        {
            if (!isFocusCharging)
            {
                isFocusCharging = true;
                focusRegenMultiplier = focusChargeRegenMultiplier; // 집중 충전 배율 할당
                UpdateFinalRegenSpeed(); // 곱연산 갱신
                Debug.Log($"[GaugeController] R키 집중 충전 가동! 배율: {focusRegenMultiplier}배 (최종: {regenSpeed / baseRegenSpeed}배)");
            }
        }
        else
        {
            if (isFocusCharging)
            {
                isFocusCharging = false;
                focusRegenMultiplier = 1f; // 집중 충전 배율 복원
                UpdateFinalRegenSpeed(); // 곱연산 갱신
                Debug.Log($"[GaugeController] R키 집중 충전 해제! (최종: {regenSpeed / baseRegenSpeed}배)");
            }
        }


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
            // [점묘화 모드 소모 차단 버그 해결]
            // 2번 점묘화(차징) 모드이고 물감이 20% (chargePaintCost) 미만인 경우,
            // 차징 공격 자체가 시작되지 않으므로 소모하지 않고 재생(regen) 로직을 적용시킵니다.
            bool canDecreasePaint = true;
            if (cursorController != null && cursorController.attackMode == 2)
            {
                if (currentPaint < cursorController.chargePaintCost)
                {
                    canDecreasePaint = false;
                }
            }

            if (canDecreasePaint)
            {
                float activeDecreaseSpeed = decreaseSpeed;

                // 2번 공격 모드(차징 모드)일 때의 소모 속도 차등 조율
                if (cursorController != null && cursorController.attackMode == 2)
                {
                    if (cursorController.IsChargeCompleted)
                    {
                        // 최대 충전 완료 후 홀드(조준 유지) 중일 때의 소모율 적용
                        activeDecreaseSpeed = decreaseSpeed * cursorController.chargeHoldDepletionMultiplier;
                    }
                    else
                    {
                        // 차지 중 소모율 적용
                        activeDecreaseSpeed = decreaseSpeed * cursorController.chargeDepletionMultiplier;
                    }

                    // [추가] 고갈(minPaintToDraw)되기 2초 전의 임계값 실시간 감지 (2초 전 = minPaintToDraw + 2초간의 소모량)
                    float dangerThreshold = minPaintToDraw + (activeDecreaseSpeed * 2f);
                    if (currentPaint <= dangerThreshold)
                    {
                        if (gaugeFeedback != null)
                        {
                            gaugeFeedback.SetLoopWarning(true); // 2초 전 돌입 시 빨간색 루프 점멸 구동
                        }
                    }
                    else
                    {
                        if (gaugeFeedback != null)
                        {
                            gaugeFeedback.SetLoopWarning(false);
                        }
                    }
                }
                else
                {
                    // 1번 모드는 루프 경고 해제
                    if (gaugeFeedback != null)
                    {
                        gaugeFeedback.SetLoopWarning(false);
                    }
                }

                currentPaint -= activeDecreaseSpeed * Time.deltaTime;
            }
            else
            {
                // 소모 중단 시 루프 경고 정지
                if (gaugeFeedback != null)
                {
                    gaugeFeedback.SetLoopWarning(false);
                }
                // 20% 미만이라 차징 불가능할 때는 물감을 실시간 충전 재생시킴
                currentPaint += regenSpeed * Time.deltaTime;
            }
        }
        else
        {
            // 그 외에는 물감 재생 및 루프 경고 비활성화
            if (gaugeFeedback != null)
            {
                gaugeFeedback.SetLoopWarning(false);
            }
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