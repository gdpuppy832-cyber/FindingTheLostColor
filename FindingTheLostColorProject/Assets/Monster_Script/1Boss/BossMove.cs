using UnityEngine;
public class BossMove : MonoBehaviour
{
    // After
    [Header("Y�� ���� ���� (�⺻ ���)")]
    public float amplitude = 1f;
    public float speed = 1f;
    public float directionChangePauseDuration = 1f; 
    [Header("���Ѵ�(��) �̵� ���� (ü�� ���� �̻� ��)")]
    public float infinityWidthX = 1.5f;
    public float infinityHeightY = 1f;
    public float infinitySpeed = 1.5f; // ���� �̻��(ȣȯ������ ���ܵ�)
    public float blueArcDuration = 1f;  // ������ -> ��� ����(���� ����) �ҿ� �ð�
    public float redArcDuration = 2f;   // ��� ���� -> ������(���� ����) �ҿ� �ð�
    public float tipPauseDuration = 1f; // ��� ����(�ٱ��� ��) ���� �� ���� �ð�
    
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

    [Header("씬 시작 대화 잠금")]
    public bool movementLocked = true;

    // 잠금 중 중력으로 떨어지는 것을 막기 위해 캐싱 (Rigidbody2D.enabled는 절대 건드리지 않음)
    float cachedGravityScale;
    RigidbodyType2D cachedBodyType;
    bool hasCachedRbState = false;
    Vector3 lockedPosition;

    // ���� ���� ��(���� ����, ũ����Ż �ı��� ���� ���� ��Ȱ��ȭ ��) �ð� ����� ���� �����ϱ� ���� ���� �ð�.
    // �̰� ������ �����ִ� ���ȿ��� Time.time�� ��� �귯��, �ٽ� ������ ����
    // sin(t) ���� ������ �������� ������ Ƣ�� ��ġ�� �����̵��ϴ� ������ �־���
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

        // movementLocked가 Inspector에서 기본 true인 상태로 씬이 시작되므로,
        // 시작 시점에 곧바로 중력을 차단해 대화 중 낙하를 방지
        if (movementLocked)
        {
            SetMovementLocked(true);
        }
    }
    void OnEnable()
    {
        // ���� ��ġ�� �ð� ���� �Բ� ���� -> �簳 �� offset�� 0 ��ó���� �ٽ� ���۵Ǿ� �� Ʀ
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
    public void SetMovementLocked(bool locked)
    {
        movementLocked = locked;

        if (rb == null) return;

        if (locked)
        {
            // 잠그는 순간의 위치를 고정 기준점으로 저장
            lockedPosition = transform.position;

            if (!hasCachedRbState)
            {
                cachedGravityScale = rb.gravityScale;
                cachedBodyType = rb.bodyType;
                hasCachedRbState = true;
            }

            // Rigidbody2D.enabled는 그대로 true 유지. 중력만 0으로 꺼서 자유낙하 방지
            rb.gravityScale = 0f;
            rb.linearVelocity = Vector2.zero;
        }
        else
        {
            // 잠금 해제 시 원래 물리 상태로 복구
            if (hasCachedRbState)
            {
                rb.gravityScale = cachedGravityScale;
                rb.bodyType = cachedBodyType;
            }

            // 해제 즉시 basePos/타이머를 현재 위치 기준으로 재시작해서 순간이동/급가속 방지
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
        if (movementLocked)
        {
            // Rigidbody2D는 절대 끄지 않되, 중력으로 아래로 떨어지지 않도록
            // 잠긴 순간의 위치에 매 프레임 고정시켜 둠
            if (rb != null)
            {
                rb.linearVelocity = Vector2.zero;
                rb.MovePosition(lockedPosition);
            }
            return;
        }

        if (infinityMode)
            MoveInfinity();
        else
            MoveYBob();
    }
    void MoveYBob()
    {
        // ���� ��ȯ���� ���� �����ִ� ����: ������ �״�� �ΰ� ���� ��ġ�� ����
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

        // cos ��ȣ�� �ٲ�� ���� = sin�� �ذ�(���� ��ȯ ����) -> �Ͻ����� ����
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
        GetInfinitySegment(infSegmentIndex, out float tStart, out float tEnd, out float baseDuration, out bool pauseAfter);

        // infinitySpeed�� ��ü �̵� �ӵ� ������ ���: ���� Ŭ���� ���� �ҿ� �ð��� ª���� �� ���� ������
        float safeSpeed = Mathf.Max(0.01f, infinitySpeed);
        float duration = baseDuration / safeSpeed;

        // ��� ����(���� ��)���� ���� ��
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

 
        // ���� ���� (�Ķ�=����/����=������ duration ���̷� ǥ����)
        infSegmentTimer += Time.fixedDeltaTime;
        float frac = Mathf.Clamp01(infSegmentTimer / duration);

        // pauseAfter == true (�Ķ� ����: ������->�����) -> ��(�����)������ ease-out
        // pauseAfter == false (���� ����: �����->������) -> ����(�����)������ ease-in
        float easedFrac = pauseAfter ? EaseOutEnd(frac) : EaseInStart(frac);
        float t = Mathf.Lerp(tStart, tEnd, easedFrac);
        ApplyInfinityPosition(t);

        if (frac >= 1f)
        {
            infSegmentTimer = 0f;
            if (pauseAfter)
            {
                // ��� ���� ���� -> ���� ����
                infPaused = true;
                infPauseTimer = 0f;
            }
            else
            {
                // ������ ��� -> �ٷ� ���� ����(�ݴ��� ����)���� ��ȯ
                infSegmentIndex = (infSegmentIndex + 1) % 4;
            }
        }
    }
    // ���κ�(1 ��ó)������ ����: ������ ���, ������ ������ 0���� -> ��� ���� ���Կ�
    float EaseOutEnd(float x)
    {
        return 1f - (1f - x) * (1f - x);
    }

    // ���ۺκ�(0 ��ó)������ ����: ������ 0���� ������, ���� ������� ���� -> ��� ���� ��Ż��
    float EaseInStart(float x)
    {
        return x * x;
    }
    // segmentIndex(0~3)�� ���� ���� ����/�ҿ�ð�/�������� ����
    void GetInfinitySegment(int index, out float tStart, out float tEnd, out float duration, out bool pauseAfter)
    {
        switch (index % 4)
        {
            case 0: // ������ -> ������ ��� ���� (�Ķ�, ����)
                tStart = 0f; tEnd = Mathf.PI * 0.5f;
                duration = blueArcDuration; pauseAfter = true;
                break;
            case 1: // ������ ��� ���� -> ������ (����, ����)
                tStart = Mathf.PI * 0.5f; tEnd = Mathf.PI;
                duration = redArcDuration; pauseAfter = false;
                break;
            case 2: // ������ -> ���� ��� ���� (�Ķ�, ����)
                tStart = Mathf.PI; tEnd = Mathf.PI * 1.5f;
                duration = blueArcDuration; pauseAfter = true;
                break;
            default: // ���� ��� ���� -> ������ (����, ����)
                tStart = Mathf.PI * 1.5f; tEnd = Mathf.PI * 2f;
                duration = redArcDuration; pauseAfter = false;
                break;
        }
    }

    // �����Լ� ��� ���� ��ġ ��� (���� ���� ����)
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