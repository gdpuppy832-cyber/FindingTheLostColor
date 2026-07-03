using UnityEngine;
public class BossMove : MonoBehaviour
{
    [Header("Y축 진동 설정 (기본 모드)")]
    public float amplitude = 1f;
    public float speed = 1f;
    [Header("무한대(∞) 이동 설정 (체력 절반 이상 시)")]
    public float infinityWidthX = 1.5f;
    public float infinityHeightY = 1f;
    public float infinitySpeed = 1.5f;
    Vector3 basePos;
    float timeOffset;
    bool infinityMode = false;
    Rigidbody2D rb;

    // 껐다 켜질 때(공격 정지, 크리스탈 파괴로 인한 강제 비활성화 등) 시간 축까지 같이 리셋하기 위한 기준 시각.
    // 이게 없으면 꺼져있는 동안에도 Time.time은 계속 흘러서, 다시 켜지는 순간
    // sin(t) 값이 이전과 동떨어진 값으로 튀며 위치가 순간이동하는 문제가 있었음
    float motionClockStart;

    void Start()
    {
        basePos = transform.position;
        timeOffset = Random.Range(0f, 100f);
        rb = GetComponent<Rigidbody2D>();
        motionClockStart = Time.time;
    }
    void OnEnable()
    {
        // 기준 위치와 시간 축을 함께 리셋 -> 재개 시 offset이 0 근처에서 다시 시작되어 안 튐
        basePos = transform.position;
        motionClockStart = Time.time;
    }
    public void SetInfinityMode(bool enable)
    {
        if (infinityMode == enable) return;
        infinityMode = enable;
        basePos = transform.position;
        motionClockStart = Time.time; // 모드 전환 시에도 시간 축 리셋으로 튐 방지
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
        float elapsed = Time.time - motionClockStart;
        float offsetY = Mathf.Sin((elapsed + timeOffset) * speed) * amplitude;
        Vector3 target = new Vector3(basePos.x, basePos.y + offsetY, basePos.z);
        MoveTo(target);
    }
    void MoveInfinity()
    {
        float elapsed = Time.time - motionClockStart;
        float t = (elapsed + timeOffset) * infinitySpeed;
        float offsetX = Mathf.Sin(t) * infinityWidthX;
        float offsetY = Mathf.Sin(t * 2f) * infinityHeightY * 0.5f;
        Vector3 target = new Vector3(basePos.x + offsetX, basePos.y + offsetY, basePos.z);
        MoveTo(target);
    }
    void MoveTo(Vector3 targetPos)
    {
        if (rb != null)
            rb.MovePosition(targetPos);
        else
            transform.position = targetPos;
    }
}