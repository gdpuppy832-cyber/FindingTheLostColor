using UnityEngine;

public class CameraFollow : MonoBehaviour
{
    [Header("추적 대상")]
    public Transform target;

    [Header("추적 설정")]
    [Tooltip("화면 아래 1/3 지점에 플레이어를 두기 위한 Y축 오프셋 값 (양수)")]
    public float yOffset = 2f;

    [Tooltip("X축(가로) 추적 시간 - 값이 작을수록 빠름")]
    public float smoothTimeX = 0.25f;

    [Tooltip("Y축(세로) 추적 시간 - 가로보다 좁으므로 더 작게(빠르게) 설정")]
    public float smoothTimeY = 0.1f;

    [Tooltip("카메라가 플레이어로부터 떨어질 수 있는 최대 거리")]
    public float maxDistance = 5f;

    [Header("맵 경계선 (카메라 이동 한계)")]
    public Vector2 minCameraPos;
    public Vector2 maxCameraPos;

    // X, Y축 각각의 관성(가속도)을 개별적으로 기억하기 위한 변수
    private float velocityX = 0f;
    private float velocityY = 0f;

    void LateUpdate()
    {
        if (target == null) return;

        // 1. 이상적인 목표 위치 계산
        Vector3 idealTargetPos = new Vector3(target.position.x, target.position.y + yOffset, transform.position.z);

        // 2. X축과 Y축을 분리하여 개별적으로 관성(SmoothDamp) 적용
        float nextX = Mathf.SmoothDamp(transform.position.x, idealTargetPos.x, ref velocityX, smoothTimeX);
        float nextY = Mathf.SmoothDamp(transform.position.y, idealTargetPos.y, ref velocityY, smoothTimeY);

        Vector3 nextPos = new Vector3(nextX, nextY, transform.position.z);

        // 3. 최대 거리 제한 (이전과 동일하게 유지)
        Vector2 offsetFromTarget = new Vector2(nextPos.x - idealTargetPos.x, nextPos.y - idealTargetPos.y);

        if (offsetFromTarget.magnitude > maxDistance)
        {
            offsetFromTarget = Vector2.ClampMagnitude(offsetFromTarget, maxDistance);
            nextPos = new Vector3(idealTargetPos.x + offsetFromTarget.x, idealTargetPos.y + offsetFromTarget.y, transform.position.z);
        }

        // 4. 맵 경계선 제한 (Clamp)
        float clampedX = Mathf.Clamp(nextPos.x, minCameraPos.x, maxCameraPos.x);
        float clampedY = Mathf.Clamp(nextPos.y, minCameraPos.y, maxCameraPos.y);

        // 5. 카메라 최종 위치 적용
        transform.position = new Vector3(clampedX, clampedY, transform.position.z);
    }
}