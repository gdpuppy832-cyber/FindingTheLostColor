// After
using UnityEngine;
public class Projectile : MonoBehaviour
{
    public LayerMask targetLayer;
    public float lifetime = 3f;
    public float blinkTime = 1f;      // 소멸 전 반짝이는 시간
    public float blinkInterval = 0.1f; // 반짝임 간격

    SpriteRenderer sr;

    // After
    Camera mainCam;

    void Start()
    {
        sr = GetComponent<SpriteRenderer>();
        if (sr == null)
            sr = GetComponentInChildren<SpriteRenderer>();

        mainCam = Camera.main;

        Destroy(gameObject, lifetime);
        Invoke(nameof(StartBlink), Mathf.Max(0f, lifetime - blinkTime));
    }

    void Update()
    {
        if (mainCam == null) return;

        Vector3 viewportPos = mainCam.WorldToViewportPoint(transform.position);
        bool onScreen = viewportPos.z > 0f &&
                         viewportPos.x > 0f && viewportPos.x < 1f &&
                         viewportPos.y > 0f && viewportPos.y < 1f;

        if (!onScreen)
            Destroy(gameObject);
    }

    void StartBlink()
    {
        if (sr != null)
            StartCoroutine(BlinkRoutine());
    }

    System.Collections.IEnumerator BlinkRoutine()
    {
        while (true)
        {
            sr.enabled = !sr.enabled;
            yield return new WaitForSeconds(blinkInterval);
        }
    }

    void OnTriggerEnter2D(Collider2D other)
    {
        HandleHit(other.gameObject);
    }

    void OnCollisionEnter2D(Collision2D collision)
    {
        HandleHit(collision.gameObject);
    }

    private void HandleHit(GameObject hitObj)
    {
        // 1. Tag 검사(Player) 혹은 레이어 마스크 일치 검사로 피격 판정 (인스펙터 미설정 방지)
        if (hitObj.CompareTag("Player") || ((1 << hitObj.layer) & targetLayer) != 0)
        {
            Debug.Log("투사체 피격: " + hitObj.name);
            PlayerHealth player = hitObj.GetComponent<PlayerHealth>();
            if (player == null) player = hitObj.GetComponentInParent<PlayerHealth>();
            if (player != null)
            {
                player.TakeDamage(0.5f); // 투사체 피해량 0.5 전달
            }
            Destroy(gameObject);
        }
    }
}