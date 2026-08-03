using UnityEngine;
using System.Collections.Generic;

public class ChunkSpawner : MonoBehaviour
{
    [Header("Chunk Prefabs")]
    [Tooltip("이어붙일 청크 프리팹들. 2개 이상 넣으면 순서대로 반복해서 사용됨")]
    public GameObject[] chunkPrefabs;

    [Tooltip("청크 하나의 X축 길이 (예: 타일맵 조각의 가로 길이)")]
    public float chunkLength = 20f;

    [Header("Player Reference")]
    public Transform player;

    [Header("Spawn / Despawn Settings")]
    [Tooltip("플레이어 앞에 항상 몇 개의 청크가 미리 준비되어 있어야 하는지")]
    public int chunksAheadCount = 3;

    [Tooltip("플레이어가 청크를 완전히 지나간 뒤, 몇 초 후에 그 청크를 삭제할지")]
    public float despawnDelay = 3f;

    // 현재 활성화된 청크들을 순서대로 관리 (가장 오래된/뒤쪽 청크가 맨 앞)
    private readonly List<ActiveChunk> activeChunks = new List<ActiveChunk>();

    private float nextSpawnX = 0f;   // 다음에 생성할 청크의 시작 X좌표
    private int nextPrefabIndex = 0; // 반복 사용할 프리팹 인덱스

    // 청크 하나의 상태를 담는 내부 클래스
    private class ActiveChunk
    {
        public GameObject obj;
        public float endX;            // 이 청크가 끝나는 X좌표
        public bool despawnScheduled; // 이미 삭제 타이머가 걸렸는지 (중복 예약 방지)
    }

    void Start()
    {
        if (player == null || chunkPrefabs == null || chunkPrefabs.Length == 0)
        {
            Debug.LogWarning("[ChunkSpawner] player 또는 chunkPrefabs가 비어있습니다.");
            return;
        }

        nextSpawnX = Mathf.Floor(player.position.x / chunkLength) * chunkLength;

        for (int i = 0; i < chunksAheadCount; i++)
        {
            SpawnNextChunk();
        }
    }

    void Update()
    {
        if (player == null || chunkPrefabs == null || chunkPrefabs.Length == 0) return;

        // 1. 플레이어 앞쪽에 청크가 충분히 준비되어 있는지 확인하고, 부족하면 계속 생성
        float requiredAheadX = player.position.x + (chunksAheadCount * chunkLength);
        while (nextSpawnX < requiredAheadX)
        {
            SpawnNextChunk();
        }

        // 2. 플레이어가 지나간 청크들에 삭제 타이머 예약
        for (int i = 0; i < activeChunks.Count; i++)
        {
            ActiveChunk chunk = activeChunks[i];
            if (chunk.despawnScheduled) continue;

            if (player.position.x > chunk.endX)
            {
                chunk.despawnScheduled = true;
                StartCoroutine(DespawnAfterDelay(chunk));
            }
        }
    }

    void SpawnNextChunk()
    {
        GameObject prefab = chunkPrefabs[nextPrefabIndex];
        Vector3 spawnPos = new Vector3(nextSpawnX, 0f, 0f);

        GameObject chunkObj = Instantiate(prefab, spawnPos, Quaternion.identity);

        activeChunks.Add(new ActiveChunk
        {
            obj = chunkObj,
            endX = nextSpawnX + chunkLength,
            despawnScheduled = false
        });

        nextSpawnX += chunkLength;

        // 프리팹이 여러 개면 순서대로 반복 (요청: 같은 패턴이 반복되게)
        nextPrefabIndex = (nextPrefabIndex + 1) % chunkPrefabs.Length;
    }

    System.Collections.IEnumerator DespawnAfterDelay(ActiveChunk chunk)
    {
        yield return new WaitForSeconds(despawnDelay);

        if (chunk.obj != null)
        {
            Destroy(chunk.obj);
        }
        activeChunks.Remove(chunk);
    }
}