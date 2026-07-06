using UnityEngine;
using UnityEngine.UI;
using TMPro;

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

    private bool isInitialized = false;

    private void Start()
    {
        // 1. 이벤트 리스너 등록은 게임 시작 시 최초 1회만 수행 (중복 등록 방지)
        InitializeListeners(masterVolume);
        InitializeListeners(bgmVolume);
        InitializeListeners(sfxVolume);
        isInitialized = true;

        // 2. 현재 저장된 볼륨 값으로 슬라이더 위치와 텍스트 갱신
        RefreshVolumeSliders();
    }

    private void OnEnable()
    {
        // 같은 씬 내에서 설정창이 꺼졌다가 다시 켜질 때마다 최신 볼륨 상태로 슬라이더를 갱신합니다.
        if (isInitialized)
        {
            RefreshVolumeSliders();
        }
    }

    // 모든 볼륨 슬라이더 수치를 최신 데이터로 동기화
    private void RefreshVolumeSliders()
    {
        UpdateVolumeSliderValue(masterVolume);
        UpdateVolumeSliderValue(bgmVolume);
        UpdateVolumeSliderValue(sfxVolume);
    }

    // 개별 볼륨 슬라이더 및 텍스트 갱신
    private void UpdateVolumeSliderValue(VolumeControlGroup group)
    {
        if (group == null || group.volumeSlider == null) return;

        float currentVolume = (group.volumeSlider.maxValue + group.volumeSlider.minValue) / 2f;

        // [진단 디버그] SoundManager.Instance 상태 체크
        if (SoundManager.Instance != null)
        {
            float savedRatio = -1f; // 매칭 실패 여부를 확인하기 위해 -1로 시작

            if (group.volumeName.Equals("Master", System.StringComparison.OrdinalIgnoreCase))
            {
                savedRatio = SoundManager.Instance.GetMasterVolume();
            }
            else if (group.volumeName.Equals("BGM", System.StringComparison.OrdinalIgnoreCase))
            {
                savedRatio = SoundManager.Instance.GetBGMVolume();
            }
            else if (group.volumeName.Equals("SFX", System.StringComparison.OrdinalIgnoreCase))
            {
                savedRatio = SoundManager.Instance.GetSFXVolume();
            }

            // 매칭에 성공한 경우 값 대입
            if (savedRatio >= 0f)
            {
                currentVolume = savedRatio * (group.volumeSlider.maxValue - group.volumeSlider.minValue) + group.volumeSlider.minValue;
            }
            else
            {
                // [이름 오타 진단]
                Debug.LogWarning($"<color=yellow>[VolumeSettings - 경고]</color> '{group.volumeName}'과 매칭되는 볼륨을 SoundManager에서 찾을 수 없습니다. 이름이 Master, BGM, SFX 인지 확인하세요. (기본값 50% 적용됨)");
            }
        }
        else
        {
            // [싱글톤 null 진단]
            Debug.LogError($"<color=red>[VolumeSettings - 치명적 오류]</color> SoundManager.Instance가 null 상태입니다! 볼륨 설정을 불러올 수 없어 기본값(50%)으로 리셋되었습니다. 씬에 SoundManager 오브젝트가 없거나 비활성화되어 있습니다.");
        }

        // 이벤트 호출 없이 값만 안전하게 갱신
        group.volumeSlider.SetValueWithoutNotify(currentVolume);
        group.lastVolumeValue = currentVolume;

        // 텍스트 % 수치 반영
        UpdateVolumeText(group, currentVolume);
    }

    // 각 UI 버튼 및 슬라이더에 이벤트 리스너를 꽂아주는 함수
    private void InitializeListeners(VolumeControlGroup group)
    {
        if (group == null || group.volumeSlider == null) return;

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
                group.muteToggle.SetIsOnWithoutNotify(false); // 이벤트 무한 호출 방지
            }
        }

        // 실제 소리를 SoundManager에 적용
        ApplyVolume(group, value);
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

        float percentage = (value - min) / range * 100f;
        group.volumeValueText.text = $"{Mathf.RoundToInt(percentage)}%";
    }

    // 실제 오디오 볼륨을 SoundManager에 전달하여 조절하는 함수
    private void ApplyVolume(VolumeControlGroup group, float value)
    {
        float min = group.volumeSlider.minValue;
        float max = group.volumeSlider.maxValue;
        float range = max - min;
        
        float ratio = range > 0f ? (value - min) / range : 0f;

        if (SoundManager.Instance != null)
        {
            if (group.volumeName.Equals("Master", System.StringComparison.OrdinalIgnoreCase))
            {
                SoundManager.Instance.SetMasterVolume(ratio);
            }
            else if (group.volumeName.Equals("BGM", System.StringComparison.OrdinalIgnoreCase))
            {
                SoundManager.Instance.SetBGMVolume(ratio);
            }
            else if (group.volumeName.Equals("SFX", System.StringComparison.OrdinalIgnoreCase))
            {
                SoundManager.Instance.SetSFXVolume(ratio);
            }
        }
    }
}
