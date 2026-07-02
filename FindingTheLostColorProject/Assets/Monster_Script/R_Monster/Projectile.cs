using UnityEngine;
public class Projectile : MonoBehaviour
{
    public LayerMask targetLayer;
    public float lifetime = 3f;
    public float blinkTime = 1f;      // 소멸 전 반짝이는 시간
    public float blinkInterval = 0.1f; // 반짝임 간격
    public bool reusable = false;      // true면 Destroy 대신 비활성화만 함 (자식으로 미리 배치된 투사체용)
    public Transform homeParent;       // 재사용 모드일 때 되돌아갈 부모 (T_EnemyAttack이 발사 직전에 직접 지정)
    SpriteRenderer sr;
    Camera mainCam;
    Rigidbody2D rb;

    void Start()
    {
        sr = GetComponent<SpriteRenderer>();
        if (sr == null)
            sr = GetComponentInChildren<SpriteRenderer>();
        rb = GetComponent<Rigidbody2D>();
        mainCam = Camera.main;

        // homeParent가 아직 설정 안 됐으면 (재사용 모드가 아니거나 첫 실행 순서상) 현재 부모를 기본값으로
        if (reusable && homeParent == null)
            homeParent = transform.parent;

        if (!reusable)
            Destroy(gameObject, lifetime);
        else
            Invoke(nameof(Despawn), lifetime);

        Invoke(nameof(StartBlink), Mathf.Max(0f, lifetime - blinkTime));
    }

    // 재사용 모드 초기화: 발사할 때마다 T_EnemyAttack이 호출
    public void ResetForFire()
    {
        CancelInvoke(); // 이전 예약된 Despawn/StartBlink 취소
        if (sr != null) sr.enabled = true;
        gameObject.SetActive(true);
        Invoke(nameof(Despawn), lifetime);
        Invoke(nameof(StartBlink), Mathf.Max(0f, lifetime - blinkTime));
    }

    void Update()
    {
        if (mainCam == null) return;
        if (reusable && !gameObject.activeSelf) return; // 숨겨진 상태면 화면 밖 체크 스킵

        Vector3 viewportPos = mainCam.WorldToViewportPoint(transform.position);
        bool onScreen = viewportPos.z > 0f &&
                         viewportPos.x > 0f && viewportPos.x < 1f &&
                         viewportPos.y > 0f && viewportPos.y < 1f;
        if (!onScreen)
            Despawn();
    }

    void StartBlink()
    {
        if (!gameObject.activeInHierarchy) return; // 이미 숨겨진 상태면 코루틴 시작 안 함
        if (sr != null)
            StartCoroutine(BlinkRoutine());
    }
    void OnDisable()
    {
        CancelInvoke(); // 비활성화되는 순간 예약된 Invoke(StartBlink, Despawn 등) 전부 취소
        StopAllCoroutines(); // 혹시 돌고 있는 BlinkRoutine도 정지
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
            Despawn();
        }
    }

    // 파괴 대신 상황에 맞게 처리 (재사용 모드면 숨기고 원래 부모로 복귀, 아니면 진짜 파괴)
    void Despawn()
    {
        CancelInvoke();

        if (!reusable)
        {
            Destroy(gameObject);
            return;
        }

        if (rb != null) rb.linearVelocity = Vector2.zero;
        gameObject.SetActive(false);
        if (homeParent != null)
        {
            transform.SetParent(homeParent);
            transform.localPosition = Vector3.zero;
        }
    }
}
