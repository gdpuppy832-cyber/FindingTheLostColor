using UnityEngine;
using UnityEngine.UI;
using TMPro; // TextMeshPro 텍스트 컴포넌트 사용을 위해 추가

public class VolumeSettings : MonoBehaviour
{
    [System.Serializable]
    public class VolumeControlGroup
    {
        public string volumeName;          // 볼륨 이름 (예: Master, BGM, SFX)
        public Slider volumeSlider;        // 볼륨 슬라이더
        public TMP_Text volumeValueText;   // 볼륨 퍼센트 표시용 TMP 텍스트
        public Toggle muteToggle;          // 음소거 토글 (체크박스/버튼)
        public Button plusButton;          // + 버튼
        public Button minusButton;         // - 버튼

        [HideInInspector] public float lastVolumeValue; // 음소거 전 기존 볼륨 값 임시 저장용
        [HideInInspector] public bool isMuted = false;
    }

    [Header("Volume Controls")]
    [SerializeField] private VolumeControlGroup masterVolume;
    [SerializeField] private VolumeControlGroup bgmVolume;
    [SerializeField] private VolumeControlGroup sfxVolume;

    [Header("Settings")]
    [SerializeField] private float stepSize = 1f; // + , - 버튼을 눌렀을 때 증감할 수치

    private void Start()
    {
        // 각 볼륨 제어 그룹 초기화
        InitializeVolumeGroup(masterVolume);
        InitializeVolumeGroup(bgmVolume);
        InitializeVolumeGroup(sfxVolume);
    }

    private void InitializeVolumeGroup(VolumeControlGroup group)
    {
        if (group == null || group.volumeSlider == null) return;

        // [수정] 게임 시작 시 슬라이더의 값을 강제로 50%(최대값과 최소값의 정확한 중간값)로 설정합니다.
        float defaultHalfVolume = (group.volumeSlider.maxValue + group.volumeSlider.minValue) / 2f;
        group.volumeSlider.value = defaultHalfVolume;

        // 기본값을 백업 변수에도 저장해 둡니다.
        group.lastVolumeValue = defaultHalfVolume;

        // 시작 시 텍스트 퍼센트(50%) 초기화
        UpdateVolumeText(group, defaultHalfVolume);

        // 1. 슬라이더 값 변경 리스너 등록
        group.volumeSlider.onValueChanged.AddListener((val) => {
            OnSliderValueChanged(group, val);
        });

        // 2. 음소거 토글 리스너 등록
        if (group.muteToggle != null)
        {
            group.isMuted = group.muteToggle.isOn;
            group.muteToggle.onValueChanged.AddListener((isOn) => {
                OnMuteToggleChanged(group, isOn);
            });
        }

        // 3. + 버튼 리스너 등록
        if (group.plusButton != null)
        {
            group.plusButton.onClick.AddListener(() => {
                AdjustVolume(group, stepSize);
            });
        }

        // 4. - 버튼 리스너 등록
        if (group.minusButton != null)
        {
            group.minusButton.onClick.AddListener(() => {
                AdjustVolume(group, -stepSize);
            });
        }
    }

    // 슬라이더 값이 변경될 때 호출
    private void OnSliderValueChanged(VolumeControlGroup group, float value)
    {
        // 텍스트 업데이트
        UpdateVolumeText(group, value);

        // 음소거 상태인데 슬라이더를 수동으로 움직여 값을 올린 경우 음소거 해제
        if (value > group.volumeSlider.minValue && group.isMuted)
        {
            group.isMuted = false;
            if (group.muteToggle != null)
            {
                group.muteToggle.SetIsOnWithoutNotify(false); // 무한 루프 방지를 위해 이벤트 호출 없이 상태만 변경
            }
        }

        // 실제 소리를 적용하려면 여기에 로직을 연결합니다.
        ApplyVolume(group.volumeName, value);
    }

    // 음소거 토글 상태가 변경될 때 호출
    private void OnMuteToggleChanged(VolumeControlGroup group, bool isOn)
    {
        group.isMuted = isOn;

        if (isOn)
        {
            // 음소거 체크 시: 현재 슬라이더 값을 임시 저장하고 슬라이더 값을 최소값(0)으로 변경
            group.lastVolumeValue = group.volumeSlider.value;
            group.volumeSlider.value = group.volumeSlider.minValue;
        }
        else
        {
            // 음소거 체크 해제 시: 저장해 둔 기존 값으로 슬라이더 복구
            // 만약 기존 값이 이미 최소값(0) 이하라면 최대/최소값의 중간값으로 복원
            if (group.lastVolumeValue <= group.volumeSlider.minValue)
            {
                group.lastVolumeValue = (group.volumeSlider.maxValue + group.volumeSlider.minValue) / 2f;
            }
            group.volumeSlider.value = group.lastVolumeValue;
        }
    }

    // + , - 버튼으로 볼륨 조절
    private void AdjustVolume(VolumeControlGroup group, float amount)
    {
        // 만약 음소거 상태에서 증감 버튼을 누르면 음소거를 해제하고 연산 진행
        if (group.isMuted)
        {
            group.isMuted = false;
            if (group.muteToggle != null)
            {
                group.muteToggle.SetIsOnWithoutNotify(false);
            }
            
            float newMutedVal = Mathf.Clamp(group.lastVolumeValue + amount, group.volumeSlider.minValue, group.volumeSlider.maxValue);
            group.volumeSlider.value = newMutedVal;
            return;
        }

        // 원래 슬라이더 값 범위(Min ~ Max) 내에서 안전하게 값을 증감
        float newVal = Mathf.Clamp(group.volumeSlider.value + amount, group.volumeSlider.minValue, group.volumeSlider.maxValue);
        group.volumeSlider.value = newVal;
    }

    // 텍스트를 % (정수형)로 변환하여 갱신하는 헬퍼 함수
    private void UpdateVolumeText(VolumeControlGroup group, float value)
    {
        if (group.volumeValueText == null) return;

        float min = group.volumeSlider.minValue;
        float max = group.volumeSlider.maxValue;
        float range = max - min;

        if (range <= 0) return;

        // 슬라이더 범위를 0 ~ 100% 비율로 변환
        float percentage = (value - min) / range * 100f;
        
        // 소수점 반올림 후 정수형으로 "X%" 형태로 텍스트 갱신
        group.volumeValueText.text = $"{Mathf.RoundToInt(percentage)}%";
    }

    // 실제 오디오 볼륨을 조절하는 함수 (콘솔 확인용 로그 탑재)
    private void ApplyVolume(string volumeName, float value)
    {
        Debug.Log($"[{volumeName}] 볼륨이 {value}로 설정되었습니다.");

        // TODO: 나중에 사운드를 관리하는 SoundManager가 있다면 이곳에 연동하면 됩니다.
    }
}
