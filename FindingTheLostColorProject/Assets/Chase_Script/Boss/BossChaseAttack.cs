using UnityEngine;
using System.Collections;

public class BossChaseAttack : MonoBehaviour
{
    [Header("Bullet Prefab")]
    [Tooltip("�߻��� źȯ ������")]
    public GameObject bulletPrefab;

    [Header("Fire Points (3�� �� ������ �߻�)")]
    [Tooltip("�߻� ��ġ 1")]
    public Transform firePoint1;
    [Tooltip("�߻� ��ġ 2")]
    public Transform firePoint2;
    [Tooltip("�߻� ��ġ 3")]
    public Transform firePoint3;

    [Header("Bullet Movement")]
    [Tooltip("źȯ �̵� �ӵ�")]
    public float bulletSpeed = 5f;

    [Header("Fire Timing")]
    [Tooltip("źȯ�� �߻��ϴ� ����(��)")]
    public float fireInterval = 1.5f;

    [Header("Bullet Lifetime")]
    [Tooltip("źȯ�� ������ �� �ڵ����� ������� �ð�(��)")]
    public float bulletLifetime = 5f;

    [Header("Paint Destroy Settings")]
    [Tooltip("����(1�� ���)�� źȯ�� ���� �־�� �ϴ� ���� �ð�(��). �� �ð��� ä��� źȯ�� �ı���")]
    public float requiredPaintOverlapTime = 1f;

    [Header("Curve Bullet Settings")]
    [Tooltip("변화구 탄환 프리팹 (일반 탄환과 별개)")]
    public GameObject curveBulletPrefab;
    [Tooltip("변화구 탄환 생성 위치 1번. 여기서 생성되면 2번 위치의 Y좌표로 이동함")]
    public Transform curveFirePoint1;
    [Tooltip("변화구 탄환 생성 위치 2번. 여기서 생성되면 1번 위치의 Y좌표로 이동함")]
    public Transform curveFirePoint2;
    [Tooltip("생성 후 몇 초가 지나야 반대쪽 위치의 Y좌표로 꺾이기 시작할지")]
    public float curveStartTime = 0.8f;
    [Tooltip("방향을 튼 이후 목표 Y좌표를 향해 이동하는 속도")]
    public float curveMoveSpeed = 6f;
    [Tooltip("일반 탄환을 몇 발 발사할 때마다 그 자리를 변화구 탄환으로 대체할지 (먹물 장막 카운트와 독립적으로 관리됨)")]
    public int bulletsBeforeCurveBullet = 15;

    private int curveBulletFireCount = 0; // 변화구 전환 카운트 (bulletFireCount와 완전히 독립적으로 관리)

    [Header("Potion Bullet Settings")]
    [Tooltip("탄환 대신 발사할 포션 프리팹 (PotionProjectile 컴포넌트 자동 부착됨)")]
    public GameObject potionProjectilePrefab;
    [Tooltip("일반 탄환을 몇 발 발사할 때마다 그 자리를 포션으로 대체할지 (먹물 장막 차례에는 절대 끼어들지 않음)")]
    public int bulletsBeforePotion = 10;

    private int potionFireCount = 0; // 포션 전환 카운트 (먹물 장막/변화구 카운트와 완전히 독립적으로 관리)

    [Header("Boss Damage")]
    [Tooltip("��� ���� ������ �������� ����ϴ� ���ݷ�. ������ �߰��� ���ݵ� �� ���� �����")]
    public int attackDamage = 1;

    [Header("Ink Curtain Settings")]
    [Tooltip("�Թ� �帷 ������")]
    public GameObject inkCurtainPrefab;
    [Tooltip("�Թ� �帷�� ������ ��ġ")]
    public Transform inkCurtainSpawnPoint;
    [Tooltip("�Թ� �帷 �̵� �ӵ� (źȯó�� ������ -> �������� �̵�)")]
    public float inkCurtainSpeed = 4f;
    [Tooltip("�Թ� �帷�� �ڵ����� ������������ �ð�(��)")]
    public float inkCurtainLifetime = 8f;
    [Tooltip("źȯ�� �� �� �߻��� ������ �Թ� �帷���� ��ȯ����")]
    public int bulletsBeforeInkCurtain = 10;
    [Tooltip("����(1�� ���)�� �Թ� �帷�� ���� �־�� �ϴ� ���� �ð�(��). źȯ�� requiredPaintOverlapTime�� ������ �帷 ���� ��")]
    public float inkCurtainRequiredPaintOverlapTime = 3f;

    [Header("Laser Settings")]
    [Tooltip("������ �ڷ��׷��� ������")]
    public GameObject laserTelegraphPrefab;
    [Tooltip("������ ��ü ������ (�浹 ������ ������ ��ü�� �ִ� Collider2D�� �״�� ���)")]
    public GameObject laserPrefab;
    [Tooltip("�������� ���� �߻�Ǵ� ��ġ (�÷��̾ �������� �ʰ� �׻� �� ��ġ���� �߻�)")]
    public Transform laserFirePoint;
    [Tooltip("�߰� ���� �� �������� ó�� ����� �� �ְ� �Ǳ������ �ð�(��)")]
    public float laserUnlockTime = 20f;
    [Tooltip("������ ���� �ĺ��� ���� ������������ ��Ÿ��(��)")]
    public float laserCooldown = 10f;
    [Tooltip("������ �ڷ��׷��� ���� �ð�(��)")]
    public float laserTelegraphDuration = 1.5f;
    [Tooltip("������ �ڷ��׷��� ����� ����(��)")]
    public float laserBlinkInterval = 0.5f;
    [Tooltip("������ ��ü�� �����Ǵ� �ð�(��)")]
    public float laserDuration = 0.5f;

    private float attackStartTime; // �߰�(���� ����) ���� �ð� - laserUnlockTime ��� ����
    private Coroutine laserLoopCoroutine;

    private int bulletFireCount = 0; // źȯ �߻� ī��Ʈ (bulletsBeforeInkCurtain�� �����ϸ� �帷 �ߵ�)

    private Coroutine fireLoopCoroutine;

    void Start()
    {
        fireLoopCoroutine = StartCoroutine(FireLoop());

        // ������ ���/��Ÿ�� ��� ���� �ð� (�߰� ���� �������� ����)
        attackStartTime = Time.time;
        laserLoopCoroutine = StartCoroutine(LaserLoop());
    }

    private IEnumerator FireLoop()
    {
        while (true)
        {
            if (bulletFireCount >= bulletsBeforeInkCurtain)
            {
                // źȯ�� bulletsBeforeInkCurtain�� �߻������� �̹� ���ʴ� �Թ� �帷���� ��ü
                FireInkCurtain();
                bulletFireCount = 0;
            }
            else
            {
                // ★ 먹물 장막 카운트(bulletFireCount)와 완전히 독립적인 변화구 카운트.
                //   일반 탄환이 나가야 할 차례 중, 15번째마다 변화구로 대체됨.
                curveBulletFireCount++;

                // ★ 포션 카운트도 마찬가지로 독립적으로 관리. 이 블록 자체가 이미
                //   "먹물 장막 차례가 아닐 때"만 실행되므로, 포션은 장막과 절대 겹치지 않음.
                potionFireCount++;

                if (curveBulletFireCount >= bulletsBeforeCurveBullet)
                {
                    FireCurveBullet();
                    curveBulletFireCount = 0;
                }
                else if (potionFireCount >= bulletsBeforePotion)
                {
                    FirePotionBullet();
                    potionFireCount = 0;
                }
                else
                {
                    FireBullet();
                }

                bulletFireCount++;
            }

            yield return new WaitForSeconds(fireInterval);
        }
    }

    private void FireBullet()
    {
        if (bulletPrefab == null)
        {
            Debug.LogWarning("[BossChaseAttack] bulletPrefab�� ����־� �߻��� �� �����ϴ�.");
            return;
        }

        Transform chosenPoint = GetRandomFirePoint();
        if (chosenPoint == null)
        {
            Debug.LogWarning("[BossChaseAttack] ��ȿ�� �߻� ��ġ�� ���� �߻��� �� �����ϴ�.");
            return;
        }

        GameObject bulletObj = Instantiate(bulletPrefab, chosenPoint.position, Quaternion.identity);

        Rigidbody2D rb = bulletObj.GetComponent<Rigidbody2D>();
        if (rb == null) rb = bulletObj.AddComponent<Rigidbody2D>();
        rb.gravityScale = 0f;
        rb.linearVelocity = Vector2.left * bulletSpeed;

        BossChaseBullet bullet = bulletObj.GetComponent<BossChaseBullet>();
        if (bullet == null) bullet = bulletObj.AddComponent<BossChaseBullet>();
        bullet.Initialize(attackDamage, bulletLifetime, requiredPaintOverlapTime);
    }

    private void FireCurveBullet()
    {
        if (curveBulletPrefab == null)
        {
            Debug.LogWarning("[BossChaseAttack] curveBulletPrefab이 비어있어 변화구를 발사할 수 없습니다.");
            return;
        }
        if (curveFirePoint1 == null || curveFirePoint2 == null)
        {
            Debug.LogWarning("[BossChaseAttack] curveFirePoint1/2가 비어있어 변화구를 발사할 수 없습니다.");
            return;
        }

        // ★ 1번/2번 위치 중 무작위로 생성 지점을 고르고, 목표는 항상 반대쪽 위치의 Y좌표로 설정
        bool spawnAtPoint1 = Random.value < 0.5f;
        Transform spawnPoint = spawnAtPoint1 ? curveFirePoint1 : curveFirePoint2;
        Transform targetPoint = spawnAtPoint1 ? curveFirePoint2 : curveFirePoint1;

        GameObject bulletObj = Instantiate(curveBulletPrefab, spawnPoint.position, Quaternion.identity);

        Rigidbody2D rb = bulletObj.GetComponent<Rigidbody2D>();
        if (rb == null) rb = bulletObj.AddComponent<Rigidbody2D>();
        rb.gravityScale = 0f;
        rb.linearVelocity = Vector2.left * bulletSpeed; // 생성 직후엔 일반 탄환과 동일하게 왼쪽으로 직진

        // 기존 BossChaseBullet을 그대로 재사용 -> 정화(붓질)/수명/데미지 시스템이 일반 탄환과 완전히 동일
        BossChaseBullet bullet = bulletObj.GetComponent<BossChaseBullet>();
        if (bullet == null) bullet = bulletObj.AddComponent<BossChaseBullet>();
        bullet.Initialize(attackDamage, bulletLifetime, requiredPaintOverlapTime);

        // 곡선 이동만 별도 컴포넌트로 추가 (BossChaseBullet 로직에는 전혀 관여하지 않음)
        BossChaseCurveBullet curveMotion = bulletObj.GetComponent<BossChaseCurveBullet>();
        if (curveMotion == null) curveMotion = bulletObj.AddComponent<BossChaseCurveBullet>();
        curveMotion.SetCurveParams(targetPoint.position, curveStartTime, curveMoveSpeed);
    }

    // 탄환 대신 포션을 발사하는 전용 메서드. 발사 위치/속도/방향은 일반 탄환과 동일하게
    // 왼쪽으로 직진하며, PotionProjectile이 회복/수명/소멸을 자체적으로 관리함.
    private void FirePotionBullet()
    {
        if (potionProjectilePrefab == null)
        {
            Debug.LogWarning("[BossChaseAttack] potionProjectilePrefab이 비어있어 포션을 발사할 수 없습니다.");
            return;
        }

        Transform chosenPoint = GetRandomFirePoint();
        if (chosenPoint == null)
        {
            Debug.LogWarning("[BossChaseAttack] 유효한 발사 위치가 없어 포션을 발사할 수 없습니다.");
            return;
        }

        GameObject potionObj = Instantiate(potionProjectilePrefab, chosenPoint.position, Quaternion.identity);

        Rigidbody2D rb = potionObj.GetComponent<Rigidbody2D>();
        if (rb == null) rb = potionObj.AddComponent<Rigidbody2D>();
        rb.gravityScale = 0f;
        rb.linearVelocity = Vector2.left * bulletSpeed;

        ChasePotion potion = potionObj.GetComponent<ChasePotion>();
        if (potion == null) potion = potionObj.AddComponent<ChasePotion>();
    }

    // 먹물 장막(Ink Curtain) 공격 전용 메서드. 탄환과 동일하게 오른쪽 -> 왼쪽으로 이동하며,
    private void FireInkCurtain()
    {
        if (inkCurtainPrefab == null)
        {
            Debug.LogWarning("[BossChaseAttack] inkCurtainPrefab�� ����־� �Թ� �帷�� �߻��� �� �����ϴ�.");
            return;
        }
        if (inkCurtainSpawnPoint == null)
        {
            Debug.LogWarning("[BossChaseAttack] inkCurtainSpawnPoint�� ����־� �Թ� �帷�� �߻��� �� �����ϴ�.");
            return;
        }

        GameObject curtainObj = Instantiate(inkCurtainPrefab, inkCurtainSpawnPoint.position, Quaternion.identity);

        Rigidbody2D rb = curtainObj.GetComponent<Rigidbody2D>();
        if (rb == null) rb = curtainObj.AddComponent<Rigidbody2D>();
        rb.gravityScale = 0f;
        rb.linearVelocity = Vector2.left * inkCurtainSpeed;

        BossInkCurtain curtain = curtainObj.GetComponent<BossInkCurtain>();
        if (curtain == null) curtain = curtainObj.AddComponent<BossInkCurtain>();
        curtain.Initialize(attackDamage, inkCurtainLifetime, inkCurtainRequiredPaintOverlapTime);
    }

    private Transform GetRandomFirePoint()
    {
        System.Collections.Generic.List<Transform> validPoints = new System.Collections.Generic.List<Transform>();
        if (firePoint1 != null) validPoints.Add(firePoint1);
        if (firePoint2 != null) validPoints.Add(firePoint2);
        if (firePoint3 != null) validPoints.Add(firePoint3);

        if (validPoints.Count == 0) return null;

        return validPoints[Random.Range(0, validPoints.Count)];
    }

    // ������ ��� ���� ����(laserUnlockTime)�� ��ٸ� ��, ������ -> ��Ÿ���� �ݺ��ϴ� ����
    private IEnumerator LaserLoop()
    {
        float elapsedSinceStart = Time.time - attackStartTime;
        if (elapsedSinceStart < laserUnlockTime)
        {
            yield return new WaitForSeconds(laserUnlockTime - elapsedSinceStart);
        }

        while (true)
        {
            yield return StartCoroutine(FireLaser());

            // ������ ���� �ĺ��� ��Ÿ�� ���
            yield return new WaitForSeconds(laserCooldown);
        }
    }

    // ������ ���� ���� �޼���. laserFirePoint ��ġ�� �����Ǿ� �÷��̾ �������� ����.
    // �ڷ��׷��� ���� -> ����� ���� -> �ڷ��׷��� ���� -> ������ ���� -> ���� -> �ڵ� ���� ������ ����
    private IEnumerator FireLaser()
    {
        if (laserFirePoint == null)
        {
            Debug.LogWarning("[BossChaseAttack] laserFirePoint�� ����־� �������� �߻��� �� �����ϴ�.");
            yield break;
        }

        // �� �ڷ��׷��� ���� (laserFirePoint ��ġ ����)
        GameObject telegraph = null;
        if (laserTelegraphPrefab != null)
        {
            telegraph = Instantiate(laserTelegraphPrefab, laserFirePoint.position, laserFirePoint.rotation);
        }
        else
        {
            Debug.LogWarning("[BossChaseAttack] laserTelegraphPrefab�� ����־� �ڷ��׷��� ���� �����մϴ�.");
        }

        SpriteRenderer telegraphSr = telegraph != null ? telegraph.GetComponent<SpriteRenderer>() : null;

        // 1.5��(laserTelegraphDuration) ���� �����ϸ� 0.5��(laserBlinkInterval) �������� �����.
        // WaitForSeconds ��� �� ������ ���� ó���ؼ�, ����ϴ� ���ȿ��� laserFirePoint ��ġ�� ��� ���󰡵��� ����
        float blinkElapsed = 0f;
        float nextBlinkTime = laserBlinkInterval;
        bool visible = true;
        while (blinkElapsed < laserTelegraphDuration)
        {
            if (telegraph != null)
            {
                telegraph.transform.position = laserFirePoint.position;
                telegraph.transform.rotation = laserFirePoint.rotation;
            }

            blinkElapsed += Time.deltaTime;

            if (blinkElapsed >= nextBlinkTime)
            {
                nextBlinkTime += laserBlinkInterval;
                visible = !visible;
                if (telegraphSr != null) telegraphSr.enabled = visible;
            }

            yield return null;
        }

        // �� �ڷ��׷��� ����
        if (telegraph != null) Destroy(telegraph);

        // �� ������ ����
        if (laserPrefab != null)
        {
            GameObject laserObj = Instantiate(laserPrefab, laserFirePoint.position, laserFirePoint.rotation);

            BossChaseLaser hazard = laserObj.GetComponent<BossChaseLaser>();
            if (hazard == null) hazard = laserObj.AddComponent<BossChaseLaser>();
            hazard.Initialize(attackDamage);
            hazard.SetPinnedPoint(laserFirePoint); // ������ �ǹ��� laserFirePoint ��ġ�� ��� ����

            // laserDuration(0.5��) ���� ���� �� �ڵ� ����
            Destroy(laserObj, laserDuration);
            yield return new WaitForSeconds(laserDuration);
        }
        else
        {
            Debug.LogWarning("[BossChaseAttack] laserPrefab�� ����־� �������� �߻��� �� �����ϴ�.");
        }
    }
    void OnDisable()
    {
        if (fireLoopCoroutine != null)
        {
            StopCoroutine(fireLoopCoroutine);
            fireLoopCoroutine = null;
        }

        // ������ ������ �Բ� ����
        if (laserLoopCoroutine != null)
        {
            StopCoroutine(laserLoopCoroutine);
            laserLoopCoroutine = null;
        }
    }
}