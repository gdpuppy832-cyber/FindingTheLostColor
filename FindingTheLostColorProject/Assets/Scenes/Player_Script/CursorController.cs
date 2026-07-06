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
    [Tooltip("차징 샷 성공 시 정화/힐량 (기본값: 8.0)")]
    public float chargeAttackHealAmount = 8.0f;
    [Tooltip("차징 샷 발사 시 소모될 물감량 (기본값: 0.2, maxPaint는 1f)")]
    public float chargePaintCost = 0.2f;
    [Tooltip("차징 중 물감 소모 비율 (기존 소모량 대비 배율, 0.3 = 30% 소모)")]
    public float chargeDepletionMultiplier = 0.3f;

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

    private int currentCursorIndex = -1;
    private GaugeController gaugeController; // 물감 게이지 스크립트 참조
    private PlayerHealth playerHealth; // 플레이어 체력 스크립트 참조

    // 차징용 내부 변수
    private float chargeTimer = 0f;
    public bool IsChargeCompleted => (attackMode == 2) && (chargeTimer >= chargeDuration);

    private Coroutine releaseEffectCoroutine;
    private Color originalSpriteColor;

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
    }

    void Update()
    {
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

        // 5. 공격 모드 스와핑 감지 (E키 입력)
        bool isEKeyPressed = false;
#if ENABLE_INPUT_SYSTEM
        if (Keyboard.current != null)
        {
            isEKeyPressed = Keyboard.current.eKey.wasPressedThisFrame;
        }
#else
        isEKeyPressed = Input.GetKeyDown(KeyCode.E);
#endif
        if (isEKeyPressed)
        {
            attackMode = (attackMode == 1) ? 2 : 1;
            ResetCharge();
            Debug.Log($"[공격 모드 스왑] 현재 모드: {attackMode}번 (1: 일반 브러시, 2: 차징 샷)");
        }

        // 6. 물감 상태 및 조건 계산
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
#if ENABLE_INPUT_SYSTEM
        if (Mouse.current != null)
        {
            isLeftClickHeld = Mouse.current.leftButton.isPressed;
            isLeftClickReleased = Mouse.current.leftButton.wasReleasedThisFrame;
        }
#else
        isLeftClickHeld = Input.GetMouseButton(0);
        isLeftClickReleased = Input.GetMouseButtonUp(0);
#endif

        bool canDraw = false;

        // ==========================================
        // [공격 방식에 따른 제어]
        // ==========================================
        if (attackMode == 2)
        {
            // 2번 방식: 1초간 마우스 차징 후 손을 뗄 때 공격
            canDraw = false; // 차징 중에는 일반 붓질 트레일을 그리지 않음

            // 발사에 필요한 물감 비용(chargePaintCost)보다 현재 게이지가 많을 때만 차징을 시작할 수 있음
            bool hasEnoughPaintForCharge = gaugeController != null && gaugeController.currentPaint >= chargePaintCost;

            if (isLeftClickHeld && hasEnoughPaintForCharge && !needsReclick && !isDead && !isDrawBlocked)
            {
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

                    // [수정] 1번 모드처럼 거리 단계별로 차징 이펙트 이미지의 기본 투명도(Alpha)를 차등 적용
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
            else
            {
                // 누르고 있지 않거나 조건이 불충족되면 차징 타이머 초기화 (단, Up하는 프레임에는 발사 처리를 위해 예외)
                if (!isLeftClickReleased)
                {
                    ResetCharge();
                }
            }

            // 마우스 버튼에서 손을 떼었을 때 (차징 샷 발사)
            if (isLeftClickReleased)
            {
                if (chargeTimer >= chargeDuration && !isDead && !isDrawBlocked)
                {
                    ExecuteChargeAttack(mouseWorldPos);

                    // 손을 뗄 때 투명해지며 확 커지는 코루틴 실행
                    if (releaseEffectCoroutine != null) StopCoroutine(releaseEffectCoroutine);
                    releaseEffectCoroutine = StartCoroutine(ReleaseVisualEffectRoutine());
                }
                else
                {
                    // 차징이 미완성인 채 뗐다면 이펙트 즉시 끄기
                    ResetCharge();
                }
            }
        }
        else
        {
            // 1번 방식: 기존의 붓질 및 정화 힐링 방식 유지
            canDraw = isLeftClickHeld && hasPaint && !needsReclick && !isDead && !isDrawBlocked;
            ResetCharge();

            // 1번 방식일 때만 주변 일반 몬스터 및 물체들 정화/치료 처리
            if (canDraw)
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
    }

    // 1번 모드의 기존 힐링 로직 분리 (중복 콜라이더에 의한 중첩 정화 버그 해결)
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

                monster.Heal(activeHealRate * Time.deltaTime);
            }
            else
            {
                RoseBush roseBush = hitCollider.GetComponent<RoseBush>();
                if (roseBush == null) roseBush = hitCollider.GetComponentInParent<RoseBush>();

                if (roseBush != null)
                {
                    if (healedObjects.Contains(roseBush.gameObject)) continue;
                    healedObjects.Add(roseBush.gameObject);

                    roseBush.Heal(activeHealRate * Time.deltaTime);
                }
                else
                {
                    ColoringBridge bridge = hitCollider.GetComponent<ColoringBridge>();
                    if (bridge == null) bridge = hitCollider.GetComponentInParent<ColoringBridge>();

                    if (bridge != null)
                    {
                        if (healedObjects.Contains(bridge.gameObject)) continue;
                        healedObjects.Add(bridge.gameObject);

                        bridge.Heal(activeHealRate * Time.deltaTime);
                    }
                    else
                    {
                        Trampoline trampoline = hitCollider.GetComponent<Trampoline>();
                        if (trampoline == null) trampoline = hitCollider.GetComponentInParent<Trampoline>();

                        if (trampoline != null)
                        {
                            if (healedObjects.Contains(trampoline.gameObject)) continue;
                            healedObjects.Add(trampoline.gameObject);

                            trampoline.Heal(activeHealRate * Time.deltaTime);
                        }
                        else
                        {
                            PuzzleLamp lamp = hitCollider.GetComponent<PuzzleLamp>();
                            if (lamp == null) lamp = hitCollider.GetComponentInParent<PuzzleLamp>();

                            if (lamp != null)
                            {
                                if (healedObjects.Contains(lamp.gameObject)) continue;
                                healedObjects.Add(lamp.gameObject);

                                lamp.Heal(activeHealRate * Time.deltaTime);
                            }
                        }
                    }
                }
            }
        }
    }

    // 2번 모드의 차징 범위 샷 공격 실행 (중복 콜라이더에 의한 중첩 대미지/정화 버그 해결)
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

        // [추가] 2번 모드 차징 공격도 플레이어와 마우스 간 거리에 따라 정화량(피해량) 감소 배율 적용
        // 근거리(0) : 100%, 중거리(1) : 70%, 원거리(2) : 40%
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

                monster.Heal(activeChargeHeal);
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

                    roseBush.Heal(activeChargeHeal);
                    hitCount++;
                    continue;
                }

                ColoringBridge bridge = hitCollider.GetComponent<ColoringBridge>();
                if (bridge == null) bridge = hitCollider.GetComponentInParent<ColoringBridge>();
                if (bridge != null)
                {
                    if (healedObjects.Contains(bridge.gameObject)) continue;
                    healedObjects.Add(bridge.gameObject);

                    bridge.Heal(activeChargeHeal);
                    hitCount++;
                    continue;
                }

                Trampoline trampoline = hitCollider.GetComponent<Trampoline>();
                if (trampoline == null) trampoline = hitCollider.GetComponentInParent<Trampoline>();
                if (trampoline != null)
                {
                    if (healedObjects.Contains(trampoline.gameObject)) continue;
                    healedObjects.Add(trampoline.gameObject);

                    trampoline.Heal(activeChargeHeal);
                    hitCount++;
                    continue;
                }

                PuzzleLamp lamp = hitCollider.GetComponent<PuzzleLamp>();
                if (lamp == null) lamp = hitCollider.GetComponentInParent<PuzzleLamp>();
                if (lamp != null)
                {
                    if (healedObjects.Contains(lamp.gameObject)) continue;
                    healedObjects.Add(lamp.gameObject);

                    lamp.Heal(activeChargeHeal);
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
        chargeTimer = 0f;
        releaseEffectCoroutine = null;
    }

    private void ResetCharge()
    {
        chargeTimer = 0f;

        if (releaseEffectCoroutine == null && chargeEffectSprite != null)
        {
            chargeEffectSprite.gameObject.SetActive(false);
        }
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