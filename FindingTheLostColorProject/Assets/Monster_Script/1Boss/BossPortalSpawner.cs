using UnityEngine;
using System.Collections;

public class BossPortalSpawner : MonoBehaviour
{
    [Header("스폰 몬스터 설정")]
    [Tooltip("첫번째 소환 패턴 시 양쪽 포탈에서 소환될 잡몹 프리팹 목록 (각각 1마리씩 스폰)")]
    public GameObject[] spawnMonsterPrefabs1;

    [Tooltip("두번째 소환 패턴 시 양쪽 포탈에서 소환될 잡몹 프리팹 목록 (각각 1마리씩 스폰)")]
    public GameObject[] spawnMonsterPrefabs2;

    [Header("포탈 트랜스폼 설정 (몬스터 스폰 지점)")]
    [Tooltip("왼쪽 포탈 스폰 중심 위치")]
    public Transform leftPortalTransform;
    [Tooltip("오른쪽 포탈 스폰 중심 위치")]
    public Transform rightPortalTransform;

    [Header("포탈 시각 오브젝트 설정 (선택사항)")]
    [Tooltip("평소엔 꺼져있다가 소환 패턴 시 활성화될 왼쪽 포탈 시각 오브젝트")]
    public GameObject leftPortalObject;
    [Tooltip("평소엔 꺼져있다가 소환 패턴 시 활성화될 오른쪽 포탈 시각 오브젝트")]
    public GameObject rightPortalObject;

    [Header("스케줄러 설정")]
    [Tooltip("보스전 돌입 후 소환 주기 (초, 기본값: 60초)")]
    public float spawnInterval = 60f;
    [Tooltip("보스 소환 스케줄러 가동 여부")]
    public bool isBossBattleStarted = true;

    private float lastSpawnTime = 0f;
    private int spawnGroupIndex = 0; // 0: 1차 소환 프리팹, 1: 2차 소환 프리팹
    private BossAttack bossAttack;   // 보스 패턴 제어 스크립트 참조 (2페이즈 감지용)

    void Start()
    {
        // 씬에서 보스 공격 컴포넌트 자동 탐색 및 참조
        bossAttack = FindFirstObjectByType<BossAttack>();

        // 보스가 활성화된 시점부터 60초 카운트다운 시작
        lastSpawnTime = Time.time;

        // 초기에 포탈 오브젝트들이 지정되어 있다면 비활성화
        if (leftPortalObject != null) leftPortalObject.SetActive(false);
        if (rightPortalObject != null) rightPortalObject.SetActive(false);
    }

    void Update()
    {
        if (!isBossBattleStarted) return;

        // [신규] 보스가 2페이즈에 진입했는지 실시간 체크 ➔ 진입 시 소환 완전 중단 및 포탈 닫기
        if (bossAttack != null && bossAttack.IsPhase2)
        {
            isBossBattleStarted = false; // 소환 스케줄러 자체를 종료
            StopAllCoroutines();         // 이미 진행 중인 소환 시퀀스 코루틴 정지
            
            // 열려 있던 포탈 오브젝트 강제 차단
            if (leftPortalObject != null) leftPortalObject.SetActive(false);
            if (rightPortalObject != null) rightPortalObject.SetActive(false);
            
            Debug.Log("[BossPortalSpawner] 보스 2페이즈 진입 감지! 포탈 소환 스케줄러를 완전히 영구 종료합니다.");
            return;
        }

        // 보스전 시작 후 spawnInterval 주기마다 포탈 소환 시전
        if (Time.time - lastSpawnTime >= spawnInterval)
        {
            lastSpawnTime = Time.time;
            StartCoroutine(SpawnSequence());
        }
    }

    /// <summary>
    /// 포탈을 열고 몬스터를 순차 스폰 후 포탈을 닫는 연출 시퀀스
    /// </summary>
    private IEnumerator SpawnSequence()
    {
        Debug.Log($"[BossPortalSpawner] 포탈 소환 개시! 그룹 인덱스: {spawnGroupIndex + 1}");

        // 1. 좌우 포탈 소환기 켜기
        if (leftPortalObject != null) leftPortalObject.SetActive(true);
        if (rightPortalObject != null) rightPortalObject.SetActive(true);

        // 포탈이 스르륵 커지거나 효과를 보이는 연출 대기 시간 (1.0초)
        yield return new WaitForSeconds(1.0f);

        // 2. 현재 소환 순서에 맞는 몬스터 프리팹 리스트 선택
        GameObject[] currentPrefabs = (spawnGroupIndex == 0) ? spawnMonsterPrefabs1 : spawnMonsterPrefabs2;

        if (currentPrefabs != null && currentPrefabs.Length > 0)
        {
            foreach (var prefab in currentPrefabs)
            {
                if (prefab != null)
                {
                    // 왼쪽 포탈 소환
                    if (leftPortalTransform != null)
                    {
                        SpawnMonster(prefab, leftPortalTransform);
                    }

                    // 오른쪽 포탈 소환
                    if (rightPortalTransform != null)
                    {
                        SpawnMonster(prefab, rightPortalTransform);
                    }

                    // 몬스터 소환 간 0.2초 텀을 주어 자연스럽게 순차 발사 연출
                    yield return new WaitForSeconds(0.2f);
                }
            }
        }

        // 3. 소환이 끝난 후 포탈이 유지되다 사라지는 여운 시간
        yield return new WaitForSeconds(1.5f);

        if (leftPortalObject != null) leftPortalObject.SetActive(false);
        if (rightPortalObject != null) rightPortalObject.SetActive(false);

        // 4. 다음 소환 인덱스 교체 (1->2->1->2 무한 반복)
        spawnGroupIndex = (spawnGroupIndex + 1) % 2;
    }

    /// <summary>
    /// 지정된 포탈에서 몬스터를 생성하고 PortalMonsterLinger를 연결해 줍니다.
    /// </summary>
    private void SpawnMonster(GameObject prefab, Transform portal)
    {
        // 미세하게 서로 다른 스폰 속도 충돌 튕김 방지용 랜덤 오프셋
        Vector3 spawnOffset = new Vector3(Random.Range(-0.25f, 0.25f), Random.Range(-0.2f, 0.2f), 0f);
        Vector3 spawnPos = portal.position + spawnOffset;

        GameObject monster = Instantiate(prefab, spawnPos, Quaternion.identity);

        // 정화되었을 때 생성 포탈로 회귀하는 연출 스크립트 연결
        PortalMonsterLinger linger = monster.GetComponent<PortalMonsterLinger>();
        if (linger == null)
        {
            linger = monster.AddComponent<PortalMonsterLinger>();
        }

        // 자기를 스폰시킨 포탈의 월드 위치 등록
        linger.Setup(portal.position);
    }

    /// <summary>
    /// 외부(예: 보스 트리거 스크립트)에서 보스전 시작을 수동으로 통지할 수 있게 해 주는 메소드
    /// </summary>
    public void StartBossSpawnScheduler()
    {
        isBossBattleStarted = true;
        lastSpawnTime = Time.time; // 시간 초기화
    }
}
