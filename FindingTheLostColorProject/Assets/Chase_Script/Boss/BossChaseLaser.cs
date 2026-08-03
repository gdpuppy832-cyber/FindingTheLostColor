using UnityEngine;
public class BossChaseLaser : MonoBehaviour
{
    [Header("전투 설정")]
    [Tooltip("이 레이저가 플레이어에게 주는 피해량 (BossChaseAttack의 attackDamage를 그대로 전달받음)")]
    public int attackDamage = 1;

    [Tooltip("레이저가 생성된 후, 실제로 피격 판정이 시작되기까지의 지연 시간 (초). 레이저 자체는 즉시 보이되 이 시간 동안은 맞아도 피해가 들어가지 않음")]
    public float hitDelay = 0.3f;

    float spawnTime;
    private SpriteRenderer spriteRenderer;
    private PolygonCollider2D polygonCollider;
    private Sprite lastSprite;

    /// <summary>
    /// BossChaseAttack이 생성 직후 호출해서 레이저의 데미지를 전달합니다.
    /// </summary>
    public void Initialize(int damage)
    {
        attackDamage = damage;
    }

    void Start()
    {
        spawnTime = Time.time;
        spriteRenderer = GetComponent<SpriteRenderer>();
        if (spriteRenderer == null)
            spriteRenderer = GetComponentInChildren<SpriteRenderer>();
        polygonCollider = GetComponent<PolygonCollider2D>();
        if (polygonCollider == null)
            polygonCollider = GetComponentInChildren<PolygonCollider2D>();
        if (spriteRenderer != null)
            lastSprite = spriteRenderer.sprite;
    }

    void Update()
    {
        // 애니메이션으로 스프라이트가 바뀔 때마다 Physics Shape를 다시 읽어와 콜라이더 모양을 실시간으로 맞춤
        if (spriteRenderer == null || polygonCollider == null)
            return;

        Sprite currentSprite = spriteRenderer.sprite;
        if (currentSprite == null || currentSprite == lastSprite)
            return;

        lastSprite = currentSprite;
        polygonCollider.autoTiling = false;
        polygonCollider.pathCount = currentSprite.GetPhysicsShapeCount();
        var points = new System.Collections.Generic.List<Vector2>();
        for (int i = 0; i < polygonCollider.pathCount; i++)
        {
            points.Clear();
            currentSprite.GetPhysicsShape(i, points);
            polygonCollider.SetPath(i, points);
        }
    }

    void OnTriggerStay2D(Collider2D other)
    {
        TryDamage(other.gameObject);
    }

    void OnCollisionStay2D(Collision2D collision)
    {
        TryDamage(collision.gameObject);
    }

    void TryDamage(GameObject obj)
    {
        // 생성 직후에는 피해 없음
        if (Time.time - spawnTime < hitDelay)
            return;

        PlayerHealth player = obj.GetComponent<PlayerHealth>();
        if (player == null)
            player = obj.GetComponentInParent<PlayerHealth>();
        if (player != null)
        {
            player.TakeDamage(attackDamage);
        }
    }
}