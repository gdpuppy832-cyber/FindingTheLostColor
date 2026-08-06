using UnityEngine;


[RequireComponent(typeof(Rigidbody2D))]
public class BossChaseCurveBullet : MonoBehaviour
{
    private Vector3 targetPos;
    private float curveStartTime;
    private float curveMoveSpeed;

    private float elapsed = 0f;
    private Rigidbody2D rb;

    public void SetCurveParams(Vector3 targetPosition, float startTime, float moveSpeed)
    {
        targetPos = targetPosition;
        curveStartTime = startTime;
        curveMoveSpeed = moveSpeed;
        rb = GetComponent<Rigidbody2D>();
    }

    void Update()
    {
        if (rb == null) return;

        elapsed += Time.deltaTime;

        // curveStartTime 이전에는 아무것도 하지 않음 -> 기존에 설정된 왼쪽 직진 velocity 그대로 유지
        if (elapsed < curveStartTime) return;

        // curveStartTime 이후: 목표 지점 방향으로 velocity를 매 프레임 조금씩 회전시켜
        // 자연스럽게 방향을 트는 효과를 만듦 (위치를 직접 옮기지 않으므로 순간이동이 발생하지 않음)
        Vector2 directionToTarget = ((Vector2)targetPos - rb.position).normalized;
        Vector2 desiredVelocity = directionToTarget * curveMoveSpeed;

        // MoveTowards로 현재 velocity를 desiredVelocity 쪽으로 부드럽게 보간
        // (Lerp/SmoothDamp 대신 MoveTowards를 사용해 회전 속도를 curveMoveSpeed에 비례하게 제어)
        rb.linearVelocity = Vector2.MoveTowards(rb.linearVelocity, desiredVelocity, curveMoveSpeed * Time.deltaTime * 2f);
    }
}