using UnityEngine;

public class MinimapFollow : MonoBehaviour
{
    [Header("Target Settings")]
    [Tooltip("미니맵 카메라가 추적할 타겟 (비워두면 Player 태그로 자동 탐색)")]
    public Transform target;

    [Tooltip("플레이어 위치 기준 Y축(위쪽)으로 카메라 중심점을 수직 오프셋만큼 들어 올려 줍니다. (높일수록 플레이어가 미니맵 아래쪽에 배치되어 전방/상단 지형이 더 넓게 보임)")]
    public float yTargetOffset = 3.5f;

    [Header("X Axis Bound Limit Settings (신규)")]
    [Tooltip("미니맵 카메라의 X축 이동 한계선 제한(Clamp) 사용 여부")]
    public bool useXBound = false;

    [Tooltip("미니맵 카메라가 이동할 수 있는 최소 X 좌표 (왼쪽 한계)")]
    public float minX = -10f;

    [Tooltip("미니맵 카메라가 이동할 수 있는 최대 X 좌표 (오른쪽 한계)")]
    public float maxX = 10f;

    [Header("Y Axis Bound Limit Settings (옵션)")]
    [Tooltip("미니맵 카메라의 Y축 이동 한계선 제한(Clamp) 사용 여부")]
    public bool useYBound = false;

    [Tooltip("미니맵 카메라가 이동할 수 있는 최소 Y 좌표 (아래쪽 한계)")]
    public float minY = -10f;

    [Tooltip("미니맵 카메라가 이동할 수 있는 최대 Y 좌표 (위쪽 한계)")]
    public float maxY = 10f;

    void Start()
    {
        if (target == null)
        {
            GameObject player = GameObject.FindGameObjectWithTag("Player");
            if (player != null)
            {
                target = player.transform;
            }
        }
    }

    void LateUpdate()
    {
        if (target != null)
        {
            float targetX = target.position.x;
            float targetY = target.position.y + yTargetOffset;

            // X축 경계선 클램프 제한 적용
            if (useXBound)
            {
                targetX = Mathf.Clamp(targetX, minX, maxX);
            }

            // Y축 경계선 클램프 제한 적용
            if (useYBound)
            {
                targetY = Mathf.Clamp(targetY, minY, maxY);
            }

            // 플레이어보다 Y축으로 yTargetOffset만큼 높은 곳을 바라보도록 설정하여 낙사 구역 등 밑에 버려지는 화면을 차단
            transform.position = new Vector3(targetX, targetY, transform.position.z);
        }
    }
}
