using UnityEngine;
using System.Collections.Generic;

public class S_MonsterTrail : MonoBehaviour
{
    [Header("자국 설정")]
    [Tooltip("자국이 유지되는 시간 (초, 기본값: 4.0초)")]
    public float lifetime = 4f;

    [Header("개별 디버프 감쇄 수치 설정 (0.0 ~ 1.0)")]
    [Tooltip("이동 속도 배율 (예: 0.5면 속도가 50% 수준으로 감소)")]
    [Range(0f, 1f)]
    public float speedMultiplier = 0.5f;

    [Tooltip("점프력 배율 (예: 0.7면 점프력이 70% 수준으로 감소)")]
    [Range(0f, 1f)]
    public float jumpMultiplier = 0.5f;

    [Tooltip("물감 자연 회복 속도 배율 (예: 0.4면 회복 속도가 40% 수준으로 감소)")]
    [Range(0f, 1f)]
    public float paintRegenMultiplier = 0.5f;

    [Header("대쉬 제약 설정 (0.0 ~ 1.0)")]
    [Tooltip("슬로우 장판 위에서의 대쉬 효율 (0.0이면 대쉬 차단/추진력 0, 1.0이면 정상 대쉬)")]
    [Range(0f, 1f)]
    public float dashMultiplier = 0f; // 기본값 0 (완전 차단)

    void Start()
    {
        // 4초 뒤 자동으로 오브젝트 파괴 (자국이 사라짐)
        Destroy(gameObject, lifetime);
    }

    void OnTriggerEnter2D(Collider2D other)
    {
        // 태그에만 의존하지 않고 PlayerMove 컴포넌트가 있는지 수색하여 플레이어 판정
        PlayerMove pm = other.GetComponent<PlayerMove>();
        if (pm == null) pm = other.GetComponentInParent<PlayerMove>();
 
        if (pm != null)
        {
            PlayerTrailDebuff debuff = pm.GetComponent<PlayerTrailDebuff>();
            if (debuff == null)
            {
                debuff = pm.gameObject.AddComponent<PlayerTrailDebuff>();
            }
 
            debuff.RegisterTrail(this);
        }
    }

    void OnTriggerExit2D(Collider2D other)
    {
        PlayerMove pm = other.GetComponent<PlayerMove>();
        if (pm == null) pm = other.GetComponentInParent<PlayerMove>();
 
        if (pm != null)
        {
            PlayerTrailDebuff debuff = pm.GetComponent<PlayerTrailDebuff>();
            if (debuff != null)
            {
                debuff.UnregisterTrail(this); // 리스트에서 이 장판 이탈 제거
            }
        }
    }
}

/// <summary>
/// 자국을 밟았을 때 플레이어에게 개별 슬로우/점프력/회복률 디버프를 안전하게 관리하는 컴포넌트
/// (여러 자국에 겹쳐 올라서거나 자국이 밟힌 도중 소멸하는 유니티의 엣지 케이스 버그 완벽 방어형 설계)
/// </summary>
public class PlayerTrailDebuff : MonoBehaviour
{
    private List<S_MonsterTrail> activeTrails = new List<S_MonsterTrail>();
    
    private float originalSpeed;
    private float originalJump;
    
    // 현재 적용 중인 능력치별 디버프 배율 기록 (1.0 = 디버프 없음)
    private float currentAppliedSpeedMultiplier = 1.0f;
    private float currentAppliedJumpMultiplier = 1.0f;
    private float currentAppliedRegenMultiplier = 1.0f;
    private float currentAppliedDashMultiplier = 1.0f; // [추가] 대쉬 효율 배율 기록
    
    private bool isStatBackedUp = false; // 원본 스탯이 백업되었는지 여부

    public float DashMultiplier => currentAppliedDashMultiplier; // [추가] 외부 참조용 프로퍼티

    private PlayerMove playerMove;
    private GaugeController gaugeController;

    // [추가] 장판 탈출 후 디버프 잔여 지속시간(0.25초) 연출용 변수들
    private float lastExitTime = -100f;
    [Tooltip("장판 구역을 벗어난 뒤 디버프(대쉬 차단, 슬로우 등)가 잔존하며 지속될 시간 (초)")]
    [SerializeField] private float graceDuration = 0.25f;
    private bool isGracePeriod = false;

    private float lastSpeedMult = 1.0f;
    private float lastJumpMult = 1.0f;
    private float lastRegenMult = 1.0f;
    private float lastDashMult = 1.0f;

    void Awake()
    {
        playerMove = GetComponent<PlayerMove>();
    }

    void Start()
    {
        // 씬에서 물감 게이지 조절 스크립트 탐색
        gaugeController = FindFirstObjectByType<GaugeController>();
    }

    /// <summary>
    /// 현재 밟은 자국 스크립트를 추적 리스트에 추가합니다.
    /// </summary>
    public void RegisterTrail(S_MonsterTrail trail)
    {
        if (trail != null && !activeTrails.Contains(trail))
        {
            activeTrails.Add(trail);
        }
    }

    /// <summary>
    /// 자국 구역을 탈출했을 때 리스트에서 제거합니다.
    /// </summary>
    public void UnregisterTrail(S_MonsterTrail trail)
    {
        if (trail != null && activeTrails.Contains(trail))
        {
            activeTrails.Remove(trail);
        }
    }

    void Update()
    {
        // 1. 유니티의 한계(오브젝트가 밟힌 채로 파괴되면 OnTriggerExit가 호출되지 않는 버그) 방어:
        // 리스트에서 이미 파괴(Destroy)되었거나 꺼진(Inactive) 자국 컴포넌트들을 일괄 제거합니다.
        activeTrails.RemoveAll(trail => trail == null || !trail.gameObject.activeInHierarchy);

        // 2. 각 스탯별로 밟고 있는 모든 자국들의 배율 중 가장 낮고 강한(수치가 가장 작은) 배율을 각각 타겟 배율로 선정
        float targetSpeedMultiplier = 1.0f;
        float targetJumpMultiplier = 1.0f;
        float targetRegenMultiplier = 1.0f;
        float targetDashMultiplier = 1.0f;

        if (activeTrails.Count > 0)
        {
            foreach (var trail in activeTrails)
            {
                if (trail.speedMultiplier < targetSpeedMultiplier) targetSpeedMultiplier = trail.speedMultiplier;
                if (trail.jumpMultiplier < targetJumpMultiplier) targetJumpMultiplier = trail.jumpMultiplier;
                if (trail.paintRegenMultiplier < targetRegenMultiplier) targetRegenMultiplier = trail.paintRegenMultiplier;
                if (trail.dashMultiplier < targetDashMultiplier) targetDashMultiplier = trail.dashMultiplier;
            }

            // 실시간 적용 배율 스냅샷 백업
            lastSpeedMult = targetSpeedMultiplier;
            lastJumpMult = targetJumpMultiplier;
            lastRegenMult = targetRegenMultiplier;
            lastDashMult = targetDashMultiplier;

            isGracePeriod = false;
        }
        else
        {
            // 모든 자국 구역을 탈출했을 때 잔여 디버프 시간(0.5초) 동안은 마지막 배율 강제 잔존 유지
            if (!isGracePeriod && (lastSpeedMult < 1.0f || lastJumpMult < 1.0f || lastRegenMult < 1.0f || lastDashMult < 1.0f))
            {
                lastExitTime = Time.time;
                isGracePeriod = true;
            }

            if (isGracePeriod)
            {
                if (Time.time - lastExitTime < graceDuration)
                {
                    // 0.5초가 지나기 전까지는 마지막 밟고 있던 배율을 타겟으로 강제 적용
                    targetSpeedMultiplier = lastSpeedMult;
                    targetJumpMultiplier = lastJumpMult;
                    targetRegenMultiplier = lastRegenMult;
                    targetDashMultiplier = lastDashMult;
                }
                else
                {
                    // 0.5초가 흐른 이후 정상 상태로 복구 및 스냅샷 배율 완전 리셋 (무한 루프 방지)
                    isGracePeriod = false;
                    lastSpeedMult = 1.0f;
                    lastJumpMult = 1.0f;
                    lastRegenMult = 1.0f;
                    lastDashMult = 1.0f;
                }
            }
        }

        // 3. 어느 하나라도 배율 변화가 감지되면 디버프 스탯 가감 적용
        if (Mathf.Abs(currentAppliedSpeedMultiplier - targetSpeedMultiplier) > 0.001f ||
            Mathf.Abs(currentAppliedJumpMultiplier - targetJumpMultiplier) > 0.001f ||
            Mathf.Abs(currentAppliedRegenMultiplier - targetRegenMultiplier) > 0.001f ||
            Mathf.Abs(currentAppliedDashMultiplier - targetDashMultiplier) > 0.001f)
        {
            UpdateDebuffStats(targetSpeedMultiplier, targetJumpMultiplier, targetRegenMultiplier, targetDashMultiplier);
        }
    }

    /// <summary>
    /// 개별 지정된 배율을 적용하여 플레이어 스탯을 갱신합니다.
    /// </summary>
    private void UpdateDebuffStats(float speedMult, float jumpMult, float regenMult, float dashMult)
    {
        // 스탯 최초 원본 백업
        if (!isStatBackedUp && playerMove != null)
        {
            originalSpeed = playerMove.moveSpeed;
            originalJump = playerMove.jumpForce;
            isStatBackedUp = true;
        }

        currentAppliedSpeedMultiplier = speedMult;
        currentAppliedJumpMultiplier = jumpMult;
        currentAppliedRegenMultiplier = regenMult;
        currentAppliedDashMultiplier = dashMult; // [추가]

        // 하나라도 디버프가 적용되는 상태라면 스탯 계산 후 적용
        if (currentAppliedSpeedMultiplier < 1.0f || currentAppliedJumpMultiplier < 1.0f || currentAppliedRegenMultiplier < 1.0f || currentAppliedDashMultiplier < 1.0f)
        {
            if (playerMove != null)
            {
                playerMove.moveSpeed = originalSpeed * currentAppliedSpeedMultiplier;
                playerMove.jumpForce = originalJump * currentAppliedJumpMultiplier;
            }

            if (gaugeController != null)
            {
                gaugeController.SetRegenMultiplier(currentAppliedRegenMultiplier);
            }
        }
        else
        {
            // 디버프 완전 해제 (모든 배율이 1.0일 때 원값 복구)
            if (playerMove != null)
            {
                playerMove.moveSpeed = originalSpeed;
                playerMove.jumpForce = originalJump;
            }

            if (gaugeController != null)
            {
                gaugeController.SetRegenMultiplier(1.0f);
            }

            isStatBackedUp = false; // 백업 잠금 해제

            Debug.Log("[Debuff] 모든 자국 구역 탈출! 플레이어 스탯이 원본 값으로 완전히 회복되었습니다.");
        }
    }

    // 플레이어가 파괴되거나 스테이지 재시작 시 안전하게 디버프를 해제하고 소멸
    void OnDestroy()
    {
        UpdateDebuffStats(1.0f, 1.0f, 1.0f, 1.0f);
    }
}
