using UnityEngine;

public class GunMuzzle : MonoBehaviour
{
    public Transform monster;  // 몬스터(부모) Transform
    public Transform target;   // 플레이어
    public float orbitDistance = 1f; // 몬스터로부터 유지할 거리

    void Update()
    {
        if (monster == null || target == null) return;

        // 몬스터 -> 플레이어 방향
        Vector2 dir = ((Vector2)target.position - (Vector2)monster.position).normalized;

        // 몬스터 중심에서 dir 방향으로 orbitDistance만큼 떨어진 위치
        transform.position = (Vector2)monster.position + dir * orbitDistance;
    }
}