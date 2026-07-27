using UnityEngine;
using System.Collections.Generic;

public class SpikeHazard : MonoBehaviour
{
    [Tooltip("한 번 닿을 때 주는 피해량")]
    public float damage = 1f;

    [Tooltip("가시가 유지되는 시간, 지나면 자동 파괴")]
    public float lifetime = 3f;

    float lastDamageTime = -999f;
    EdgeCollider2D edge;
    SpriteRenderer spriteRenderer;

    Sprite lastSprite;

    readonly List<Vector2> physicsShape = new List<Vector2>();

    void Start()
    {
        edge = GetComponent<EdgeCollider2D>();
        spriteRenderer = GetComponent<SpriteRenderer>();

        UpdateCollider();

        Destroy(gameObject, lifetime);
    }

    void OnTriggerStay2D(Collider2D other)
    {
        TryDamage(other.gameObject);
    }

    void OnCollisionStay2D(Collision2D collision)
    {
        TryDamage(collision.gameObject);
    }
    void Update()
    {
        if (spriteRenderer.sprite != lastSprite)
        {
            UpdateCollider();
        }
    }

    void TryDamage(GameObject obj)
    {
        if (!obj.CompareTag("Player")) return;


        PlayerHealth player = obj.GetComponent<PlayerHealth>();
        if (player == null) player = obj.GetComponentInParent<PlayerHealth>();
        if (player != null)
        {
            player.TakeDamage(damage);
            lastDamageTime = Time.time;
        }
    }
    void UpdateCollider()
    {
        if (edge == null || spriteRenderer == null || spriteRenderer.sprite == null)
            return;

        lastSprite = spriteRenderer.sprite;

        physicsShape.Clear();

        int shapeCount = lastSprite.GetPhysicsShapeCount();

        if (shapeCount == 0)
            return;

        lastSprite.GetPhysicsShape(0, physicsShape);

        edge.SetPoints(physicsShape);
    }
}