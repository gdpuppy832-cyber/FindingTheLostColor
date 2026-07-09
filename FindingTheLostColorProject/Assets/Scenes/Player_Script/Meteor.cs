using UnityEngine;
using System.Collections;
using System.Collections.Generic;

public class Meteor : MonoBehaviour
{
    private Vector3 moveDirection;
    
    [Header("Meteor Settings")]
    [Tooltip("별똥별 낙하 속도 (CursorController에 의해 자동 조절되지만 개별 튜닝 가능)")]
    [SerializeField] public float speed = 15f;
    [Tooltip("지형 충돌 시 폭발 피해 반경")]
    [SerializeField] private float explosionRadius = 1.8f;

    [Header("Explosion Visual Settings")]
    [Tooltip("폭발 시 2번 모드처럼 커지는 스케일 배율 (기본값: 3.0배)")]
    [SerializeField] private float explosionScaleMultiplier = 3.0f;
    [Tooltip("폭발 시 스르륵 투명해지며 사라지는 시간 (초, 기본값: 0.25초)")]
    [SerializeField] private float explosionFadeDuration = 0.25f;
    [Tooltip("스폰 직후 지형 충돌을 무시할 유예 시간 (초, 기본값: 0.7초)")]
    [SerializeField] private float terrainIgnoreDuration = 0.7f;

    [Header("Ultimate Damage Settings")]
    [Tooltip("별똥별의 정화 피해량 (기본값: 2.0)")]
    [SerializeField] private float damage = 2.0f;

    // [수정] 별똥별마다 독립적인 중복피격판정을 위해 개별 해시셋 관리
    // 한 별똥별당 직접 충돌 대미지 1회 + 폭발 범위 대미지 1회, 총 2회 피격 허용
    private HashSet<GameObject> directHitObjects = new HashSet<GameObject>();
    private HashSet<GameObject> explosionHitObjects = new HashSet<GameObject>();

    private bool isExploded = false;
    private float aliveTime = 0f;

    // 초기 설정 함수 (sharedHitSet 매개변수 제거)
    public void Initialize(Vector3 direction, float speed, float explosionRadius)
    {
        this.moveDirection = direction.normalized;
        this.speed = speed;
        this.explosionRadius = explosionRadius;
    }

    void Update()
    {
        // 폭발 연출이 진행 중일 때는 이동과 회전을 정지
        if (isExploded) return;

        // 등속 낙하
        transform.position += moveDirection * speed * Time.deltaTime;

        // 시각적 효과를 위해 자전 연출
        transform.Rotate(0f, 0f, -360f * Time.deltaTime);

        // 평생 안 닿는 예외적 상황 방지 (4초 뒤 자동 소멸)
        aliveTime += Time.deltaTime;
        if (aliveTime > 4.0f)
        {
            Destroy(gameObject);
        }
    }

    private void OnTriggerEnter2D(Collider2D collision)
    {
        if (isExploded) return;

        // 1. 낙하 중 몬스터/오브젝트 직접 충돌 처리
        NormalMonster monster = collision.GetComponent<NormalMonster>();
        if (monster == null) monster = collision.GetComponentInParent<NormalMonster>();
        if (monster != null)
        {
            TryApplyDamage(monster.gameObject, monster);
            return;
        }

        RoseBush roseBush = collision.GetComponent<RoseBush>();
        if (roseBush == null) roseBush = collision.GetComponentInParent<RoseBush>();
        if (roseBush != null)
        {
            TryApplyDamage(roseBush.gameObject, roseBush);
            return;
        }

        ColoringBridge bridge = collision.GetComponent<ColoringBridge>();
        if (bridge == null) bridge = collision.GetComponentInParent<ColoringBridge>();
        if (bridge != null)
        {
            TryApplyDamage(bridge.gameObject, bridge);
            return;
        }

        Trampoline trampoline = collision.GetComponent<Trampoline>();
        if (trampoline == null) trampoline = collision.GetComponentInParent<Trampoline>();
        if (trampoline != null)
        {
            TryApplyDamage(trampoline.gameObject, trampoline);
            return;
        }

        PuzzleLamp lamp = collision.GetComponent<PuzzleLamp>();
        if (lamp == null) lamp = collision.GetComponentInParent<PuzzleLamp>();
        if (lamp != null)
        {
            TryApplyDamage(lamp.gameObject, lamp);
            return;
        }

        // 2. 지형 충돌 처리 (스폰 후 유예 시간이 지났을 때만 지형 폭발 활성화)
        if (aliveTime >= terrainIgnoreDuration)
        {
            string objName = collision.gameObject.name.ToLower();
            string parentName = collision.transform.parent != null ? collision.transform.parent.name.ToLower() : "";

            if (objName.Contains("platform") || 
                parentName.Contains("platform") ||
                collision.gameObject.layer == LayerMask.NameToLayer("Ground") || 
                collision.gameObject.tag == "Ground" || 
                collision.gameObject.tag == "Platform" || 
                objName.Contains("ground") || 
                objName.Contains("tilemap"))
            {
                Explode();
            }
        }
    }

    // 직접 충돌 힐/피해 적용 (별똥별 자체의 directHitObjects 리스트 관리)
    private void TryApplyDamage(GameObject targetObj, MonoBehaviour targetComponent)
    {
        // 이미 이 별똥별의 직접 충돌 피해를 입은 적이면 통과
        if (directHitObjects.Contains(targetObj)) return;
        
        // 이미 정화(사망) 완료된 대상은 타격 대상에서 제외
        if (targetComponent is NormalMonster monster && monster.IsPurified) return;
        if (targetComponent is RoseBush roseBush && roseBush.IsPurified) return;
        if (targetComponent is ColoringBridge bridge && bridge.IsPurified) return;
        if (targetComponent is Trampoline trampoline && trampoline.IsPurified) return;
        if (targetComponent is PuzzleLamp lamp && lamp.IsPurified) return;

        // H고양이의 잠복(위장) 상태 감지
        if (targetComponent is NormalMonster normalMonster)
        {
            H_MonsterMove hMove = normalMonster.GetComponent<H_MonsterMove>();
            if (hMove == null) hMove = normalMonster.GetComponentInParent<H_MonsterMove>();
            if (hMove != null && hMove.IsAmbushed) return;
        }

        // 직접 충돌 목록에 기록
        directHitObjects.Add(targetObj);

        // 정화 피해 힐 적용
        if (targetComponent is NormalMonster targetMonster) targetMonster.Heal(damage);
        else if (targetComponent is RoseBush targetRoseBush) targetRoseBush.Heal(damage);
        else if (targetComponent is ColoringBridge targetBridge) targetBridge.Heal(damage);
        else if (targetComponent is Trampoline targetTrampoline) targetTrampoline.Heal(damage);
        else if (targetComponent is PuzzleLamp targetLamp) targetLamp.Heal(damage);

        Debug.Log($"[궁극기] 별똥별 직접 충돌 정화! 대상: {targetObj.name}, 치유량: {damage}");
    }

    // 지형 충돌 시 폭발하여 범위 정화 적용 및 팽창 페이드아웃 효과 실행
    private void Explode()
    {
        isExploded = true;
        Debug.Log("[궁극기] 별똥별 지형 충돌 폭발!");

        // 중복 충돌을 방지하기 위해 콜라이더 즉시 비활성화
        Collider2D col = GetComponent<Collider2D>();
        if (col != null) col.enabled = false;

        // 폭발 반경 내의 콜라이더 긁어와 대미지/힐 즉시 적용 (1프레임 판정)
        Collider2D[] hitColliders = Physics2D.OverlapCircleAll(transform.position, explosionRadius);
        foreach (var hitCollider in hitColliders)
        {
            NormalMonster monster = hitCollider.GetComponent<NormalMonster>();
            if (monster == null) monster = hitCollider.GetComponentInParent<NormalMonster>();
            if (monster != null)
            {
                TryApplyExplosionDamage(monster.gameObject, monster);
                continue;
            }

            RoseBush roseBush = hitCollider.GetComponent<RoseBush>();
            if (roseBush == null) roseBush = hitCollider.GetComponentInParent<RoseBush>();
            if (roseBush != null) { TryApplyExplosionDamage(roseBush.gameObject, roseBush); continue; }

            ColoringBridge bridge = hitCollider.GetComponent<ColoringBridge>();
            if (bridge == null) bridge = hitCollider.GetComponentInParent<ColoringBridge>();
            if (bridge != null) { TryApplyExplosionDamage(bridge.gameObject, bridge); continue; }

            Trampoline trampoline = hitCollider.GetComponent<Trampoline>();
            if (trampoline == null) trampoline = hitCollider.GetComponentInParent<Trampoline>();
            if (trampoline != null) { TryApplyExplosionDamage(trampoline.gameObject, trampoline); continue; }

            PuzzleLamp lamp = hitCollider.GetComponent<PuzzleLamp>();
            if (lamp == null) lamp = hitCollider.GetComponentInParent<PuzzleLamp>();
            if (lamp != null) { TryApplyExplosionDamage(lamp.gameObject, lamp); continue; }
        }

        // 2번 모드 이펙트처럼 이미지 팽창 및 페이드아웃 연출 코루틴 실행
        StartCoroutine(ExplodeVisualEffectRoutine());
    }

    // 팽창 및 페이드아웃 코루틴 구현
    private IEnumerator ExplodeVisualEffectRoutine()
    {
        SpriteRenderer sr = GetComponent<SpriteRenderer>();
        if (sr == null) sr = GetComponentInChildren<SpriteRenderer>();

        if (sr != null)
        {
            Vector3 startScale = transform.localScale;
            Vector3 targetScale = startScale * explosionScaleMultiplier;
            Color startColor = sr.color;
            float elapsedTime = 0f;

            while (elapsedTime < explosionFadeDuration)
            {
                elapsedTime += Time.deltaTime;
                float t = elapsedTime / explosionFadeDuration;

                // 스케일 팽창 (Start -> Target)
                transform.localScale = Vector3.Lerp(startScale, targetScale, t);

                // 알파 투명도 감소 (1 -> 0)
                float newAlpha = Mathf.Lerp(startColor.a, 0f, t);
                sr.color = new Color(startColor.r, startColor.g, startColor.b, newAlpha);

                yield return null;
            }
        }

        // 연출 완료 후 게임오브젝트 파괴
        Destroy(gameObject);
    }

    // 폭발 피해 적용 (별똥별 자체의 explosionHitObjects 리스트 관리)
    private void TryApplyExplosionDamage(GameObject targetObj, MonoBehaviour targetComponent)
    {
        // 이미 이 별똥별의 폭발 범위 피해를 입은 적이면 통과
        if (explosionHitObjects.Contains(targetObj)) return;

        // 이미 정화(사망) 완료된 대상은 폭발 피해 적용에서 제외
        if (targetComponent is NormalMonster monster && monster.IsPurified) return;
        if (targetComponent is RoseBush roseBush && roseBush.IsPurified) return;
        if (targetComponent is ColoringBridge bridge && bridge.IsPurified) return;
        if (targetComponent is Trampoline trampoline && trampoline.IsPurified) return;
        if (targetComponent is PuzzleLamp lamp && lamp.IsPurified) return;

        // H고양이의 잠복(위장) 상태 감지
        if (targetComponent is NormalMonster targetMonster)
        {
            H_MonsterMove hMove = targetMonster.GetComponent<H_MonsterMove>();
            if (hMove == null) hMove = targetMonster.GetComponentInParent<H_MonsterMove>();
            if (hMove != null && hMove.IsAmbushed) return;
        }

        // 폭발 피해 목록에 기록
        explosionHitObjects.Add(targetObj);

        if (targetComponent is NormalMonster finalMonster) finalMonster.Heal(damage);
        else if (targetComponent is RoseBush finalRoseBush) finalRoseBush.Heal(damage);
        else if (targetComponent is ColoringBridge finalBridge) finalBridge.Heal(damage);
        else if (targetComponent is Trampoline finalTrampoline) finalTrampoline.Heal(damage);
        else if (targetComponent is PuzzleLamp finalLamp) finalLamp.Heal(damage);

        Debug.Log($"[궁극기] 별똥별 폭발 정화! 대상: {targetObj.name}, 치유량: {damage}");
    }
}
