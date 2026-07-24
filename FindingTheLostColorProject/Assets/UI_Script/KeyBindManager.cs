using UnityEngine;
using System;
using System.Collections;
using System.Collections.Generic;

public class KeyBindManager : MonoBehaviour
{
    public static KeyBindManager Instance { get; private set; }

    [Header("Key Bindings (기본값 설정)")]
    public KeyCode LeftKey = KeyCode.A;
    public KeyCode RightKey = KeyCode.D;
    public KeyCode JumpKey = KeyCode.Space;
    public KeyCode InteractKey = KeyCode.W;
    public KeyCode DashKey = KeyCode.LeftShift;
    public KeyCode ChangeAttackKey = KeyCode.E;
    public KeyCode RecoverPaintKey = KeyCode.R;

    private Dictionary<string, KeyCode> keys = new Dictionary<string, KeyCode>();
    private bool isRebinding = false;

    public bool IsRebinding => isRebinding;

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
            LoadKeys();
        }
        else
        {
            Destroy(gameObject);
        }
    }

    /// <summary>
    /// 로컬에 저장된 키 설정을 로드합니다. (없으면 멤버 변수의 기본값 사용)
    /// </summary>
    public void LoadKeys()
    {
        LeftKey = (KeyCode)Enum.Parse(typeof(KeyCode), PlayerPrefs.GetString("Key_Left", "A"));
        RightKey = (KeyCode)Enum.Parse(typeof(KeyCode), PlayerPrefs.GetString("Key_Right", "D"));
        JumpKey = (KeyCode)Enum.Parse(typeof(KeyCode), PlayerPrefs.GetString("Key_Jump", "Space"));
        DashKey = (KeyCode)Enum.Parse(typeof(KeyCode), PlayerPrefs.GetString("Key_Dash", "LeftShift"));
        ChangeAttackKey = (KeyCode)Enum.Parse(typeof(KeyCode), PlayerPrefs.GetString("Key_ChangeAttack", "E"));
        RecoverPaintKey = (KeyCode)Enum.Parse(typeof(KeyCode), PlayerPrefs.GetString("Key_RecoverPaint", "R"));

        keys["Left"] = LeftKey;
        keys["Right"] = RightKey;
        keys["Jump"] = JumpKey;
        keys["Interact"] = InteractKey;
        keys["Dash"] = DashKey;
        keys["ChangeAttack"] = ChangeAttackKey;
        keys["RecoverPaint"] = RecoverPaintKey;
    }

    /// <summary>
    /// 현재 설정된 키들을 로컬 디스크에 반영구 보관합니다.
    /// </summary>
    public void SaveKeys()
    {
        PlayerPrefs.SetString("Key_Left", LeftKey.ToString());
        PlayerPrefs.SetString("Key_Right", RightKey.ToString());
        PlayerPrefs.SetString("Key_Interact", InteractKey.ToString());
        PlayerPrefs.SetString("Key_Dash", DashKey.ToString());
        PlayerPrefs.SetString("Key_ChangeAttack", ChangeAttackKey.ToString());
        PlayerPrefs.SetString("Key_RecoverPaint", RecoverPaintKey.ToString());
        PlayerPrefs.Save();
    }

    /// <summary>
    /// 지정된 액션의 현재 단축키 KeyCode를 반환합니다.
    /// </summary>
    public KeyCode GetKey(string actionName)
    {
        if (keys.ContainsKey(actionName))
        {
            return keys[actionName];
        }
        return KeyCode.None;
    }

    /// <summary>
    /// 외부 UI 버튼에서 특정 액션의 키 재할당을 트리거할 때 가동하는 함수
    /// </summary>
    /// <param name="actionName">재할당할 액션명 ("Left", "Right", "Jump", "Interact", "Dash")</param>
    /// <param name="onCompleted">입력 성공 시 버튼 텍스트를 바꾸기 위해 호출할 콜백</param>
    public void StartRebinding(string actionName, Action<KeyCode> onCompleted)
    {
        if (isRebinding) return;
        StartCoroutine(RebindRoutine(actionName, onCompleted));
    }

    private IEnumerator RebindRoutine(string actionName, Action<KeyCode> onCompleted)
    {
        isRebinding = true;

        // 혹시 대기 전 프레임에 이미 누르고 있던 입력이 튀어 오작동하는 것을 방지
        yield return null;

        KeyCode newKey = KeyCode.None;
        bool keyDetected = false;

        // 플레이어가 아무 키나 누를 때까지 무한 대기
        while (!keyDetected)
        {
            if (Input.anyKeyDown)
            {
                // 이번 프레임에 눌린 키보드 키 검색
                foreach (KeyCode key in Enum.GetValues(typeof(KeyCode)))
                {
                    // 마우스 버튼이나 ESC(취소용 예외키)는 단축키 바인딩 대상에서 보수적으로 배제
                    if (Input.GetKeyDown(key) && 
                        key != KeyCode.Escape && 
                        !key.ToString().Contains("Mouse"))
                    {
                        newKey = key;
                        keyDetected = true;
                        break;
                    }
                }
            }
            yield return null;
        }

        // 해당 액션의 키 코드 교체 및 캐시 갱신
        if (newKey != KeyCode.None)
        {
            if (actionName == "Left") LeftKey = newKey;
            else if (actionName == "Right") RightKey = newKey;
            else if (actionName == "Jump") JumpKey = newKey;
            else if (actionName == "Interact") InteractKey = newKey;
            else if (actionName == "Dash") DashKey = newKey;
            else if (actionName == "ChangeAttack") ChangeAttackKey = newKey;
            else if (actionName == "RecoverPaint") RecoverPaintKey = newKey;

            keys[actionName] = newKey;
            SaveKeys();

            Debug.Log($"[KeyBindManager] '{actionName}' 키 바인딩이 '{newKey}' 로 성공적으로 변경 및 저장되었습니다!");

            // 성공 콜백 호출
            onCompleted?.Invoke(newKey);
        }

        isRebinding = false;
    }
}
