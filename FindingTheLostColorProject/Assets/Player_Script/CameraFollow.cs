using UnityEngine;

public class CameraFollow : MonoBehaviour
{
    [Header("대상")]
    public Transform target;

    [Header("설정")]
    [Tooltip("화면 아래 1/3 지점에 플레이어를 위치시키기 위한 Y축 오프셋 (기본값: 2)")]
    public float yOffset = 2f;

    [Tooltip("X축 이동 시간 - 부드러운 이동 속도")]
    public float smoothTimeX = 0.25f;

    [Tooltip("Y축 이동 시간 - 위아래 움직임")]
    public float smoothTimeY = 0.1f;

    [Tooltip("카메라가 플레이어로부터 가질 수 있는 최대 거리")]
    public float maxDistance = 5f;

    [Header("카메라 한계선 (카메라 이동 한계)")]
    public Vector2 minCameraPos;
    public Vector2 maxCameraPos;

    private float velocityX = 0f;
    private float velocityY = 0f;

    // 카메라 흔들림(Camera Shake) 변수
    private float shakeIntensity = 0f;
    private float shakeTimeRemaining = 0f;
    private Vector3 shakeOffset = Vector3.zero;

    void LateUpdate()
    {
        if (target == null) return;

        // 1. 이상적인 카메라 목표 위치 계산
        Vector3 idealTargetPos = new Vector3(target.position.x, target.position.y + yOffset, transform.position.z);

        // 2. X축 Y축 분리하여 SmoothDamp 적용
        float nextX = Mathf.SmoothDamp(transform.position.x, idealTargetPos.x, ref velocityX, smoothTimeX);
        float nextY = Mathf.SmoothDamp(transform.position.y, idealTargetPos.y, ref velocityY, smoothTimeY);

        Vector3 nextPos = new Vector3(nextX, nextY, transform.position.z);

        // 3. 최대 거리 제한
        Vector2 offsetFromTarget = new Vector2(nextPos.x - idealTargetPos.x, nextPos.y - idealTargetPos.y);
        if (offsetFromTarget.magnitude > maxDistance)
        {
            offsetFromTarget = Vector2.ClampMagnitude(offsetFromTarget, maxDistance);
            nextPos = new Vector3(idealTargetPos.x + offsetFromTarget.x, idealTargetPos.y + offsetFromTarget.y, transform.position.z);
        }

        // 4. 카메라 한계선 Clamping
        float clampedX = Mathf.Clamp(nextPos.x, minCameraPos.x, maxCameraPos.x);
        float clampedY = Mathf.Clamp(nextPos.y, minCameraPos.y, maxCameraPos.y);

        // 5. 카메라 흔들림(Shake) 오프셋 계산
        if (shakeTimeRemaining > 0f)
        {
            float offsetX = Random.Range(-1f, 1f) * shakeIntensity;
            float offsetY = Random.Range(-1f, 1f) * shakeIntensity;
            shakeOffset = new Vector3(offsetX, offsetY, 0f);

            shakeTimeRemaining -= Time.deltaTime;
        }
        else
        {
            shakeOffset = Vector3.zero;
        }

        // 최종 위치 적용 (흔들림 오프셋 추가)
        transform.position = new Vector3(clampedX, clampedY, transform.position.z) + shakeOffset;
    }

    /// <summary>
    /// 외부에서 카메라 흔들림 효과를 트리거하는 함수
    /// </summary>
    /// <param name="intensity">흔들림 강도 (세기)</param>
    /// <param name="duration">흔들림 지속 시간</param>
    public void TriggerShake(float intensity, float duration)
    {
        shakeIntensity = intensity;
        shakeTimeRemaining = duration;
    }
}