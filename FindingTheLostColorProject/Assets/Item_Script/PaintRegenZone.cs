using UnityEngine;

public class PaintRegenZone : MonoBehaviour
{
    public enum DetectionMode { TriggerCollider, ProximityDistance }

    [Header("Detection Settings")]
    [Tooltip("감지 방식 (TriggerCollider: 트리거 콜라이더 영역 진입 / ProximityDistance: 단순 일정 거리 이하 감지)")]
    public DetectionMode detectionMode = DetectionMode.TriggerCollider;

    [Tooltip("ProximityDistance 모드 시 활성화될 감지 반경")]
    public float activeRadius = 3f;

    [Header("Regeneration Boost Settings")]
    [Tooltip("플레이어가 가까이 있을 때 물감 재생 속도 배율(기본값: 4.0 -> 4배 빠르게 재생)")]
    public float regenMultiplier = 4f;

    [Header("Visual & Effects")]
    [Tooltip("플레이어가 버프를 받는 동안 재생할 파티클 시스템 (옵션)")]
    public ParticleSystem activeParticles;

    [Tooltip("버프 활성화 시 아우라/오브젝트가 회전하는 속도 (0이면 회전 안 함)")]
    public float rotationSpeed = 45f;

    private GaugeController playerGauge;
    private bool isPlayerInZone = false;

    void Start()
    {
        // 파티클 시스템이 지정되어 있으면 기본적으로 정지 상태로 초기화
        if (activeParticles != null)
        {
            var mainModule = activeParticles.main;
            mainModule.loop = true;
            activeParticles.Stop();
        }
    }

    void Update()
    {
        // 1. 단순 거리 측정 모드인 경우 거리 실시간 체크
        if (detectionMode == DetectionMode.ProximityDistance)
        {
            FindPlayerGaugeIfNull();

            if (playerGauge != null)
            {
                float distance = Vector2.Distance(transform.position, playerGauge.transform.position);
                bool shouldBoost = distance <= activeRadius;

                if (shouldBoost && !isPlayerInZone)
                {
                    ApplyBoost();
                }
                else if (!shouldBoost && isPlayerInZone)
                {
                    RemoveBoost();
                }
            }
        }

        // 2. 버프가 활성화되어 있을 때 시각적 피드백 (자체 회전 연출)
        if (isPlayerInZone && rotationSpeed != 0f)
        {
            transform.Rotate(Vector3.forward * rotationSpeed * Time.deltaTime);
        }
    }

    private void ApplyBoost()
    {
        if (playerGauge != null)
        {
            isPlayerInZone = true;
            playerGauge.SetRegenMultiplier(regenMultiplier);
            Debug.Log($"[PaintRegenZone] 플레이어 진입! 물감 재생 배율 1.0 -> {regenMultiplier}배 증가");

            // 파티클 재생
            if (activeParticles != null && !activeParticles.isPlaying)
            {
                activeParticles.Play();
            }
        }
    }

    private void RemoveBoost()
    {
        if (playerGauge != null)
        {
            isPlayerInZone = false;
            playerGauge.SetRegenMultiplier(1f);
            Debug.Log("[PaintRegenZone] 플레이어 이탈! 물감 재생 배율 정상 복구 (1.0배)");

            // 파티클 정지
            if (activeParticles != null)
            {
                activeParticles.Stop();
            }
        }
    }

    private void FindPlayerGaugeIfNull()
    {
        if (playerGauge == null)
        {
            playerGauge = FindFirstObjectByType<GaugeController>();
        }
    }

    #region Trigger 2D Collision Mode (트리거 콜라이더 사용 시)
    private void OnTriggerEnter2D(Collider2D collision)
    {
        if (detectionMode != DetectionMode.TriggerCollider) return;

        // 부딪힌 대상이 플레이어인지 확인 (PlayerHealth가 있는지 검사)
        PlayerHealth player = collision.GetComponent<PlayerHealth>();
        if (player == null) player = collision.GetComponentInParent<PlayerHealth>();

        if (player != null)
        {
            playerGauge = player.GetComponent<GaugeController>();
            if (playerGauge == null) playerGauge = player.GetComponentInChildren<GaugeController>();
            if (playerGauge == null) FindPlayerGaugeIfNull();

            ApplyBoost();
        }
    }

    private void OnTriggerExit2D(Collider2D collision)
    {
        if (detectionMode != DetectionMode.TriggerCollider) return;

        PlayerHealth player = collision.GetComponent<PlayerHealth>();
        if (player == null) player = collision.GetComponentInParent<PlayerHealth>();

        if (player != null)
        {
            RemoveBoost();
        }
    }
    #endregion

    // 에디터 기즈모로 감지 반경 가시화 (ProximityDistance 모드 튜닝용)
    private void OnDrawGizmosSelected()
    {
        if (detectionMode == DetectionMode.ProximityDistance)
        {
            Gizmos.color = Color.cyan;
            Gizmos.DrawWireSphere(transform.position, activeRadius);
        }
    }
}
