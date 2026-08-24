using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

public class BossInkCurtain : MonoBehaviour
{
    [Header("���� ����")]
    [Tooltip("�� �Թ� �帷�� �÷��̾�� �ִ� ���ط� (BossChaseAttack���� ���޹���, attackDamage ����)")]
    public int attackDamage = 1;

    [Header("���� ����")]
    [Tooltip("���� �� �ڵ����� ������������ �ð�(��)")]
    public float lifetime = 8f;

    [Header("���� �ı� ����")]
    [Tooltip("��(1�� ���)�� �� �帷�� ���� �־�� �ϴ� ���� �ð�(��). �� �ð��� ä��� �帷�� �ı��� (requiredPaintOverlapTime ����)")]
    
    public float requiredPaintOverlapTime = 1f;
    private float currentPaintOverlapTime = 0f;
    private CursorController cursorController;
    private Collider2D myCollider;

    [Header("���� ��Ż ���� ����")]
    [Tooltip("���� ��ħ ������ ������ ��, �� �ð�(��) �ȿ� �ٽ� ��ġ�� ���� �ð��� �������� �ʰ� �̾. " +
                 "�帷�� ��� �̵��ϴ� ǥ���̶� ���콺�� ���� ��� ����� ���൵�� ���� ���ư��� �ʵ��� �ϴ� ���� �ð�")]
    public float overlapBreakGraceTime = 0.15f;
    private float breakTimer = 0f; // ������ ���� ä�� ����� �ð� (���� �ð��� ��)

    [Header("Hit Sound")]
    [Tooltip("이 먹물 장막이 플레이어에게 닿았을 때 재생할 효과음")]
    public AudioClip hitSFX;
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
            // ��ġ�� �ִ� ������ ��Ż Ÿ�̸Ӹ� �����ϰ� ���� �ð��� ��� ����
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

            if (hitSFX != null && SoundManager.Instance != null)
                SoundManager.Instance.PlaySFXWithOffset(hitSFX, 0f);

            Destroy(gameObject);
        }
    }
}