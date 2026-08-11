using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 지정된 X좌표 범위 (-rangeX ~ rangeX) 사이에서 무작위로 위치를 선정하여
/// 일정 주기마다 프리팹을 소환하는 스포너 스크립트.
/// 기본 프리팹과 15% 확률로 등장하는 다른 프리팹 소환 및 오브젝트 풀링을 지원합니다.
/// </summary>
public class RandomRangeSpawner : MonoBehaviour
{
    [Header("스폰 설정 (Prefab Settings)")]
    [Tooltip("기본 소환 프리팹 오브젝트 (기본 85% 확률)")]
    public GameObject prefabToSpawn;

    [Tooltip("15% 확률로 등장할 다른/희귀 블록 프리팹 오브젝트")]
    public GameObject secondaryPrefabToSpawn;

    [Range(0f, 1f)]
    [Tooltip("다른 블록 프리팹이 등장할 확률 (기본값: 0.15 = 15%)")]
    public float secondarySpawnChance = 0.15f;

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

    [Header("오브젝트 풀링 설정 (Object Pooling Settings)")]
    [Tooltip("오브젝트 풀링 사용 여부 (체크 시 Instantiate/Destroy 대신 재사용하여 렉 방지)")]
    public bool useObjectPooling = true;

    [Tooltip("미리 소환해둘 기본 풀 크기 (초기 생성 개수)")]
    public int initialPoolSize = 10;

    private float timer = 0.0f;
    private Queue<GameObject> primaryPoolQueue = new Queue<GameObject>();
    private Queue<GameObject> secondaryPoolQueue = new Queue<GameObject>();

    void Start()
    {
        timer = spawnInterval - initialDelay; // 초기 대기시간 반영

        // 미리 지정된 개수만큼 오브젝트를 생성하여 비활성화 상태로 풀링 준비 (Pre-warm)
        if (useObjectPooling)
        {
            if (prefabToSpawn != null)
            {
                for (int i = 0; i < initialPoolSize; i++)
                {
                    CreateNewPooledObject(prefabToSpawn, primaryPoolQueue);
                }
            }

            if (secondaryPrefabToSpawn != null)
            {
                int secondaryPoolCount = Mathf.Max(3, Mathf.RoundToInt(initialPoolSize * secondarySpawnChance));
                for (int i = 0; i < secondaryPoolCount; i++)
                {
                    CreateNewPooledObject(secondaryPrefabToSpawn, secondaryPoolQueue);
                }
            }
        }
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
    /// 지정된 -N ~ N 범위 내에서 랜덤 X좌표를 추출하여 프리팹을 소환합니다. (15% 확률로 다른 프리팹 소환)
    /// </summary>
    public void SpawnObject()
    {
        if (prefabToSpawn == null) return;

        // 15% 확률 (secondarySpawnChance) 로 다른 프리팹 선택
        bool spawnSecondary = (secondaryPrefabToSpawn != null) && (Random.value < secondarySpawnChance);
        GameObject selectedPrefab = spawnSecondary ? secondaryPrefabToSpawn : prefabToSpawn;

        // -rangeX ~ +rangeX 사이의 무작위 X값 추출
        float randomX = Random.Range(-rangeX, rangeX);

        // 스포너의 현재 위치 기준 X offset + Y offset 적용
        Vector3 spawnPosition = new Vector3(
            transform.position.x + randomX,
            transform.position.y + spawnOffsetY,
            transform.position.z
        );

        if (useObjectPooling)
        {
            GameObject objToSpawn = GetPooledObject(selectedPrefab, spawnSecondary);
            objToSpawn.transform.position = spawnPosition;
            objToSpawn.transform.rotation = Quaternion.identity;
            objToSpawn.SetActive(true);
        }
        else
        {
            Instantiate(selectedPrefab, spawnPosition, Quaternion.identity);
        }
    }

    /// <summary>
    /// 풀에서 비활성화 상태인 오브젝트를 대여하거나, 부족하면 신규 생성합니다.
    /// </summary>
    private GameObject GetPooledObject(GameObject targetPrefab, bool isSecondary)
    {
        Queue<GameObject> targetQueue = isSecondary ? secondaryPoolQueue : primaryPoolQueue;

        while (targetQueue.Count > 0)
        {
            GameObject pooledObj = targetQueue.Dequeue();
            if (pooledObj != null && !pooledObj.activeSelf)
            {
                return pooledObj;
            }
        }

        // 여분의 풀 오브젝트가 없을 경우 추가 생성
        return CreateNewPooledObject(targetPrefab, targetQueue);
    }

    /// <summary>
    /// 풀에 보관할 신규 비활성화 오브젝트를 생성합니다.
    /// </summary>
    private GameObject CreateNewPooledObject(GameObject prefab, Queue<GameObject> queue)
    {
        GameObject newObj = Instantiate(prefab, transform);
        newObj.SetActive(false);
        return newObj;
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
