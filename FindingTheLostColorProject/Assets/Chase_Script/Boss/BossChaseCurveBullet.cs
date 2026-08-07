using UnityEngine;

[RequireComponent(typeof(Rigidbody2D))]
public class BossChaseCurveBullet : MonoBehaviour
{
    private float targetY;
    private float curveStartTime;
    private float curveMoveSpeed;

    private float elapsed = 0f;
    private bool reachedTargetY = false;
    private Rigidbody2D rb;

    public void SetCurveParams(Vector3 targetPosition, float startTime, float moveSpeed)
    {
        targetY = targetPosition.y;
        curveStartTime = startTime;
        curveMoveSpeed = moveSpeed;
        rb = GetComponent<Rigidbody2D>();
    }

    void Update()
    {
        if (rb == null || reachedTargetY) return;

        elapsed += Time.deltaTime;

        // curveStartTime 이전에는 아무것도 하지 않음 -> 기존에 설정된 왼쪽 직진 velocity 그대로 유지
        if (elapsed < curveStartTime) return;

        // curveStartTime 이후: X축 속도(왼쪽 직진)는 그대로 두고, Y축 속도만 목표 Y를 향해 부여
        float currentY = rb.position.y;
        float direction = Mathf.Sign(targetY - currentY);
        float velocityY = direction * curveMoveSpeed;

        rb.linearVelocity = new Vector2(rb.linearVelocity.x, velocityY);

        // 목표 Y에 실제로 도달했는지 매 프레임 확인 (한 프레임에 지나치는 것을 방지하기 위해 MoveTowards로 위치 보정)
        float newY = Mathf.MoveTowards(currentY, targetY, curveMoveSpeed * Time.deltaTime);
        rb.position = new Vector2(rb.position.x, newY);

        if (Mathf.Approximately(newY, targetY))
        {
            reachedTargetY = true;
            rb.linearVelocity = new Vector2(rb.linearVelocity.x, 0f); // Y축 이동만 정지, X축은 계속 유지
        }
    }
}