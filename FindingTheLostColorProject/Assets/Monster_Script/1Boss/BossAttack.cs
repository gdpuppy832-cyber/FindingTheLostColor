using UnityEngine;
using System.Collections;
using System.Collections.Generic;

public class BossAttack : MonoBehaviour
{
    // ===== 공통 =====
    public Transform target;               // 비워두면 Player 태그로 자동 탐색
    public LayerMask groundLayer;
    public float attackCooldown = 1f;       // 공격 종료 후 다음 공격까지 대기 시간
    public float bossAttackDamage = 1f;     // 모든 보스 공격(가시/레이저/서리/암흑구슬/번개/암영결계)이 공통으로 사용하는 피해량

    [Header("Contact Damage")]
    public GameObject contactHitbox;        // 보스 본체와 별도로 몸통 충돌 피해를 판정할 히트박스 오브젝트 (ContactHit + Collider2D 필요)
    public float contactDamage = 1f;        // 몸통 충돌 시 입히는 피해량
    Vector3 contactHitboxOffset; // 보스 계층에서 분리된 후에도 위치를 따라가기 위한 오프셋 (보스 기준 상대 위치)


    public List<BossCrystal> crystals = new List<BossCrystal>(); // 씬에 미리 배치된 크리스탈들을 Inspector에서 연결 (BossCrystal은 NormalMonster를 상속하므로 CursorController가 그대로 붓질 감지함)

    [Header("Color Orb")]
    public GameObject colorOrbPrefab;          // 색채 구슬 프리팹 (ColorOrb 컴포넌트 자동 부착됨, 비워두면 임시 생성)
    public float colorOrbHealth = 15f;         // 색채 구슬 체력
    public float colorOrbSpawnOffsetY = -2f;   // 보스 기준 아래로 얼마나 떨어진 위치에 소환할지

    [Header("Black Fogs")]
    public List<BlackFog> blackFogs = new List<BlackFog>(); // 씬에 미리 배치된 좌/우 안개 오브젝트들을 Inspector에서 연결

    bool phase2Unlocked = false; // false면 크리스탈 페이즈, true면 2페이즈(공격 가능)
    int destroyedCrystalCount = 0;
    Collider2D[] bossOwnColliders; // 크리스탈 페이즈 동안 붓질(OverlapCircleAll) 감지를 막기 위해 비활성화할 보스 콜라이더

    bool isAttacking = false;
    float nextAttackAllowedTime = 0f;
    Coroutine currentAttackCoroutine; // 2페이즈 전환 시 진행 중인 공격을 정확히 멈추기 위한 참조
    Vector3 initialPosition; // 보스가 처음 배치된 위치 (구역 공격 등 위치 고정이 필요한 공격의 기준점)
    BossMove flyMove; // 2페이즈 진입 시 무한대(∞) 이동으로 전환하기 위해 자동으로 찾아두는 참조
    NormalMonster bossHealth; // 크리스탈 파괴 시 체력 회복을 위해 자동으로 찾아두는 참조 (보스 자신의 NormalMonster)

    List<GameObject> activeTelegraphMarkers = new List<GameObject>();
    List<GameObject> activeLaserObjects = new List<GameObject>(); // 발동 중인 레이저 본체도 강제 중단 시 정리 대상에 포함
    List<GameObject> activeSpikes = new List<GameObject>();        // 소환된 가시도 2페이즈 전환 시 강제 정리 대상에 포함
    List<GameObject> activeFrostCrystals = new List<GameObject>(); // 소환된 서리 수정도 2페이즈 전환 시 강제 정리 대상에 포함
    List<GameObject> activeDarkOrbs = new List<GameObject>();       // 암흑 구슬도 강제 정리 대상에 포함
    GameObject activeDarkCloud;                                     // 현재 진행 중인 먹구름 (동시에 하나만 존재)
    GameObject activeLightning;                                     // 현재 발동 중인 번개

    // ================= 색채 소용돌이 (1P/2P 공용, 랜덤 풀에는 포함되지 않고 다른 패턴과 동시에 발동) =================
    [Header("Color Whirlpool")]
    public GameObject colorWhirlpoolPrefab;              // 색채 소용돌이 프리팹 (비워두면 임시 생성)
    GameObject colorWhirlpoolTemplate;                    // colorWhirlpoolPrefab의 런타임 복제 템플릿 (원본 보호용)
    [Tooltip("보스의 처음 배치 위치(initialPosition) 기준 오프셋 (Y를 음수로 하면 아래쪽에 소환됨)")]
    public Vector2 colorWhirlpoolSpawnOffset = new Vector2(0f, -3f);
    [Tooltip("소용돌이가 서서히 나타나는 시간 (초)")]
    public float colorWhirlpoolFadeInDuration = 2f;
    [Tooltip("끌어당김 판정 반경 (피해 판정은 프리팹의 콜라이더 크기로 결정됨)")]
    public float colorWhirlpoolPullRadius = 5f;
    [Tooltip("반경 안에서 중심으로 끌어당기는 힘")]
    public float colorWhirlpoolPullForce = 10f;
    [Tooltip("이 거리 이내는 중앙 구역으로 취급해 끌어당기지 않고 둔화만 적용")]
    public float colorWhirlpoolMinEffectiveDistance = 0.5f;
    [Tooltip("끌려가는 속도의 최대치. 무적 시간 등으로 속도 리셋이 없어도 이 이상 빨라지지 않음")]
    public float colorWhirlpoolMaxPullSpeed = 8f;
    [Tooltip("소용돌이가 유지되는 최대 시간 (초). 짝지어진 다른 공격이 이보다 먼저 끝나면 그때 같이 종료됨")]
    public float colorWhirlpoolDuration = 10f;
    public float fallbackColorWhirlpoolSize = 5f;        // 프리팹 없을 때 임시 소용돌이 크기
    [Tooltip("소용돌이를 제외한 다른 공격이 이 횟수만큼 나온 뒤, 다음 공격 때 소용돌이가 함께 발동됨 (일종의 '쿨타임' 역할)")]
    public int colorWhirlpoolTriggerCount = 3;

    int nonWhirlpoolAttackCount = 0;      // 소용돌이를 제외한 다른 패턴이 몇 번 나왔는지 (3이 되면 다음 기회에 소용돌이 발동)
    Coroutine activeWhirlpoolCoroutine;    // 현재 진행 중인 소용돌이 코루틴 (함께 시작된 패턴이 끝나면 강제 종료용)
    GameObject activeColorWhirlpool;       // 현재 씬에 존재하는 소용돌이 오브젝트

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
            new AttackEntry("ShadowBarrier", ShadowBarrierAttackRoutine),
            new AttackEntry("DarkOrb", DarkOrbAttackRoutine),
            new AttackEntry("DarkCloud", DarkCloudAttackRoutine),
        };
    }

    void Start()
    {
        initialPosition = transform.position; // 보스가 처음 배치된 위치를 기록 (구역 공격 기준점)

        if (target == null)
        {
            GameObject player = GameObject.FindGameObjectWithTag("Player");
            if (player != null) target = player.transform;
        }



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

        if (frostTelegraphMarkerPrefab != null)
        {
            frostTelegraphMarkerTemplate = Instantiate(frostTelegraphMarkerPrefab, frostTelegraphMarkerPrefab.transform.position, frostTelegraphMarkerPrefab.transform.rotation);
            frostTelegraphMarkerTemplate.transform.SetParent(null);
            frostTelegraphMarkerTemplate.SetActive(false);
            frostTelegraphMarkerPrefab.SetActive(false);
        }

        if (shadowBarrierTelegraphPrefab != null)
        {
            shadowBarrierTelegraphTemplate = Instantiate(shadowBarrierTelegraphPrefab, shadowBarrierTelegraphPrefab.transform.position, shadowBarrierTelegraphPrefab.transform.rotation);
            shadowBarrierTelegraphTemplate.transform.SetParent(null);
            shadowBarrierTelegraphTemplate.SetActive(false);
            shadowBarrierTelegraphPrefab.SetActive(false); // 원본(보스의 자식)은 그냥 숨겨만 두고 다시는 건드리지 않음
        }

        if (shadowBarrierHazardPrefab != null)
        {
            shadowBarrierHazardTemplate = Instantiate(shadowBarrierHazardPrefab, shadowBarrierHazardPrefab.transform.position, shadowBarrierHazardPrefab.transform.rotation);
            shadowBarrierHazardTemplate.transform.SetParent(null);
            shadowBarrierHazardTemplate.SetActive(false);
            shadowBarrierHazardPrefab.SetActive(false);
        }

        if (darkOrbPrefab != null)
        {
            darkOrbTemplate = Instantiate(darkOrbPrefab, darkOrbPrefab.transform.position, darkOrbPrefab.transform.rotation);
            darkOrbTemplate.transform.SetParent(null);
            darkOrbTemplate.SetActive(false);
            darkOrbPrefab.SetActive(false);
        }

        if (darkCloudPrefab != null)
        {
            darkCloudTemplate = Instantiate(darkCloudPrefab, darkCloudPrefab.transform.position, darkCloudPrefab.transform.rotation);
            darkCloudTemplate.transform.SetParent(null);
            darkCloudTemplate.SetActive(false);
            darkCloudPrefab.SetActive(false);
        }

        if (lightningPrefab != null)
        {
            lightningTemplate = Instantiate(lightningPrefab, lightningPrefab.transform.position, lightningPrefab.transform.rotation);
            lightningTemplate.transform.SetParent(null);
            lightningTemplate.SetActive(false);
            lightningPrefab.SetActive(false);
        }

        if (colorWhirlpoolPrefab != null)
        {
            colorWhirlpoolTemplate = Instantiate(colorWhirlpoolPrefab, colorWhirlpoolPrefab.transform.position, colorWhirlpoolPrefab.transform.rotation);
            colorWhirlpoolTemplate.transform.SetParent(null);
            colorWhirlpoolTemplate.SetActive(false);
            colorWhirlpoolPrefab.SetActive(false);
        }

        if (contactHitbox != null)
        {
            contactHitboxOffset = contactHitbox.transform.position - transform.position; // 분리 전 상대 위치 기록
            contactHitbox.transform.SetParent(null, true); // worldPositionStays: true → 위치 유지하며 분리

            contactHitbox.gameObject.layer = 0; // Default

            Collider2D hitboxCol = contactHitbox.GetComponent<Collider2D>();
            if (hitboxCol != null)
            {
                hitboxCol.isTrigger = true;
                hitboxCol.enabled = true; // 1페이즈/2페이즈 관계없이 항상 켜둠
            }

            ContactHit relay = contactHitbox.GetComponent<ContactHit>();
            if (relay == null) relay = contactHitbox.AddComponent<ContactHit>();
            relay.onTriggerStay += TryContactDamage;
        }

        List<Collider2D> ownColliderList = new List<Collider2D>();
        Collider2D[] allChildColliders = GetComponentsInChildren<Collider2D>(true);
        foreach (var col in allChildColliders)
        {
            if (col == null) continue;
            if (col.GetComponentInParent<BossCrystal>() != null) continue; // 크리스탈 콜라이더는 그대로 둠
            ownColliderList.Add(col);
            col.isTrigger = true;
        }
        bossOwnColliders = ownColliderList.ToArray();

        // 크리스탈 페이즈(1페이즈) 동안에는 보스 자신 + 자식의 콜라이더를 모두 꺼서
        // CursorController의 OverlapCircleAll에 아예 걸리지 않게 함 (붓질로 체력이 차거나 공격당하는 것을 원천 차단)
        SetBossColliderState(false);



        flyMove = GetComponent<BossMove>();
        if (flyMove == null) flyMove = GetComponentInChildren<BossMove>();

        bossHealth = GetComponent<NormalMonster>();

        // 크리스탈들의 파괴 이벤트를 구독해서 전부 파괴되면 2페이즈로 전환
        foreach (var crystal in crystals)
        {
            if (crystal != null) crystal.OnCrystalDestroyed += HandleCrystalDestroyed;
        }

        // 서리비 텔레그래프: 보스의 자식으로 미리 배치돼 있으면 보스가 움직일 때 같이 움직이므로,
        // 현재 월드 위치를 유지한 채로 부모에서 분리(SetParent(null, true))하고 평소엔 꺼둠
        foreach (var marker in frostTelegraphMarkers)
        {
            if (marker == null) continue;
            marker.transform.SetParent(null, true); // worldPositionStays: true → 위치 그대로 유지하며 분리
            marker.SetActive(false);
        }
    }

    void HandleCrystalDestroyed()
    {
        destroyedCrystalCount++;


        // "실제로 이 컴포넌트가 꺼져서 코루틴이 강제 종료됐었는지"를 재활성화하기 전에 먼저 확인.
        // 이 값을 나중에 확인하면 이미 enabled = true로 바뀐 뒤라 항상 false로 나와서 구분이 안 됨
        bool wasDisabled = !enabled;

        if (wasDisabled) enabled = true;

        // 컴포넌트가 실제로 꺼졌던 경우에만 공격 상태를 복구함.
        // (컴포넌트가 안 꺼졌다면 진행 중인 공격 코루틴은 여전히 살아있으므로 isAttacking을 건드리면 안 됨 -
        //  건드리면 Update()가 새 공격을 중복 시작시켜서, 두 코루틴이 activeTelegraphMarkers/activeLaserObjects를
        //  같이 건드리다가 진행 중이던 텔레그래프가 엉뚱하게 파괴되는 문제가 있었음)
        if (wasDisabled)
        {
            isAttacking = false;
            nextAttackAllowedTime = Time.time + attackCooldown;

            // 컴포넌트가 꺼지면서 코루틴이 중간에 죽어 정리가 안 됐을 수 있는 잔여 오브젝트 정리
            foreach (var marker in activeTelegraphMarkers)
            {
                if (marker != null) Destroy(marker);
            }
            activeTelegraphMarkers.Clear();

            foreach (var laser in activeLaserObjects)
            {
                if (laser != null) Destroy(laser);
            }
            activeLaserObjects.Clear();
        }

        if (destroyedCrystalCount >= crystals.Count)
        {
            phase2Unlocked = true;
            nonWhirlpoolAttackCount = 0; // 페이즈 전환 시 소용돌이 발동 카운트 리셋

            // 2페이즈로 넘어가는 순간, 진행 중이던 1페이즈 공격을 강제로 중단시킴
            if (isAttacking && currentAttackCoroutine != null)
            {
                StopCoroutine(currentAttackCoroutine);
                currentAttackCoroutine = null;

                // 함께 진행 중이던 소용돌이도 강제 종료
                if (activeWhirlpoolCoroutine != null)
                {
                    StopCoroutine(activeWhirlpoolCoroutine);
                    activeWhirlpoolCoroutine = null;
                }
                if (activeColorWhirlpool != null)
                {
                    Destroy(activeColorWhirlpool);
                    activeColorWhirlpool = null;
                }

                // 코루틴이 중간에 끊기면서 스스로 정리하지 못한 잔여 오브젝트들을 직접 정리
                foreach (var marker in activeTelegraphMarkers)
                {
                    if (marker != null) Destroy(marker);
                }
                activeTelegraphMarkers.Clear();

                foreach (var laser in activeLaserObjects)
                {
                    if (laser != null) Destroy(laser);
                }
                activeLaserObjects.Clear();

                foreach (var spike in activeSpikes)
                {
                    if (spike != null) Destroy(spike);
                }
                activeSpikes.Clear();

                foreach (var crystal in activeFrostCrystals)
                {
                    if (crystal != null) Destroy(crystal);
                }
                activeFrostCrystals.Clear();

                // 서리비 공격 중이었다면, 미리 배치된 텔레그래프도 다시 숨겨줌
                foreach (var marker in frostTelegraphMarkers)
                {
                    if (marker == null) continue;
                    SpriteRenderer sr = marker.GetComponentInChildren<SpriteRenderer>();
                    if (sr != null) sr.enabled = true; // 다음 공격을 위해 보이는 상태로 초기화
                    marker.SetActive(false);
                }

                isAttacking = false;
                nextAttackAllowedTime = Time.time + attackCooldown;
            }

            SetBossColliderState(true);
            if (flyMove != null) flyMove.SetInfinityMode(true); // 2페이즈 진입과 동시에 무한대(∞) 이동 패턴으로 전환

            // 크리스탈 4개 파괴 보상: 보스 체력을 최대 체력의 절반만큼 회복
            if (bossHealth != null)
            {
                bossHealth.Heal(bossHealth.maxHealth * 0.5f);
            }

            ColorOrb spawnedOrb = SpawnColorOrb(); // 2페이즈 진입과 동시에 보스 아래에 색채 구슬 소환

            foreach (var fog in blackFogs)
            {
                if (fog == null) continue;
                fog.SetTarget(spawnedOrb);
                fog.StartMoving();
            }
        }
    }


    void TryContactDamage(Collider2D other)
    {
        if (bossHealth != null && bossHealth.IsPurified) return;

        PlayerHealth player = other.GetComponent<PlayerHealth>();
        if (player == null) player = other.GetComponentInParent<PlayerHealth>();

        if (player != null)
        {
            player.TakeDamage(contactDamage);
        }
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
        // 보스 계층에서 분리된 contactHitbox가 보스를 계속 따라다니도록 매 프레임 위치 동기화
        if (contactHitbox != null)
        {
            contactHitbox.transform.position = transform.position + contactHitboxOffset;
        }

        if (isAttacking || Time.time < nextAttackAllowedTime || target == null)
            return;



        List<AttackEntry> pool = GetCurrentPhasePool();
        if (pool == null || pool.Count == 0) return;

        // 소용돌이가 떠 있는 동안에는 프리즘 샤워(FrostRain)를 후보에서 제외
        // (소용돌이는 애초에 FrostRain과 함께 시작되지 않지만, 이미 떠 있는 상태에서
        //  나중에 FrostRain이 선택되는 것도 막기 위함)
        if (activeColorWhirlpool != null)
        {
            pool = pool.FindAll(a => a.name != "FrostRain");
            if (pool.Count == 0) return;
        }

        AttackEntry chosen = PickRandomAttack(pool);
        if (chosen == null) return;

        currentAttackCoroutine = StartCoroutine(RunAttack(chosen));
    }

    List<AttackEntry> GetCurrentPhasePool()
    {
        // 체력 비율이 아니라 크리스탈 파괴 여부로 페이즈가 결정됨
        // 크리스탈이 남아있으면 1페이즈 풀, 다 깨지면(phase2Unlocked) 2페이즈 풀
        // 2페이즈에서는 1페이즈 공격으로 폴백하지 않음 - 2페이즈 공격이 아직 없다면
        // (Update()에서 pool.Count == 0으로 처리되어) 보스가 그냥 대기 상태가 됨
        return phase2Unlocked ? phase2Attacks : phase1Attacks;
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

        // 프리즘 샤워 직전에 나왔던 패턴을 기억해뒀다가, 동반 패턴 선정 시 제외하기 위해
        // lastUsedAttack을 덮어쓰기 전에 미리 별도 변수로 백업해둠
        AttackEntry previousAttack = lastUsedAttack;
        lastUsedAttack = attack;

        // 소용돌이는 프리즘 샤워(FrostRain)와 절대 겹치지 않으며,
        // 소용돌이 제외 다른 패턴이 colorWhirlpoolTriggerCount번 나온 뒤에야 다음 기회에 발동함
        bool triggerWhirlpool = attack.name != "FrostRain" && nonWhirlpoolAttackCount >= colorWhirlpoolTriggerCount;

        if (triggerWhirlpool)
        {
            activeWhirlpoolCoroutine = StartCoroutine(ColorWhirlpoolAttackRoutine());
            nonWhirlpoolAttackCount = 0;
        }

        Coroutine companionCoroutine = null;

        if (attack.name == "FrostRain")
        {
            // 프리즘 샤워는 텔레그래프 단계를 먼저 끝낸 뒤에야 동반 패턴을 시작시킴
            // (동반 패턴의 텔레그래프가 프리즘 샤워 텔레그래프와 겹쳐 보이지 않도록)
            yield return RunFrostTelegraphPhase();

            AttackEntry companion = PickFrostRainCompanion(previousAttack);
            if (companion != null)
            {
                companionCoroutine = StartCoroutine(companion.routine());
            }

            yield return RunFrostRainPhase();
        }
        else
        {
            // StartCoroutine으로 한 번 더 감싸면 별도의 독립 코루틴이 되어버려서,
            // 이 RunAttack 코루틴만 StopCoroutine 해도 안쪽 attack.routine()은 안 멈추는 문제가 있었음
            // (서리비 등 attack.routine() 내부에서 또 코루틴을 중첩 시작하는 경우 특히 문제)
            // -> 같은 코루틴 체인으로 직접 실행되도록 변경
            yield return attack.routine();
        }

        // 동반 패턴이 프리즘 샤워보다 오래 지속되는 경우, 끝날 때까지 마저 대기
        // (동반 패턴이 먼저 끝났다면 이미 완료된 코루틴이라 즉시 통과됨)
        if (companionCoroutine != null)
        {
            yield return companionCoroutine;
        }

        // 소용돌이는 함께 시작된 패턴이 먼저 끝나도 강제 종료하지 않고,
        // 자기 자신의 colorWhirlpoolDuration이 다 될 때까지 독립적으로 유지됨
        if (!triggerWhirlpool)
        {
            nonWhirlpoolAttackCount++;
        }

        isAttacking = false;
        nextAttackAllowedTime = Time.time + attackCooldown;
    }

    // 프리즘 샤워와 함께 나올 동반 패턴을 고름.
    // frostRainCompanionChance 확률로 아예 동반 패턴 없이 진행될 수도 있음.
    // previousAttack(프리즘 샤워 직전에 나왔던 패턴)은 후보에서 제외됨.
    AttackEntry PickFrostRainCompanion(AttackEntry previousAttack)
    {
        if (Random.value > frostRainCompanionChance) return null;

        List<AttackEntry> candidates = new List<AttackEntry>(phase1Attacks);
        candidates.RemoveAll(a => a.name == "FrostRain");
        if (previousAttack != null) candidates.Remove(previousAttack);

        if (candidates.Count == 0) return null;

        return candidates[Random.Range(0, candidates.Count)];
    }

    // ================= 가시 함정 공격 (1페이즈) =================
    [Header("1P Spike")]
    public GameObject telegraphMarkerPrefab;      // 경고 표시 프리팹 (SpriteRenderer 포함, 비워두면 임시 마커 생성)
    public GameObject spikePrefab;                // 가시 프리팹 (Collider2D는 Trigger로, 비워두면 임시 가시 생성)
    GameObject telegraphMarkerTemplate;           // telegraphMarkerPrefab의 런타임 복제 템플릿 (원본 보호용)
    GameObject spikeTemplate;                     // spikePrefab의 런타임 복제 템플릿 (원본 보호용)
    public float spikeTelegraphDuration = 1.5f;      // 텔레그래프 지속 시간
    public float spikeTelegraphBlinkInterval = 0.5f; // 깜빡임 간격
    [Tooltip("가시 유지 시간")]
    public float spikeLifetime = 3f;               // 가시가 유지되는 시간
    [Tooltip("가시 생성 반경")]
    public float spikeSearchRadius = 8f;           // 보스 주변 랜덤 위치 탐색 반경
    public float spikeGroundRaycastDistance = 20f; // 바닥 탐색용 레이캐스트 최대 거리
    public int spikeMaxSearchAttempts = 20;        // 유효 바닥 못 찾을 때 재시도 최대 횟수
    public float spikeMinDistance = 1.5f;           // 가시끼리 최소 간격 (겹침 방지)
    public float spikeMaxHeightAboveBoss = 3f;      // 보스 기준 이 값보다 높은 땅에는 가시 생성 안 함 (여유 허용치)

    [Tooltip("두 번째 공격 시작 전 대기 시간 (첫 번째 가시가 사라진 뒤)")]
    public float spikeSecondWaveDelay = 0.5f;

    [Header("Prism Shower Combo")]
    [Range(0f, 1f)]
    [Tooltip("프리즘 샤워(FrostRain) 발동 시, 다른 1페이즈 패턴이 함께 나올 확률 (0 = 절대 안 겹침, 1 = 항상 겹침)")]
    public float frostRainCompanionChance = 0.5f;

    IEnumerator SpikeTrapAttackRoutine()
    {
        // 1차 공격: 위치 4곳 결정 -> 텔레그래프 -> 가시 생성
        List<Vector2> firstPositions = new List<Vector2>();
        yield return RunSpikeTelegraphPhase(firstPositions, null);
        SpawnSpikesAtPositions(firstPositions);

        // 2차 텔레그래프는 "2차 가시가 실제로 생성되는 시점"에 정확히 맞춰 끝나도록 시작 시점을 계산.
        // (1차 가시 생성 시점 기준으로, 2차 가시는 spikeLifetime + spikeSecondWaveDelay 후에 생성되므로
        //  텔레그래프가 그 시점에 딱 끝나도록 그보다 spikeTelegraphDuration만큼 먼저 시작함)
        float timeUntilSecondSpawn = spikeLifetime + spikeSecondWaveDelay;
        float telegraphStartDelay = Mathf.Max(0f, timeUntilSecondSpawn - spikeTelegraphDuration);
        yield return new WaitForSeconds(telegraphStartDelay);

        List<Vector2> secondPositions = new List<Vector2>();
        yield return RunSpikeTelegraphPhase(secondPositions, firstPositions);

        // 텔레그래프 지속 시간이 길어서(spikeTelegraphDuration > timeUntilSecondSpawn인 경우)
        // 계산된 생성 시점보다 늦게 끝났을 수 있으니, 남은 시간이 있다면 마저 대기
        float elapsedSinceFirstSpawn = telegraphStartDelay + spikeTelegraphDuration;
        float remaining = timeUntilSecondSpawn - elapsedSinceFirstSpawn;
        if (remaining > 0f)
        {
            yield return new WaitForSeconds(remaining);
        }

        // 텔레그래프가 끝나는 순간 = 2차 가시가 생성되는 순간
        SpawnSpikesAtPositions(secondPositions);
        yield return new WaitForSeconds(spikeLifetime);
    }

    // 가시 함정 공격의 텔레그래프 단계(위치 결정 -> 텔레그래프 표시 -> 깜빡임 -> 제거)만 처리.
    // 가시 생성은 포함하지 않으므로, 다른 웨이브와 시간을 겹쳐서 동시에 진행시킬 수 있음.
    // resultPositions: 이번 사이클에서 실제로 사용할 위치들이 채워짐 (다음 사이클의 회피 대상으로 재사용)
    // avoidPositions: 이 위치들과는 겹치지 않게 새 위치를 뽑음 (null이면 회피 없음)
    IEnumerator RunSpikeTelegraphPhase(List<Vector2> resultPositions, List<Vector2> avoidPositions)
    {
        // 1. 위치 4곳 결정: 플레이어 위치 1곳 + 랜덤 바닥 위치 3곳
        resultPositions.Add(GetGroundPositionBelow(target.position));

        int found = 0;
        int attempts = 0;
        while (found < 3 && attempts < spikeMaxSearchAttempts)
        {
            attempts++;
            Vector2 randomPoint = (Vector2)transform.position + Random.insideUnitCircle * spikeSearchRadius;
            Vector2? groundPos = TryFindGroundPosition(randomPoint);
            if (groundPos.HasValue
                && IsFarEnough(groundPos.Value, resultPositions)
                && (avoidPositions == null || IsFarEnough(groundPos.Value, avoidPositions)))
            {
                resultPositions.Add(groundPos.Value);
                found++;
            }
        }

        // 2. 각 위치에 텔레그래프 마커 생성
        List<GameObject> markers = new List<GameObject>();
        foreach (var pos in resultPositions)
        {
            GameObject marker = SpawnTelegraphMarker(pos);
            if (marker != null)
            {
                markers.Add(marker);
                activeTelegraphMarkers.Add(marker);
            }
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
            activeTelegraphMarkers.Remove(marker);
        }
    }

    // 주어진 위치들에 실제 가시를 생성 (텔레그래프 단계와 분리되어, 원하는 타이밍에 독립적으로 호출 가능)
    void SpawnSpikesAtPositions(List<Vector2> positions)
    {
        foreach (var pos in positions)
        {
            GameObject spike = SpawnSpike(pos);
            if (spike != null) activeSpikes.Add(spike);
        }
    }

    // ================= 레이저 공격 (1페이즈) =================
    [Header("1P Laser")]
    public GameObject laserTelegraphPrefab;   // 가로로 긴 경고 라인 프리팹 (비워두면 임시 마커 생성)
    public GameObject laserPrefab;            // 레이저 몸체 프리팹 (비워두면 임시 레이저 생성)
    GameObject laserTelegraphTemplate;        // laserTelegraphPrefab의 런타임 복제 템플릿 (원본 보호용)
    GameObject laserTemplate;                 // laserPrefab의 런타임 복제 템플릿 (원본 보호용)
    public float laserTelegraphDuration = 2f;       // 텔레그래프 지속 시간
    public float laserTelegraphBlinkInterval = 0.5f; // 깜빡임 간격
    [Tooltip("레이저 지속 시간")]
    public float laserActiveDuration = 5f;          // 레이저 발동 유지 시간
    

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
        hazard.damage = bossAttackDamage;


        return laser;
    }

    // ================= 서리비 공격 (1페이즈) =================
    [Header("1P FrostCrystal")]
    public GameObject frostCrystalPrefab;            // 서리 수정 프리팹 (비워두면 임시 생성)
    GameObject frostCrystalTemplate;                  // frostCrystalPrefab의 런타임 복제 템플릿 (원본 보호용)
    [Tooltip("보스기준 프리즘 샤워 Y좌표")]
    public float frostRangrY = 6f;      // 보스보다 이만큼 높은 Y좌표에서 생성
    [Tooltip("보스기준 프리즘 샤워 X좌표")]
    public float frostRangeX = 12f;                // 보스 X좌표 기준 좌우로 퍼지는 폭 (예: 12면 보스 기준 -6 ~ +6 범위)
    [Tooltip("공격 시간")]
    public float frostRainDuration = 4f;              // 비가 내리는 총 시간
    [Tooltip("생성 주기")]
    public float frostSpawnInterval = 0.3f;           // 생성 주기
    [Tooltip("주기마다 새성되는 개수")]
    public int frostSpawnCountPerTick = 5;            // 주기마다 생성되는 개수
    
    public float frostFallInitialSpeed = 0f;          // 낙하 시작 속도
    public float frostFallAcceleration = 15f;         // 낙하 가속도
    public float frostMaxLifetime = 6f;               // 바닥에 못 닿았을 때 안전장치용 최대 생존 시간

    public GameObject frostTelegraphMarkerPrefab;      // 세로 경고선 프리팹 (비워두면 임시 마커 생성)
    GameObject frostTelegraphMarkerTemplate;           // frostTelegraphMarkerPrefab의 런타임 복제 템플릿 (원본 보호용)

    public List<GameObject> frostTelegraphMarkers = new List<GameObject>(); // 보스의 자식으로 미리 배치해둔 텔레그래프들 (Inspector에서 연결). 비워두면 기존 동적 생성 방식 사용
    public float frostTelegraphDuration = 2f;          // 텔레그래프 지속 시간
    public float frostTelegraphBlinkInterval = 0.5f;   // 깜빡임 간격
    public int frostTelegraphColumnCount = 10;         // frostRangeX 범위를 몇 개의 세로 열로 나눠서 검사할지
    public float frostTelegraphCheckDistance = 30f;    // 스폰 지점에서 땅이 있는지 확인하는 레이캐스트 거리
    public float frostTelegraphLineLength = 15f;       // 프리팹이 없을 때 임시 경고선의 세로 길이
    public float frostTelegraphLineThickness = 0.3f;   // 프리팹이 없을 때 임시 경고선의 두께

    IEnumerator FrostRainAttackRoutine()
    {
        yield return RunFrostTelegraphPhase();
        yield return RunFrostRainPhase();
    }

    // 프리즘 샤워의 텔레그래프 단계(경고 표시 -> 깜빡임 -> 숨김)만 처리.
    // 동반 패턴을 텔레그래프가 끝난 시점에 맞춰 시작시킬 수 있도록 별도 함수로 분리함.
    IEnumerator RunFrostTelegraphPhase()
    {
        // 1. 씬에 미리 배치된 텔레그래프 오브젝트들을 활성화 (평소엔 꺼져있던 것을 보이게 함)
        foreach (var marker in frostTelegraphMarkers)
        {
            if (marker == null) continue;
            marker.SetActive(true);
            // SpriteRenderer가 꺼진 상태로 저장되어 있을 수 있으므로 활성화 시점에 명시적으로 켜줌
            SpriteRenderer sr = marker.GetComponentInChildren<SpriteRenderer>(true);
            if (sr != null) sr.enabled = true;
        }

        // 2. 2초 동안 0.5초 간격으로 깜빡임
        float telegraphElapsed = 0f;
        bool visible = true;
        while (telegraphElapsed < frostTelegraphDuration)
        {
            yield return new WaitForSeconds(frostTelegraphBlinkInterval);
            telegraphElapsed += frostTelegraphBlinkInterval;
            visible = !visible;
            foreach (var marker in frostTelegraphMarkers)
            {
                if (marker == null) continue;
                SpriteRenderer sr = marker.GetComponentInChildren<SpriteRenderer>();
                if (sr != null) sr.enabled = visible;
            }
        }

        // 3. 텔레그래프를 다시 숨김 (Destroy가 아니라 SetActive(false) - 오브젝트는 계속 재사용)
        foreach (var marker in frostTelegraphMarkers)
        {
            if (marker == null) continue;
            SpriteRenderer sr = marker.GetComponentInChildren<SpriteRenderer>();
            if (sr != null) sr.enabled = true; // 다음 공격 때를 위해 깜빡임 상태를 보이는 상태로 초기화
            marker.SetActive(false);
        }
    }

    // 프리즘 샤워의 실제 서리비 낙하 단계만 처리 (텔레그래프 단계와 분리)
    IEnumerator RunFrostRainPhase()
    {
        // SpawnFrostTick도 StartCoroutine으로 중첩시키면 독립 코루틴이 되어
        // 바깥의 RunAttack을 멈춰도 계속 살아남는 문제가 있으므로 직접 yield
        float elapsed = 0f;
        while (elapsed < frostRainDuration)
        {
            yield return SpawnFrostTick();
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

            // 보스의 현재 위치가 아니라 처음 배치된 위치(initialPosition) 기준으로 스폰
            // -> 2페이즈에서 보스가 이동해도 서리비 범위가 항상 동일한 위치에 고정됨
            float x = initialPosition.x + Random.Range(-frostRangeX * 0.5f, frostRangeX * 0.5f);
            float y = initialPosition.y + frostRangrY;
            GameObject crystal = SpawnFrostCrystal(new Vector2(x, y));
            if (crystal != null) activeFrostCrystals.Add(crystal);

            prevTime = t;
        }

        // 구간의 나머지 시간을 채워서 다음 tick과 정확히 frostSpawnInterval 간격을 유지
        float remaining = frostSpawnInterval - prevTime;
        if (remaining > 0f) yield return new WaitForSeconds(remaining);
    }
    // ================= 암흑 구슬 공격 (2페이즈) =================
    [Header("2P DarkOrb")]
    public GameObject darkOrbPrefab;              // 암흑 구슬 프리팹 (비워두면 임시 생성)
    GameObject darkOrbTemplate;                    // darkOrbPrefab의 런타임 복제 템플릿 (원본 보호용)
    public int darkOrbCount = 3;                   // 구슬 개수
    public float darkOrbOrbitRadius = 2f;          // 보스 주위를 도는 궤도 반지름
    [Tooltip("회전 시간")]
    public float darkOrbOrbitDuration = 2f;        // 궤도 회전 지속 시간 (이 시간 동안 한 바퀴 돎)
    [Tooltip("발사 간격")]
    public float darkOrbLaunchInterval = 0.8f;     // 구슬이 하나씩 발사되는 간격
    [Tooltip("추적 시간")]
    public float darkOrbTrackDuration = 2f;        // 발사된 구슬이 플레이어를 추적하는 시간
    [Tooltip("속도(플레이어 속도 기준 배율)")]
    public float darkOrbSpeedMultiplier = 1f;      // 플레이어 속도 기준 배율 (1보다 크면 플레이어보다 빠르게, 작으면 느리게

    public float fallbackDarkOrbSize = 0.6f;       // 프리팹 없을 때 임시 구슬 크기

    IEnumerator DarkOrbAttackRoutine()
    {
        // 1. 구슬 darkOrbCount개를 보스 주위에 균등한 각도로 배치하며 생성
        List<GameObject> orbs = new List<GameObject>();
        List<DarkOrbHazard> hazards = new List<DarkOrbHazard>();
        float[] initialAngles = new float[darkOrbCount];

        for (int i = 0; i < darkOrbCount; i++)
        {
            float angle = (360f / darkOrbCount) * i * Mathf.Deg2Rad;
            initialAngles[i] = angle;

            Vector2 spawnPos = (Vector2)transform.position + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * darkOrbOrbitRadius;
            GameObject orb = SpawnDarkOrb(spawnPos);
            orbs.Add(orb);
            hazards.Add(orb != null ? orb.GetComponent<DarkOrbHazard>() : null);
        }
        activeDarkOrbs.AddRange(orbs);

        bool[] launched = new bool[darkOrbCount];
        float rotationElapsed = 0f;
        // 한 바퀴(2π)를 darkOrbOrbitDuration 동안 도는 각속도. 각도가 증가하는 방향 = 반시계방향
        float angularSpeed = (2f * Mathf.PI) / Mathf.Max(darkOrbOrbitDuration, 0.01f);

        // 2. 궤도 회전만 하는 구간 (darkOrbOrbitDuration 초)
        while (rotationElapsed < darkOrbOrbitDuration)
        {
            rotationElapsed += Time.deltaTime;
            UpdateOrbitPositions(orbs, initialAngles, launched, rotationElapsed, angularSpeed);
            yield return null;
        }

        // 3. 순차 발사 (아직 발사 안 된 구슬은 계속 궤도 회전을 유지)
        Vector3 launchTarget = target != null ? target.position : transform.position;
        float playerSpeed = GetPlayerMoveSpeed() * darkOrbSpeedMultiplier;

        for (int i = 0; i < darkOrbCount; i++)
        {
            if (orbs[i] != null && hazards[i] != null)
            {
                hazards[i].Launch(target, darkOrbTrackDuration, playerSpeed);
                launched[i] = true;
                activeDarkOrbs.Remove(orbs[i]); // 발사된 순간부터는 스스로 관리하므로 강제 정리 목록에서 제외
            }

            // 마지막 구슬 발사 후에는 대기할 필요 없음
            if (i == darkOrbCount - 1) break;

            float waitElapsed = 0f;
            while (waitElapsed < darkOrbLaunchInterval)
            {
                waitElapsed += Time.deltaTime;
                rotationElapsed += Time.deltaTime;
                UpdateOrbitPositions(orbs, initialAngles, launched, rotationElapsed, angularSpeed);
                yield return null;
            }
        }
    }

    // 아직 발사되지 않은 구슬들의 위치를 보스 중심 기준 궤도 위로 갱신
    void UpdateOrbitPositions(List<GameObject> orbs, float[] initialAngles, bool[] launched, float elapsed, float angularSpeed)
    {
        for (int i = 0; i < orbs.Count; i++)
        {
            if (launched[i] || orbs[i] == null) continue;

            float angle = initialAngles[i] + elapsed * angularSpeed;
            Vector2 pos = (Vector2)transform.position + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * darkOrbOrbitRadius;
            orbs[i].transform.position = pos;
        }
    }

    // target(플레이어)에 PlayerMove가 붙어있으면 그 moveSpeed를 그대로 읽어와 구슬 속도로 사용
    float GetPlayerMoveSpeed()
    {
        if (target == null) return 7f; // PlayerMove를 못 찾을 때를 대비한 기본값
        PlayerMove playerMove = target.GetComponent<PlayerMove>();
        if (playerMove == null) playerMove = target.GetComponentInParent<PlayerMove>();
        return playerMove != null ? playerMove.moveSpeed : 7f;
    }

    GameObject SpawnDarkOrb(Vector2 pos)
    {
        GameObject orb;
        if (darkOrbTemplate != null)
        {
            orb = Instantiate(darkOrbTemplate, pos, Quaternion.identity);
            orb.SetActive(true); // 템플릿이 꺼져있어도 복제본은 반드시 켜서 생성
        }
        else
        {
            // 프리팹이 없으면 임시 암흑 구슬 생성 (검은 원)
            orb = new GameObject("DarkOrb_Temp");
            orb.transform.position = pos;
            SpriteRenderer sr = orb.AddComponent<SpriteRenderer>();
            sr.color = new Color(0.1f, 0.05f, 0.15f, 1f);
            sr.sprite = CreateTempSquareSprite();
            orb.transform.localScale = Vector3.one * fallbackDarkOrbSize;

            CircleCollider2D col = orb.AddComponent<CircleCollider2D>();
            col.isTrigger = true;
        }

        ForceAllCollidersToTrigger(orb);

        DarkOrbHazard hazard = orb.GetComponent<DarkOrbHazard>();
        if (hazard == null) hazard = orb.AddComponent<DarkOrbHazard>();
        hazard.damage = bossAttackDamage;

        return orb;
    }
    // ================= 먹구름 공격 (2페이즈) =================
    [Header("2P DarkCloud")]
    public GameObject darkCloudPrefab;             // 먹구름 프리팹 (비워두면 임시 생성)
    GameObject darkCloudTemplate;                   // darkCloudPrefab의 런타임 복제 템플릿 (원본 보호용)
    public GameObject lightningPrefab;              // 번개 프리팹 (비워두면 임시 생성)
    GameObject lightningTemplate;                   // lightningPrefab의 런타임 복제 템플릿 (원본 보호용)

    public Vector2 darkCloudSpawnOffset = Vector2.zero; // 보스의 처음 배치 위치(initialPosition) 기준으로 이 값만큼 이동해서 생성 (Y값 조절 가능)

    [Tooltip("먹구름 나타나는 시간")]
    public float darkCloudFadeInDuration = 3f;      // 먹구름이 서서히 나타나는 시간
    [Tooltip("번개 치기까지 걸리는 시간")]
    public float darkCloudHoldDuration = 6f;        // 완전히 나타난 뒤 번개가 치기까지 대기 시간
    [Tooltip("붓질을 해야하는 시간")]
    public float darkCloudPaintEraseDuration = 3f;  // 누적 붓질 시간이 이 값에 도달하면 먹구름이 지워짐 (공격 취소)
    public float lightningLifetime = 0.4f;          // 번개가 유지되는 시간
    public float lightningLength = 100f;            // 번개가 뻗어나가는 길이 (충분히 크게 잡아 화면 끝까지 이어지도록)

    public float fallbackDarkCloudSize = 4f;        // 프리팹 없을 때 임시 먹구름 크기

    public float fallbackLightningWidth = 0.6f;     // 프리팹 없을 때 임시 번개 가로 크기
    public float fallbackLightningHeight = 6f;      // 프리팹 없을 때 임시 번개 세로 크기

    IEnumerator DarkCloudAttackRoutine()
    {
        // 1. 보스가 처음 배치된 위치 + 오프셋 지점에 먹구름 생성
        Vector3 cloudSpawnPos = initialPosition + (Vector3)darkCloudSpawnOffset;
        GameObject cloud = SpawnDarkCloud(cloudSpawnPos);
        activeDarkCloud = cloud;
        DarkCloudHazard hazard = cloud != null ? cloud.GetComponent<DarkCloudHazard>() : null;

        if (hazard == null)
        {
            // 안전장치: 컴포넌트를 못 붙였다면 그냥 공격을 종료
            if (cloud != null) Destroy(cloud);
            activeDarkCloud = null;
            yield break;
        }

        // 2. 먹구름이 붓질로 지워지거나(공격 취소), 번개를 쏠 준비가 될 때까지 대기
        while (!hazard.IsErased && !hazard.IsReadyToStrike)
        {
            yield return null;
        }

        bool wasErased = hazard.IsErased;
        activeDarkCloud = null; // 어느 쪽이든 이 시점부터는 BossAttack이 직접 관리할 필요 없음 (지워지는 중이면 스스로 파괴, 발동이면 아래에서 즉시 제거)

        if (wasErased)
        {
            // 붓질로 지워졌으므로 번개 없이 공격 종료 (먹구름은 스스로 페이드아웃 후 파괴됨)
            yield break;
        }

        // 3. 번개 발동: 먹구름 위치 -> 플레이어 위치를 잇는 형태로 생성
        Vector3 cloudPos = cloudSpawnPos; // 먹구름이 생성됐던 그 위치 (오프셋 반영)
        if (cloud != null) hazard.StartFadeOutAndDestroy(); // 번개가 치는 순간 즉시 사라지지 않고 서서히 페이드아웃

        Vector3 strikePos = target != null ? target.position : transform.position;
        GameObject lightning = SpawnLightning(cloudPos, strikePos);
        activeLightning = lightning;

        yield return new WaitForSeconds(lightningLifetime);

        if (lightning != null) Destroy(lightning);
        activeLightning = null;
    }

    GameObject SpawnDarkCloud(Vector3 pos)
    {
        GameObject cloud;
        if (darkCloudTemplate != null)
        {
            cloud = Instantiate(darkCloudTemplate, pos, Quaternion.identity);
            cloud.SetActive(true); // 템플릿이 꺼져있어도 복제본은 반드시 켜서 생성
        }
        else
        {
            // 프리팹이 없으면 임시 먹구름 생성 (짙은 회색 사각형)
            cloud = new GameObject("DarkCloud_Temp");
            cloud.transform.position = pos;
            SpriteRenderer sr = cloud.AddComponent<SpriteRenderer>();
            sr.color = new Color(0.2f, 0.2f, 0.25f, 1f);
            sr.sprite = CreateTempSquareSprite();
            cloud.transform.localScale = Vector3.one * fallbackDarkCloudSize;

            CircleCollider2D col = cloud.AddComponent<CircleCollider2D>();
            col.isTrigger = true;
        }

        ForceAllCollidersToTrigger(cloud);

        DarkCloudHazard hazard = cloud.GetComponent<DarkCloudHazard>();
        if (hazard == null) hazard = cloud.AddComponent<DarkCloudHazard>();
        hazard.fadeInDuration = darkCloudFadeInDuration;
        hazard.holdDuration = darkCloudHoldDuration;
        hazard.paintEraseDuration = darkCloudPaintEraseDuration;

        return cloud;
    }

    // fromPos(먹구름 위치)에서 시작해서, toPos(플레이어) 방향으로 lightningLength만큼
    // 즉시 완성된 형태로 뻗어나가는 번개를 생성.
    GameObject SpawnLightning(Vector3 fromPos, Vector3 toPos)
    {
        GameObject lightning;
        if (lightningTemplate != null)
        {
            lightning = Instantiate(lightningTemplate, fromPos, Quaternion.identity);
            lightning.SetActive(true); // 템플릿이 꺼져있어도 복제본은 반드시 켜서 생성
        }
        else
        {
            // 프리팹이 없으면 임시 번개 생성 (노란 세로 막대)
            lightning = new GameObject("Lightning_Temp");
            lightning.transform.position = fromPos;

            SpriteRenderer sr = lightning.AddComponent<SpriteRenderer>();
            sr.color = new Color(1f, 0.95f, 0.3f, 0.9f);
            sr.sprite = CreateTempSquareSprite();
            lightning.transform.localScale = new Vector3(fallbackLightningWidth, 1f, 1f); // 세로 길이는 Init()이 채움

            BoxCollider2D col = lightning.AddComponent<BoxCollider2D>();
            col.isTrigger = true;
        }

        ForceAllCollidersToTrigger(lightning);

        LightningHazard hazard = lightning.GetComponent<LightningHazard>();
        if (hazard == null) hazard = lightning.AddComponent<LightningHazard>();
        hazard.damage = bossAttackDamage;
        hazard.lifetime = lightningLifetime;
        hazard.Init(fromPos, toPos, lightningLength); // 시작점 고정, 목표 방향으로 lightningLength만큼 즉시 뻗어나감

        return lightning;
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
    // ================= 암영 결계 공격 (2페이즈) =================
    [Header("2P Shadow Barrier")]
    public GameObject shadowBarrierTelegraphPrefab;           // 경고 표시 프리팹 (비워두면 임시 마커 생성)
    public GameObject shadowBarrierHazardPrefab;               // 실제 피해 판정 영역 프리팹 (비워두면 임시 생성)
    GameObject shadowBarrierTelegraphTemplate;                 // shadowBarrierTelegraphPrefab의 런타임 복제 템플릿 (원본 보호용)
    GameObject shadowBarrierHazardTemplate;                    // shadowBarrierHazardPrefab의 런타임 복제 템플릿 (원본 보호용)
    public float shadowBarrierTelegraphDuration = 2f;          // 텔레그래프 지속 시간
    public float shadowBarrierTelegraphBlinkInterval = 0.5f;   // 깜빡임 간격
    [Tooltip("공격 지속 시간")]
    public float shadowBarrierActiveDuration = 1.5f;           // 텔레그래프 종료 후 실제 피해 판정이 유지되는 시간

    public float fallbackShadowBarrierWidth = 3f;              // 프리팹 없을 때 컬럼 하나의 가로 크기
    public float fallbackShadowBarrierHeight = 10f;            // 프리팹 없을 때 컬럼 하나의 세로 크기

    public Vector2 shadowBarrierSpawnOffset = Vector2.zero;    // 보스의 처음 배치 위치(initialPosition) 기준으로 이 값만큼 이동해서 생성

    IEnumerator ShadowBarrierAttackRoutine()
    {
        // 컬럼 하나의 실제 가로 크기를 오브젝트(프리팹) 기준으로 읽어와서,
        // 5칸을 그 크기 그대로 나란히 배치함 (강제 스케일 조정 없음)
        float columnWidth = GetShadowBarrierColumnWidth();
        Vector3 basePos = initialPosition + (Vector3)shadowBarrierSpawnOffset; // 보스 초기 위치 + 인스펙터에서 조절 가능한 오프셋
        float leftX = basePos.x - (columnWidth * 5f) / 2f;
        float centerY = basePos.y;

        // 왼쪽부터 1~5번째 컬럼 기준: 짝(2,4) 먼저 -> 홀(1,3,5) 나중
        int[] evenColumns = { 2, 4 };
        int[] oddColumns = { 1, 3, 5 };

        yield return RunShadowBarrierGroup(evenColumns, leftX, columnWidth, centerY);
        yield return RunShadowBarrierGroup(oddColumns, leftX, columnWidth, centerY);
    }
    float GetShadowBarrierColumnWidth()
    {
        GameObject reference = shadowBarrierHazardTemplate != null ? shadowBarrierHazardTemplate : shadowBarrierTelegraphTemplate;
        if (reference != null)
        {
            BoxCollider2D box = reference.GetComponentInChildren<BoxCollider2D>(true);
            if (box != null && box.size.x > 0.001f)
                return box.size.x * box.transform.lossyScale.x;

            Collider2D anyCol = reference.GetComponentInChildren<Collider2D>(true);
            if (anyCol != null && anyCol.bounds.size.x > 0.001f)
                return anyCol.bounds.size.x;

            SpriteRenderer sr = reference.GetComponentInChildren<SpriteRenderer>(true);
            if (sr != null && sr.sprite != null)
                return sr.sprite.bounds.size.x * sr.transform.lossyScale.x;
        }
        return fallbackShadowBarrierWidth;
    }

    IEnumerator RunShadowBarrierGroup(int[] columns, float leftX, float columnWidth, float centerY)
    {
        // 1. 해당 그룹(짝 또는 홀)의 컬럼마다 텔레그래프 생성
        List<GameObject> markers = new List<GameObject>();
        List<float> columnCenters = new List<float>();
        foreach (int col in columns)
        {
            float centerX = leftX + columnWidth * (col - 0.5f); // 1-indexed 컬럼의 중심 X좌표
            columnCenters.Add(centerX);

            GameObject marker = SpawnShadowBarrierTelegraph(new Vector2(centerX, centerY));
            if (marker != null) markers.Add(marker);
        }
        activeTelegraphMarkers.AddRange(markers);

        // 2. 2초 동안 0.5초 간격으로 깜빡임
        float elapsed = 0f;
        bool visible = true;
        while (elapsed < shadowBarrierTelegraphDuration)
        {
            yield return new WaitForSeconds(shadowBarrierTelegraphBlinkInterval);
            elapsed += shadowBarrierTelegraphBlinkInterval;
            visible = !visible;
            foreach (var m in markers)
            {
                if (m == null) continue;
                SpriteRenderer sr = m.GetComponent<SpriteRenderer>();
                if (sr != null) sr.enabled = visible;
            }
        }

        // 3. 텔레그래프 제거
        foreach (var m in markers)
        {
            if (m != null) Destroy(m);
            activeTelegraphMarkers.Remove(m);
        }

        // 4. 실제 피해 판정 영역 생성 (컬럼마다)
        List<GameObject> barriers = new List<GameObject>();
        foreach (var centerX in columnCenters)
        {
            GameObject barrier = SpawnShadowBarrierHazard(new Vector2(centerX, centerY));
            if (barrier != null) barriers.Add(barrier);
        }

        // 5. 발동 유지 시간만큼 대기 후 제거
        yield return new WaitForSeconds(shadowBarrierActiveDuration);

        foreach (var barrier in barriers)
        {
            if (barrier != null) Destroy(barrier);
        }
    }

    GameObject SpawnShadowBarrierTelegraph(Vector2 center)
    {
        GameObject marker;
        if (shadowBarrierTelegraphTemplate != null)
        {
            // 프리팹 원본 크기를 그대로 사용 (스케일 강제 조정 없음)
            marker = Instantiate(shadowBarrierTelegraphTemplate, center, Quaternion.identity);
            marker.SetActive(true);
        }
        else
        {
            marker = new GameObject("ShadowBarrierTelegraph_Temp");
            marker.transform.position = center;
            SpriteRenderer sr = marker.AddComponent<SpriteRenderer>();
            sr.color = new Color(1f, 0.3f, 0.3f, 0.4f);
            sr.sprite = CreateTempSquareSprite();
            marker.transform.localScale = new Vector3(fallbackShadowBarrierWidth, fallbackShadowBarrierHeight, 1f);
        }

        return marker;
    }

    GameObject SpawnShadowBarrierHazard(Vector2 center)
    {
        GameObject barrier;
        if (shadowBarrierHazardTemplate != null)
        {
            // 프리팹 원본 크기를 그대로 사용 (스케일 강제 조정 없음)
            barrier = Instantiate(shadowBarrierHazardTemplate, center, Quaternion.identity);
            barrier.SetActive(true);
        }
        else
        {
            barrier = new GameObject("ShadowBarrierHazard_Temp");
            barrier.transform.position = center;
            SpriteRenderer sr = barrier.AddComponent<SpriteRenderer>();
            sr.color = new Color(1f, 0f, 0f, 0.6f);
            sr.sprite = CreateTempSquareSprite();
            barrier.transform.localScale = new Vector3(fallbackShadowBarrierWidth, fallbackShadowBarrierHeight, 1f);

            BoxCollider2D col = barrier.AddComponent<BoxCollider2D>();
            col.isTrigger = true;
        }


        SetLayerRecursively(barrier, LayerMask.NameToLayer("Default"));

        ForceAllCollidersToTrigger(barrier);

        ShadowBarrierHazard hazard = barrier.GetComponent<ShadowBarrierHazard>();
        if (hazard == null) hazard = barrier.AddComponent<ShadowBarrierHazard>();
        hazard.lifetime = shadowBarrierActiveDuration + 0.5f;
        hazard.damage = bossAttackDamage;

        return barrier;
    }
    // ================= 색채 소용돌이 (1P/2P 공용) =================
    IEnumerator ColorWhirlpoolAttackRoutine()
    {
        Vector3 spawnPos = initialPosition + (Vector3)colorWhirlpoolSpawnOffset;
        GameObject whirlpool = SpawnColorWhirlpool(spawnPos);
        activeColorWhirlpool = whirlpool;

        // 최대 colorWhirlpoolDuration(기본 10초) 동안 유지됨.
        // 짝지어진 다른 공격이 이보다 먼저 끝나면, RunAttack이 StopCoroutine + Destroy로 더 일찍 정리함
        yield return new WaitForSeconds(colorWhirlpoolDuration);

        if (activeColorWhirlpool != null)
        {
            Destroy(activeColorWhirlpool);
            activeColorWhirlpool = null;
        }
    }

    GameObject SpawnColorWhirlpool(Vector3 pos)
    {
        GameObject whirlpool;
        if (colorWhirlpoolTemplate != null)
        {
            whirlpool = Instantiate(colorWhirlpoolTemplate, pos, Quaternion.identity);
            whirlpool.SetActive(true); // 템플릿이 꺼져있어도 복제본은 반드시 켜서 생성
        }
        else
        {
            // 프리팹이 없으면 임시 소용돌이 생성 (보라색 원)
            whirlpool = new GameObject("ColorWhirlpool_Temp");
            whirlpool.transform.position = pos;
            SpriteRenderer sr = whirlpool.AddComponent<SpriteRenderer>();
            sr.color = new Color(0.6f, 0.3f, 1f, 1f);
            sr.sprite = CreateTempSquareSprite();
            whirlpool.transform.localScale = Vector3.one * fallbackColorWhirlpoolSize;

            CircleCollider2D col = whirlpool.AddComponent<CircleCollider2D>();
            col.isTrigger = true;
        }

        ForceAllCollidersToTrigger(whirlpool);

        ColorWhirlpoolHazard hazard = whirlpool.GetComponent<ColorWhirlpoolHazard>();
        if (hazard == null) hazard = whirlpool.AddComponent<ColorWhirlpoolHazard>();
        hazard.SetStats(
               colorWhirlpoolFadeInDuration,
               bossAttackDamage,
               colorWhirlpoolPullRadius,
               colorWhirlpoolPullForce,
               colorWhirlpoolMinEffectiveDistance,
               colorWhirlpoolMaxPullSpeed
           );

        return whirlpool;
    }

    // ================= 색채 구슬 (2페이즈 진입 시 1회 소환) =================
    ColorOrb SpawnColorOrb()
    {
        Vector3 spawnPos = transform.position + new Vector3(0f, colorOrbSpawnOffsetY, 0f);
        GameObject orbObj;

        if (colorOrbPrefab != null)
        {
            orbObj = Instantiate(colorOrbPrefab, spawnPos, Quaternion.identity);
        }
        else
        {
            // 프리팹이 없으면 임시 색채 구슬 생성 (보라색 원)
            orbObj = new GameObject("ColorOrb_Temp");
            orbObj.transform.position = spawnPos;
            SpriteRenderer sr = orbObj.AddComponent<SpriteRenderer>();
            sr.color = new Color(0.8f, 0.4f, 1f, 1f);
            sr.sprite = CreateTempSquareSprite();
            orbObj.transform.localScale = Vector3.one * 1.2f;

            CircleCollider2D col = orbObj.AddComponent<CircleCollider2D>();
            col.isTrigger = true;
        }

        ColorOrb orb = orbObj.GetComponent<ColorOrb>();
        if (orb == null) orb = orbObj.AddComponent<ColorOrb>();
        orb.maxHealth = colorOrbHealth;
        orb.currentHealth = colorOrbHealth;

        return orb;
    }

    GameObject SpawnFrostCrystal(Vector2 pos)
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
        hazard.damage = bossAttackDamage;
        hazard.maxLifetime = frostMaxLifetime;
        hazard.groundLayer = groundLayer;

        return crystal;
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
            SetLayerRecursively(marker, LayerMask.NameToLayer("Default"));

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

    GameObject SpawnSpike(Vector2 pos)
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
        hazard.damage = bossAttackDamage;

        return spike;
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

    void SetLayerRecursively(GameObject obj, int layer)
    {
        if (layer == -1) return; // 해당 레이어를 못 찾은 경우 안전하게 무시
        obj.layer = layer;
        foreach (Transform child in obj.transform)
        {
            SetLayerRecursively(child.gameObject, layer);
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

        // 궤도 회전 중이던(아직 발사되지 않은) 암흑 구슬도 함께 정리
        foreach (var orb in activeDarkOrbs)
        {
            if (orb != null) Destroy(orb);
        }
        activeDarkOrbs.Clear();

        // 진행 중이던 먹구름/번개도 코루틴이 강제 중단되면 남을 수 있으므로 함께 정리
        if (activeDarkCloud != null) Destroy(activeDarkCloud);
        activeDarkCloud = null;

        if (activeLightning != null) Destroy(activeLightning);
        activeLightning = null;

        if (activeWhirlpoolCoroutine != null)
        {
            StopCoroutine(activeWhirlpoolCoroutine);
            activeWhirlpoolCoroutine = null;
        }
        if (activeColorWhirlpool != null) Destroy(activeColorWhirlpool);
        activeColorWhirlpool = null;
    }
}