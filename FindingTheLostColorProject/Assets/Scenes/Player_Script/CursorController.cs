using UnityEngine;
using System.Collections;
using System.Collections.Generic;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

public class CursorController : MonoBehaviour
{
    public Transform player; // 플레이어 오브젝트
    public TrailRenderer trail; // TrailRenderer 오브젝트

    [Header("Cursor Settings")]
    public List<Texture2D> cursorTextures;
    public Vector2 hotSpot = new Vector2(16, 16);

    [Header("Distance Thresholds")]
    public float mediumDistance = 3.8f;
    public float intenseDistance = 6.7f;

    [Header("Trail Settings")]
    public float trailWidth = 0.15f; // 트레일 선 굵기

    [Header("Paint Healing Settings")]
    [Tooltip("붓질(좌클릭)로 몬스터를 정화할 수 있는 반경 (브러시 크기, 기본값: 1.2)")]
    public float paintRadius = 1.2f;

    [Tooltip("근거리일 때 초당 회복량 (기본값: 1.0)")]
    public float closeHealRate = 1.0f;

    [Tooltip("중거리일 때 초당 회복량 (기본값: 0.7)")]
    public float mediumHealRate = 0.7f;

    [Tooltip("원거리일 때 초당 회복량 (기본값: 0.4)")]
    public float farHealRate = 0.4f;

    [Header("Attack Mode Settings (Swapping)")]
    [Tooltip("현재 공격 모드 (1: 일반 브러시, 2: 차징 샷)")]
    [Range(1, 2)] public int attackMode = 1;

    [Header("Charge Attack Settings")]
    [Tooltip("차징 완료에 필요한 시간 (초, 기본값: 1.0)")]
    public float chargeDuration = 1.0f;
    [Tooltip("차징 샷 폭발 반경 (범위, 기본값: 2.5)")]
    public float chargeAttackRadius = 2.5f;
    [Tooltip("차징 샷 성공 시 정화/힐량 (기본값: 2.0)")]
    public float chargeAttackHealAmount = 2.0f;
    [Tooltip("차징 샷 발사 시 소모될 물감량 (기본값: 0.2, maxPaint는 1f)")]
    public float chargePaintCost = 0.2f;
    [Tooltip("차징 중 물감 소모 비율 (기존 소모량 대비 배율, 0.3 = 30% 소모)")]
    public float chargeDepletionMultiplier = 0.3f;
    [Tooltip("차징 완료 후 발사 대기(Aim/Hold) 중일 때의 물감 소모 비율 (기존 소모량 대비 배율, 기본값: 0.3)")]
    public float chargeHoldDepletionMultiplier = 0.3f;

    [Header("Charge Visual Effect Settings")]
    [Tooltip("차징 시 마우스 커서 위치에 나타나서 크기가 변할 스프라이트 렌더러")]
    [SerializeField] private SpriteRenderer chargeEffectSprite;
    [Tooltip("차징 시작 시 이펙트의 최소 크기 (스케일)")]
    [SerializeField] private float minChargeVisualScale = 0.2f;
    [Tooltip("차징 완료(1초) 시 이펙트의 최대 크기 (스케일)")]
    [SerializeField] private float maxChargeVisualScale = 1.5f;
    [Tooltip("마우스를 뗄 때 이펙트가 순간적으로 커질 비율 배율 (예: 2.5 = 기존의 2.5배로 커짐)")]
    [SerializeField] private float releaseScaleMultiplier = 2.5f;
    [Tooltip("마우스를 뗄 때 스르륵 투명해지며 커지는 연출 시간 (초)")]
    [SerializeField] private float releaseFadeDuration = 0.2f;

    [Header("Super Ultimate Settings")]
    [Tooltip("궁극기 별똥별 프리팹 (지정하지 않을 경우 구체 오브젝트 자동 생성)")]
    [SerializeField] private GameObject meteorPrefab;
    [Tooltip("별똥별 낙하 속도 (기본값: 15.0)")]
    [SerializeField] private float meteorSpeed = 15f;
    [Tooltip("별똥별 지형 충돌 시 폭발 피해 반경 (기본값: 1.8)")]
    [SerializeField] private float meteorExplosionRadius = 1.8f;

    [Header("Charge Attack Black Fog Settings")]
    [Tooltip("2번 공격 폭발 시 검은 안개(BlackFog)를 부드럽게 밀어낼 거리 수치 (기본값: 2.5)")]
    [SerializeField] private float chargeFogPushAmount = 2.5f;

    [Header("Super Ultimate Spawn Settings (Min/Max Ranges)")]
    [Tooltip("별똥별 낙하 대상 최소 범위 (플레이어 기준 최소 빗겨날 거리, 기본값: 2.0)")]
    [SerializeField] private float minSpawnRange = 2.0f;
    [Tooltip("별똥별 낙하 대상 최대 범위 (플레이어 기준 최대 빗겨날 거리, 기본값: 7.0)")]
    [SerializeField] private float maxSpawnRange = 7.0f;
    [Tooltip("별똥별 스폰 높이 (타겟 지점 기준 하늘 Y축 거리, 기본값: 9.0)")]
    [SerializeField] private float spawnHeight = 9.0f;
    [Tooltip("사선 낙하를 위한 수평 오프셋 (스폰 지점이 타겟 지점보다 좌/우로 빗겨나 시작하는 거리)")]
    [SerializeField] private float diagonalHorizontalOffset = 4.0f;

    private int currentCursorIndex = -1;
    private GaugeController gaugeController; // 물감 게이지 스크립트 참조
    private PlayerHealth playerHealth; // 플레이어 체력 스크립트 참조
    private GaugeVisualFeedback gaugeFeedback; // [추가] 물감 게이지 비주얼 피드백 참조

    // 차징용 내부 변수
    private float chargeTimer = 0f;
    public bool IsChargeCompleted => (attackMode == 2) && (chargeTimer >= chargeDuration);

    private Coroutine releaseEffectCoroutine;
    private Color originalSpriteColor;
    private bool wasDrawingLastFrame = false; // [추가] 이전 프레임 그리기/차징 여부 기억 변수
    private float chatterTimer = 0f;          // [추가] 마우스 튐(채터링) 방지용 타이머
    private bool isActuallyCharging = false;  // [추가] 현재 마우스 클릭 상태와 무관하게 논리적으로 차징 중인지 여부
    private const float CHATTER_GRACE_TIME = 0.05f; // [추가] 마우스 클릭 튐 유예 시간 (0.05초)
    
    private Vector3 lastMouseScreenPos;      // [추가] 마우스 움직임 감지용 이전 프레임 위치
    private float mouseMoveLingerTimer = 0f; // [추가] 마우스 움직임 판정 잔존 타이머 (0.15초)

    void Start()
    {
        if (trail == null) trail = GetComponent<TrailRenderer>();
        if (trail != null)
        {
            trail.emitting = false;
            trail.widthMultiplier = trailWidth;
        }

        // 시작 시 차징 이펙트 스프라이트는 꺼둡니다.
        if (chargeEffectSprite != null)
        {
            originalSpriteColor = chargeEffectSprite.color;
            chargeEffectSprite.gameObject.SetActive(false);
        }

        // 씬에서 게이지 및 체력 컨트롤러 검색 및 참조
        gaugeController = FindFirstObjectByType<GaugeController>();
        playerHealth = FindFirstObjectByType<PlayerHealth>();
        gaugeFeedback = FindFirstObjectByType<GaugeVisualFeedback>(); // 피드백 컴포넌트 탐색

        // 마우스 시작 위치 기록
        lastMouseScreenPos = Input.mousePosition;
#if ENABLE_INPUT_SYSTEM
        if (Mouse.current != null)
        {
            lastMouseScreenPos = Mouse.current.position.ReadValue();
        }
#endif
    }

    void Update()
    {
        // 마우스 실시간 움직임 감지 및 0.15초 판정 유지
        Vector3 currentMouseScreenPos = Input.mousePosition;
#if ENABLE_INPUT_SYSTEM
        if (Mouse.current != null)
        {
            currentMouseScreenPos = Mouse.current.position.ReadValue();
        }
#endif
        float deltaMouse = Vector3.Distance(currentMouseScreenPos, lastMouseScreenPos);
        lastMouseScreenPos = currentMouseScreenPos;

        if (deltaMouse > 0.1f) // 미세 잡음 방지용 0.1 픽셀 문턱값
        {
            mouseMoveLingerTimer = 0.15f; // 움직임 판정 0.15초 동안 유지
        }
        else
        {
            mouseMoveLingerTimer -= Time.deltaTime;
        }

        // 1. 마우스 위치 이동
#if ENABLE_INPUT_SYSTEM
        Vector3 mouseScreenPos = Mouse.current != null ? (Vector3)Mouse.current.position.ReadValue() : Input.mousePosition;
#else
        Vector3 mouseScreenPos = Input.mousePosition;
#endif
        Vector3 mouseWorldPos = Camera.main.ScreenToWorldPoint(mouseScreenPos);
        mouseWorldPos.z = 0;
        transform.position = mouseWorldPos;

        // 2. 거리 측정
        float distance = Vector2.Distance(player.position, transform.position);

        // 3. 거리 단계 구분 (0:근, 1:중, 2:원)
        int nextIndex = (distance < mediumDistance) ? 0 : (distance < intenseDistance) ? 1 : 2;

        // 4. 인덱스 변경 시 커서 변경 및 트레일 스타일 업데이트
        if (nextIndex != currentCursorIndex)
        {
            if (cursorTextures != null && nextIndex < cursorTextures.Count)
            {
                Cursor.SetCursor(cursorTextures[nextIndex], hotSpot, CursorMode.Auto);
            }
            UpdateTrailStyle(nextIndex);
            currentCursorIndex = nextIndex;
        }

        // 5. 공격 모드 스와핑 감지 (KeyBindManager 연동)
        KeyCode changeAttackKey = (KeyBindManager.Instance != null) ? KeyBindManager.Instance.ChangeAttackKey : KeyCode.E;
        bool isEKeyPressed = Input.GetKeyDown(changeAttackKey);
        if (isEKeyPressed)
        {
            attackMode = (attackMode == 1) ? 2 : 1;
            ResetCharge();
            Debug.Log($"[공격 모드 스왑] 현재 모드: {attackMode}번 (1: 일반 브러시, 2: 차징 샷)");
        }

        // 6. 궁극기 발사 감지 (마우스 우클릭 입력)
        bool isRightClickPressed = false;
#if ENABLE_INPUT_SYSTEM
        if (Mouse.current != null)
        {
            isRightClickPressed = Mouse.current.rightButton.wasPressedThisFrame;
        }
#else
        isRightClickPressed = Input.GetMouseButtonDown(1);
#endif

        // 궁극기 게이지가 완충된 상태에서 우클릭 입력 시 실행
        if (isRightClickPressed && SuperGaugeController.Instance != null && SuperGaugeController.Instance.IsFullyCharged)
        {
            SuperGaugeController.Instance.UseSuper();
            StartCoroutine(SpawnMeteorShowerRoutine(mouseWorldPos));
        }

        // 7. 물감 상태 및 조건 계산
        bool hasPaint = false;
        bool needsReclick = false;
        if (gaugeController != null)
        {
            hasPaint = gaugeController.currentPaint >= gaugeController.minPaintToDraw; // 최소 기준치 이상
            needsReclick = gaugeController.NeedsReclick;
        }

        // 사망 여부 및 피격 시 그리기 끊김(0.01초) 상태 감지
        bool isDead = playerHealth != null && playerHealth.IsDead;
        bool isDrawBlocked = playerHealth != null && playerHealth.IsDrawBlocked;

        bool isLeftClickHeld = false;
        bool isLeftClickReleased = false;
        bool isLeftClickDown = false;
#if ENABLE_INPUT_SYSTEM
        if (Mouse.current != null)
        {
            isLeftClickHeld = Mouse.current.leftButton.isPressed;
            isLeftClickReleased = Mouse.current.leftButton.wasReleasedThisFrame;
            isLeftClickDown = Mouse.current.leftButton.wasPressedThisFrame;
        }
#else
        isLeftClickHeld = Input.GetMouseButton(0);
        isLeftClickReleased = Input.GetMouseButtonUp(0);
        isLeftClickDown = Input.GetMouseButtonDown(0);
#endif

        // [효과음 연동] 물감이 부족한 상태에서 그리기를 시도할 때 단발성 경고음 출력 (점묘화 모드 20% 제한 대응)
        bool isPaintLackForAttack = false;
        if (attackMode == 2)
        {
            // 2번 점묘화(차징) 모드는 시작 시 chargePaintCost (20%) 이상이 필요합니다.
            isPaintLackForAttack = (gaugeController != null && gaugeController.currentPaint < chargePaintCost);
        }
        else
        {
            // 1번 일반 모드는 최소 그리기 가능 선인 hasPaint (minPaintToDraw) 조건 적용
            isPaintLackForAttack = !hasPaint;
        }

        if (isLeftClickDown && isPaintLackForAttack && !isDead && !isDrawBlocked)
        {
            if (SoundManager.Instance != null)
            {
                SoundManager.Instance.PlaySFX(SoundManager.SFXType.NoPaint, 0.75f);
            }
            if (gaugeFeedback != null)
            {
                gaugeFeedback.TriggerFeedback(); // [시각적 피드백] 점멸 및 흔들림 재생
            }
        }

        bool canDraw = false;

        // ==========================================
        // [공격 방식에 따른 제어]
        // ==========================================
        if (attackMode == 2)
        {
            // 2번 방식: 1초간 마우스 차징 후 손을 뗄 때 공격
            canDraw = false; // 차징 중에는 일반 붓질 트레일을 그리지 않음

            // [버그 수정] 차징을 시작할 때(timer == 0)는 chargePaintCost 이상의 물감이 있어야 하나, 
            // 이미 차징을 시작해서 진행 중일 때는 물감이 minPaintToDraw(0.02) 이하로 완전히 바닥나지 않는 한 차징 상태를 계속 유지합니다.
            bool canStartOrContinueCharge = false;
            if (gaugeController != null)
            {
                if (chargeTimer > 0f)
                {
                    canStartOrContinueCharge = gaugeController.currentPaint >= gaugeController.minPaintToDraw;
                }
                else
                {
                    canStartOrContinueCharge = gaugeController.currentPaint >= chargePaintCost;
                }
            }

            // 차징 조건 충족 여부
            bool isConditionMet = canStartOrContinueCharge && !needsReclick && !isDead && !isDrawBlocked;

            if (isLeftClickHeld && isConditionMet)
            {
                // 실시간 차징 상태 활성화 및 튐 타이머 리셋
                isActuallyCharging = true;
                chatterTimer = 0f;

                // 이전 발사 코루틴이 돌고 있다면 즉시 멈추고 초기화
                if (releaseEffectCoroutine != null)
                {
                    StopCoroutine(releaseEffectCoroutine);
                    releaseEffectCoroutine = null;
                }

                // 차징 타이머 증가 및 한계값 클램프
                chargeTimer += Time.deltaTime;
                float progress = Mathf.Clamp01(chargeTimer / chargeDuration);

                // 차징 이펙트 시각적 업데이트 (크기 점점 키우기)
                if (chargeEffectSprite != null)
                {
                    chargeEffectSprite.gameObject.SetActive(true);
                    float currentScale = Mathf.Lerp(minChargeVisualScale, maxChargeVisualScale, progress);
                    chargeEffectSprite.transform.localScale = new Vector3(currentScale, currentScale, 1f);

                    // 1번 모드처럼 거리 단계별로 차징 이펙트 이미지의 기본 투명도(Alpha)를 차등 적용
                    float[] alphas = { 1.0f, 0.6f, 0.3f };
                    float distanceAlpha = 1.0f;
                    if (currentCursorIndex >= 0 && currentCursorIndex < alphas.Length)
                    {
                        distanceAlpha = alphas[currentCursorIndex];
                    }

                    Color tempColor = originalSpriteColor;
                    tempColor.a = distanceAlpha * 0.85f; // 플레이어와 마우스 거리가 멀어질수록 흐려집니다.
                    chargeEffectSprite.color = tempColor;
                }

                if (chargeTimer >= chargeDuration)
                {
                    Debug.Log("[CursorController] 차징 완료! 마우스를 떼면 발사합니다.");
                }
            }
            else if (isActuallyCharging)
            {
                // 차징 중이었으나 마우스 클릭이 순간 튀었거나 조건이 깨진 경우 ➔ 0.08초 동안 마우스 복구 대기
                chatterTimer += Time.deltaTime;

                // 유예 시간을 초과했거나(마우스 뗌 유지), 피격/사망/물감 완전 고갈 등의 락이 걸린 경우 릴리즈 연출 처리 (임시 튐은 유예시간동안 스무스하게 무시됨)
                if (chatterTimer >= CHATTER_GRACE_TIME || !isConditionMet)
                {
                    isActuallyCharging = false;
                    chatterTimer = 0f;

                    if (chargeTimer >= chargeDuration && !isDead && !isDrawBlocked)
                    {
                        ExecuteChargeAttack(mouseWorldPos);
                        ResetCharge(); // [추가] 발사 즉시 차징 데이터를 리셋하여 빠른 재클릭(따닥) 연사 버그 방지

                        // 손을 뗄 때 투명해지며 확 커지는 코루틴 실행
                        if (releaseEffectCoroutine != null) StopCoroutine(releaseEffectCoroutine);
                        releaseEffectCoroutine = StartCoroutine(ReleaseVisualEffectRoutine());
                    }
                    else
                    {
                        // 완충되지 않았거나 조건이 불충족한 상태에서 떼진 경우 취소
                        ResetCharge();
                    }
                }
            }
            else
            {
                // 차징 중이 아닐 때 클릭을 뗐다면 확실히 초기화
                ResetCharge();
            }
        }
        else
        {
            // 1번 방식: 기존의 붓질 및 정화 힐링 방식 유지
            canDraw = isLeftClickHeld && hasPaint && !needsReclick && !isDead && !isDrawBlocked;
            ResetCharge();

            // 1번 방식일 때만 주변 일반 몬스터 및 물체들 정화/치료 처리
            // 마우스 움직임 판정(mouseMoveLingerTimer > 0f)이 감지되고 있을 때만 실제 정화 피해량이 가해집니다.
            if (canDraw && mouseMoveLingerTimer > 0f)
            {
                float activeHealRate = closeHealRate;
                if (currentCursorIndex == 1) activeHealRate = mediumHealRate;
                else if (currentCursorIndex == 2) activeHealRate = farHealRate;

                ApplyNormalHealing(mouseWorldPos, activeHealRate);
            }
        }

        // 트레일 방출 제어
        if (trail != null)
        {
            trail.emitting = canDraw;
        }

        // [효과음 연동] 그리고 있는 프레임 동안 붓 칠하는 사운드 재생 (매니저 자체 쿨타임으로 시끄러움 예방)
        if (canDraw)
        {
            if (SoundManager.Instance != null)
            {
                SoundManager.Instance.PlaySFX(SoundManager.SFXType.Painting, 0.45f);
            }
        }

        // [효과음 연동] 그리거나 차징을 하던 도중 물감이 완전히 바닥난(동난) 순간 1회 경고음 출력
        bool isCurrentlyDrawingOrCharging = canDraw || (attackMode == 2 && chargeTimer > 0f && isLeftClickHeld);
        if (wasDrawingLastFrame && !isCurrentlyDrawingOrCharging && !hasPaint && !isDead && isLeftClickHeld)
        {
            if (SoundManager.Instance != null)
            {
                SoundManager.Instance.PlaySFX(SoundManager.SFXType.NoPaint, 0.75f);
            }
            if (gaugeFeedback != null)
            {
                gaugeFeedback.TriggerFeedback(); // [시각적 피드백] 점멸 및 흔들림 재생
            }
        }
        wasDrawingLastFrame = isCurrentlyDrawingOrCharging;
    }

    // 1번 모드의 기존 힐링 로직 (위장 상태 몬스터 필터링 및 궁극기 게이지 충전 제외)
    private void ApplyNormalHealing(Vector3 mouseWorldPos, float activeHealRate)
    {
        Collider2D[] hitColliders = Physics2D.OverlapCircleAll(mouseWorldPos, paintRadius);
        HashSet<GameObject> healedObjects = new HashSet<GameObject>(); // 한 프레임에 중복 치료 방지용

        foreach (var hitCollider in hitColliders)
        {
            NormalMonster monster = hitCollider.GetComponent<NormalMonster>();
            if (monster == null)
            {
                monster = hitCollider.GetComponentInParent<NormalMonster>();
            }

            if (monster != null)
            {
                if (healedObjects.Contains(monster.gameObject)) continue;
                healedObjects.Add(monster.gameObject);

                // [수정] H고양이의 잠복(위장) 상태 감지
                H_MonsterMove hMove = monster.GetComponent<H_MonsterMove>();
                if (hMove == null) hMove = monster.GetComponentInParent<H_MonsterMove>();
                bool isAmbushed = hMove != null && hMove.IsAmbushed;

                // 이미 정화 완료(사망)되었거나, 잠복(위장) 상태인 몬스터는 힐과 궁극기 가산에서 배제합니다.
                if (!monster.IsPurified && !isAmbushed)
                {
                    float healAmount = activeHealRate * Time.deltaTime;
                    monster.Heal(healAmount);

                    if (SuperGaugeController.Instance != null)
                    {
                        SuperGaugeController.Instance.AddSuperGauge(healAmount);
                    }
                }
            }
            else
            {
                RoseBush roseBush = hitCollider.GetComponent<RoseBush>();
                if (roseBush == null) roseBush = hitCollider.GetComponentInParent<RoseBush>();

                if (roseBush != null)
                {
                    if (healedObjects.Contains(roseBush.gameObject)) continue;
                    healedObjects.Add(roseBush.gameObject);

                    // 정화 완료된 덤불 배제
                    if (!roseBush.IsPurified)
                    {
                        float healAmount = activeHealRate * Time.deltaTime;
                        roseBush.Heal(healAmount);

                        if (SuperGaugeController.Instance != null)
                        {
                            SuperGaugeController.Instance.AddSuperGauge(healAmount);
                        }
                    }
                }
                else
                {
                    ColoringBridge bridge = hitCollider.GetComponent<ColoringBridge>();
                    if (bridge == null) bridge = hitCollider.GetComponentInParent<ColoringBridge>();

                    if (bridge != null)
                    {
                        if (healedObjects.Contains(bridge.gameObject)) continue;
                        healedObjects.Add(bridge.gameObject);

                        // 정화 완료된 다리 배제
                        if (!bridge.IsPurified)
                        {
                            float healAmount = activeHealRate * Time.deltaTime;
                            bridge.Heal(healAmount);

                            if (SuperGaugeController.Instance != null)
                            {
                                SuperGaugeController.Instance.AddSuperGauge(healAmount);
                            }
                        }
                    }
                    else
                    {
                        Trampoline trampoline = hitCollider.GetComponent<Trampoline>();
                        if (trampoline == null) trampoline = hitCollider.GetComponentInParent<Trampoline>();

                        if (trampoline != null)
                        {
                            if (healedObjects.Contains(trampoline.gameObject)) continue;
                            healedObjects.Add(trampoline.gameObject);

                            // 정화 완료된 트램펄린 배제
                            if (!trampoline.IsPurified)
                            {
                                float healAmount = activeHealRate * Time.deltaTime;
                                trampoline.Heal(healAmount);

                                if (SuperGaugeController.Instance != null)
                                {
                                    SuperGaugeController.Instance.AddSuperGauge(healAmount);
                                }
                            }
                        }
                        else
                        {
                            PuzzleLamp lamp = hitCollider.GetComponent<PuzzleLamp>();
                            if (lamp == null) lamp = hitCollider.GetComponentInParent<PuzzleLamp>();

                            if (lamp != null)
                            {
                                if (healedObjects.Contains(lamp.gameObject)) continue;
                                healedObjects.Add(lamp.gameObject);

                                // 정화 완료된 등불 배제
                                if (!lamp.IsPurified)
                                {
                                    float healAmount = activeHealRate * Time.deltaTime;
                                    lamp.Heal(healAmount);

                                    if (SuperGaugeController.Instance != null)
                                    {
                                        SuperGaugeController.Instance.AddSuperGauge(healAmount);
                                    }
                                }
                            }
                        }
                    }
                }
            }
        }
    }

    // 2번 모드의 차징 범위 샷 공격 실행 (위장 상태 몬스터 필터링 및 궁극기 게이지 충전 제외)
    private void ExecuteChargeAttack(Vector3 attackPos)
    {
        // 1. 물감 잔량 강제 소모 (GaugeController 연동)
        if (gaugeController != null)
        {
            gaugeController.currentPaint -= chargePaintCost;
            gaugeController.currentPaint = Mathf.Clamp(gaugeController.currentPaint, 0f, gaugeController.maxPaint);
        }

        // 2. 소리 효과음 재생 (SoundManager 연동)
        if (SoundManager.Instance != null)
        {
            // Resources/SoundResource 폴더에 해당 사운드 클립이 존재한다면 재생
            // 예: SoundManager.Instance.PlaySFX("ChargeBoom");
        }

        // 2번 모드 차징 공격도 플레이어와 마우스 간 거리에 따라 정화량(피해량) 감소 배율 적용
        float[] damageMultipliers = { 1.0f, 0.7f, 0.4f };
        float activeChargeHeal = chargeAttackHealAmount;
        if (currentCursorIndex >= 0 && currentCursorIndex < damageMultipliers.Length)
        {
            activeChargeHeal = chargeAttackHealAmount * damageMultipliers[currentCursorIndex];
        }

        // 3. 범위 정화/힐 일괄 적용
        Collider2D[] hitColliders = Physics2D.OverlapCircleAll(attackPos, chargeAttackRadius);
        HashSet<GameObject> healedObjects = new HashSet<GameObject>(); // 중복 치료 방지용
        int hitCount = 0;

        foreach (var hitCollider in hitColliders)
        {
            NormalMonster monster = hitCollider.GetComponent<NormalMonster>();
            if (monster == null) monster = hitCollider.GetComponentInParent<NormalMonster>();

            if (monster != null)
            {
                if (healedObjects.Contains(monster.gameObject)) continue;
                healedObjects.Add(monster.gameObject);

                // [수정] H고양이의 잠복(위장) 상태 감지
                H_MonsterMove hMove = monster.GetComponent<H_MonsterMove>();
                if (hMove == null) hMove = monster.GetComponentInParent<H_MonsterMove>();
                bool isAmbushed = hMove != null && hMove.IsAmbushed;

                // 이미 정화 완료(사망)되었거나 잠복(위장) 상태인 몬스터는 힐과 궁극기 가산에서 배제
                if (!monster.IsPurified && !isAmbushed)
                {
                    monster.Heal(activeChargeHeal);

                    // 궁극기 게이지 가산
                    if (SuperGaugeController.Instance != null)
                    {
                        SuperGaugeController.Instance.AddSuperGauge(activeChargeHeal);
                    }
                }
                hitCount++;
            }
            else
            {
                RoseBush roseBush = hitCollider.GetComponent<RoseBush>();
                if (roseBush == null) roseBush = hitCollider.GetComponentInParent<RoseBush>();
                if (roseBush != null)
                {
                    if (healedObjects.Contains(roseBush.gameObject)) continue;
                    healedObjects.Add(roseBush.gameObject);

                    // 정화 완료된 덤불 배제
                    if (!roseBush.IsPurified)
                    {
                        roseBush.Heal(activeChargeHeal);

                        if (SuperGaugeController.Instance != null)
                        {
                            SuperGaugeController.Instance.AddSuperGauge(activeChargeHeal);
                        }
                    }
                    hitCount++;
                    continue;
                }

                ColoringBridge bridge = hitCollider.GetComponent<ColoringBridge>();
                if (bridge == null) bridge = hitCollider.GetComponentInParent<ColoringBridge>();
                if (bridge != null)
                {
                    if (healedObjects.Contains(bridge.gameObject)) continue;
                    healedObjects.Add(bridge.gameObject);

                    // 정화 완료된 다리 배제
                    if (!bridge.IsPurified)
                    {
                        bridge.Heal(activeChargeHeal);

                        if (SuperGaugeController.Instance != null)
                        {
                            SuperGaugeController.Instance.AddSuperGauge(activeChargeHeal);
                        }
                    }
                    hitCount++;
                    continue;
                }

                Trampoline trampoline = hitCollider.GetComponent<Trampoline>();
                if (trampoline == null) trampoline = hitCollider.GetComponentInParent<Trampoline>();
                if (trampoline != null)
                {
                    if (healedObjects.Contains(trampoline.gameObject)) continue;
                    healedObjects.Add(trampoline.gameObject);

                    // 정화 완료된 트램펄린 배제
                    if (!trampoline.IsPurified)
                    {
                        trampoline.Heal(activeChargeHeal);

                        if (SuperGaugeController.Instance != null)
                        {
                            SuperGaugeController.Instance.AddSuperGauge(activeChargeHeal);
                        }
                    }
                    hitCount++;
                    continue;
                }

                PuzzleLamp lamp = hitCollider.GetComponent<PuzzleLamp>();
                if (lamp == null) lamp = hitCollider.GetComponentInParent<PuzzleLamp>();
                if (lamp != null)
                {
                    if (healedObjects.Contains(lamp.gameObject)) continue;
                    healedObjects.Add(lamp.gameObject);

                    // 정화 완료된 등불 배제
                    if (!lamp.IsPurified)
                    {
                        lamp.Heal(activeChargeHeal);

                        if (SuperGaugeController.Instance != null)
                        {
                            SuperGaugeController.Instance.AddSuperGauge(activeChargeHeal);
                        }
                    }
                    hitCount++;
                    continue;
                }

                // [추가] 검은 안개(BlackFog) 감지 및 인스펙터 지정 거리만큼 즉시 밀어내기 적용
                BlackFog fog = hitCollider.GetComponent<BlackFog>();
                if (fog == null) fog = hitCollider.GetComponentInParent<BlackFog>();
                if (fog != null)
                {
                    if (healedObjects.Contains(fog.gameObject)) continue;
                    healedObjects.Add(fog.gameObject);

                    fog.PushBack(chargeFogPushAmount);
                    hitCount++;
                    continue;
                }
            }
        }

        Debug.Log($"[차징 샷 발사] {activeChargeHeal} (거리 배율 반영) 정화 적용! (타격 수: {hitCount}개, 소모 물감: {chargePaintCost})");
    }

    // 마우스를 뗄 때 이펙트가 투명해지며 확 커지며 흩어지는 코루틴
    private IEnumerator ReleaseVisualEffectRoutine()
    {
        if (chargeEffectSprite == null) yield break;

        Vector3 startScale = chargeEffectSprite.transform.localScale;
        Vector3 targetScale = startScale * releaseScaleMultiplier;
        Color startColor = chargeEffectSprite.color;
        float elapsedTime = 0f;

        while (elapsedTime < releaseFadeDuration)
        {
            elapsedTime += Time.deltaTime;
            float t = elapsedTime / releaseFadeDuration;

            // 크기 증가 (Lerp)
            chargeEffectSprite.transform.localScale = Vector3.Lerp(startScale, targetScale, t);

            // 알파값 감소 (투명도가 점점 높아지며 스르륵 사라짐)
            float newAlpha = Mathf.Lerp(startColor.a, 0f, t);
            chargeEffectSprite.color = new Color(startColor.r, startColor.g, startColor.b, newAlpha);

            yield return null;
        }

        chargeEffectSprite.gameObject.SetActive(false);
        releaseEffectCoroutine = null;
    }

    // ==========================================================
    // [궁극기: 별똥별 투하 연출 코루틴 (6방 순차 폭격으로 변경)]
    // ==========================================================
    private IEnumerator SpawnMeteorShowerRoutine(Vector3 mouseTargetPos)
    {
        Debug.Log("[궁극기 발동] 하늘에서 무지개 별똥별 샤워가 6방 내립니다!");

        // 패턴 (1타:마우스, 2타:왼쪽, 3타:오른쪽)을 2회 돌려 총 6방 생성
        for (int round = 0; round < 2; round++)
        {
            // 1/4타. 우클릭 누른 마우스 좌표로 1번째 별똥별 낙하 (왼쪽 위 -> 오른쪽 아래 사선)
            Vector3 spawnPos1 = mouseTargetPos + new Vector3(-diagonalHorizontalOffset, spawnHeight, 0f);
            Vector3 dir1 = (mouseTargetPos - spawnPos1).normalized;
            SpawnMeteor(spawnPos1, dir1);

            yield return new WaitForSeconds(0.2f);

            // 2/5타. 플레이어 기준 왼쪽 (최소 ~ 최대) 범위 내 랜덤 위치로 2번째 별똥별 사선 낙하 (왼쪽 위 -> 오른쪽 아래 사선)
            float targetX2 = player.position.x - Random.Range(minSpawnRange, maxSpawnRange);
            float targetY2 = player.position.y;
            Vector3 targetPos2 = new Vector3(targetX2, targetY2, 0f);
            Vector3 spawnPos2 = targetPos2 + new Vector3(-diagonalHorizontalOffset, spawnHeight, 0f);
            Vector3 dir2 = (targetPos2 - spawnPos2).normalized;
            SpawnMeteor(spawnPos2, dir2);

            yield return new WaitForSeconds(0.2f);

            // 3/6타. 플레이어 기준 오른쪽 (최소 ~ 최대) 범위 내 랜덤 위치로 3번째 별똥별 사선 낙하 (왼쪽 위 -> 오른쪽 아래 사선)
            float targetX3 = player.position.x + Random.Range(minSpawnRange, maxSpawnRange);
            float targetY3 = player.position.y;
            Vector3 targetPos3 = new Vector3(targetX3, targetY3, 0f);
            Vector3 spawnPos3 = targetPos3 + new Vector3(-diagonalHorizontalOffset, spawnHeight, 0f);
            Vector3 dir3 = (targetPos3 - spawnPos3).normalized;
            SpawnMeteor(spawnPos3, dir3);

            // 3타를 발사하고 4타로 넘어가기 전에도 0.2초 대기
            if (round == 0)
            {
                yield return new WaitForSeconds(0.2f);
            }
        }
    }

    // 개별 별똥별 스폰 및 초기화 헬퍼 함수
    private void SpawnMeteor(Vector3 spawnPos, Vector3 direction)
    {
        GameObject meteorObj;

        if (meteorPrefab != null)
        {
            meteorObj = Instantiate(meteorPrefab, spawnPos, Quaternion.identity);
        }
        else
        {
            // [방어 코드] 만약 별똥별 프리팹을 인스펙터에 지정하지 않았을 경우, 임시 구체를 생성해 줍니다.
            meteorObj = new GameObject("TempMeteor");
            meteorObj.transform.position = spawnPos;
            meteorObj.transform.localScale = Vector3.one * 0.7f;

            SpriteRenderer sr = meteorObj.AddComponent<SpriteRenderer>();
            sr.sprite = CreateTempMeteorSprite();
            sr.color = Color.yellow; // 노란색 별똥별 색상 지정
            sr.sortingOrder = 100;   // 배경이나 지형 뒤로 가려지지 않도록 정렬 순서를 높임

            CircleCollider2D col2D = meteorObj.AddComponent<CircleCollider2D>();
            col2D.isTrigger = true;

            // [추가] 유니티 2D 물리 충돌 판정을 발동시키기 위해 Rigidbody2D 부착 (지형 뚫림 방지)
            Rigidbody2D rb = meteorObj.AddComponent<Rigidbody2D>();
            rb.bodyType = RigidbodyType2D.Kinematic; // 스크립트 등속 운동 제어를 위해 Kinematic 설정
            rb.simulated = true;
            rb.collisionDetectionMode = CollisionDetectionMode2D.Continuous; // 빠른 속도 낙하 시 통과 감지 방지
        }

        // Meteor 스크립트가 붙어있는지 확인 후 부착 및 초기화
        Meteor meteorComponent = meteorObj.GetComponent<Meteor>();
        if (meteorComponent == null)
        {
            meteorComponent = meteorObj.AddComponent<Meteor>();
        }

        meteorComponent.Initialize(direction, meteorSpeed, meteorExplosionRadius);
    }

    // 임시 노란색 원형 Sprite 동적 생성 함수 (유니티 CreatePrimitive Assertion 에디터 오류 완벽 우회)
    private Sprite CreateTempMeteorSprite()
    {
        Texture2D texture = new Texture2D(32, 32);
        Color[] colors = new Color[32 * 32];
        for (int y = 0; y < 32; y++)
        {
            for (int x = 0; x < 32; x++)
            {
                float dx = x - 15.5f;
                float dy = y - 15.5f;
                if (dx * dx + dy * dy <= 15.5f * 15.5f)
                {
                    colors[y * 32 + x] = Color.white;
                }
                else
                {
                    colors[y * 32 + x] = Color.clear;
                }
            }
        }
        texture.SetPixels(colors);
        texture.Apply();
        return Sprite.Create(texture, new Rect(0, 0, 32, 32), new Vector2(0.5f, 0.5f), 32f);
    }

    private void ResetCharge(bool forceCancel = false)
    {
        chargeTimer = 0f;
        chatterTimer = 0f;
        isActuallyCharging = false;

        // [수정] 강제 취소가 아니고 릴리즈 연출 코루틴(부드러운 소멸)이 재생 중인 동안에는 강제 종료 및 비활성화를 건너뜁니다!
        if (!forceCancel && releaseEffectCoroutine != null)
        {
            return;
        }

        // 발사 연출 코루틴이 돌고 있다면 강제 취소합니다.
        if (releaseEffectCoroutine != null)
        {
            StopCoroutine(releaseEffectCoroutine);
            releaseEffectCoroutine = null;
        }

        // 강제로 차징 구체 스프라이트를 꺼 줍니다.
        if (chargeEffectSprite != null)
        {
            chargeEffectSprite.gameObject.SetActive(false);
        }
    }

    private void OnDisable()
    {
        // 씬 전환, 비활성화 시 차징 상태를 강제로 완전히 리셋하여 이펙트 박제 방지
        ResetCharge(true);
    }

    void UpdateTrailStyle(int index)
    {
        if (trail == null) return;

        // 거리 단계에 따른 투명도 설정
        float[] alphas = { 1.0f, 0.6f, 0.3f };
        if (index >= alphas.Length) return;
        float targetAlpha = alphas[index];

        // 거리 단계에 따른 색상 강도(Intensity) 설정 (가까움: 100%, 중간: 75%, 멈: 50%)
        float[] colorIntensities = { 1.0f, 0.75f, 0.5f };
        float intensity = colorIntensities[index];

        // 그라데이션 설정
        Gradient gradient = new Gradient();

        // 색상 키 설정 (무지개색 그라데이션에 강도 보정 적용 - 색이 연해지는 파스텔 효과)
        GradientColorKey[] colorKeys = new GradientColorKey[7];
        colorKeys[0] = new GradientColorKey(GetAdjustedColor(Color.red, intensity), 0.0f);
        colorKeys[1] = new GradientColorKey(GetAdjustedColor(new Color(1f, 0.5f, 0f), intensity), 0.16f); // 주황
        colorKeys[2] = new GradientColorKey(GetAdjustedColor(Color.yellow, intensity), 0.33f);
        colorKeys[3] = new GradientColorKey(GetAdjustedColor(Color.green, intensity), 0.5f);
        colorKeys[4] = new GradientColorKey(GetAdjustedColor(Color.blue, intensity), 0.66f);
        colorKeys[5] = new GradientColorKey(GetAdjustedColor(new Color(0.29f, 0f, 0.51f), intensity), 0.83f); // 남색
        colorKeys[6] = new GradientColorKey(GetAdjustedColor(new Color(0.56f, 0f, 1f), intensity), 1.0f); // 보라

        // 투명도 키 설정 (뒤로 갈수록 투명해짐)
        GradientAlphaKey[] alphaKeys = new GradientAlphaKey[2];
        alphaKeys[0] = new GradientAlphaKey(targetAlpha, 0.0f);
        alphaKeys[1] = new GradientAlphaKey(0.0f, 1.0f); // 끝부분 투명

        // 그라데이션 키 설정
        gradient.SetKeys(colorKeys, alphaKeys);
        trail.colorGradient = gradient;
    }

    private Color GetAdjustedColor(Color original, float intensity)
    {
        return Color.Lerp(original, Color.white, 1f - intensity);
    }

    // 붓질(힐) 반경 및 차징 공격 반경을 에디터 씬 뷰에 표시해 주는 기즈모 함수
    private void OnDrawGizmosSelected()
    {
        // 1번 모드 브러시 범위 (주황색)
        Gizmos.color = new Color(1f, 0.5f, 0f, 0.4f);
        Gizmos.DrawWireSphere(transform.position, paintRadius);

        // 2번 모드 차징 범위 (하늘색)
        Gizmos.color = new Color(0f, 0.7f, 1f, 0.4f);
        Gizmos.DrawWireSphere(transform.position, chargeAttackRadius);
    }
}