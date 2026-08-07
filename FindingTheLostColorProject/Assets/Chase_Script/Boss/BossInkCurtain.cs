using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

public class BossInkCurtain : MonoBehaviour
{
    [Header("전투 설정")]
    [Tooltip("이 먹물 장막이 플레이어에게 주는 피해량 (BossChaseAttack에서 전달받음, attackDamage 재사용)")]
    public int attackDamage = 1;

    [Header("수명 설정")]
    [Tooltip("생성 후 자동으로 사라지기까지의 시간(초)")]
    public float lifetime = 8f;

    [Header("붓질 파괴 설정")]
    [Tooltip("붓(1번 모드)이 이 장막과 겹쳐 있어야 하는 누적 시간(초). 이 시간을 채우면 장막이 파괴됨 (requiredPaintOverlapTime 재사용)")]
    
    public float requiredPaintOverlapTime = 1f;
    private float currentPaintOverlapTime = 0f;
    private CursorController cursorController;
    private Collider2D myCollider;

    [Header("붓질 이탈 유예 설정")]
    [Tooltip("붓질 겹침 조건이 깨졌을 때, 이 시간(초) 안에 다시 겹치면 누적 시간을 리셋하지 않고 이어감. " +
             "장막이 계속 이동하는 표적이라 마우스가 아주 잠깐 벗어나도 진행도가 전부 날아가지 않도록 하는 유예 시간")]
    public float overlapBreakGraceTime = 0.15f;
    private float breakTimer = 0f; // 조건이 깨진 채로 경과한 시간 (유예 시간과 비교)

    public void Initialize(int damage, float life, float requiredOverlap)
    {
        attackDamage = damage;
        lifetime = life;
        requiredPaintOverlapTime = requiredOverlap;
    }

    void Start()
    {
        cursorController = FindFirstObjectByType<CursorController>();
        myCollider = GetComponent<Collider2D>();
        if (myCollider == null) myCollider = GetComponentInChildren<Collider2D>();
        Destroy(gameObject, lifetime);
    }

    void Update()
    {
        bool isOverlapping = false;
        if (cursorController != null && cursorController.attackMode == 1
            && cursorController.trail != null && cursorController.trail.emitting)
        {
            Vector2 mousePos = cursorController.transform.position;

            if (myCollider != null)
            {
                Vector2 closestPoint = myCollider.ClosestPoint(mousePos);
                float distanceToSurface = Vector2.Distance(closestPoint, mousePos);
                isOverlapping = distanceToSurface <= cursorController.paintRadius;
            }
            else
            {
                float distance = Vector2.Distance(transform.position, mousePos);
                isOverlapping = distance <= cursorController.paintRadius;
            }
        }

        if (isOverlapping)
        {
            // 겹치고 있는 동안은 이탈 타이머를 리셋하고 누적 시간을 계속 쌓음
            breakTimer = 0f;
            currentPaintOverlapTime += Time.deltaTime;

            if (currentPaintOverlapTime >= requiredPaintOverlapTime)
            {
                Destroy(gameObject);
            }
        }
        else
        {
            breakTimer += Time.deltaTime;
            if (breakTimer >= overlapBreakGraceTime)
            {
                currentPaintOverlapTime = 0f;
            }
        }
    }

    void OnTriggerEnter2D(Collider2D other)
    {
        if (other == null) return;
        PlayerHealth player = other.GetComponent<PlayerHealth>();
        if (player == null) player = other.GetComponentInParent<PlayerHealth>();
        if (player != null)
        {
            player.TakeDamage(attackDamage);
            Destroy(gameObject);
        }
    }
}