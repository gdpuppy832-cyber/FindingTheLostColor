using UnityEngine;

public class AutoCameraMove : MonoBehaviour
{
    [Header("Move Settings")]
    [Tooltip("�ڵ����� ������(X�� +)���� �̵��ϴ� �ӵ�")]
    public float moveSpeed = 5f;

    [Header("Start Position")]
    [Tooltip("���� ���� �� ī�޶� �̵��� ���� ��ġ")]
    public Vector3 startPosition = Vector3.zero;

    private float fixedY;
    private float fixedZ;
    [Header("씬 시작 컷씬 잠금")]
    [Tooltip("true면 카메라 자동 이동이 완전히 정지됩니다.")]
    public bool movementLocked = true;

    public void SetMovementLocked(bool locked)
    {
        movementLocked = locked;
    }
    void Start()
    {
        // ���� ���� �� ������ ���� ��ġ�� ��� �̵�
        transform.position = startPosition;

        // ���� �ڵ� �̵� �߿��� Y, Z�� ���� ��ġ ���� �״�� ����
        fixedY = startPosition.y;
        fixedZ = startPosition.z;
    }

    void Update()
    {
        if (movementLocked) return;

        float newX = transform.position.x + moveSpeed * Time.deltaTime;
        transform.position = new Vector3(newX, fixedY, fixedZ);
    }
}