using UnityEngine;
using System.Collections;
using System.Collections.Generic;

public class BossAttack : MonoBehaviour
{
    // ===== 공통 =====
    [Header("공통")]
    public Transform target;               // 비워두면 Player 태그로 자동 탐색
    public NormalMonster bossHealth;        // 페이즈 판별용 (읽기 전용 참조만, NormalMonster는 수정하지 않음)
    [Range(0f, 1f)] public float phase2ThresholdRatio = 0.5f; // F_HealthMoveSwitcher와 같은 값으로 맞추면 이동 전환과 동시에 페이즈 전환됨
    public LayerMask groundLayer;
    public MonoBehaviour moveScript;        // 공격 중 이동을 멈추고 싶다면 연결 (선택 사항, 비워두면 이동 제어 안 함)
    public float attackCooldown = 1f;       // 공격 종료 후 다음 공격까지 대기 시간


    bool isAttacking = false;
    bool canAttack = true;

    // ===== 공격 정의 =====
    private delegate IEnumerator AttackRoutineDelegate();

    private class AttackEntry
    {
        public string name;
        public AttackRoutineDelegate routine;
        public AttackEntry(string n, AttackRoutineDelegate r) { name = n; routine = r; }
    }

    private List<AttackEntry> phase1Attacks;
    private List<AttackEntry> phase2Attacks;
    private AttackEntry lastUsedAttack = null; // 페이즈 구분 없이 "바로 직전 공격"을 기억 (페이즈 전환 시 자동으로 그 페이즈 풀에 없으면 제외 대상에서 빠짐)

    void Awake()
    {
        // ===== 1페이즈 공격 풀 =====
        phase1Attacks = new List<AttackEntry>
        {
            new AttackEntry("SpikeTrap", SpikeTrapAttackRoutine),
            // 1페이즈 공격을 더 추가하려면 여기에 계속 등록
        };

        // ===== 2페이즈 공격 풀 =====
        phase2Attacks = new List<AttackEntry>
        {
            // 2페이즈 공격은 아직 없음 - 추가되면 여기에 등록
        };
    }

    void Start()
    {
        if (target == null)
        {
            GameObject player = GameObject.FindGameObjectWithTag("Player");
            if (player != null) target = player.transform;
        }

        if (bossHealth == null) bossHealth = GetComponent<NormalMonster>();

        // spikePrefab/telegraphMarkerPrefab이 보스 프리팹 안의 자식 오브젝트를 직접 참조하는 경우,
        // 그 원본을 계속 Instantiate 소스로 쓰면 프리팹이 손상될 수 있어(Missing GameObject 원인)
        // 시작 시 딱 한 번 복제해서 "런타임 전용 템플릿"을 만들고, 이후엔 그 복제본만 사용함
        if (spikePrefab != null)
        {
            spikeTemplate = Instantiate(spikePrefab, spikePrefab.transform.position, spikePrefab.transform.rotation);
            spikeTemplate.transform.SetParent(null);
            spikeTemplate.SetActive(false);
            spikePrefab.SetActive(false); // 원본은 그냥 숨겨만 두고 다시는 건드리지 않음
        }

        if (telegraphMarkerPrefab != null)
        {
            telegraphMarkerTemplate = Instantiate(telegraphMarkerPrefab, telegraphMarkerPrefab.transform.position, telegraphMarkerPrefab.transform.rotation);
            telegraphMarkerTemplate.transform.SetParent(null);
            telegraphMarkerTemplate.SetActive(false);
            telegraphMarkerPrefab.SetActive(false); // 원본은 그냥 숨겨만 두고 다시는 건드리지 않음
        }

        // 보스 본체(자기 자신)의 콜라이더만 트리거로 설정 (자식의 ContactRelay용 콜라이더는 건드리지 않음)
        // Rigidbody2D는 Dynamic 유지 - 트리거 콜라이더끼리는 어차피 물리적으로 밀리지 않음
        foreach (var col in GetComponents<Collider2D>())
        {
            col.isTrigger = true;
        }
    

    // 보스 자신의 콜라이더를 트리거로 만들어서 플레이어와 물리적으로 부딪히지 않고 관통되게 함
    // (BossMove가 transform.position을 직접 덮어쓰는 방식이라, 트리거가 아니면 물리엔진이
    //  겹침을 풀려고 플레이어를 밀어내는 현상이 발생함 - 점프해서 아래에서 파고들 때 특히 두드러짐)
    Collider2D[] bossColliders = GetComponents<Collider2D>();
        foreach (var col in bossColliders)
        {
            col.isTrigger = true;
        }
    }

    void Update()
    {
        if (isAttacking || !canAttack || target == null || bossHealth == null) return;

        List<AttackEntry> pool = GetCurrentPhasePool();
        if (pool == null || pool.Count == 0) return; // 해당 페이즈에 등록된 공격이 없으면 대기

        AttackEntry chosen = PickRandomAttack(pool);
        if (chosen == null) return;

        StartCoroutine(RunAttack(chosen));
    }

    List<AttackEntry> GetCurrentPhasePool()
    {
        float ratio = bossHealth.maxHealth > 0f ? bossHealth.currentHealth / bossHealth.maxHealth : 0f;
        // ratio가 낮으면(체력 채워지기 전 = 초반) 1페이즈, threshold 이상(F_HealthMoveSwitcher와 동일 기준)이면 2페이즈
        return ratio >= phase2ThresholdRatio ? phase2Attacks : phase1Attacks;
    }

    AttackEntry PickRandomAttack(List<AttackEntry> pool)
    {
        if (pool.Count == 0) return null;
        if (pool.Count == 1) return pool[0]; // 하나뿐이면 반복 방지가 불가능하니 그대로 사용

        List<AttackEntry> candidates = new List<AttackEntry>(pool);
        if (lastUsedAttack != null) candidates.Remove(lastUsedAttack);

        return candidates[Random.Range(0, candidates.Count)];
    }

    IEnumerator RunAttack(AttackEntry attack)
    {
        isAttacking = true;
        canAttack = false;
        lastUsedAttack = attack;

        if (moveScript != null) moveScript.enabled = false;

        yield return StartCoroutine(attack.routine());

        if (moveScript != null) moveScript.enabled = true;
        isAttacking = false;

        yield return new WaitForSeconds(attackCooldown);
        canAttack = true;
    }

    // ================= 가시 함정 공격 (1페이즈) =================
    [Header("가시 함정 공격 설정")]
    public GameObject telegraphMarkerPrefab;      // 경고 표시 프리팹 (SpriteRenderer 포함, 비워두면 임시 마커 생성)
    public GameObject spikePrefab;                // 가시 프리팹 (Collider2D는 Trigger로, 비워두면 임시 가시 생성)
    GameObject telegraphMarkerTemplate;           // telegraphMarkerPrefab의 런타임 복제 템플릿 (원본 보호용)
    GameObject spikeTemplate;                     // spikePrefab의 런타임 복제 템플릿 (원본 보호용)
    public float spikeTelegraphDuration = 2f;      // 텔레그래프 지속 시간
    public float spikeTelegraphBlinkInterval = 0.5f; // 깜빡임 간격
    public float spikeLifetime = 3f;               // 가시가 유지되는 시간
    public float spikeSearchRadius = 8f;           // 보스 주변 랜덤 위치 탐색 반경
    public float spikeGroundRaycastDistance = 20f; // 바닥 탐색용 레이캐스트 최대 거리
    public int spikeMaxSearchAttempts = 20;        // 유효 바닥 못 찾을 때 재시도 최대 횟수
    public float spikeMinDistance = 1.5f;           // 가시끼리 최소 간격 (겹침 방지)
    public float spikeMaxHeightAboveBoss = 3f;      // 보스 기준 이 값보다 높은 땅에는 가시 생성 안 함 (여유 허용치)

    IEnumerator SpikeTrapAttackRoutine()
    {
        // 1. 위치 4곳 결정: 플레이어 위치 1곳 + 랜덤 바닥 위치 3곳
        List<Vector2> spawnPositions = new List<Vector2>
        {
            GetGroundPositionBelow(target.position)
        };

        int found = 0;
        int attempts = 0;
        while (found < 3 && attempts < spikeMaxSearchAttempts)
        {
            attempts++;
            Vector2 randomPoint = (Vector2)transform.position + Random.insideUnitCircle * spikeSearchRadius;
            Vector2? groundPos = TryFindGroundPosition(randomPoint);
            if (groundPos.HasValue && IsFarEnough(groundPos.Value, spawnPositions))
            {
                spawnPositions.Add(groundPos.Value);
                found++;
            }
        }

        // 2. 각 위치에 텔레그래프 마커 생성
        List<GameObject> markers = new List<GameObject>();
        foreach (var pos in spawnPositions)
        {
            GameObject marker = SpawnTelegraphMarker(pos);
            if (marker != null) markers.Add(marker);
        }

        // 3. 2초 동안 0.5초 간격으로 투명해졌다 돌아오는 깜빡임
        float elapsed = 0f;
        bool visible = true;
        while (elapsed < spikeTelegraphDuration)
        {
            yield return new WaitForSeconds(spikeTelegraphBlinkInterval);
            elapsed += spikeTelegraphBlinkInterval;
            visible = !visible;
            foreach (var marker in markers)
            {
                if (marker == null) continue;
                SpriteRenderer sr = marker.GetComponent<SpriteRenderer>();
                if (sr != null) sr.enabled = visible;
            }
        }

        // 4. 텔레그래프 제거
        foreach (var marker in markers)
        {
            if (marker != null) Destroy(marker);
        }

        // 5. 가시 생성 (각 위치마다)
        foreach (var pos in spawnPositions)
        {
            SpawnSpike(pos);
        }

        // 6. 가시가 살아있는 3초 대기 (가시 자체는 SpikeHazard가 스스로 lifetime 관리)
        yield return new WaitForSeconds(spikeLifetime);
    }

    // 특정 위치 바로 아래(수직) 바닥을 찾음 (플레이어 위치 기준)
    Vector2 GetGroundPositionBelow(Vector2 fromPos)
    {
        RaycastHit2D hit = Physics2D.Raycast(fromPos + Vector2.up * 0.5f, Vector2.down, spikeGroundRaycastDistance, groundLayer);
        return hit.collider != null ? hit.point : fromPos;
    }

    Vector2? TryFindGroundPosition(Vector2 randomPoint)
    {
        // 보스는 항상 바닥과 천장 사이(빈 공간)에 떠 있다고 가정하고,
        // 보스 자신의 y좌표에서 바로 아래로 쏨 (천장을 뚫고 지나갈 일이 없음)
        Vector2 origin = new Vector2(randomPoint.x, transform.position.y);
        RaycastHit2D hit = Physics2D.Raycast(
            origin,
            Vector2.down,
            spikeGroundRaycastDistance,
            groundLayer
        );

        if (hit.collider == null) return null;

        // 보스보다 spikeMaxHeightAboveBoss 이상 높은 위치의 땅(발판)에는 가시를 생성하지 않음
        if (hit.point.y > transform.position.y + spikeMaxHeightAboveBoss) return null;

        return hit.point;
    }

    // spawnPositions에 이미 있는 위치들과 최소 간격 이상 떨어져 있는지 확인
    bool IsFarEnough(Vector2 pos, List<Vector2> existing)
    {
        foreach (var p in existing)
        {
            if (Vector2.Distance(pos, p) < spikeMinDistance)
                return false;
        }
        return true;
    }

    GameObject SpawnTelegraphMarker(Vector2 pos)
    {
        if (telegraphMarkerTemplate != null)
        {
            GameObject marker = Instantiate(telegraphMarkerTemplate, pos, Quaternion.identity);
            marker.SetActive(true); // 템플릿이 꺼져있어도 복제본은 반드시 켜서 생성
            AlignBottomToGround(marker, pos); // 마커 바닥이 땅 표면에 닿도록 보정
            return marker;
        }

        // 프리팹이 없으면 임시 경고 마커 생성 (반투명 노란 사각형)
        GameObject tempMarker = new GameObject("SpikeTelegraph_Temp");
        tempMarker.transform.position = pos;
        SpriteRenderer sr = tempMarker.AddComponent<SpriteRenderer>();
        sr.color = new Color(1f, 0.9f, 0f, 0.6f);
        sr.sprite = CreateTempSquareSprite();
        tempMarker.transform.localScale = Vector3.one * 0.8f;
        AlignBottomToGround(tempMarker, pos); // 마커 바닥이 땅 표면에 닿도록 보정
        return tempMarker;
    }

    void SpawnSpike(Vector2 pos)
    {
        GameObject spike;
        if (spikeTemplate != null)
        {
            spike = Instantiate(spikeTemplate, pos, Quaternion.identity);
            spike.SetActive(true); // 템플릿이 꺼져있어도 복제본은 반드시 켜서 생성
        }
        else
        {
            // 프리팹이 없으면 임시 가시 생성 (회색 사각형 + 트리거 콜라이더)
            spike = new GameObject("Spike_Temp");
            spike.transform.position = pos;
            SpriteRenderer sr = spike.AddComponent<SpriteRenderer>();
            sr.color = Color.gray;
            sr.sprite = CreateTempSquareSprite();
            spike.transform.localScale = new Vector3(0.6f, 1f, 1f);

            BoxCollider2D col = spike.AddComponent<BoxCollider2D>();
            col.isTrigger = true;
        }

        // 프리팹을 쓰는 경우에도 플레이어와 물리적으로 부딪히지 않도록
        // (본체 + 자식 오브젝트 포함) 모든 콜라이더를 트리거로 강제 설정
        ForceAllCollidersToTrigger(spike);

        AlignBottomToGround(spike, pos); // 가시 바닥이 땅 표면에 닿도록 보정

        SpikeHazard hazard = spike.GetComponent<SpikeHazard>();
        if (hazard == null) hazard = spike.AddComponent<SpikeHazard>();
        hazard.lifetime = spikeLifetime;
    }

    // spike의 실제 바닥(월드 기준 min.y)이 groundPos.y에 오도록 위로 밀어올림
    void AlignBottomToGround(GameObject spike, Vector2 groundPos)
    {
        Bounds bounds;
        Collider2D col = spike.GetComponent<Collider2D>();
        if (col != null)
        {
            bounds = col.bounds;
        }
        else
        {
            SpriteRenderer sr = spike.GetComponent<SpriteRenderer>();
            if (sr == null) return; // 기준 삼을 게 없으면 보정하지 않음
            bounds = sr.bounds;
        }

        float bottomOffset = spike.transform.position.y - bounds.min.y; // 피벗이 바닥보다 얼마나 위에 있는지
        spike.transform.position = new Vector3(groundPos.x, groundPos.y + bottomOffset, spike.transform.position.z);
    }

    // spike 본체와 모든 자식 오브젝트의 콜라이더를 트리거로 강제 설정
    // (플레이어가 가시를 물리적으로 밀어내거나 막히지 않고 그대로 통과하게 하기 위함)
    void ForceAllCollidersToTrigger(GameObject spike)
    {
        Collider2D[] colliders = spike.GetComponentsInChildren<Collider2D>(true);
        foreach (var c in colliders)
        {
            c.isTrigger = true;
        }
    }

    Sprite CreateTempSquareSprite()
    {
        int size = 32;
        Texture2D tex = new Texture2D(size, size);
        for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
                tex.SetPixel(x, y, Color.white);
        tex.Apply();
        return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f));
    }
}