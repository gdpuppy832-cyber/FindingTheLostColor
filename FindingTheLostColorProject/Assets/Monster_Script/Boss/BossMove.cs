using UnityEngine;

public class BossMove : MonoBehaviour
{
    [Header("Y축 진동 설정 (기본 모드)")]
    public float amplitude = 1f;   // 시작 위치 기준 위아래로 움직이는 범위 (최대 진폭)
    public float speed = 1f;       // 진동 속도 (클수록 빠르게 왔다갔다)

    [Header("무한대(∞) 이동 설정 (체력 절반 이상 시)")]
    public float infinityWidthX = 1.5f;  // 좌우 폭
    public float infinityHeightY = 1f;   // 상하 폭
    public float infinitySpeed = 1.5f;   // 무한대 궤적 이동 속도

    Vector3 basePos;   // 기준이 되는 시작 위치
    float timeOffset;  // 몬스터마다 위상을 다르게 해서 같은 타이밍에 안 움직이게 함
    bool infinityMode = false; // true가 되면 Y축 왕복 대신 무한대(∞) 궤적으로 전환
    Rigidbody2D rb;    // transform 직접 이동 대신 물리 기반 이동에 사용 (트리거 판정 누락 방지)
    void Start()
    {
        basePos = transform.position;
        timeOffset = Random.Range(0f, 100f); // 여러 마리가 있어도 서로 다르게 흔들리도록
        rb = GetComponent<Rigidbody2D>();
    }

    void OnEnable()
    {
        // 공격 후딜레이 등으로 껐다 켜질 때, 기준 위치를 현재 위치로 다시 잡아서
        // 순간적으로 튀는 현상 방지
        basePos = transform.position;
    }

    // NormalMonster가 체력이 절반(예: 60 중 30)에 도달했을 때 호출
    public void SetInfinityMode(bool enable)
    {
        if (infinityMode == enable) return;
        infinityMode = enable;
        basePos = transform.position; // 모드 전환 시 순간 이동(튐) 방지
    }

    void FixedUpdate()
    {
        if (infinityMode)
            MoveInfinity();
        else
            MoveYBob();
    }
    void MoveYBob()
    {
        float offsetY = Mathf.Sin((Time.time + timeOffset) * speed) * amplitude;
        Vector3 target = new Vector3(basePos.x, basePos.y + offsetY, basePos.z);
        MoveTo(target);
    }
    void MoveInfinity()
    {
        // 리사주 곡선(sin/cos 2:1 비율)으로 숫자 8 모양(∞) 궤적을 그림
        float t = (Time.time + timeOffset) * infinitySpeed;
        float offsetX = Mathf.Sin(t) * infinityWidthX;
        float offsetY = Mathf.Sin(t * 2f) * infinityHeightY * 0.5f;
        Vector3 target = new Vector3(basePos.x + offsetX, basePos.y + offsetY, basePos.z);
        MoveTo(target);
    }
    // Rigidbody2D가 있으면 물리 기반으로, 없으면(예외 상황) transform으로 폴백
    void MoveTo(Vector3 targetPos)
    {
        if (rb != null)
            rb.MovePosition(targetPos);
        else
            transform.position = targetPos;
    }
}