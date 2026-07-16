using UnityEngine;

public class MinimapIconStabilizer : MonoBehaviour
{
    private Quaternion initialRotation;
    private Vector3 initialScale;

    void Start()
    {
        // 월드 회전을 고정 (부모가 회전하더라도 미니맵 점은 꼿꼿이 유지)
        initialRotation = Quaternion.identity;
        initialScale = transform.localScale;
    }

    void LateUpdate()
    {
        // 1. 회전 고정
        transform.rotation = initialRotation;

        // 2. 부모 오브젝트의 뒤집힘(Scale.x가 음수가 됨) 및 스케일 변화 방어
        if (transform.parent != null)
        {
            Vector3 parentScale = transform.parent.localScale;
            
            // 부모의 크기가 0이 아닐 때만 보간 스케일 적용 (분모가 0이 되는 예방)
            float parentX = Mathf.Abs(parentScale.x) > 0.001f ? Mathf.Abs(parentScale.x) : 1f;
            float parentY = Mathf.Abs(parentScale.y) > 0.001f ? Mathf.Abs(parentScale.y) : 1f;
            float parentZ = Mathf.Abs(parentScale.z) > 0.001f ? Mathf.Abs(parentScale.z) : 1f;

            transform.localScale = new Vector3(
                initialScale.x / parentX,
                initialScale.y / parentY,
                initialScale.z / parentZ
            );
        }
    }
}
