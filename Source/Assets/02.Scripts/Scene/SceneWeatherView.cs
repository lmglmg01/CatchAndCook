using System;
using UnityEngine;

/// <summary>
/// 날씨 전용 오브젝트, 카메라 배경색, 안개색과 공용 대상의 머티리얼을 관리합니다.
/// </summary>
public sealed class SceneWeatherView : MonoBehaviour
{
    [Header("공용 카메라")]
    [SerializeField] Camera _targetCamera;

    [Header("날씨별 설정")]
    [SerializeField]
    WeatherSettings[] _weatherSettings =
    {
        new WeatherSettings(WeatherType.Sunny),
        new WeatherSettings(WeatherType.Cloudy),
        new WeatherSettings(WeatherType.Rainy)
    };

    [Header("공용 대상")]
    [SerializeField]
    SharedMaterialTarget[] _sharedTargets =
        Array.Empty<SharedMaterialTarget>();

    // 아직 날씨를 적용하지 않은 상태를 Count로 구분합니다.
    WeatherType _currentWeather = WeatherType.Count;

    public WeatherType CurrentWeather => _currentWeather;

    /// <summary>
    /// 지정한 날씨의 오브젝트, 카메라 배경색, 안개색과 공용 머티리얼을 적용합니다.
    /// </summary>
    public void ApplyWeather(WeatherType weather)
    {
        // 요청한 날씨에 해당하는 설정을 찾습니다.
        WeatherSettings selected = FindSettings(weather);

        // 설정이 없으면 현재 표현을 유지합니다.
        if (selected == null)
        {
            Debug.LogWarning(
                $"등록되지 않은 날씨입니다: {weather}",
                this);

            return;
        }

        // 다른 날씨의 전용 오브젝트를 먼저 비활성화합니다.
        foreach (WeatherSettings settings in _weatherSettings)
        {
            if (settings != null && settings != selected)
            {
                settings.SetObjectsActive(false);
            }
        }

        // 각 공용 대상에 선택한 날씨의 머티리얼 적용을 요청합니다.
        if (_sharedTargets != null)
        {
            foreach (SharedMaterialTarget target in _sharedTargets)
            {
                target?.ApplyWeather(weather);
            }
        }

        // 선택적으로 공용 카메라의 배경색을 변경합니다.
        selected.ApplyCameraSettings(_targetCamera);

        // 선택적으로 씬의 안개색을 변경합니다.
        selected.ApplyFogColor();

        // 선택한 날씨의 전용 오브젝트를 활성화합니다.
        selected.SetObjectsActive(true);

        // 마지막으로 적용한 날씨를 기록합니다.
        _currentWeather = weather;
    }

    /// <summary>
    /// 인스펙터 메뉴에서 Sunny 날씨를 적용합니다.
    /// </summary>
    [ContextMenu("Weather/Apply Sunny")]
    public void ApplySunny()
    {
        // 공통 적용 경로로 맑은 날씨를 적용합니다.
        ApplyWeather(WeatherType.Sunny);
    }

    /// <summary>
    /// 인스펙터 메뉴에서 Cloudy 날씨를 적용합니다.
    /// </summary>
    [ContextMenu("Weather/Apply Cloudy")]
    public void ApplyCloudy()
    {
        // 공통 적용 경로로 흐린 날씨를 적용합니다.
        ApplyWeather(WeatherType.Cloudy);
    }

    /// <summary>
    /// 인스펙터 메뉴에서 Rainy 날씨를 적용합니다.
    /// </summary>
    [ContextMenu("Weather/Apply Rainy")]
    public void ApplyRainy()
    {
        // 공통 적용 경로로 비 오는 날씨를 적용합니다.
        ApplyWeather(WeatherType.Rainy);
    }

    /// <summary>
    /// 지정한 날씨에 해당하는 설정을 반환합니다.
    /// </summary>
    WeatherSettings FindSettings(WeatherType weather)
    {
        // 실제 날씨가 아니거나 설정 배열이 없으면 검색하지 않습니다.
        if (weather == WeatherType.Count || _weatherSettings == null)
        {
            return null;
        }

        // 배열 순서 대신 날씨 값으로 설정을 찾습니다.
        foreach (WeatherSettings settings in _weatherSettings)
        {
            if (settings != null && settings.Weather == weather)
            {
                return settings;
            }
        }

        // 일치하는 설정이 없음을 반환합니다.
        return null;
    }

    /// <summary>
    /// 날씨 하나의 전용 오브젝트와 안개색 설정을 보관합니다.
    /// </summary>
    [Serializable]
    sealed class WeatherSettings
    {
        [SerializeField] WeatherType _weather;

        [Tooltip("이 날씨에서만 활성화할 씬 오브젝트입니다.")]
        [SerializeField]
        GameObject[] _objects = Array.Empty<GameObject>();

        [Header("카메라 배경색 설정")]
        [SerializeField] bool _changeCameraBackground;

        [SerializeField] Color _cameraBackgroundColor = Color.black;

        [Header("안개색 설정")]
        [SerializeField] bool _changeFogColor;

        [SerializeField] Color _fogColor = Color.gray;

        public WeatherType Weather => _weather;

        /// <summary>
        /// 날씨 설정의 초기 WeatherType을 지정합니다.
        /// </summary>
        public WeatherSettings(WeatherType weather)
        {
            // 일반 직렬화 데이터의 초기 날씨를 설정합니다.
            _weather = weather;
        }

        /// <summary>
        /// 등록된 날씨 전용 오브젝트의 활성 상태를 변경합니다.
        /// </summary>
        public void SetObjectsActive(bool active)
        {
            // 오브젝트 배열이 없으면 처리를 종료합니다.
            if (_objects == null)
            {
                return;
            }

            // 등록된 오브젝트를 순서대로 확인합니다.
            foreach (GameObject target in _objects)
            {
                // 비어 있거나 이미 같은 상태인 대상은 건너뜁니다.
                if (target == null || target.activeSelf == active)
                {
                    continue;
                }

                // 날씨 전용 오브젝트의 활성 상태를 변경합니다.
                target.SetActive(active);
            }
        }

        /// <summary>
        /// 배경색 변경이 설정된 경우 공용 카메라의 배경색을 변경합니다.
        /// </summary>
        public void ApplyCameraSettings(Camera targetCamera)
        {
            // 변경하지 않는 날씨이거나 카메라가 없으면 기존 설정을 유지합니다.
            if (!_changeCameraBackground || targetCamera == null)
            {
                return;
            }

            // 위치와 시야각 등 다른 카메라 설정은 유지하고 배경색만 변경합니다.
            targetCamera.backgroundColor = _cameraBackgroundColor;
        }

        /// <summary>
        /// 안개색 변경이 설정된 경우 씬의 안개색을 변경합니다.
        /// </summary>
        public void ApplyFogColor()
        {
            // 체크하지 않았다면 현재 안개색을 유지합니다.
            if (!_changeFogColor)
            {
                return;
            }

            // 안개의 활성 상태와 거리는 유지하고 색상만 변경합니다.
            RenderSettings.fogColor = _fogColor;
        }
    }

    /// <summary>
    /// 공용 MeshRenderer 하나와 해당 대상의 날씨별 머티리얼을 보관합니다.
    /// </summary>
    [Serializable]
    sealed class SharedMaterialTarget
    {
        [SerializeField] MeshRenderer _targetRenderer;

        [SerializeField]
        WeatherMaterialSettings[] _weatherMaterials =
        {
            new WeatherMaterialSettings(WeatherType.Sunny),
            new WeatherMaterialSettings(WeatherType.Cloudy),
            new WeatherMaterialSettings(WeatherType.Rainy)
        };

        /// <summary>
        /// 이 공용 대상에 지정한 날씨의 머티리얼을 적용합니다.
        /// </summary>
        public void ApplyWeather(WeatherType weather)
        {
            // 대상이나 날씨별 설정이 없으면 처리를 종료합니다.
            if (_targetRenderer == null ||
                _weatherMaterials == null ||
                weather == WeatherType.Count)
            {
                return;
            }

            // 이 대상에 등록된 날씨별 머티리얼 중 일치하는 항목을 찾습니다.
            foreach (WeatherMaterialSettings settings in _weatherMaterials)
            {
                if (settings == null || settings.Weather != weather)
                {
                    continue;
                }

                // 해당 날씨의 머티리얼을 적용하고 검색을 종료합니다.
                settings.ApplyTo(_targetRenderer);
                return;
            }
        }
    }

    /// <summary>
    /// 날씨 하나에 사용할 슬롯별 머티리얼을 보관합니다.
    /// </summary>
    [Serializable]
    sealed class WeatherMaterialSettings
    {
        [SerializeField] WeatherType _weather;

        [Tooltip("Renderer의 슬롯 순서대로 등록합니다. 비어 있는 항목은 기존 재료를 유지합니다.")]
        [SerializeField]
        Material[] _materials = Array.Empty<Material>();

        public WeatherType Weather => _weather;

        /// <summary>
        /// 머티리얼 설정의 초기 WeatherType을 지정합니다.
        /// </summary>
        public WeatherMaterialSettings(WeatherType weather)
        {
            // 이 머티리얼 목록을 사용할 날씨를 설정합니다.
            _weather = weather;
        }

        /// <summary>
        /// 전달받은 Renderer의 기존 슬롯에 머티리얼을 적용합니다.
        /// </summary>
        public void ApplyTo(MeshRenderer targetRenderer)
        {
            // 대상이나 교체할 재료가 없으면 처리를 종료합니다.
            if (targetRenderer == null ||
                _materials == null ||
                _materials.Length == 0)
            {
                return;
            }

            // 머티리얼 인스턴스를 생성하지 않고 현재 슬롯 배열을 가져옵니다.
            Material[] currentMaterials = targetRenderer.sharedMaterials;

            // 기존 슬롯 수를 넘지 않는 범위에서만 교체합니다.
            int count = Mathf.Min(
                currentMaterials.Length,
                _materials.Length);

            bool changed = false;

            // 같은 인덱스의 슬롯에 등록된 재료를 적용합니다.
            for (int i = 0; i < count; i++)
            {
                // 비어 있거나 이미 같은 재료인 슬롯은 유지합니다.
                if (_materials[i] == null ||
                    currentMaterials[i] == _materials[i])
                {
                    continue;
                }

                // 슬롯의 머티리얼 참조를 교체합니다.
                currentMaterials[i] = _materials[i];
                changed = true;
            }

            // 실제 변경이 있을 때만 Renderer에 반영합니다.
            if (changed)
            {
                targetRenderer.sharedMaterials = currentMaterials;
            }
        }
    }
}
