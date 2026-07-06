using UnityEngine;
public class BossMove : MonoBehaviour
{
    // After
    [Header("Y축 진동 설정 (기본 모드)")]
    public float amplitude = 1f;
    public float speed = 1f;
    public float directionChangePauseDuration = 1f; 
    [Header("무한대(∞) 이동 설정 (체력 절반 이상 시)")]
    public float infinityWidthX = 1.5f;
    public float infinityHeightY = 1f;
    public float infinitySpeed = 1.5f; // 현재 미사용(호환용으로 남겨둠)
    public float blueArcDuration = 1f;  // 교차점 -> 노란 지점(빠른 구간) 소요 시간
    public float redArcDuration = 2f;   // 노란 지점 -> 교차점(느린 구간) 소요 시간
    public float tipPauseDuration = 1f; // 노란 지점(바깥쪽 끝) 도달 시 정지 시간
    
    Vector3 basePos;
    float timeOffset;
    bool infinityMode = false;

    float yBobPhase;
    float prevCos = 1f;     
    bool yBobPaused = false;
    float yBobPauseTimer = 0f;

    int infSegmentIndex = 0;
    float infSegmentTimer = 0f;
    bool infPaused = false;
    float infPauseTimer = 0f;
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

        yBobPhase = 0f;
        prevCos = Mathf.Cos(timeOffset);
        yBobPaused = false;
        yBobPauseTimer = 0f;
    }
    void OnEnable()
    {
        // 기준 위치와 시간 축을 함께 리셋 -> 재개 시 offset이 0 근처에서 다시 시작되어 안 튐
        basePos = transform.position;
        motionClockStart = Time.time;

        yBobPhase = 0f;
        prevCos = Mathf.Cos(timeOffset);
        yBobPaused = false;
        yBobPauseTimer = 0f;

        infSegmentIndex = 0;
        infSegmentTimer = 0f;
        infPaused = false;
        infPauseTimer = 0f;
    }
    public void SetInfinityMode(bool enable)
    {
        if (infinityMode == enable) return;
        infinityMode = enable;
        basePos = transform.position;
        motionClockStart = Time.time;

        infSegmentIndex = 0;
        infSegmentTimer = 0f;
        infPaused = false;
        infPauseTimer = 0f;
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
        // 방향 전환으로 인해 멈춰있는 동안: 위상을 그대로 두고 현재 위치만 유지
        if (yBobPaused)
        {
            yBobPauseTimer += Time.fixedDeltaTime;
            if (yBobPauseTimer >= directionChangePauseDuration)
            {
                yBobPaused = false;
                yBobPauseTimer = 0f;
            }

            float heldY = Mathf.Sin(yBobPhase + timeOffset) * amplitude;
            MoveTo(new Vector3(basePos.x, basePos.y + heldY, basePos.z));
            return;
        }

        yBobPhase += Time.fixedDeltaTime * speed;
        float angle = yBobPhase + timeOffset;
        float offsetY = Mathf.Sin(angle) * amplitude;

        // cos 부호가 바뀌는 순간 = sin의 극값(방향 전환 지점) -> 일시정지 시작
        float currentCos = Mathf.Cos(angle);
        if (currentCos * prevCos < 0f)
        {
            yBobPaused = true;
            yBobPauseTimer = 0f;
        }
        prevCos = currentCos;

        Vector3 target = new Vector3(basePos.x, basePos.y + offsetY, basePos.z);
        MoveTo(target);
    }
    void MoveInfinity()
    {
        GetInfinitySegment(infSegmentIndex, out float tStart, out float tEnd, out float duration, out bool pauseAfter);

        // 노란 지점(구간 끝)에서 정지 중
        if (infPaused)
        {
            infPauseTimer += Time.fixedDeltaTime;
            ApplyInfinityPosition(tEnd);

            if (infPauseTimer >= tipPauseDuration)
            {
                infPaused = false;
                infPauseTimer = 0f;
                infSegmentIndex = (infSegmentIndex + 1) % 4;
                infSegmentTimer = 0f;
            }
            return;
        }

 
        // 구간 진행 (파랑=빠름/빨강=느림은 duration 차이로 표현됨)
        infSegmentTimer += Time.fixedDeltaTime;
        float frac = Mathf.Clamp01(infSegmentTimer / duration);

        // pauseAfter == true (파랑 구간: 교차점->노란점) -> 끝(노란점)에서만 ease-out
        // pauseAfter == false (빨강 구간: 노란점->교차점) -> 시작(노란점)에서만 ease-in
        float easedFrac = pauseAfter ? EaseOutEnd(frac) : EaseInStart(frac);
        float t = Mathf.Lerp(tStart, tEnd, easedFrac);
        ApplyInfinityPosition(t);

        if (frac >= 1f)
        {
            infSegmentTimer = 0f;
            if (pauseAfter)
            {
                // 노란 지점 도달 -> 정지 시작
                infPaused = true;
                infPauseTimer = 0f;
            }
            else
            {
                // 교차점 통과 -> 바로 다음 구간(반대쪽 루프)으로 순환
                infSegmentIndex = (infSegmentIndex + 1) % 4;
            }
        }
    }
    // 끝부분(1 근처)에서만 감속: 시작은 등속, 끝에서 서서히 0으로 -> 노란 지점 진입용
    float EaseOutEnd(float x)
    {
        return 1f - (1f - x) * (1f - x);
    }

    // 시작부분(0 근처)에서만 가속: 시작은 0에서 서서히, 끝은 등속으로 도달 -> 노란 지점 이탈용
    float EaseInStart(float x)
    {
        return x * x;
    }
    // segmentIndex(0~3)에 따른 각도 구간/소요시간/정지여부 정의
    void GetInfinitySegment(int index, out float tStart, out float tEnd, out float duration, out bool pauseAfter)
    {
        switch (index % 4)
        {
            case 0: // 교차점 -> 오른쪽 노란 지점 (파랑, 빠름)
                tStart = 0f; tEnd = Mathf.PI * 0.5f;
                duration = blueArcDuration; pauseAfter = true;
                break;
            case 1: // 오른쪽 노란 지점 -> 교차점 (빨강, 느림)
                tStart = Mathf.PI * 0.5f; tEnd = Mathf.PI;
                duration = redArcDuration; pauseAfter = false;
                break;
            case 2: // 교차점 -> 왼쪽 노란 지점 (파랑, 빠름)
                tStart = Mathf.PI; tEnd = Mathf.PI * 1.5f;
                duration = blueArcDuration; pauseAfter = true;
                break;
            default: // 왼쪽 노란 지점 -> 교차점 (빨강, 느림)
                tStart = Mathf.PI * 1.5f; tEnd = Mathf.PI * 2f;
                duration = redArcDuration; pauseAfter = false;
                break;
        }
    }

    // 사인함수 기반 ∞자 위치 계산 (기존 공식 재사용)
    void ApplyInfinityPosition(float t)
    {
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