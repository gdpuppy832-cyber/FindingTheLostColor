using UnityEngine;
// 2페이즈 암영 결계 공격(짝/홀 컬럼)의 실제 피해 판정을 담당하는 컴포넌트
public class ShadowBarrierHazard : MonoBehaviour
{
    [Tooltip("한 번 틱마다 주는 피해량")]
    public float damage = 1f;
    [Tooltip("이 영역이 유지되는 시간, 지나면 자동 파괴")]
    public float lifetime = 3f;
    [Tooltip("생성 후 실제 피해가 시작되기까지의 시간")]
    public float hitDelay = 0.3f;

    float spawnTime;

    private SpriteRenderer spriteRenderer;
    private PolygonCollider2D polygonCollider;
    private Sprite lastSprite;
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

        Destroy(gameObject, lifetime);
    }
    void Update()
    {
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
        if (Time.time - spawnTime < hitDelay)
            return;

        PlayerHealth player = obj.GetComponent<PlayerHealth>();
        if (player == null) player = obj.GetComponentInParent<PlayerHealth>();

        if (player != null)
        {
            player.TakeDamage(damage);
        }
    }
}