using UnityEngine;
public class Projectile : MonoBehaviour
{
    public LayerMask targetLayer;
    public float lifetime = 3f;
    public float blinkTime = 1f;      // 소멸 전 반짝이는 시간
    public float blinkInterval = 0.1f; // 반짝임 간격
    public bool reusable = false;      // true면 Destroy 대신 비활성화만 함 (자식으로 미리 배치된 투사체용)
    public Transform homeParent;       // 재사용 모드일 때 되돌아갈 부모 (T_EnemyAttack이 발사 직전에 직접 지정)

    public float offscreenMargin = 0.5f;

    [Header("발사 지연 및 회전 설정")]
    [Tooltip("생성된 후 실제로 날아가기 시작하기까지의 대기 시간 (초)")]
    public float preFlightDelay = 0.3f;

    [Tooltip("날아가는 동안 초당 회전하는 각도 (양수면 반시계, 음수면 시계 방향)")]
    public float rotationSpeed = 720f;

    SpriteRenderer sr;
    Camera mainCam;
    Rigidbody2D rb;

    bool isFlying = false;  // preFlightDelay가 끝나서 실제로 이동+회전 중인지 여부

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

        // 발사한 스크립트(R_EnemyAttack 등)가 이미 rb.linearVelocity를 설정해둔 상태이므로,
        // 그 값을 저장해두고 일단 제자리에 멈춰있다가 preFlightDelay 뒤에 실제로 날아가게 함
        BeginDelayedLaunch();
    }

    // 재사용 모드 초기화: 발사할 때마다 T_EnemyAttack이 호출
    public void ResetForFire()
    {
        CancelInvoke(); // 이전 예약된 Despawn/StartBlink 취소
        StopAllCoroutines(); // 이전 발사 때 돌던 지연 발사/회전 코루틴 정지
        if (sr != null) sr.enabled = true;
        gameObject.SetActive(true);
        Invoke(nameof(Despawn), lifetime);
        Invoke(nameof(StartBlink), Mathf.Max(0f, lifetime - blinkTime));

        transform.rotation = Quaternion.identity; // 회전 각도 초기화 (이전 발사 때 회전한 상태가 남지 않도록)
        BeginDelayedLaunch();
    }

    // 이동(속도)은 생성 즉시 그대로 유지하고, 회전만 preFlightDelay 뒤에 시작되게 함
    void BeginDelayedLaunch()
    {
        isFlying = false;
        StartCoroutine(DelayedLaunchRoutine());
    }

    System.Collections.IEnumerator DelayedLaunchRoutine()
    {
        yield return new WaitForSeconds(preFlightDelay);
        isFlying = true;
    }

    void Update()
    {
        // preFlightDelay가 끝나 실제로 날아가는 동안에는 계속 회전시킴
        if (isFlying)
            transform.Rotate(0f, 0f, rotationSpeed * Time.deltaTime);

        if (mainCam == null) return;
        if (reusable && !gameObject.activeSelf) return; // 숨겨진 상태면 화면 밖 체크 스킵

        Vector3 viewportPos = mainCam.WorldToViewportPoint(transform.position);

        // 뷰포트 기준 0~1이 화면 안쪽. offscreenMargin만큼 여유를 둬서,
        // 화면 경계를 살짝 벗어나는 정도로는 사라지지 않고 그보다 더 멀리 나가야 소멸함
        bool tooFarOffscreen = viewportPos.z < 0f ||
                                viewportPos.x < -offscreenMargin || viewportPos.x > 1f + offscreenMargin ||
                                viewportPos.y < -offscreenMargin || viewportPos.y > 1f + offscreenMargin;

        if (tooFarOffscreen)
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
        isFlying = false;

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
