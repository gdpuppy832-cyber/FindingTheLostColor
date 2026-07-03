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

    
    public List<BossCrystal> crystals = new List<BossCrystal>(); // 씬에 미리 배치된 크리스탈들을 Inspector에서 연결 (BossCrystal은 NormalMonster를 상속하므로 CursorController가 그대로 붓질 감지함)
    public BossMove flyMove; // 크리스탈 파괴 완료 시 무한대(∞) 이동으로 전환하기 위한 참조 (비워두면 자동 탐색)

    bool phase2Unlocked = false; // false면 크리스탈 페이즈, true면 2페이즈(공격 가능)
    int destroyedCrystalCount = 0;
    Collider2D[] bossOwnColliders; // 크리스탈 페이즈 동안 붓질(OverlapCircleAll) 감지를 막기 위해 비활성화할 보스 콜라이더

    bool isAttacking = false;
    float nextAttackAllowedTime = 0f;

    List<GameObject> activeTelegraphMarkers = new List<GameObject>();
    List<GameObject> activeLaserObjects = new List<GameObject>(); // 발동 중인 레이저 본체도 강제 중단 시 정리 대상에 포함

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
            new AttackEntry("Laser", LaserAttackRoutine),
            new AttackEntry("FrostRain", FrostRainAttackRoutine),
        };

        // ===== 2페이즈 공격 풀 =====
        phase2Attacks = new List<AttackEntry>
        {

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
            telegraphMarkerPrefab.SetActive(false);
        }

        if (laserPrefab != null)
        {
            laserTemplate = Instantiate(laserPrefab, laserPrefab.transform.position, laserPrefab.transform.rotation);
            laserTemplate.transform.SetParent(null);
            laserTemplate.SetActive(false);
            laserPrefab.SetActive(false);
        }

        if (laserTelegraphPrefab != null)
        {
            laserTelegraphTemplate = Instantiate(laserTelegraphPrefab, laserTelegraphPrefab.transform.position, laserTelegraphPrefab.transform.rotation);
            laserTelegraphTemplate.transform.SetParent(null);
            laserTelegraphTemplate.SetActive(false);
            laserTelegraphPrefab.SetActive(false);
        }

        if (frostCrystalPrefab != null)
        {
            frostCrystalTemplate = Instantiate(frostCrystalPrefab, frostCrystalPrefab.transform.position, frostCrystalPrefab.transform.rotation);
            frostCrystalTemplate.transform.SetParent(null);
            frostCrystalTemplate.SetActive(false);
            frostCrystalPrefab.SetActive(false);
        }

        if (frostCrystalHitboxPrefab != null)
        {
            frostCrystalHitboxTemplate = Instantiate(frostCrystalHitboxPrefab, frostCrystalHitboxPrefab.transform.position, frostCrystalHitboxPrefab.transform.rotation);
            frostCrystalHitboxTemplate.transform.SetParent(null);
            frostCrystalHitboxTemplate.SetActive(false);
            frostCrystalHitboxPrefab.SetActive(false);
        }

        if (frostTelegraphMarkerPrefab != null)
        {
            frostTelegraphMarkerTemplate = Instantiate(frostTelegraphMarkerPrefab, frostTelegraphMarkerPrefab.transform.position, frostTelegraphMarkerPrefab.transform.rotation);
            frostTelegraphMarkerTemplate.transform.SetParent(null);
            frostTelegraphMarkerTemplate.SetActive(false);
            frostTelegraphMarkerPrefab.SetActive(false);
        }

        // 보스 본체(자기 자신)의 콜라이더만 트리거로 설정 (자식의 ContactRelay용 콜라이더는 건드리지 않음)
        // Rigidbody2D는 Dynamic 유지 - 트리거 콜라이더끼리는 어차피 물리적으로 밀리지 않음
        bossOwnColliders = GetComponents<Collider2D>();
        foreach (var col in bossOwnColliders)
        {
            col.isTrigger = true;
        }

        // 크리스탈 페이즈 동안에는 보스 자신의 콜라이더를 꺼서
        // CursorController의 OverlapCircleAll에 아예 걸리지 않게 함 (붓질로 체력이 차는 것을 원천 차단)
        SetBossColliderState(false);

        if (flyMove == null) flyMove = GetComponent<BossMove>();
        if (flyMove == null) flyMove = GetComponentInChildren<BossMove>();

        // 크리스탈들의 파괴 이벤트를 구독해서 전부 파괴되면 2페이즈로 전환
        foreach (var crystal in crystals)
        {
            if (crystal != null) crystal.OnCrystalDestroyed += HandleCrystalDestroyed;
        }
    }

    void HandleCrystalDestroyed()
    {
        destroyedCrystalCount++;
        Debug.Log($"[BossAttack] 크리스탈 파괴됨: {destroyedCrystalCount}/{crystals.Count}, isAttacking={isAttacking}, enabled={enabled}");

        if (!enabled) enabled = true;
        if (moveScript != null && !moveScript.enabled) moveScript.enabled = true;
        if (flyMove != null && !flyMove.enabled) flyMove.enabled = true;

        bool wasInterrupted = isAttacking;
        isAttacking = false;

        if (wasInterrupted)
        {
            if (moveScript != null) moveScript.enabled = true;
            nextAttackAllowedTime = Time.time + attackCooldown;
        }

        if (destroyedCrystalCount >= crystals.Count)
        {
            phase2Unlocked = true;
            SetBossColliderState(true);
            if (flyMove != null) flyMove.SetInfinityMode(true);
            Debug.Log("[BossAttack] 크리스탈 4개 모두 파괴 - 2페이즈로 전환");
        }

        // 추가: 복구 완료 후 최종 상태 확인
        Debug.Log($"[BossAttack] 복구 완료 -> isAttacking={isAttacking}, enabled={enabled}, nextAttackAllowedTime={nextAttackAllowedTime}, currentTime={Time.time}, poolCount={GetCurrentPhasePool()?.Count}");
    }



    void SetBossColliderState(bool enabled)
    {
        if (bossOwnColliders == null) return;
        foreach (var col in bossOwnColliders)
        {
            if (col != null) col.enabled = enabled;
        }
    }

    void Update()
    {
        if (isAttacking || Time.time < nextAttackAllowedTime || target == null || bossHealth == null)
            return;
        
        

        List<AttackEntry> pool = GetCurrentPhasePool();
        if (pool == null || pool.Count == 0) return;

        AttackEntry chosen = PickRandomAttack(pool);
        if (chosen == null) return;

        StartCoroutine(RunAttack(chosen));
    }

    List<AttackEntry> GetCurrentPhasePool()
    {
        // 체력 비율이 아니라 크리스탈 파괴 여부로 페이즈가 결정됨
        // 크리스탈이 남아있으면 1페이즈 풀, 다 깨지면(phase2Unlocked) 2페이즈 풀
        // 단, 2페이즈 공격이 아직 등록되지 않았다면(개발 중) 보스가 멈추지 않도록 1페이즈 풀을 계속 사용
        if (phase2Unlocked && phase2Attacks.Count > 0) return phase2Attacks;
        return phase1Attacks;
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
        lastUsedAttack = attack;

        if (moveScript != null) moveScript.enabled = false;

        yield return StartCoroutine(attack.routine());

        if (moveScript != null) moveScript.enabled = true;
        isAttacking = false;
        nextAttackAllowedTime = Time.time + attackCooldown; // 코루틴 WaitForSeconds 대신 시간 값으로 쿨다운 관리 (중단되어도 안전)
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
        activeTelegraphMarkers.Clear();
        foreach (var pos in spawnPositions)
        {
            GameObject marker = SpawnTelegraphMarker(pos);
            if (marker != null) activeTelegraphMarkers.Add(marker);
        }

        // 3. 2초 동안 0.5초 간격으로 투명해졌다 돌아오는 깜빡임
        float elapsed = 0f;
        bool visible = true;
        while (elapsed < spikeTelegraphDuration)
        {
            yield return new WaitForSeconds(spikeTelegraphBlinkInterval);
            elapsed += spikeTelegraphBlinkInterval;
            visible = !visible;
            foreach (var marker in activeTelegraphMarkers)
            {
                if (marker == null) continue;
                SpriteRenderer sr = marker.GetComponent<SpriteRenderer>();
                if (sr != null) sr.enabled = visible;
            }
        }

        // 4. 텔레그래프 제거
        foreach (var marker in activeTelegraphMarkers)
        {
            if (marker != null) Destroy(marker);
        }
        activeTelegraphMarkers.Clear();

        // 5. 가시 생성 (각 위치마다)
        foreach (var pos in spawnPositions)
        {
            SpawnSpike(pos);
        }

        // 6. 가시가 살아있는 3초 대기 (가시 자체는 SpikeHazard가 스스로 lifetime 관리)
        yield return new WaitForSeconds(spikeLifetime);
    }

    // ================= 레이저 공격 (1페이즈) =================
    [Header("레이저 공격 설정")]
    public GameObject laserTelegraphPrefab;   // 가로로 긴 경고 라인 프리팹 (비워두면 임시 마커 생성)
    public GameObject laserPrefab;            // 레이저 몸체 프리팹 (비워두면 임시 레이저 생성)
    GameObject laserTelegraphTemplate;        // laserTelegraphPrefab의 런타임 복제 템플릿 (원본 보호용)
    GameObject laserTemplate;                 // laserPrefab의 런타임 복제 템플릿 (원본 보호용)
    public float laserTelegraphDuration = 2f;       // 텔레그래프 지속 시간
    public float laserTelegraphBlinkInterval = 0.5f; // 깜빡임 간격
    public float laserActiveDuration = 5f;          // 레이저 발동 유지 시간
    public float laserDamage = 1f;                  // 레이저 접촉 시 틱당 피해량

    [Header("레이저 임시 대체용 크기 (Laser Prefab을 비워뒀을 때만 사용됨)")]
    public float fallbackLaserWidth = 20f;           // 프리팹 없을 때 임시 레이저 가로 길이
    public float fallbackLaserThickness = 0.6f;      // 프리팹 없을 때 임시 레이저 두께


    IEnumerator LaserAttackRoutine()
    {
        // 공격 시작 시점의 플레이어 y좌표를 스냅샷으로 고정 (레이저 라인의 높이가 도중에 바뀌지 않도록)
        float laserY = target != null ? target.position.y : transform.position.y;
        Vector2 laserPos = new Vector2(transform.position.x, laserY);

        // 1. 텔레그래프 라인 생성
        GameObject marker = SpawnLaserTelegraph(laserPos);
        if (marker != null) activeTelegraphMarkers.Add(marker);

        // 2. 2초 동안 0.5초 간격으로 투명해졌다 돌아오는 깜빡임
        float elapsed = 0f;
        bool visible = true;
        while (elapsed < laserTelegraphDuration)
        {
            yield return new WaitForSeconds(laserTelegraphBlinkInterval);
            elapsed += laserTelegraphBlinkInterval;
            visible = !visible;
            if (marker != null)
            {
                SpriteRenderer sr = marker.GetComponent<SpriteRenderer>();
                if (sr != null) sr.enabled = visible;
            }
        }

        // 3. 텔레그래프 제거
        if (marker != null) Destroy(marker);
        activeTelegraphMarkers.Remove(marker);

        // 4. 레이저 발사 후 5초 유지
        GameObject laser = SpawnLaser(laserPos);
        if (laser != null) activeLaserObjects.Add(laser);

        yield return new WaitForSeconds(laserActiveDuration);

        // 5. 레이저 제거 (LaserHazard 자체도 lifetime으로 스스로 파괴되지만, 안전하게 이중 처리)
        if (laser != null) Destroy(laser);
        activeLaserObjects.Remove(laser);
    }

    GameObject SpawnLaserTelegraph(Vector2 pos)
    {
        if (laserTelegraphTemplate != null)
        {
            GameObject marker = Instantiate(laserTelegraphTemplate, pos, Quaternion.identity);
            marker.SetActive(true); // 템플릿이 꺼져있어도 복제본은 반드시 켜서 생성
            return marker;
        }

        // 프리팹이 없으면 임시 경고 라인 생성 (반투명 빨간 가로 막대)
        GameObject tempMarker = new GameObject("LaserTelegraph_Temp");
        tempMarker.transform.position = pos;
        SpriteRenderer sr = tempMarker.AddComponent<SpriteRenderer>();
        sr.color = new Color(1f, 0.2f, 0.2f, 0.5f);
        sr.sprite = CreateTempSquareSprite();
        tempMarker.transform.localScale = new Vector3(fallbackLaserWidth, fallbackLaserThickness, 1f);
        return tempMarker;
    }

    GameObject SpawnLaser(Vector2 pos)
    {
        GameObject laser;
        if (laserTemplate != null)
        {
            laser = Instantiate(laserTemplate, pos, Quaternion.identity);
            laser.SetActive(true); // 템플릿이 꺼져있어도 복제본은 반드시 켜서 생성
        }
        else
        {
            // 프리팹이 없으면 임시 레이저 생성 (붉은 가로 막대 + 트리거 콜라이더)
            laser = new GameObject("Laser_Temp");
            laser.transform.position = pos;
            SpriteRenderer sr = laser.AddComponent<SpriteRenderer>();
            sr.color = new Color(1f, 0f, 0f, 0.85f);
            sr.sprite = CreateTempSquareSprite();
            laser.transform.localScale = new Vector3(fallbackLaserWidth, fallbackLaserThickness, 1f);

            BoxCollider2D col = laser.AddComponent<BoxCollider2D>();
            col.isTrigger = true;
        }

        // 플레이어와 물리적으로 부딪히지 않도록 모든 콜라이더를 트리거로 강제 설정
        ForceAllCollidersToTrigger(laser);

        LaserHazard hazard = laser.GetComponent<LaserHazard>();
        if (hazard == null) hazard = laser.AddComponent<LaserHazard>();
        hazard.lifetime = laserActiveDuration;
        hazard.damage = laserDamage;


        return laser;
    }

    // ================= 서리비 공격 (1페이즈) =================
    [Header("서리비 공격 설정")]
    public GameObject frostCrystalPrefab;            // 서리 수정 프리팹 (비워두면 임시 생성)
    public GameObject frostCrystalHitboxPrefab;       // 피격 반경을 결정할 별도 히트박스 오브젝트 (ContactRelay + Collider2D 필요, 비워두면 수정 자체 콜라이더로 판정)
    GameObject frostCrystalTemplate;                  // frostCrystalPrefab의 런타임 복제 템플릿 (원본 보호용)
    GameObject frostCrystalHitboxTemplate;            // frostCrystalHitboxPrefab의 런타임 복제 템플릿 (원본 보호용)
    public float frostSpawnYAboveBoss = 6f;      // 보스보다 이만큼 높은 Y좌표에서 생성
    public float frostRainDuration = 4f;              // 비가 내리는 총 시간
    public float frostSpawnInterval = 0.3f;           // 생성 주기
    public int frostSpawnCountPerTick = 5;            // 주기마다 생성되는 개수
    public float frostRangeX = 12f;                // 보스 X좌표 기준 좌우로 퍼지는 폭 (예: 12면 보스 기준 -6 ~ +6 범위)
    public float frostFallInitialSpeed = 0f;          // 낙하 시작 속도
    public float frostFallAcceleration = 15f;         // 낙하 가속도
    public float frostDamage = 1f;                    // 서리 수정 접촉 시 피해량
    public float frostMaxLifetime = 6f;               // 바닥에 못 닿았을 때 안전장치용 최대 생존 시간

    [Header("서리비 텔레그래프 설정")]
    public GameObject frostTelegraphMarkerPrefab;      // 세로 경고선 프리팹 (비워두면 임시 마커 생성)
    GameObject frostTelegraphMarkerTemplate;           // frostTelegraphMarkerPrefab의 런타임 복제 템플릿 (원본 보호용)

    [Header("서리비 텔레그래프 (씬에 미리 배치된 오브젝트 사용 시)")]
    public List<GameObject> frostTelegraphMarkers = new List<GameObject>(); // 보스의 자식으로 미리 배치해둔 텔레그래프들 (Inspector에서 연결). 비워두면 기존 동적 생성 방식 사용
    public float frostTelegraphDuration = 2f;          // 텔레그래프 지속 시간
    public float frostTelegraphBlinkInterval = 0.5f;   // 깜빡임 간격
    public int frostTelegraphColumnCount = 10;         // frostRangeX 범위를 몇 개의 세로 열로 나눠서 검사할지
    public float frostTelegraphCheckDistance = 30f;    // 스폰 지점에서 땅이 있는지 확인하는 레이캐스트 거리
    public float frostTelegraphLineLength = 15f;       // 프리팹이 없을 때 임시 경고선의 세로 길이
    public float frostTelegraphLineThickness = 0.3f;   // 프리팹이 없을 때 임시 경고선의 두께

    IEnumerator FrostRainAttackRoutine()
    {
        // 1. frostRangeX 범위를 frostTelegraphColumnCount개의 세로 열로 나누고,
        //    각 열의 스폰 지점에서 아래로 레이캐스트를 쏴서 땅이 있는 열은 제외함
        float half = frostRangeX * 0.5f;
        float spawnY = transform.position.y + frostSpawnYAboveBoss;

        // 각 열 위치에 텔레그래프 프리팹을 그대로 생성 (프리팹이 이미 맵 모양에 맞게 잘려있으므로 별도 스케일/길이 계산 없음)
        List<GameObject> frostMarkers = new List<GameObject>();
        for (int i = 0; i < frostTelegraphColumnCount; i++)
        {
            float t = frostTelegraphColumnCount <= 1 ? 0.5f : (float)i / (frostTelegraphColumnCount - 1);
            float x = transform.position.x - half + frostRangeX * t;

            GameObject marker = SpawnFrostTelegraph(new Vector2(x, spawnY));
            if (marker != null) frostMarkers.Add(marker);
        }

        // 3. 2초 동안 0.5초 간격으로 깜빡임
        float telegraphElapsed = 0f;
        bool visible = true;
        while (telegraphElapsed < frostTelegraphDuration)
        {
            yield return new WaitForSeconds(frostTelegraphBlinkInterval);
            telegraphElapsed += frostTelegraphBlinkInterval;
            visible = !visible;
            foreach (var marker in frostMarkers)
            {
                if (marker == null) continue;
                SpriteRenderer sr = marker.GetComponent<SpriteRenderer>();
                if (sr != null) sr.enabled = visible;
            }
        }

        // 4. 텔레그래프 제거
        foreach (var marker in frostMarkers)
        {
            if (marker != null) Destroy(marker);
        }

        // 5. 서리비 시작 (기존 로직 그대로)
        float elapsed = 0f;
        while (elapsed < frostRainDuration)
        {
            yield return StartCoroutine(SpawnFrostTick());
            elapsed += frostSpawnInterval;
        }
    }

    // frostSpawnInterval 구간 안에서 무작위 시점 여러 개를 뽑아, 그 시점마다 하나씩 서리 수정을 생성
    // (한 번에 다 쏟아지지 않고 진짜 비처럼 흩어져서 떨어지게 하기 위함)
    IEnumerator SpawnFrostTick()
    {
        // 0 ~ frostSpawnInterval 사이의 무작위 시점을 개수만큼 뽑아서 오름차순 정렬
        List<float> spawnTimes = new List<float>();
        for (int i = 0; i < frostSpawnCountPerTick; i++)
        {
            spawnTimes.Add(Random.Range(0f, frostSpawnInterval));
        }
        spawnTimes.Sort();

        float prevTime = 0f;
        foreach (var t in spawnTimes)
        {
            float wait = t - prevTime;
            if (wait > 0f) yield return new WaitForSeconds(wait);

            float x = transform.position.x + Random.Range(-frostRangeX * 0.5f, frostRangeX * 0.5f);
            float y = transform.position.y + frostSpawnYAboveBoss;
            SpawnFrostCrystal(new Vector2(x, y));

            prevTime = t;
        }

        // 구간의 나머지 시간을 채워서 다음 tick과 정확히 frostSpawnInterval 간격을 유지
        float remaining = frostSpawnInterval - prevTime;
        if (remaining > 0f) yield return new WaitForSeconds(remaining);
    }

    // 서리비 범위 표시용 경고 마커 생성 (프리팹이 이미 원하는 모양/크기로 잘려있으므로 그대로 생성만 함)
    GameObject SpawnFrostTelegraph(Vector2 spawnPos)
    {
        if (frostTelegraphMarkerTemplate != null)
        {
            GameObject marker = Instantiate(frostTelegraphMarkerTemplate, spawnPos, Quaternion.identity);
            marker.SetActive(true); // 템플릿이 꺼져있어도 복제본은 반드시 켜서 생성
            return marker;
        }

        // 프리팹이 없으면 임시 마커 생성 (반투명 하늘색 사각형)
        GameObject tempMarker = new GameObject("FrostTelegraph_Temp");
        tempMarker.transform.position = spawnPos;
        SpriteRenderer sr = tempMarker.AddComponent<SpriteRenderer>();
        sr.color = new Color(0.6f, 0.85f, 1f, 0.5f);
        sr.sprite = CreateTempSquareSprite();
        tempMarker.transform.localScale = Vector3.one;
        return tempMarker;
    }

    void SpawnFrostCrystal(Vector2 pos)
    {
        GameObject crystal;
        if (frostCrystalTemplate != null)
        {
            // 프리팹 원본을 그대로 복제 (위치, 회전, 크기, 자식 구조 등 모든 게 원본과 동일하게 유지됨)
            // 이후 위치만 원하는 스폰 지점으로 옮김
            crystal = Instantiate(frostCrystalTemplate);
            crystal.transform.position = pos;
            crystal.SetActive(true);
        }
        else
        {
            // 프리팹이 없으면 임시 서리 수정 생성 (하늘색 사각형 + 트리거 콜라이더)
            crystal = new GameObject("FrostCrystal_Temp");
            crystal.transform.position = pos;
            SpriteRenderer sr = crystal.AddComponent<SpriteRenderer>();
            sr.color = new Color(0.7f, 0.9f, 1f, 1f);
            sr.sprite = CreateTempSquareSprite();
            crystal.transform.localScale = new Vector3(0.4f, 0.4f, 1f);

            CircleCollider2D col = crystal.AddComponent<CircleCollider2D>();
            col.isTrigger = true;
        }

        ForceAllCollidersToTrigger(crystal);

        Rigidbody2D rb = crystal.GetComponent<Rigidbody2D>();
        if (rb == null) rb = crystal.AddComponent<Rigidbody2D>();
        rb.bodyType = RigidbodyType2D.Kinematic; // 물리 충돌엔 안 밀리고 트리거 이벤트만 받기 위함
        rb.gravityScale = 0f; // 실제 낙하는 FrostCrystalHazard가 직접 이동시킴
        rb.constraints = RigidbodyConstraints2D.FreezeRotation; // 물리 연산으로 인한 의도치 않은 회전만 막음 (디자인된 회전은 유지)

        FrostCrystalHazard hazard = crystal.GetComponent<FrostCrystalHazard>();
        if (hazard == null) hazard = crystal.AddComponent<FrostCrystalHazard>();
        hazard.initialSpeed = frostFallInitialSpeed;
        hazard.acceleration = frostFallAcceleration;
        hazard.damage = frostDamage;
        hazard.maxLifetime = frostMaxLifetime;
        hazard.groundLayer = groundLayer;

        // 피격 반경용 별도 히트박스 오브젝트를 자식으로 붙임 (연결되어 있을 때만)
        if (frostCrystalHitboxTemplate != null)
        {
            GameObject hitboxInstance = Instantiate(frostCrystalHitboxTemplate, crystal.transform);
            hitboxInstance.SetActive(true);
            hitboxInstance.transform.localPosition = Vector3.zero;

            ForceAllCollidersToTrigger(hitboxInstance); // 히트박스 콜라이더도 트리거로 강제 설정 (플레이어 밀림 방지)

            ContactRelay relay = hitboxInstance.GetComponent<ContactRelay>();
            if (relay != null) hazard.SetHitboxRelay(relay);
        }
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
    void OnDisable()
    { 
        foreach (var marker in activeTelegraphMarkers)
        {
            if (marker != null) Destroy(marker);
        }
        activeTelegraphMarkers.Clear();

        // 발동 중이던 레이저도 코루틴이 강제 중단되면 남을 수 있으므로 함께 정리
        foreach (var laser in activeLaserObjects)
        {
            if (laser != null) Destroy(laser);
        }
        activeLaserObjects.Clear();
    }
}