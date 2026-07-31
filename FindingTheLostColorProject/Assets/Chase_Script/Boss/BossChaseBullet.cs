using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

public class BossChaseBullet : MonoBehaviour
{
    [Header("전투 설정")]
    [Tooltip("이 탄환이 플레이어에게 주는 피해량 (BossChaseAttack에서 전달받음)")]
    public int attackDamage = 1;

    [Header("수명 설정")]
    [Tooltip("생성 후 자동으로 사라지기까지의 시간(초)")]
    public float lifetime = 5f;

    [Header("붓질 파괴 설정")]
    [Tooltip("붓(1번 모드)이 이 탄환과 겹쳐 있어야 하는 누적 시간(초). 이 시간을 채우면 탄환이 파괴됨")]
    public float requiredPaintOverlapTime = 1f;

    private float currentPaintOverlapTime = 0f;
    private CursorController cursorController;

    /// <summary>
    /// BossChaseAttack이 생성 직후 호출해서 탄환의 세부 설정을 전달합니다.
    /// </summary>
    public void Initialize(int damage, float life, float requiredOverlap)
    {
        attackDamage = damage;
        lifetime = life;
        requiredPaintOverlapTime = requiredOverlap;
    }

    void Start()
    {
        cursorController = FindFirstObjectByType<CursorController>();
        Destroy(gameObject, lifetime);
    }

    void Update()
    {
        if (cursorController == null) return;
        if (cursorController.attackMode != 1) return;

        if (cursorController.trail == null || !cursorController.trail.emitting) return;

        float distance = Vector2.Distance(transform.position, cursorController.transform.position);
        if (distance > cursorController.paintRadius) return;
        currentPaintOverlapTime += Time.deltaTime;
        if (currentPaintOverlapTime >= requiredPaintOverlapTime)
        {
            Destroy(gameObject);
        }
    }

    private bool IsLeftMouseHeld()
    {
#if ENABLE_INPUT_SYSTEM
        if (Mouse.current != null)
        {
            return Mouse.current.leftButton.isPressed;
        }
        return false;
#else
        return Input.GetMouseButton(0);
#endif
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