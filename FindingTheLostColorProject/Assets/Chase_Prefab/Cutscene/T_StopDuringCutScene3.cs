using UnityEngine;

public class T_StopDuringCutScene3 : MonoBehaviour
{
    [Header("연결")]
    [Tooltip("현재 씬에서 컷씬을 담당하는 T_CutScene3")]
    public T_CutScene3 cutScene;

    private Rigidbody2D rigid;

    private RigidbodyConstraints2D originalConstraints;
    private Vector2 originalVelocity;

    private MonoBehaviour movementScript;

    private bool isLocked = false;


    private void Awake()
    {
        rigid = GetComponent<Rigidbody2D>();

        if (rigid == null)
        {
            Debug.LogWarning(
                "[T_StopDuringCutScene3] " +
                "Rigidbody2D가 없습니다."
            );
        }

        // 이 오브젝트의 이동 스크립트
        movementScript = GetComponent<EnemyMove>();
    }


    private void Update()
    {
        if (cutScene == null)
            return;

        if (cutScene.IsCutsceneRunning)
        {
            LockObject();
        }
        else
        {
            UnlockObject();
        }
    }


    private void LockObject()
    {
        if (isLocked)
        {
            // 컷씬 중에는 계속 속도를 0으로 유지
            if (rigid != null)
                rigid.linearVelocity = Vector2.zero;

            return;
        }

        isLocked = true;

        // 원래 상태 저장
        if (rigid != null)
        {
            originalConstraints = rigid.constraints;
            originalVelocity = rigid.linearVelocity;

            rigid.linearVelocity = Vector2.zero;

            rigid.constraints =
                originalConstraints |
                RigidbodyConstraints2D.FreezePositionX |
                RigidbodyConstraints2D.FreezePositionY;
        }

        // EnemyMove 같은 이동 스크립트도 정지
        if (movementScript != null)
        {
            movementScript.enabled = false;
        }
    }


    private void UnlockObject()
    {
        if (!isLocked)
            return;

        isLocked = false;

        if (rigid != null)
        {
            rigid.constraints = originalConstraints;

            // 컷씬 직후 갑자기 튀어나가지 않도록 속도 0
            rigid.linearVelocity = Vector2.zero;
        }

        if (movementScript != null)
        {
            movementScript.enabled = true;
        }
    }


    private void OnDisable()
    {
        UnlockObject();
    }
}