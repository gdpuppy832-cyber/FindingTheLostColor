using UnityEngine;


[RequireComponent(typeof(NormalMonster))]
public class BossHPMove : MonoBehaviour
{
    [Tooltip("체력이 이 비율(0~1) 이상이 되면 이동 패턴이 무한대(∞) 궤적으로 전환됨 (기본값: 0.5 = 절반)")]
    [Range(0f, 1f)] public float infinityMoveThresholdRatio = 0.5f;

    NormalMonster normalMonster;
    BossMove flyMove;
    bool hasSwitched = false;

    void Awake()
    {
        normalMonster = GetComponent<NormalMonster>();

        flyMove = GetComponent<BossMove>();
        if (flyMove == null) flyMove = GetComponentInChildren<BossMove>();
        if (flyMove == null) flyMove = GetComponentInParent<BossMove>();
    }

    void Update()
    {
        if (hasSwitched || normalMonster == null || flyMove == null) return;

        float threshold = normalMonster.maxHealth * infinityMoveThresholdRatio;
        if (normalMonster.currentHealth >= threshold)
        {
            hasSwitched = true;
            flyMove.SetInfinityMode(true);
            Debug.Log($"[BossHPMove] {gameObject.name} 체력 {normalMonster.currentHealth}/{normalMonster.maxHealth} 도달 - 무한대(∞) 이동 패턴으로 전환");
        }
    }
}