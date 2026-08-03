using UnityEngine;

/// <summary>
/// 지정된 X좌표 범위 (-rangeX ~ rangeX) 사이에서 무작위로 위치를 선정하여
/// 일정 주기마다 프리팹을 계속해서 소환(Spawn)하는 스포너 스크립트.
/// </summary>
public class RandomRangeSpawner : MonoBehaviour
{
    [Header("스폰 설정 (Prefab Settings)")]
    [Tooltip("소환할 프리팹 오브젝트")]
    public GameObject prefabToSpawn;

    [Header("스폰 범위 설정 (X Range)")]
    [Tooltip("기준 위치로부터 좌우 X좌표 범위 (예: 5 입력 시 -5 ~ +5 사이에서 생성됨)")]
    public float rangeX = 5.0f;

    [Tooltip("기본 Y좌표 위치 offset (자기 자신 Y 위치 기준)")]
    public float spawnOffsetY = 0.0f;

    [Header("스폰 타이머 설정 (Spawn Timing)")]
    [Tooltip("소환 간격 (초 단위)")]
    public float spawnInterval = 1.0f;

    [Tooltip("게임 시작 시 첫 소환까지의 대기 시간")]
    public float initialDelay = 0.5f;

    [Tooltip("자동 스폰 가동 여부")]
    public bool isSpawning = true;

    private float timer = 0.0f;

    void Start()
    {
        timer = spawnInterval - initialDelay; // 초기 대기시간 반영
    }

    void Update()
    {
        if (!isSpawning || prefabToSpawn == null) return;

        timer += Time.deltaTime;
        if (timer >= spawnInterval)
        {
            timer = 0.0f;
            SpawnObject();
        }
    }

    /// <summary>
    /// 지정된 -N ~ N 범위 내에서 랜덤 X좌표를 추출하여 프리팹을 소환합니다.
    /// </summary>
    public void SpawnObject()
    {
        if (prefabToSpawn == null) return;

        // -rangeX ~ +rangeX 사이의 무작위 X값 추출
        float randomX = Random.Range(-rangeX, rangeX);

        // 스포너의 현재 위치 기준 X offset + Y offset 적용
        Vector3 spawnPosition = new Vector3(
            transform.position.x + randomX,
            transform.position.y + spawnOffsetY,
            transform.position.z
        );

        Instantiate(prefabToSpawn, spawnPosition, Quaternion.identity);
    }

    // 에디터 씬 뷰에서 스폰 범위를 초록색 선으로 보여줌
    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.green;
        Vector3 leftPos = transform.position + new Vector3(-rangeX, spawnOffsetY, 0);
        Vector3 rightPos = transform.position + new Vector3(rangeX, spawnOffsetY, 0);
        Gizmos.DrawLine(leftPos, rightPos);
        Gizmos.DrawWireSphere(leftPos, 0.2f);
        Gizmos.DrawWireSphere(rightPos, 0.2f);
    }
}
