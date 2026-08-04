using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 메테오 폭발 시 튀어나오는 클러스터 파편 스크립트.
/// 지정된 각도(-45도, 0도, 45도)로 튀어올랐다가 포물선을 그리며 하강하며,
/// 지형을 관통(IsTrigger)하면서 적/몬스터/보스에게 추가 정화 피해를 줍니다.
/// </summary>
public class MeteorClusterShard : MonoBehaviour
{
    [Header("Shard Settings")]
    [Tooltip("클러스터 파편 개별 데미지 (기본값: 5.0)")]
    public float damage = 5.0f;

    [Tooltip("초기 튀어오르는 속도")]
    public float initialSpeed = 12.0f;

    [Tooltip("포물선 가속도를 위한 중력값")]
    public float gravity = -30.0f;

    [Tooltip("파편 수명 (초)")]
    public float lifetime = 3.0f;

    private Vector2 velocity;
    private HashSet<GameObject> hitObjects = new HashSet<GameObject>();

    /// <summary>
    /// 파편 초기화 (각도, 데미지, 스프라이트, 크기)
    /// </summary>
    public void Initialize(Vector2 angleDirection, float shardDamage, Sprite fallbackSprite, Vector3 parentScale)
    {
        damage = shardDamage;
        velocity = angleDirection.normalized * initialSpeed;

        // 1. 이미 Collider2D가 있으면 isTrigger = true 로 설정, 없으면 생성
        Collider2D col = GetComponent<Collider2D>();
        if (col == null)
        {
            CircleCollider2D circleCol = gameObject.AddComponent<CircleCollider2D>();
            circleCol.radius = 0.3f;
            col = circleCol;
        }
        col.isTrigger = true; // 지형 관통 설정

        // 2. 이미 SpriteRenderer가 있으면 기존 이미지 보존, 없으면 추가
        SpriteRenderer sr = GetComponent<SpriteRenderer>();
        if (sr == null)
        {
            sr = gameObject.AddComponent<SpriteRenderer>();
            if (fallbackSprite != null)
            {
                sr.sprite = fallbackSprite;
            }
            transform.localScale = parentScale * 0.45f;
        }

        // 레이어 정렬 보장
        sr.sortingOrder = 150;

        // 3. 자동 수명 파괴
        Destroy(gameObject, lifetime);
    }

    void Update()
    {
        // 1. 중력 적용 포물선 이동 (속도 Y 차감 ➔ 곡선 하강)
        velocity.y += gravity * Time.deltaTime;
        transform.position += (Vector3)velocity * Time.deltaTime;

        // 2. 파편 시각적 회전 연출
        transform.Rotate(0f, 0f, 480f * Time.deltaTime);

        // 3. 지형 관통 상태로 적/몬스터/보스 타격 검사
        CheckCollisions();
    }

    private void CheckCollisions()
    {
        Collider2D[] closeColliders = Physics2D.OverlapCircleAll(transform.position, 0.5f);
        foreach (var col in closeColliders)
        {
            if (col == null || col.gameObject == gameObject) continue;

            NormalMonster monster = col.GetComponentInParent<NormalMonster>();
            if (monster == null) monster = col.GetComponent<NormalMonster>();

            BossAttack boss = col.GetComponentInParent<BossAttack>();
            if (boss == null) boss = col.GetComponent<BossAttack>();

            RoseBush roseBush = col.GetComponentInParent<RoseBush>();
            if (roseBush == null) roseBush = col.GetComponent<RoseBush>();

            ColoringBridge bridge = col.GetComponentInParent<ColoringBridge>();
            if (bridge == null) bridge = col.GetComponent<ColoringBridge>();

            Trampoline trampoline = col.GetComponentInParent<Trampoline>();
            if (trampoline == null) trampoline = col.GetComponent<Trampoline>();

            PuzzleLamp lamp = col.GetComponentInParent<PuzzleLamp>();
            if (lamp == null) lamp = col.GetComponent<PuzzleLamp>();

            if (monster != null) ApplyShardDamage(monster.gameObject, monster);
            else if (boss != null) ApplyShardDamage(boss.gameObject, boss);
            else if (roseBush != null) ApplyShardDamage(roseBush.gameObject, roseBush);
            else if (bridge != null) ApplyShardDamage(bridge.gameObject, bridge);
            else if (trampoline != null) ApplyShardDamage(trampoline.gameObject, trampoline);
            else if (lamp != null) ApplyShardDamage(lamp.gameObject, lamp);
        }
    }

    private void ApplyShardDamage(GameObject targetObj, MonoBehaviour targetComponent)
    {
        if (hitObjects.Contains(targetObj)) return;

        if (targetComponent is NormalMonster monster && monster.IsPurified) return;
        if (targetComponent is RoseBush roseBush && roseBush.IsPurified) return;
        if (targetComponent is ColoringBridge bridge && bridge.IsPurified) return;
        if (targetComponent is Trampoline trampoline && trampoline.IsPurified) return;
        if (targetComponent is PuzzleLamp lamp && lamp.IsPurified) return;

        hitObjects.Add(targetObj);

        if (targetComponent is NormalMonster finalMonster) finalMonster.Heal(damage);
        else if (targetComponent is BossAttack finalBoss)
        {
            NormalMonster bHP = finalBoss.GetComponent<NormalMonster>();
            if (bHP == null) bHP = finalBoss.GetComponentInParent<NormalMonster>();
            if (bHP != null) bHP.Heal(damage);
        }
        else if (targetComponent is RoseBush finalRoseBush) finalRoseBush.Heal(damage);
        else if (targetComponent is ColoringBridge finalBridge) finalBridge.Heal(damage);
        else if (targetComponent is Trampoline finalTrampoline) finalTrampoline.Heal(damage);
        else if (targetComponent is PuzzleLamp finalLamp) finalLamp.Heal(damage);

        Debug.Log($"[궁극기 파편] 클러스터 파편 타격! 대상: {targetObj.name}, 데미지: {damage}");
    }
}
