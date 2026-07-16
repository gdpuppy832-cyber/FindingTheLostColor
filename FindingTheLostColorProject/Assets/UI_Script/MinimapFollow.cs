using UnityEngine;

public class MinimapFollow : MonoBehaviour
{
    [Header("Target Settings")]
    [Tooltip("미니맵 카메라가 추적할 타겟 (비워두면 Player 태그로 자동 탐색)")]
    public Transform target;

    [Tooltip("플레이어 위치 기준 Y축(위쪽)으로 카메라 중심점을 수직 오프셋만큼 들어 올려 줍니다. (높일수록 플레이어가 미니맵 아래쪽에 배치되어 전방/상단 지형이 더 넓게 보임)")]
    public float yTargetOffset = 3.5f;

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
            // 플레이어보다 Y축으로 yTargetOffset만큼 높은 곳을 바라보도록 설정하여 낙사 구역 등 밑에 버려지는 화면을 차단
            transform.position = new Vector3(target.position.x, target.position.y + yTargetOffset, transform.position.z);
        }
    }
}
