using UnityEngine;

public class HPBarFollow : MonoBehaviour
{
    public Transform target;
    public Vector3 offset = new Vector3(0, 1.5f, 0);

    void LateUpdate()
    {
        if (target == null) return;

        transform.position = target.position + offset;

        transform.rotation = Quaternion.identity;
    }
}