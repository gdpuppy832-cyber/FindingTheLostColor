using UnityEngine;

public class EnemyAttack : MonoBehaviour
{
    float telegraphTime = 1f;    // 경고 표시 시간
    float attackWidth = 2f;      // 공격 판정 가로 길이
    float attackHeight = 1f;     // 공격 판정 세로 길이
    float postDelay = 0.5f;      // 공격 후 이동 불가 딜레이
    float attackCooldown = 2f;   // 공격 쿨타임

    public LayerMask targetLayer;
    public SpriteRenderer telegraphSprite; // 경고 영역 표시용 
    bool isAttacking = false;  // 공격 진행 중 
    bool canAttack = true;     // 쿨타임 여부
    EnemyMove enemyMove;



    public ContactRelay contactHitbox;
    void Start()
    {
        enemyMove = GetComponent<EnemyMove>();
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

        PlayerHealth player = other.GetComponent<PlayerHealth>();
        if (player == null) player = other.GetComponentInParent<PlayerHealth>();
        if (player != null)
        {
            float damage = 0.5f;
            NormalMonster nm = GetComponent<NormalMonster>();
            if (nm == null) nm = GetComponentInParent<NormalMonster>();
            if (nm != null) damage = nm.attackDamage;

            player.TakeDamage(damage);
        }
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

        if (enemyMove != null)
            enemyMove.enabled = false;

        // 공격 방향 계산 (마우스/타겟이 아닌 몬스터가 바라보는 방향)
        float dir = -Mathf.Sign(transform.localScale.x); 
        if (dir == 0f) dir = 1f; 

        Collider2D selfCol = GetComponent<Collider2D>();
        float edgeOffset = selfCol != null ? selfCol.bounds.extents.x : 0f; 

        Vector2 attackCenter = (Vector2)transform.position
            + new Vector2(dir * edgeOffset, 0f)           
            + new Vector2(dir * attackWidth / 2f, 0f);    

        // 경고 장판(Telegraph) 활성화
        if (telegraphSprite != null)
        {
            telegraphSprite.enabled = true;
            telegraphSprite.transform.position = new Vector3(attackCenter.x, attackCenter.y, telegraphSprite.transform.position.z);
            telegraphSprite.size = new Vector2(attackWidth, attackHeight);
        }
        yield return new WaitForSeconds(telegraphTime);

        if (telegraphSprite != null)
            telegraphSprite.enabled = false;

        // 공격 및 데미지 전달
        Collider2D hit = Physics2D.OverlapBox(attackCenter, new Vector2(attackWidth, attackHeight), 0f, targetLayer);
        if (hit != null)
        {
            Debug.Log("피격: " + hit.name);
            PlayerHealth player = hit.GetComponent<PlayerHealth>();
            if (player == null) player = hit.GetComponentInParent<PlayerHealth>();
            if (player != null)
            {
                float damage = 0.5f;
                NormalMonster nm = GetComponent<NormalMonster>();
                if (nm == null) nm = GetComponentInParent<NormalMonster>();
                if (nm != null) damage = nm.attackDamage;

                player.TakeDamage(damage);
            }
        }

        //후딜레이 
        yield return new WaitForSeconds(postDelay);

        if (enemyMove != null)
            enemyMove.enabled = true; 

        isAttacking = false;

        //쿨타임
        yield return new WaitForSeconds(attackCooldown);
        canAttack = true;
    }

    
}