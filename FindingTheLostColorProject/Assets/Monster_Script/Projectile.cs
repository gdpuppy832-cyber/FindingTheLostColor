using UnityEngine;

public class Projectile : MonoBehaviour
{
    public LayerMask targetLayer;
    public LayerMask obstacleLayer; // 벽/지형 등 장애물 레이어
    public float lifetime = 3f;

    void Start()
    {
        Destroy(gameObject, lifetime);
    }

    void OnTriggerEnter2D(Collider2D other)
    {
        if (((1 << other.gameObject.layer) & targetLayer) != 0)
        {
            Debug.Log("투사체 적중: " + other.name);
            Destroy(gameObject);
        }
    }

    void OnCollisionEnter2D(Collision2D collision)
    {
        if (((1 << collision.gameObject.layer) & obstacleLayer) != 0)
        {
            Debug.Log("투사체 장애물에 부딪힘: " + collision.gameObject.name);
            Destroy(gameObject);
        }
        else if (((1 << collision.gameObject.layer) & targetLayer) != 0)
        {
            Debug.Log("투사체 적중: " + collision.gameObject.name);
            Destroy(gameObject);
        }
    }
}