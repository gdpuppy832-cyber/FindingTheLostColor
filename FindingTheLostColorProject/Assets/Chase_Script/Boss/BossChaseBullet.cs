using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

public class BossChaseBullet : MonoBehaviour
{
    [Header("���� ����")]
    [Tooltip("�� źȯ�� �÷��̾�� �ִ� ���ط� (BossChaseAttack���� ���޹���)")]
    public int attackDamage = 1;

    [Header("���� ����")]
    [Tooltip("���� �� �ڵ����� ������������ �ð�(��)")]
    public float lifetime = 5f;

    [Header("���� �ı� ����")]
    [Tooltip("��(1�� ���)�� �� źȯ�� ���� �־�� �ϴ� ���� �ð�(��). �� �ð��� ä��� źȯ�� �ı���")]
    public float requiredPaintOverlapTime = 1f;

    [Header("ȸ�� ����")]
    [Tooltip("�ʴ� ȸ�� �ӵ�(��)")]
    public float rotationSpeed = 180f;

    [Tooltip("ȸ�� ��")]
    public Vector3 rotationAxis = Vector3.forward;

    [Header("Hit Sound")]
    [Tooltip("이 탄환(변화구 포함)이 플레이어에게 닿았을 때 재생할 효과음")]
    public AudioClip hitSFX;

    private float currentPaintOverlapTime = 0f;
    private CursorController cursorController;

    /// <summary>
    /// BossChaseAttack�� ���� ���� ȣ���ؼ� źȯ�� ���� ������ �����մϴ�.
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
        transform.Rotate(rotationAxis, rotationSpeed * Time.deltaTime);

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

            if (hitSFX != null && SoundManager.Instance != null)
                SoundManager.Instance.PlaySFXWithOffset(hitSFX, 0f);

            Destroy(gameObject);
        }
    }
}