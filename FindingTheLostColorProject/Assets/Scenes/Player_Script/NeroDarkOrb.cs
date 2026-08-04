using UnityEngine;

/// <summary>
/// 네로의 방해 공격 - 위에서 아래로 하강하는 암흑 구슬 투사체.
/// 플레이어를 관통(Trigger)하면서 데미지를 주고, 충분히 하강하거나 수명이 다하면 스스로 파괴됩니다.
/// </summary>
[RequireComponent(typeof(Collider2D))]
public class NeroDarkOrb : MonoBehaviour
{
    [Header("이동 및 속도 설정")]
    [Tooltip("아래로 떨어지는 하강 속도")]
    public float fallSpeed = 12f;

    [Header("데미지 설정")]
    [Tooltip("플레이어 타격 시 입히는 데미지 양")]
    public float damageAmount = 1f;

    [Header("소멸 조건 설정")]
    [Tooltip("생성 위치 기준으로 아래로 이동 가능한 최대 거리 (예: 35 입력 시 생성 지점보다 35m 아래로 내려가면 파괴)")]
    public float maxTravelDistance = 35f;

    [Tooltip("최대 수명 (초, 이 시간이 지나면 파괴)")]
    public float maxLifetime = 10f;

    private bool hasHitPlayer = false; // 중복 타격 방지 플래그 (1회 관통 피격)
    private float initialSpawnY;      // 생성 시점의 시작 Y좌표
    private float spawnTime;          // 생성된 시각

    void Start()
    {
        initialSpawnY = transform.position.y;
        spawnTime = Time.time;

        // Collider2D를 이펙트 관통을 위해 반드시 IsTrigger로 설정
        Collider2D col = GetComponent<Collider2D>();
        if (col != null)
        {
            col.isTrigger = true;
        }

        // 안전장치: 최대 수명 후 파괴
        Destroy(gameObject, maxLifetime);
    }

    void Update()
    {
        // 1. 위에서 아래로 일정한 속도로 직선 하강 이동
        transform.Translate(Vector3.down * fallSpeed * Time.deltaTime, Space.World);

        // 2. 소멸 조건 체크 (생성되자마자 지워지지 않도록 상대 거리/최소 시간 적용)
        CheckOutOfBounds();
    }

    /// <summary>
    /// 플레이어 통과(Trigger) 피격 판정
    /// </summary>
    private void OnTriggerEnter2D(Collider2D collision)
    {
        // 이미 플레이어를 공격했거나 플레이어가 아니면 무시
        if (hasHitPlayer) return;

        // PlayerHealth 컴포넌트를 가진 플레이어 검색
        PlayerHealth playerHealth = collision.GetComponent<PlayerHealth>();
        if (playerHealth == null)
        {
            playerHealth = collision.GetComponentInParent<PlayerHealth>();
        }

        if (playerHealth != null)
        {
            hasHitPlayer = true; // 1회 피격 처리 (통과해서 지나감)
            playerHealth.TakeDamage(damageAmount);
            Debug.Log("[NeroDarkOrb] 암흑 구슬이 플레이어를 관통하며 피해를 주었습니다.");
        }
    }

    /// <summary>
    /// 구슬이 이동한 상대 거리 및 생존 시간을 감지하여 안전하게 파괴
    /// </summary>
    private void CheckOutOfBounds()
    {
        // 1. 생성 위치에서 아래로 이동한 총 거리가 maxTravelDistance를 넘으면 삭제
        float distanceTraveled = initialSpawnY - transform.position.y;
        if (distanceTraveled >= maxTravelDistance)
        {
            Destroy(gameObject);
            return;
        }

        // 2. 최소 1초 이상 생존했고, 카메라 뷰포트 화면 아래 멀리(viewPos.y < -0.5) 완전히 벗어난 경우 삭제
        if (Time.time - spawnTime > 1.0f && Camera.main != null)
        {
            Vector3 viewPos = Camera.main.WorldToViewportPoint(transform.position);
            if (viewPos.y < -0.5f)
            {
                Destroy(gameObject);
            }
        }
    }
}
