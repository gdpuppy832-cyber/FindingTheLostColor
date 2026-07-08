using UnityEngine;


public class BlackFog : MonoBehaviour
{
    [Header("타겟 (색채 구슬)")]
    public Transform target; // 비워두면 씬에서 ColorOrb를 자동 탐색

    public enum FogSide { Left, Right } // 구슬 기준 어느 쪽에서 소환될지 (인스펙터에서 명시적으로 지정)

    [Header("이동 설정")]
    public FogSide side = FogSide.Left;  // 좌/우 각각 별도의 안개 오브젝트로 배치하므로, 위치 추론 대신 직접 지정
    public float spawnDistanceX = 10f;   // 구슬 기준 X축으로 이만큼 떨어진 위치에서 시작
    public float travelDuration = 10f;   // 방해 없이 구슬까지 도달하는 데 걸리는 시간 (이 값으로 전진 속도가 계산됨)
    public float pushBackSpeed = -1f;    // 공격받는 동안 밀려나는 속도 (음수로 두면 전진 속도와 동일하게 자동 설정)

    [Header("피해 설정")]
    public float damagePerSecond = 1f;   // 안개 범위 안에 구슬이 있을 때 초당 피해량

    [Header("피격 판정 (플레이어 붓질)")]
    // attackRadius 대신, 안개 오브젝트(또는 자식)에 붙은 콜라이더의 실제 모양을 그대로 판정 기준으로 사용
    Collider2D hitCollider;

    float forwardSpeed;
    Vector3 spawnPosition;
    ColorOrb orbTarget;

    public float velocitySmoothTime = 0.3f;
    float currentVelocityX = 0f;   // 현재 실제로 적용 중인 X축 속도 (부드럽게 보간되는 값)
    float velocitySmoothRef = 0f;  // SmoothDamp 내부 참조 변수

    // CursorController를 수정할 수 없으므로, 안개가 스스로 마우스/붓 위치와 좌클릭 여부를 감지함
    Transform cursorTransform; // 씬의 CursorController 오브젝트 (마우스를 따라다니는 그 오브젝트)
    GaugeController gaugeController; // 물감 잔량 확인용
    PlayerHealth playerHealth; // 사망/피격 상태 확인용
    bool isBeingAttacked = false;

    // 2페이즈가 시작되기 전까지는 안개가 제자리에서 대기함 (BossAttack이 StartMoving()을 호출하면 true로 전환)
    bool isActivated = false;


    // 외부(BossAttack)에서 명시적으로 타겟 구슬을 지정할 때 호출.
    // Start()에서 씬을 자동 탐색해 엉뚱한(기존) 구슬을 이미 찾아뒀더라도,
    // 이 함수가 호출되면 그 값을 덮어써서 정확히 이 구슬을 쫓아가게 됨
    public void SetTarget(ColorOrb orb)
    {
        if (orb == null) return;
        target = orb.transform;
        orbTarget = orb;
    }

    // target 기준으로 side 방향에 spawnDistanceX만큼 떨어진 위치로 이동시킴
    // X는 spawnDistanceX만큼 떨어진 위치로, Y는 구슬과 같은 높이로 맞춰서
    // 콜라이더가 전진했을 때 실제로 구슬과 겹칠 수 있도록 함
    void PositionAtSpawnDistance()
    {
        if (target == null) return;

        float dir = (side == FogSide.Left) ? -1f : 1f;
        Vector3 pos = transform.position;
        pos.x = target.position.x + dir * spawnDistanceX;
        transform.position = pos;
    }

    public void StartMoving()
    {
        // target을 아직 못 찾았다면 이제서야(구슬이 생성된 뒤) 다시 탐색
        if (target == null)
        {
            ColorOrb foundOrb = FindFirstObjectByType<ColorOrb>();
            if (foundOrb != null)
            {
                target = foundOrb.transform;
                orbTarget = foundOrb;
            }
        }

        if (target != null)
        {
            // spawnDistanceX 기준으로 구슬에서 side 방향으로 떨어진 위치에 배치
            PositionAtSpawnDistance();

            spawnPosition = transform.position;

            // 콜라이더 가장자리가 도달 기준이므로(Update()의 pivotStopX와 동일한 계산),
            // 실제로 멈추는 지점까지의 거리를 기준으로 속도를 계산해야 travelDuration이 정확히 지켜짐.
            // (이전엔 target.position까지의 전체 거리로 속도를 계산했는데,
            //  실제로는 halfWidth만큼 못 미쳐서 멈추기 때문에 travelDuration보다 일찍 도착하는 문제가 있었음)
            float dir = (side == FogSide.Left) ? -1f : 1f;
            float halfWidth = hitCollider != null ? hitCollider.bounds.extents.x : 0f;
            float pivotStopX = target.position.x + dir * halfWidth;
            float travelDistance = Mathf.Abs(spawnPosition.x - pivotStopX);

            forwardSpeed = travelDuration > 0f ? travelDistance / travelDuration : 0f;
            if (pushBackSpeed < 0f) pushBackSpeed = forwardSpeed;
        }

        isActivated = true;
    }

    void Start()
    {
        if (target == null)
        {
            ColorOrb foundOrb = FindFirstObjectByType<ColorOrb>();
            if (foundOrb != null) target = foundOrb.transform;
        }

        // CursorController는 수정하지 않고, 씬에서 그 오브젝트를 찾아 위치만 읽어옴
        CursorController cursor = FindFirstObjectByType<CursorController>();
        if (cursor != null) cursorTransform = cursor.transform;

        gaugeController = FindFirstObjectByType<GaugeController>();
        playerHealth = FindFirstObjectByType<PlayerHealth>();

        // 붓질 피격 판정에 사용할 콜라이더를 미리 찾아둠 (본체 우선, 없으면 자식에서 탐색)
        hitCollider = GetComponent<Collider2D>();
        if (hitCollider == null) hitCollider = GetComponentInChildren<Collider2D>();

        if (target != null) orbTarget = target.GetComponent<ColorOrb>();

        // target이 이미 연결되어 있다면(인스펙터에서 직접 연결한 경우) 여기서 바로 배치
        if (target != null) PositionAtSpawnDistance();

        spawnPosition = transform.position;
    }

    void Update()
    {
        if (target == null) return;
        if (!isActivated) return; // 2페이즈가 시작되기 전까지는 움직이지도, 피해를 주지도 않음

        float dir = (side == FogSide.Left) ? -1f : 1f; // 구슬 기준 안개가 있는 쪽 방향 (Start에서 정한 side와 동일하게 유지)

        // 붓질 판정: CursorController의 canDraw와 동일한 조건 - 실제로 트레일에 색이 나오는 상태일 때만 "공격받는 중"으로 판정
        bool hasPaint = gaugeController == null || gaugeController.currentPaint >= gaugeController.minPaintToDraw;
        bool needsReclick = gaugeController != null && gaugeController.NeedsReclick;
        bool isDead = playerHealth != null && playerHealth.IsDead;
        bool isDrawBlocked = playerHealth != null && playerHealth.IsDrawBlocked;
        bool canDraw = Input.GetMouseButton(0) && hasPaint && !needsReclick && !isDead && !isDrawBlocked;

        isBeingAttacked = false;
        if (cursorTransform != null && hitCollider != null && canDraw)
        {
            if (hitCollider.OverlapPoint(cursorTransform.position))
            {
                isBeingAttacked = true;
            }
        }

        // 목표 속도(전진은 -forwardSpeed, 밀려남은 +pushBackSpeed) 사이를 부드럽게 보간
        // -> 붓질이 시작/종료되는 순간 속도가 뚝 끊기지 않고 서서히 전환됨
        float targetVelocityX = isBeingAttacked ? (dir * pushBackSpeed) : (-dir * forwardSpeed);
        currentVelocityX = Mathf.SmoothDamp(currentVelocityX, targetVelocityX, ref velocitySmoothRef, velocitySmoothTime);

        transform.position += new Vector3(currentVelocityX * Time.deltaTime, 0f, 0f);


        float halfWidth = hitCollider != null ? hitCollider.bounds.extents.x : 0f;
        float pivotStopX = target.position.x + dir * halfWidth;
        float minX = Mathf.Min(pivotStopX, spawnPosition.x);
        float maxX = Mathf.Max(pivotStopX, spawnPosition.x);
        float clampedX = Mathf.Clamp(transform.position.x, minX, maxX);
        transform.position = new Vector3(clampedX, transform.position.y, transform.position.z);


        // 안개 콜라이더 모양 안에 구슬이 들어와 있으면 초당 피해
        // (원형 반경 대신 콜라이더의 실제 모양을 그대로 사용 - attackRadius 판정과 동일한 방식)
        bool overlapping = hitCollider != null && hitCollider.OverlapPoint(target.position);
        
        if (orbTarget != null && hitCollider != null)
        {
            if (hitCollider.OverlapPoint(target.position))
            {
                orbTarget.TakeDamage(damagePerSecond * Time.deltaTime);
            }
        }

    }

    private void OnDrawGizmosSelected()
    {
        // fogRadius가 사라졌으므로, 대신 실제 판정에 쓰이는 콜라이더의 바운드를 표시
        if (hitCollider != null)
        {
            Gizmos.color = new Color(0f, 0f, 0f, 0.4f);
            Gizmos.DrawWireCube(hitCollider.bounds.center, hitCollider.bounds.size);
        }
    }
}