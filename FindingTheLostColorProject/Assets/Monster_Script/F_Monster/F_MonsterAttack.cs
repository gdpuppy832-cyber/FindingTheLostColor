using UnityEngine;

public class F_EnemyAttack : MonoBehaviour
{
    float telegraphTime = 0.5f;    // 경고 표시 시간
    float attackWidth = 2f;      // 공격 판정 가로 길이
    float attackHeight = 1f;     // 공격 판정 세로 길이
    float postDelay = 0.5f;      // 공격 후 이동 불가 딜레이
    float attackCooldown = 2f;   // 공격 쿨타임

    public LayerMask targetLayer;

    public SpriteRenderer telegraphSprite; // 경고 영역 표시용 SpriteRenderer (자식 오브젝트 권장)

    bool isAttacking = false;  // 공격 진행 중 (텔레그래프~판정~후딜레이 전체)
    bool canAttack = true;     // 쿨타임 여부

    F_EnemyMove F_enemyMove;
    public ContactRelay contactHitbox; // 자식 오브젝트(ContactHitbox)의 ContactRelay 연결

    void Start()
    {
        F_enemyMove = GetComponent<F_EnemyMove>();
        if (telegraphSprite != null)
            telegraphSprite.enabled = false;

        int playerLayer = LayerMask.NameToLayer("Player");
        int monsterLayer = LayerMask.NameToLayer("Monster");
        if (playerLayer != -1 && monsterLayer != -1)
            Physics2D.IgnoreLayerCollision(playerLayer, monsterLayer, true);

        if (contactHitbox != null)
        {
            contactHitbox.onTriggerEnter += TryContactDamage;
            contactHitbox.onTriggerStay += TryContactDamage;
        }
    }
    void TryContactDamage(Collider2D other)
    {
        if (!other.CompareTag("Player")) return;
        Debug.Log("플레이어에게 접촉 피해를 입혔다: ");
    }
    void Update()
    {
        if (isAttacking || !canAttack) return;

        float dir = -Mathf.Sign(transform.localScale.x);
        if (dir == 0f) dir = 1f;

        Collider2D selfCol = GetComponent<Collider2D>();
        float edgeOffset = selfCol != null ? selfCol.bounds.extents.x : 0f;

        Vector2 attackCenter = (Vector2)transform.position
            + new Vector2(dir * edgeOffset, 0f)
            + new Vector2(dir * attackWidth / 2f, 0f);

        Collider2D targetInRange = Physics2D.OverlapBox(attackCenter, new Vector2(attackWidth, attackHeight), 0f, targetLayer);
        if (targetInRange != null)
        {
            StartCoroutine(AttackRoutine());
        }
    }

    System.Collections.IEnumerator AttackRoutine()
    {
        isAttacking = true;
        canAttack = false;

        if (F_enemyMove != null)
            F_enemyMove.enabled = false;

        // 공격 방향 (오브젝트가 바라보는 방향)
        float dir = -Mathf.Sign(transform.localScale.x);
        if (dir == 0f) dir = 1f;

        Collider2D selfCol = GetComponent<Collider2D>();
        float edgeOffset = selfCol != null ? selfCol.bounds.extents.x : 0f;

        Vector2 attackCenter = (Vector2)transform.position
            + new Vector2(dir * edgeOffset, 0f)
            + new Vector2(dir * attackWidth / 2f, 0f);

        //텔레그래프 표시
        if (telegraphSprite != null)
        {
            telegraphSprite.enabled = true;
            telegraphSprite.transform.position = new Vector3(attackCenter.x, attackCenter.y, telegraphSprite.transform.position.z);
            telegraphSprite.size = new Vector2(attackWidth, attackHeight);
        }
        yield return new WaitForSeconds(telegraphTime);

        if (telegraphSprite != null)
            telegraphSprite.enabled = false;

        // 공격 판정
        Collider2D hit = Physics2D.OverlapBox(attackCenter, new Vector2(attackWidth, attackHeight), 0f, targetLayer);
        if (hit != null)
        {
            Debug.Log("공격 적중: " + hit.name);
        }

        //후딜레이 
        yield return new WaitForSeconds(postDelay);

        if (F_enemyMove != null)
            F_enemyMove.enabled = true;

        isAttacking = false;

        //쿨타임
        yield return new WaitForSeconds(attackCooldown);
        canAttack = true;
    }


}