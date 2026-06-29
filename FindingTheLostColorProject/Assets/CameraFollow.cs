using UnityEngine;

public class CameraFollow : MonoBehaviour
{
    [Header("추적 대상")]
    public Transform target;

    [Header("추적 설정")]
    [Tooltip("화면 아래 1/3 지점에 플레이어를 두기 위한 Y축 오프셋 값 (양수)")]
    public float yOffset = 2f;

    [Header("맵 경계선 (카메라 이동 한계)")]
    [Tooltip("카메라가 더 이상 내려가거나 왼쪽으로 가지 못하는 최소 좌표")]
    public Vector2 minCameraPos;
    [Tooltip("카메라가 더 이상 올라가거나 오른쪽으로 가지 못하는 최대 좌표")]
    public Vector2 maxCameraPos;

    void LateUpdate()
    {
        if (target == null) return;

        // 1. 목표 위치 계산 
        // [규칙 4-1] X축은 플레이어 위치를 그대로 따라감 (화면 중앙)
        float targetX = target.position.x;

        // [규칙 4-2] Y축은 플레이어 위치에 오프셋을 더해 화면 하단 1/3에 위치하도록 조정
        float targetY = target.position.y + yOffset;

        // 2. 맵 경계선 제한 (Clamp)
        // [규칙 5-1 & 5-2] 카메라가 설정된 min, max 좌표를 절대 벗어나지 못하도록 가둠
        float clampedX = Mathf.Clamp(targetX, minCameraPos.x, maxCameraPos.x);
        float clampedY = Mathf.Clamp(targetY, minCameraPos.y, maxCameraPos.y);

        // 3. 카메라 최종 위치 적용 (Z축은 기존 카메라의 Z값 유지)
        transform.position = new Vector3(clampedX, clampedY, transform.position.z);
    }
}