using UnityEngine;

public class BlackFog : MonoBehaviour
{
    [Header("??? (??a ????)")]
    public Transform target; // ????? ?????? ColorOrb?? ??? ???

    public enum FogSide { Left, Right } // ???? ???? ??? ????? ??????? (????????? ????????? ????)

    [Header("??? ????")]
    public FogSide side = FogSide.Left;  // ??/?? ???? ?????? ??? ????????? ???????, ??? ??? ??? ???? ????
    public float spawnDistanceX = 10f;   // ???? ???? X?????? ???? ?????? ??????? ????
    public float travelDuration = 10f;   // ???? ???? ???????? ??????? ?? ????? ?? (?? ?????? ???? ????? ????)
    public float pushBackSpeed = -1f;    // ?????? ???? ?????? ??? (?????? ?? ???? ????? ??????? ??? ????)

    [Header("???? ????")]
    public float damagePerSecond = 1f;   // ??? ???? ??? ?????? ???? ?? ??? ?????

    [Header("??? ???? (????? ????)")]
    Collider2D hitCollider;

    float forwardSpeed;
    Vector3 spawnPosition;
    ColorOrb orbTarget;

    public float velocitySmoothTime = 0.3f;
    float currentVelocityX = 0f;   // ???? ?????? ???? ???? X?? ??? (??? ??????? ??)
    float velocitySmoothRef = 0f;  // SmoothDamp ???? ???? ????

    Transform cursorTransform; // ???? CursorController ??????? (???J?? ??????? ?? ???????)
    GaugeController gaugeController; // ???? ??? ???
    PlayerHealth playerHealth; // ???/??? ???? ???
    bool isBeingAttacked = false;

    // 2?????? ?????? ???????? ????? ????????? ????? (BossAttack?? StartMoving()?? ?????? true?? ???)
    bool isActivated = false;

    private CursorController cachedCursorController; // 캐싱된 커서 컨트롤러 참조

    [Header("Explosion Push Settings")]
    [Tooltip("폭발로 밀려날 때의 부드러운 감속 속도 배율 (높을수록 빠르게 이동, 기본값: 5.0)")]
    [SerializeField] private float explosionPushLerpSpeed = 5.0f;

    // 폭발 넉백 내부 상태 변수
    private float targetPushX;
    private bool isPushingByExplosion = false;

    public void SetTarget(ColorOrb orb)
    {
        if (orb == null) return;
        target = orb.transform;
        orbTarget = orb;
    }

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
            PositionAtSpawnDistance();
            spawnPosition = transform.position;

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

        CursorController cursor = FindFirstObjectByType<CursorController>();
        if (cursor != null)
        {
            cursorTransform = cursor.transform;
            cachedCursorController = cursor;
        }

        gaugeController = FindFirstObjectByType<GaugeController>();
        playerHealth = FindFirstObjectByType<PlayerHealth>();

        hitCollider = GetComponent<Collider2D>();
        if (hitCollider == null) hitCollider = GetComponentInChildren<Collider2D>();

        if (target != null) orbTarget = target.GetComponent<ColorOrb>();
        if (target != null) PositionAtSpawnDistance();

        spawnPosition = transform.position;
    }

    void Update()
    {
        if (target == null) return;
        if (!isActivated) return;

        float dir = (side == FogSide.Left) ? -1f : 1f;

        if (isPushingByExplosion)
        {
            // [부드러운 밀림 연출] 폭발 피격 시 목표 지점(targetPushX)으로 부드럽게 이동(Lerp)시킵니다.
            float newX = Mathf.Lerp(transform.position.x, targetPushX, Time.deltaTime * explosionPushLerpSpeed);
            transform.position = new Vector3(newX, transform.position.y, transform.position.z);

            // 거의 근접하게 도달하면 넉백 상태를 해제하여 자연스럽게 다시 복구
            if (Mathf.Abs(transform.position.x - targetPushX) < 0.05f)
            {
                transform.position = new Vector3(targetPushX, transform.position.y, transform.position.z);
                isPushingByExplosion = false;
            }
        }
        else
        {
            // 기존 1번 브러시 공격 및 자동 전진 처리
            bool hasPaint = gaugeController == null || gaugeController.currentPaint >= gaugeController.minPaintToDraw;
            bool needsReclick = gaugeController != null && gaugeController.NeedsReclick;
            bool isDead = playerHealth != null && playerHealth.IsDead;
            bool isDrawBlocked = playerHealth != null && playerHealth.IsDrawBlocked;

            bool isMode1 = cachedCursorController != null && cachedCursorController.attackMode == 1;
            bool canDraw = isMode1 && Input.GetMouseButton(0) && hasPaint && !needsReclick && !isDead && !isDrawBlocked;

            isBeingAttacked = false;
            if (cursorTransform != null && hitCollider != null && canDraw)
            {
                if (hitCollider.OverlapPoint(cursorTransform.position))
                {
                    isBeingAttacked = true;
                }
            }

            float targetVelocityX = isBeingAttacked ? (dir * pushBackSpeed) : (-dir * forwardSpeed);
            currentVelocityX = Mathf.SmoothDamp(currentVelocityX, targetVelocityX, ref velocitySmoothRef, velocitySmoothTime);

            transform.position += new Vector3(currentVelocityX * Time.deltaTime, 0f, 0f);

            float halfWidth = hitCollider != null ? hitCollider.bounds.extents.x : 0f;
            float pivotStopX = target.position.x + dir * halfWidth;
            float minX = Mathf.Min(pivotStopX, spawnPosition.x);
            float maxX = Mathf.Max(pivotStopX, spawnPosition.x);
            float clampedX = Mathf.Clamp(transform.position.x, minX, maxX);
            transform.position = new Vector3(clampedX, transform.position.y, transform.position.z);
        }

        if (orbTarget != null && hitCollider != null)
        {
            if (hitCollider.OverlapPoint(target.position))
            {
                orbTarget.TakeDamage(damagePerSecond * Time.deltaTime);
            }
        }
    }

    // [수정] 2번 차징 모드 발사 시(마우스 뗄 때) 폭발 효과로 인해 안개를 즉시가 아닌 부드럽게 넉백시키는 방식
    public void PushBack(float amount)
    {
        float dir = (side == FogSide.Left) ? -1f : 1f;
        
        // 부드럽게 이동시킬 목표 좌표 설정
        targetPushX = transform.position.x + (dir * amount);

        // 클램프 범위를 미리 계산하여 목표지점 자체가 오차범위를 벗어나지 않도록 대입
        if (target != null)
        {
            float halfWidth = hitCollider != null ? hitCollider.bounds.extents.x : 0f;
            float pivotStopX = target.position.x + dir * halfWidth;
            float minX = Mathf.Min(pivotStopX, spawnPosition.x);
            float maxX = Mathf.Max(pivotStopX, spawnPosition.x);
            targetPushX = Mathf.Clamp(targetPushX, minX, maxX);
        }

        // 부드러운 넉백 모드 가동
        isPushingByExplosion = true;
        Debug.Log($"[BlackFog] 차징 샷 폭발 타격! 안개가 {targetPushX} 지점을 향해 부드럽게 밀려납니다.");
    }

    private void OnDrawGizmosSelected()
    {
        if (hitCollider != null)
        {
            Gizmos.color = new Color(0f, 0f, 0f, 0.4f);
            Gizmos.DrawWireCube(hitCollider.bounds.center, hitCollider.bounds.size);
        }
    }
}