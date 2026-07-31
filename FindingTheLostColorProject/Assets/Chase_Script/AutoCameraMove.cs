using UnityEngine;

public class AutoCameraMove : MonoBehaviour
{
    [Header("Move Settings")]
    [Tooltip("자동으로 오른쪽(X축 +)으로 이동하는 속도")]
    public float moveSpeed = 5f;

    [Header("Start Position")]
    [Tooltip("게임 시작 시 카메라가 이동할 시작 위치")]
    public Vector3 startPosition = Vector3.zero;

    private float fixedY;
    private float fixedZ;

    void Start()
    {
        // 게임 시작 시 지정된 시작 위치로 즉시 이동
        transform.position = startPosition;

        // 이후 자동 이동 중에도 Y, Z는 시작 위치 값을 그대로 유지
        fixedY = startPosition.y;
        fixedZ = startPosition.z;
    }

    void Update()
    {
        float newX = transform.position.x + moveSpeed * Time.deltaTime;
        transform.position = new Vector3(newX, fixedY, fixedZ);
    }
}