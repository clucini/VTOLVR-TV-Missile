using System;
using System.Collections;
using OC;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.UI.Extensions;
using UnityStandardAssets.ImageEffects;
using VTOLVR.Multiplayer;

public class TVMFDPage : MonoBehaviour, IRequiresOpticalTargeter, IQSVehicleComponent, IPersistentVehicleData, ILocalizationUser
{
    public enum SensorModes
    {
        DAY = 0,
        NIGHT = 1,
        COLOR = 2
    }

    public enum TGPModes
    {
        TGT = 0,
        PIP = 1,
        HEAD = 2,
        FWD = 3
    }

    public HelmetController helmet;

    public GameObject targetRT;

    public Camera TVCamera;

    public Actor actor;

    public RectTransform borderTf;

    public Transform bearingRotator;

    public DashMapDisplay map;

    public AudioSource uiAudioSource;

    public AudioClip audio_zoomInClip;

    public AudioClip audio_zoomOutClip;

    public AudioClip audio_areaLockClip;

    public AudioClip audio_targetLockClip;

    public AudioClip audio_headModeClip;

    public float slewRate;

    private HUDWeaponInfo hudInfo;

    public float dayRampOffset = 0.312f;

    public float nightRampOffset = 0.92f;

    public SensorModes sensorMode = SensorModes.COLOR;

    private string[] sensorModeLabels = new string[3] { "DAY", "NIGHT", "COLOR" };

    private int sensorModeCount;

    public Grayscale grayScaleEffect;

    public IlluminateVesselsOnRender targetIlluminator;

    public CameraFogSettings cameraFog;

    public float[] fovs;

    public MultiUserVehicleSync muvs;

    public OpticalTargeter opticalTargeter;

    private float lockedTime;

    public Text sensorModeText;

    public Text rangeText;

    public GameObject rangeObject;

    public GameObject lockDisplayObject;

    public GameObject actorLockDisplayObject;

    public FlightInfo flightInfo;

    public Transform rollTf;

    public Transform pitchTf;

    public GameObject gimbalLimitObject;

    public GameObject headModeDisplayObj;

    public GameObject friendObject;

    public GameObject foeObject;

    private GPSTargetSystem gpsSystem;

    public GameObject errorObject;

    public Text errorText;

    private float targetRange;

    private float lerpedBorderSize = 10f;

    private WeaponManager wm;

    private string[] tgpModeLabels = new string[4] { "TGT", "PIP", "HEAD", "FWD" };

    private bool gotMfdp;

    private MFDPage _mfdp;

    public MFDPage ovrdMFDPage;

    public MFDPortalPage portalPage;

    private MeasurementManager measurements;

    private bool _switchedOn;

    private bool slewing;

    private bool iffActive;

    private string s_hmd;

    private string s_tgp_noLock = "NO LOCK";

    private string s_tgp_noGpsGroup = "NO GPS GROUP";

    private string s_tgp_noTarget = "NO TARGET";

    private string s_tgp_notSOI = "NOT SOI";

    private bool started;

    public Texture irModeSkyTexture;

    private bool helmetDisplay;

    private bool hmdView = true;

    private float tsButtonDownTime;

    private bool hasResetThumbStick = true;

    public bool lerpZoom;

    public float zoomLerpRate = 10f;

    public Action<int> OnSetFovIdx;

    private Coroutine autoSlewRoutine;

    public ErrorFlasher errorFlasher;

    private Coroutine errorRoutine;

    private ConfigNode myQNode;

    [Header("Limit Line")]
    public GameObject limitLineDisplayObj;

    public UILineRenderer limitLineRenderer;

    public Transform limitPositionTf;

    public float limitLineScale = 35f;

    public int lineLimitVertCount = 40;

    public float vesselRelMaxPitch = 30f;

    private bool limLineVisible = true;

    public int fovIdx { get; private set; }

    public bool remoteOnly { get; set; }

    public bool locked
    {
        get
        {
            if ((bool)opticalTargeter)
            {
                return opticalTargeter.locked;
            }
            return false;
        }
    }

    public TGPModes tgpMode { get; private set; }

    public MFDPage mfdPage
    {
        get
        {
            if (!gotMfdp)
            {
                if ((bool)ovrdMFDPage)
                {
                    _mfdp = ovrdMFDPage;
                }
                else
                {
                    _mfdp = GetComponent<MFDPage>();
                }
                gotMfdp = true;
            }
            return _mfdp;
        }
    }

    public bool isSOI
    {
        get
        {
            if (!mfdPage || !mfdPage.isSOI)
            {
                if ((bool)portalPage)
                {
                    return portalPage.isSOI;
                }
                return false;
            }
            return true;
        }
    }

    public bool powered
    {
        get
        {
            if ((bool)opticalTargeter)
            {
                _switchedOn = opticalTargeter.powered;
                return _switchedOn;
            }
            return _switchedOn;
        }
        set
        {
            if ((bool)opticalTargeter)
            {
                opticalTargeter.powered = value;
            }
            _switchedOn = value;
        }
    }

    private string qsNodeName => base.gameObject.name + "_TargetingMFDPage";

    public event Action<SensorModes> OnSetSensorMode;

    public event Action OnRemoteLaserToggleRequest;

    public event Action<bool> OnTGPPwrButton;

    public event Action<TGPModes> OnSetMode;

    private void PlayAudio(AudioClip clip)
    {
        if ((bool)uiAudioSource)
        {
            uiAudioSource.Stop();
            uiAudioSource.PlayOneShot(clip);
        }
    }

    public void ApplyLocalization()
    {
        s_hmd = VTLocalizationManager.GetString("s_hmd", "HMD", "TGP page label");
        s_tgp_noLock = VTLocalizationManager.GetString("s_tgp_noLock", "NO LOCK", "TGP page error");
        s_tgp_noGpsGroup = VTLocalizationManager.GetString("s_tgp_noGpsGroup", "NO GPS GROUP", "TGP page error");
        s_tgp_noTarget = VTLocalizationManager.GetString("s_tgp_noTarget", "NO TARGET", "TGP page error");
        s_tgp_notSOI = VTLocalizationManager.GetString("s_tgp_notSOI", "NOT SOI", "TGP page error");
        for (int i = 0; i < tgpModeLabels.Length; i++)
        {
            string[] array = tgpModeLabels;
            int num = i;
            string key = $"s_tgpMode_{i}";
            TGPModes tGPModes = (TGPModes)i;
            array[num] = VTLocalizationManager.GetString(key, tGPModes.ToString(), "TGP mode label");
        }
        for (int j = 0; j < sensorModeLabels.Length; j++)
        {
            string[] array2 = sensorModeLabels;
            int num2 = j;
            string key2 = $"tgp_sensorMode_{j}";
            SensorModes sensorModes = (SensorModes)j;
            array2[num2] = VTLocalizationManager.GetString(key2, sensorModes.ToString(), "TGP sensor mode label");
        }
    }

    private void Awake()
    {
        ApplyLocalization();
        sensorModeCount = Enum.GetValues(typeof(SensorModes)).Length;
        if (!portalPage)
        {
            portalPage = GetComponent<MFDPortalPage>();
        }
        wm = GetComponentInParent<WeaponManager>();
        measurements = GetComponentInParent<MeasurementManager>();
        powered = false;
        hudInfo = wm.GetComponentInChildren<HUDWeaponInfo>();
        if ((bool)mfdPage)
        {
            mfdPage.OnInputAxis.AddListener(OnSetThumbstick);
            mfdPage.OnInputButtonDown.AddListener(OnThumbstickDown);
            mfdPage.OnInputButtonUp.AddListener(OnThumbstickUp);
            mfdPage.OnInputAxisReleased.AddListener(OnResetThumbstick);
            mfdPage.OnDeactivatePage.AddListener(OnDeactivatePage);
        }
        else if ((bool)portalPage)
        {
            portalPage.OnInputAxis.AddListener(OnSetThumbstick);
            portalPage.OnInputButtonDown.AddListener(OnThumbstickDown);
            portalPage.OnInputButtonUp.AddListener(OnThumbstickUp);
            portalPage.OnInputAxisReleased.AddListener(OnResetThumbstick);
            portalPage.OnSetPageStateEvent += OnSetPageState;
            portalPage.OnShowPage.AddListener(TGPPowerOn);
        }
        if ((bool)TVCamera)
        {
            TVCamera.fieldOfView = fovs[fovIdx];
        }
        SetSensorMode(SensorModes.DAY);
        tgpMode = TGPModes.FWD;
        targetRT.SetActive(value: false);
        headModeDisplayObj.SetActive(value: false);
        errorObject.SetActive(value: false);
        rangeObject.SetActive(value: false);
        Debug.Log("TargetingMFDPage Awake");
    }

    private void Start()
    {
        Setup();
    }

    private void Setup()
    {
        if (started)
        {
            return;
        }
        started = true;
        if (!wm)
        {
            wm = GetComponentInParent<WeaponManager>();
        }
        if ((bool)wm.opticalTargeter)
        {
            if (!TVCamera)
            {
                // TODO: Convert to missile
               TVCamera = wm.opticalTargeter.cameraTransform.GetComponent<Camera>();
            }
            if ((bool)targetingCamera)
            {
                LODManager.instance.tcam = targetingCamera;
            }
        }
        UpdateLaserMarkerText();
        if ((bool)mfdPage)
        {
            mfdPage.SetText("tgpMode", tgpModeLabels[(int)tgpMode]);
        }
        else if ((bool)portalPage)
        {
            portalPage.SetText("tgpMode", tgpModeLabels[(int)tgpMode]);
        }
        gpsSystem = wm.gpsSystem;
        if ((bool)limitLineRenderer)
        {
            SetupLimitLine();
        }
        UpdateLimLineVisibility();
        Debug.Log("TargetingMFDPage Setup");
    }

    private void OnDeactivatePage()
    {
        errorObject.SetActive(value: false);
    }

    private void OnSetPageState(MFDPortalPage.PageStates s)
    {
        if (s == MFDPortalPage.PageStates.SubSized || s == MFDPortalPage.PageStates.Minimized)
        {
            OnDeactivatePage();
        }
    }

    public void ToggleSensorMode()
    {
        if (!started)
        {
            Setup();
        }
        if ((bool)opticalTargeter)
        {
            if (NoControlInMP())
            {
                DisplayErrorMessage(s_tgp_notSOI);
                return;
            }
            sensorMode = (SensorModes)((int)(sensorMode + 1) % sensorModeCount);
            SetSensorMode(sensorMode);
        }
    }

    private void SetSensorMode(SensorModes sensorMode, bool sendEvent = true)
    {
        this.sensorMode = sensorMode;
        if ((bool)targetingCamera)
        {
            switch (sensorMode)
            {
                case SensorModes.DAY:
                    grayScaleEffect.enabled = true;
                    grayScaleEffect.rampOffset = dayRampOffset;
                    targetIlluminator.enabled = true;
                    cameraFog.fogMode = FogMode.Linear;
                    cameraFog.overrideFogTexture = irModeSkyTexture;
                    cameraFog.overrideSkyTexture = irModeSkyTexture;
                    break;
                case SensorModes.NIGHT:
                    grayScaleEffect.enabled = true;
                    grayScaleEffect.rampOffset = nightRampOffset;
                    targetIlluminator.enabled = true;
                    cameraFog.fogMode = FogMode.Linear;
                    cameraFog.overrideFogTexture = irModeSkyTexture;
                    cameraFog.overrideSkyTexture = irModeSkyTexture;
                    break;
                case SensorModes.COLOR:
                    grayScaleEffect.enabled = false;
                    targetIlluminator.enabled = false;
                    cameraFog.fogMode = FogMode.ExponentialSquared;
                    cameraFog.overrideFogTexture = null;
                    cameraFog.overrideSkyTexture = null;
                    break;
            }
        }
        sensorModeText.text = sensorModeLabels[(int)sensorMode];
        this.OnSetSensorMode?.Invoke(sensorMode);
    }

    public void RemoteSetSensorMode(SensorModes mode)
    {
        SetSensorMode(mode, sendEvent: false);
    }

    public void SetHelmet(HelmetController h)
    {
        helmet = h;
        if ((bool)opticalTargeter)
        {
            MeshRenderer component = helmet.displayQuad.GetComponent<MeshRenderer>();
            if ((bool)component)
            {
                MaterialPropertyBlock materialPropertyBlock = new MaterialPropertyBlock();
                materialPropertyBlock.SetTexture("_MainTex", targetingCamera.targetTexture);
                component.SetPropertyBlock(materialPropertyBlock);
            }
        }
        helmet.RefreshHMCSUpdate();
        if (helmet.tgpDisplayEnabled != helmetDisplay)
        {
            helmet.ToggleDisplay();
            headModeDisplayObj.SetActive(helmetDisplay);
            targetRT.SetActive(!helmetDisplay);
        }
        helmet.displayQuadParent.SetActive(hmdView);
    }

    public void SetOpticalTargeter(OpticalTargeter t)
    {
        if (!started)
        {
            Setup();
        }
        opticalTargeter = t;
        if ((bool)opticalTargeter)
        {
            targetingCamera = opticalTargeter.GetComponentInChildren<Camera>();
            if ((bool)targetingCamera)
            {
                targetingCamera.fieldOfView = fovs[fovIdx];
                LODManager.instance.tcam = targetingCamera;
                if ((bool)helmet)
                {
                    MeshRenderer component = helmet.displayQuad.GetComponent<MeshRenderer>();
                    if ((bool)component)
                    {
                        MaterialPropertyBlock materialPropertyBlock = new MaterialPropertyBlock();
                        materialPropertyBlock.SetTexture("_MainTex", targetingCamera.targetTexture);
                        component.SetPropertyBlock(materialPropertyBlock);
                    }
                }
                if (VTResources.useOverCloud && !targetingCamera.gameObject.GetComponent<OverCloudCamera>())
                {
                    OverCloudCamera overCloudCamera = targetingCamera.gameObject.AddComponent<OverCloudCamera>();
                    overCloudCamera.lightSampleCount = SampleCount.Low;
                    overCloudCamera.scatteringMaskSamples = SampleCount.Low;
                    overCloudCamera.includeCascadedShadows = false;
                    overCloudCamera.renderScatteringMask = false;
                    overCloudCamera.renderVolumetricClouds = false;
                    overCloudCamera.downsample2DClouds = true;
                }
            }
            targetIlluminator = opticalTargeter.GetComponentInChildren<IlluminateVesselsOnRender>();
            cameraFog = opticalTargeter.GetComponentInChildren<CameraFogSettings>();
            grayScaleEffect = opticalTargeter.GetComponentInChildren<Grayscale>();
            SetSensorMode(sensorMode);
            SetupLimitLine();
        }
        else
        {
            targetingCamera = null;
            targetIlluminator = null;
            cameraFog = null;
            grayScaleEffect = null;
        }
        UpdateLaserMarkerText();
    }

    private bool IsGimbalLimit()
    {
        if ((bool)opticalTargeter)
        {
            return opticalTargeter.isGimbalLimit;
        }
        return false;
    }

    private void LateUpdate()
    {
        if (!opticalTargeter)
        {
            if (powered)
            {
                TGPPwrButton();
            }
            return;
        }
        if ((bool)wm.battery && !wm.battery.Drain(0.01f * Time.deltaTime) && powered)
        {
            TGPPowerOff();
        }
        if (locked)
        {
            if (!opticalTargeter.lockedSky)
            {
                targetRange = Mathf.Round(Vector3.Distance(targetingCamera.transform.position, opticalTargeter.lockTransform.position));
                rangeObject.SetActive(value: true);
            }
            if (iffActive && (opticalTargeter.laserOccluded || !opticalTargeter.lockedActor))
            {
                UpdateIFF();
            }
            if (map.gameObject.activeInHierarchy)
            {
                map.ShowTGPIcon(opticalTargeter.lockTransform.position);
            }
        }
        else if (!remoteOnly)
        {
            if (tgpMode == TGPModes.FWD)
            {
                opticalTargeter.overriddenDirSmoothRate = 20f;
                opticalTargeter.OverrideAimToDirection(actor.transform.forward, actor.transform.up);
            }
            else if (tgpMode == TGPModes.PIP)
            {
                opticalTargeter.overriddenDirSmoothRate = 20f;
                if (wm.isMasterArmed && !wm.noArms && (bool)wm.currentEquip && wm.currentEquip.GetCount() > 0)
                {
                    if (wm.currentEquip is IGDSCompatible && (bool)hudInfo.gds && hudInfo.gds.gameObject.activeInHierarchy && hudInfo.gds.isTargetLocked)
                    {
                        opticalTargeter.OverrideAimToDirection(hudInfo.gds.gdsAimPoint - opticalTargeter.cameraTransform.position, actor.transform.up);
                    }
                    else
                    {
                        opticalTargeter.OverrideAimToDirection(wm.currentEquip.GetAimPoint() - opticalTargeter.cameraTransform.position, actor.transform.up);
                    }
                }
                else
                {
                    opticalTargeter.OverrideAimToDirection(actor.transform.forward, actor.transform.up);
                }
            }
            else if (tgpMode == TGPModes.HEAD)
            {
                if (targetingCamera.fieldOfView < 30f)
                {
                    opticalTargeter.overriddenDirSmoothRate = Mathf.Max(targetingCamera.fieldOfView, 2f);
                }
                else
                {
                    opticalTargeter.overriddenDirSmoothRate = 100f;
                }
                opticalTargeter.OverrideAimToDirection(VRHead.instance.transform.forward, VRHead.instance.transform.up);
                if (helmet.tgpDisplayEnabled)
                {
                    helmet.gimbalLimitObj.SetActive(IsGimbalLimit());
                }
                helmet.displayQuad.SetActive(helmet.tgpDisplayEnabled);
                headModeDisplayObj.SetActive(helmet.tgpDisplayEnabled);
                targetRT.SetActive(!helmet.tgpDisplayEnabled);
            }
        }
        if (IsGimbalLimit())
        {
            gimbalLimitObject.SetActive(value: true);
            borderTf.gameObject.SetActive(value: false);
            if (iffActive)
            {
                UpdateIFF();
            }
        }
        else
        {
            gimbalLimitObject.SetActive(value: false);
            borderTf.gameObject.SetActive(value: true);
        }
        if (!locked || opticalTargeter.lockedSky || slewing)
        {
            map.HideTGPIcon();
            rangeObject.SetActive(value: false);
        }
        UpdateDisplay();
        slewing = false;
        if (tgpMode != TGPModes.HEAD && tsButtonDownTime > 0f)
        {
            tsButtonDownTime += Time.deltaTime;
            if (tsButtonDownTime > 1f)
            {
                MFDHeadButton();
                tsButtonDownTime = -1f;
            }
        }
        if ((bool)limitPositionTf)
        {
            UpdateLimitPos();
        }
    }

    private void OnRenderCamera(Camera c)
    {
        if (tgpMode == TGPModes.HEAD && (bool)targetingCamera && targetingCamera.fieldOfView > 30f && !IsGimbalLimit() && !remoteOnly)
        {
            targetingCamera.transform.rotation = VRHead.instance.cam.transform.rotation;
        }
    }

    public void ToggleHelmetDisplay()
    {
        if (!started)
        {
            Setup();
        }
        helmet.ToggleDisplay();
        headModeDisplayObj.SetActive(helmet.tgpDisplayEnabled);
        targetRT.SetActive(!helmet.tgpDisplayEnabled);
        helmetDisplay = helmet.tgpDisplayEnabled;
    }

    public void ToggleHMDView()
    {
        if (!started)
        {
            Setup();
        }
        helmet.displayQuadParent.SetActive(!helmet.displayQuadParent.activeSelf);
        hmdView = helmet.displayQuadParent.activeSelf;
        if ((bool)mfdPage)
        {
            mfdPage.SetText("hmdViewStatus", s_hmd, helmet.displayQuadParent.activeSelf ? Color.green : Color.white);
        }
    }

    public void ToggleLaserMarker()
    {
        if (remoteOnly)
        {
            this.OnRemoteLaserToggleRequest?.Invoke();
        }
        else if ((bool)opticalTargeter)
        {
            opticalTargeter.visibleLaser = !opticalTargeter.visibleLaser;
            UpdateLaserMarkerText();
        }
    }

    public void UpdateLaserMarkerText()
    {
        bool flag = (bool)opticalTargeter && opticalTargeter.visibleLaser;
        if ((bool)portalPage)
        {
            portalPage.SetText("laserMarkerStatus", flag ? "ON" : "OFF", flag ? Color.green : Color.white);
        }
        if ((bool)mfdPage)
        {
            mfdPage.SetText("laserMarkerStatus", flag ? "ON" : "OFF", flag ? Color.green : Color.white);
        }
    }

    private void Slew(Vector2 direction)
    {
        if (!(Time.time - lockedTime < 0.25f))
        {
            StopAutoSlew();
            slewing = true;
            opticalTargeter.Slew(direction, slewRate * (fovs[fovIdx] / 60f));
            UpdateIFF();
        }
    }

    public void MFDHeadButton()
    {
        if (!powered)
        {
            return;
        }
        if (!started)
        {
            Setup();
        }
        if (NoControlInMP())
        {
            DisplayErrorMessage(s_tgp_notSOI);
        }
        else if (locked || tgpMode != TGPModes.HEAD)
        {
            opticalTargeter.Unlock();
            SetMode(TGPModes.HEAD);
            if ((bool)mfdPage && !mfdPage.isSOI)
            {
                mfdPage.ToggleInput();
            }
            if ((bool)portalPage && !portalPage.isSOI)
            {
                portalPage.ToggleInput();
            }
            helmet.lockTransform = opticalTargeter.lockTransform;
            if (!helmet.tgpDisplayEnabled)
            {
                ToggleHelmetDisplay();
            }
            PlayAudio(audio_headModeClip);
        }
        else
        {
            SetMode(TGPModes.TGT);
        }
    }

    public void DisableHeadMode()
    {
        if (tgpMode == TGPModes.HEAD)
        {
            if (helmet.tgpDisplayEnabled)
            {
                helmet.ToggleDisplay();
            }
            headModeDisplayObj.SetActive(value: false);
            targetRT.SetActive(value: true);
            helmetDisplay = false;
        }
    }

    public bool Lock(Vector3 origin, Vector3 direction)
    {
        if (!started)
        {
            Setup();
        }
        opticalTargeter.Lock(origin, direction);
        if (locked)
        {
            targetRT.SetActive(value: true);
            if (helmet.tgpDisplayEnabled)
            {
                ToggleHelmetDisplay();
            }
            targetingCamera.enabled = true;
            lockedTime = Time.time;
            helmet.lockTransform = opticalTargeter.lockTransform;
            rangeObject.SetActive(value: true);
            if ((bool)opticalTargeter.lockedActor)
            {
                PlayAudio(audio_targetLockClip);
            }
            else
            {
                PlayAudio(audio_areaLockClip);
            }
        }
        else
        {
            rangeObject.SetActive(value: false);
        }
        opticalTargeter.CheckOcclusion();
        UpdateIFF();
        return locked;
    }

    private void ForceLockActor(Actor a)
    {
        opticalTargeter.ForceLockActor(a);
        if (locked)
        {
            targetRT.SetActive(value: true);
            if (helmet.tgpDisplayEnabled)
            {
                ToggleHelmetDisplay();
            }
            targetingCamera.enabled = true;
            lockedTime = Time.time;
            helmet.lockTransform = opticalTargeter.lockTransform;
            rangeObject.SetActive(value: true);
            if ((bool)opticalTargeter.lockedActor)
            {
                PlayAudio(audio_targetLockClip);
            }
            else
            {
                PlayAudio(audio_areaLockClip);
            }
        }
        else
        {
            rangeObject.SetActive(value: false);
        }
        opticalTargeter.CheckOcclusion();
        UpdateIFF();
    }

    public void AreaLock(Vector3 position)
    {
        if (!started)
        {
            Setup();
        }
        opticalTargeter.AreaLockPosition(position);
        if (locked)
        {
            targetRT.SetActive(value: true);
            if (helmet.tgpDisplayEnabled)
            {
                ToggleHelmetDisplay();
            }
            targetingCamera.enabled = true;
            lockedTime = Time.time;
            helmet.lockTransform = opticalTargeter.lockTransform;
            rangeObject.SetActive(value: true);
            PlayAudio(audio_areaLockClip);
        }
        else
        {
            rangeObject.SetActive(value: false);
        }
        opticalTargeter.CheckOcclusion();
        UpdateIFF();
    }

    public void UpdateIFF()
    {
        foeObject.SetActive(value: false);
        friendObject.SetActive(value: false);
        iffActive = false;
        if ((bool)opticalTargeter && opticalTargeter.locked && (bool)wm && (bool)wm.actor && !opticalTargeter.laserOccluded && (bool)opticalTargeter.lockedActor)
        {
            iffActive = true;
            if (opticalTargeter.lockedActor.team == wm.actor.team)
            {
                friendObject.SetActive(value: true);
            }
            else
            {
                foeObject.SetActive(value: true);
            }
        }
    }

    private void TGPPowerOn()
    {
        if ((bool)targetingCamera)
        {
            powered = true;
            targetingCamera.enabled = true;
            targetRT.SetActive(value: true);
        }
        if ((bool)opticalTargeter)
        {
            UpdateIFF();
        }
        UpdateLaserMarkerText();
    }

    private void TGPPowerOff()
    {
        SetMode(TGPModes.FWD);
        if ((bool)opticalTargeter)
        {
            opticalTargeter.Unlock();
        }
        targetRT.SetActive(value: false);
        if (helmet.tgpDisplayEnabled)
        {
            ToggleHelmetDisplay();
        }
        helmet.displayQuad.SetActive(value: false);
        helmet.gimbalLimitObj.SetActive(value: false);
        helmet.lockTransform = null;
        rangeObject.SetActive(value: false);
        gimbalLimitObject.SetActive(value: false);
        powered = false;
        if ((bool)opticalTargeter)
        {
            UpdateIFF();
        }
        UpdateLaserMarkerText();
    }

    public void TGPPwrButton()
    {
        if (!started)
        {
            Setup();
        }
        if (powered)
        {
            TGPPowerOff();
        }
        else
        {
            TGPPowerOn();
            SetMode(TGPModes.FWD, showMulticrewError: false);
        }
        this.OnTGPPwrButton?.Invoke(powered);
    }

    public void RemoteSetPower(bool p)
    {
        if (p && !powered)
        {
            TGPPowerOn();
        }
        else if (!p && powered)
        {
            TGPPowerOff();
        }
    }

    public void OpenPage()
    {
        if (!started)
        {
            Setup();
        }
        if ((bool)targetingCamera)
        {
            targetingCamera.enabled = true;
            SetMode(tgpMode, showMulticrewError: false);
            CurrentCameraEvents.OnCameraPreCull -= OnRenderCamera;
            CurrentCameraEvents.OnCameraPreCull += OnRenderCamera;
        }
    }

    public void CloseOut()
    {
        if ((bool)targetingCamera)
        {
            targetingCamera.enabled = false;
        }
        CurrentCameraEvents.OnCameraPreCull -= OnRenderCamera;
        if (helmet.tgpDisplayEnabled)
        {
            ToggleHelmetDisplay();
        }
        if (tgpMode == TGPModes.HEAD)
        {
            MFDHeadButton();
        }
    }

    private void OnDestroy()
    {
        CurrentCameraEvents.OnCameraPreCull -= OnRenderCamera;
    }

    public void OnThumbstickDown()
    {
        if (!base.gameObject.activeInHierarchy || !powered)
        {
            return;
        }
        if (NoControlInMP())
        {
            DisplayErrorMessage(s_tgp_notSOI);
            return;
        }
        if (tgpMode != TGPModes.HEAD)
        {
            tsButtonDownTime = 0.01f;
        }
        if (locked)
        {
            SetMode(TGPModes.TGT);
        }
        else if (tgpMode == TGPModes.PIP || tgpMode == TGPModes.FWD)
        {
            SetMode(TGPModes.TGT);
        }
        else if (tgpMode == TGPModes.HEAD)
        {
            MFDHeadButton();
        }
    }

    public void OnThumbstickUp()
    {
        tsButtonDownTime = -1f;
    }

    public void OnSetThumbstick(Vector3 axes)
    {
        if (!base.gameObject.activeInHierarchy || !powered)
        {
            return;
        }
        if (NoControlInMP())
        {
            DisplayErrorMessage(s_tgp_notSOI);
            return;
        }
        if (locked)
        {
            Slew(new Vector2(axes.x, axes.y));
        }
        else if (tgpMode == TGPModes.HEAD)
        {
            if (hasResetThumbStick)
            {
                if (axes.y > 0f)
                {
                    ZoomIn();
                }
                else
                {
                    ZoomOut();
                }
            }
        }
        else
        {
            SetMode(TGPModes.TGT);
        }
        hasResetThumbStick = false;
    }

    public void OnResetThumbstick()
    {
        hasResetThumbStick = true;
        if (!remoteOnly && locked)
        {
            opticalTargeter.StopEofSlew();
            opticalTargeter.Unlock();
            opticalTargeter.Lock(opticalTargeter.cameraTransform.position, opticalTargeter.lockTransform.position - opticalTargeter.cameraTransform.position);
            if ((bool)opticalTargeter.lockedActor)
            {
                PlayAudio(audio_targetLockClip);
            }
            else if (opticalTargeter.locked)
            {
                PlayAudio(audio_areaLockClip);
            }
            SetMode(TGPModes.TGT);
        }
    }

    public void ZoomIn()
    {
        if (!powered)
        {
            return;
        }
        if (NoControlInMP())
        {
            DisplayErrorMessage(s_tgp_notSOI);
            return;
        }
        if (fovIdx < fovs.Length - 1)
        {
            fovIdx++;
            PlayAudio(audio_zoomInClip);
        }
        if (!lerpZoom)
        {
            targetingCamera.fieldOfView = fovs[fovIdx];
        }
        OnSetFovIdx?.Invoke(fovIdx);
    }

    public void ZoomOut()
    {
        if (!powered)
        {
            return;
        }
        if (NoControlInMP())
        {
            DisplayErrorMessage(s_tgp_notSOI);
            return;
        }
        if (fovIdx > 0)
        {
            fovIdx--;
            PlayAudio(audio_zoomOutClip);
        }
        if (fovIdx < 0)
        {
            fovIdx = 0;
        }
        if (!lerpZoom)
        {
            targetingCamera.fieldOfView = fovs[fovIdx];
        }
        OnSetFovIdx?.Invoke(fovIdx);
    }

    public void RemoteSetFovIdx(int idx)
    {
        if (fovIdx != idx)
        {
            if (idx > fovIdx)
            {
                PlayAudio(audio_zoomInClip);
            }
            else
            {
                PlayAudio(audio_zoomOutClip);
            }
            fovIdx = idx;
            if (!lerpZoom)
            {
                targetingCamera.fieldOfView = fovs[fovIdx];
            }
        }
    }

    public void PointForward()
    {
        SetMode(TGPModes.FWD);
    }

    public void ToggleMode()
    {
        if (tgpMode == TGPModes.TGT && wm.isMasterArmed && !wm.noArms)
        {
            SetMode(TGPModes.PIP);
        }
        else
        {
            SetMode(TGPModes.TGT);
        }
    }

    public void ToWaypoint()
    {
        if (!started)
        {
            Setup();
        }
        if (NoControlInMP())
        {
            DisplayErrorMessage(s_tgp_notSOI);
        }
        else if ((bool)WaypointManager.instance.currentWaypoint && powered)
        {
            SlewAndLockPosition(WaypointManager.instance.currentWaypoint.TransformPoint(WaypointManager.instance.waypointLocalOffset), 360f);
        }
    }

    private void UpdateModeText()
    {
        if ((bool)mfdPage)
        {
            mfdPage.SetText("tgpMode", tgpModeLabels[(int)tgpMode]);
        }
        else if ((bool)portalPage)
        {
            portalPage.SetText("tgpMode", tgpModeLabels[(int)tgpMode]);
        }
    }

    private void SetMode(TGPModes mode, bool showMulticrewError = true)
    {
        if (!powered)
        {
            return;
        }
        if (NoControlInMP())
        {
            if (showMulticrewError)
            {
                DisplayErrorMessage(s_tgp_notSOI);
            }
            return;
        }
        if (tgpMode != mode)
        {
            tgpMode = mode;
            this.OnSetMode?.Invoke(mode);
        }
        StopAutoSlew();
        UpdateModeText();
        if ((bool)opticalTargeter)
        {
            if (mode == TGPModes.TGT)
            {
                if (!opticalTargeter.locked)
                {
                    Lock(opticalTargeter.cameraTransform.position, opticalTargeter.cameraTransform.forward);
                }
            }
            else
            {
                opticalTargeter.Unlock();
            }
        }
        if (helmet.tgpDisplayEnabled != (mode == TGPModes.HEAD))
        {
            ToggleHelmetDisplay();
        }
        if ((bool)opticalTargeter)
        {
            opticalTargeter.CheckOcclusion();
        }
        UpdateIFF();
    }

    public void RemoteSetMode(TGPModes mode)
    {
        if (!remoteOnly)
        {
            Debug.LogError("RemoteSetMode called on TGP when not remoteOnly!");
            return;
        }
        tgpMode = mode;
        UpdateModeText();
        UpdateIFF();
    }

    public void SlewAndLockPosition(Vector3 worldPosition, float slewRate)
    {
        if (!started)
        {
            Setup();
        }
        SetMode(TGPModes.TGT);
        opticalTargeter.Unlock();
        StopAutoSlew();
        autoSlewRoutine = StartCoroutine(AutoSlewRoutine(worldPosition, slewRate));
    }

    public void SlewAndLockActor(Actor a, float slewRate)
    {
        if (!started)
        {
            Setup();
        }
        SetMode(TGPModes.TGT);
        opticalTargeter.Unlock();
        StopAutoSlew();
        autoSlewRoutine = StartCoroutine(AutoSlewActorRoutine(a, slewRate));
    }

    private IEnumerator AutoSlewRoutine(Vector3 worldPosition, float slewRate)
    {
        FixedPoint tgtPoint = new FixedPoint(worldPosition);
        Vector3 fwd = opticalTargeter.cameraTransform.forward;
        while (Vector3.Angle(opticalTargeter.cameraTransform.forward, tgtPoint.point - opticalTargeter.cameraTransform.position) > 0.5f)
        {
            Vector3 normalized = (tgtPoint.point - opticalTargeter.cameraTransform.position).normalized;
            fwd = Vector3.RotateTowards(fwd, normalized, slewRate * ((float)Math.PI / 180f) * Time.deltaTime, 0f);
            opticalTargeter.overriddenDirSmoothRate = 10f;
            opticalTargeter.OverrideAimToDirection(fwd, Vector3.up);
            if (IsGimbalLimit())
            {
                opticalTargeter.AreaLockPosition(tgtPoint.point);
                yield break;
            }
            slewing = true;
            yield return null;
        }
        if (Vector3.Distance(tgtPoint.point, opticalTargeter.cameraTransform.position) > opticalTargeter.maxLockingDistance)
        {
            AreaLock(tgtPoint.point);
            yield break;
        }
        Lock(opticalTargeter.cameraTransform.position, tgtPoint.point - opticalTargeter.cameraTransform.position);
        if (!opticalTargeter.lockedActor && Vector3.Distance(opticalTargeter.lockTransform.position, tgtPoint.point) > 10f)
        {
            AreaLock(tgtPoint.point);
        }
    }

    private IEnumerator AutoSlewActorRoutine(Actor a, float slewRate)
    {
        FixedPoint fallbackPoint = new FixedPoint(a.position);
        Vector3 fwd = opticalTargeter.cameraTransform.forward;
        while ((bool)a && Vector3.Angle(opticalTargeter.cameraTransform.forward, a.position - opticalTargeter.cameraTransform.position) > 0.5f)
        {
            Vector3 normalized = (a.position - opticalTargeter.cameraTransform.position).normalized;
            fwd = Vector3.RotateTowards(fwd, normalized, slewRate * ((float)Math.PI / 180f) * Time.deltaTime, 0f);
            opticalTargeter.overriddenDirSmoothRate = 10f;
            opticalTargeter.OverrideAimToDirection(fwd, Vector3.up);
            if (IsGimbalLimit())
            {
                opticalTargeter.AreaLockPosition(a.position);
                yield break;
            }
            slewing = true;
            fallbackPoint.point = a.position;
            yield return null;
        }
        if (!a)
        {
            AreaLock(fallbackPoint.point);
        }
        else if (Vector3.Distance(a.position, opticalTargeter.cameraTransform.position) > opticalTargeter.maxLockingDistance || !TargetManager.instance.CheckTargetVisibility(actor.team, a, opticalTargeter.maxLockingDistance, 50f, opticalTargeter.cameraTransform.position, teamCheck: false, -1f, hitboxOccluded: false))
        {
            AreaLock(a.position);
        }
        else
        {
            ForceLockActor(a);
        }
    }

    public void StopAutoSlew()
    {
        if (autoSlewRoutine != null)
        {
            StopCoroutine(autoSlewRoutine);
        }
    }

    private void UpdateDisplay()
    {
        if (rangeObject.activeInHierarchy)
        {
            rangeText.text = measurements.FormattedDistance(targetRange);
        }
        UpdateBearing();
        lockDisplayObject.SetActive(value: false);
        actorLockDisplayObject.SetActive(value: false);
        if (lerpZoom)
        {
            targetingCamera.fieldOfView = Mathf.Lerp(targetingCamera.fieldOfView, fovs[fovIdx], zoomLerpRate * Time.deltaTime);
        }
        opticalTargeter.lockingFOV = Mathf.Min(10f, targetingCamera.fieldOfView / 6f);
        float num;
        if ((bool)opticalTargeter.lockedActor)
        {
            num = 10f;
            actorLockDisplayObject.SetActive(value: true);
        }
        else
        {
            if (opticalTargeter.locked && !opticalTargeter.lockedSky && !slewing && !IsGimbalLimit())
            {
                lockDisplayObject.SetActive(value: true);
            }
            num = 100f * opticalTargeter.lockingFOV / targetingCamera.fieldOfView;
        }
        num /= borderTf.localScale.x;
        if (borderTf.gameObject.activeSelf)
        {
            lerpedBorderSize = Mathf.Lerp(lerpedBorderSize, num, 10f * Time.deltaTime);
            borderTf.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, lerpedBorderSize);
            borderTf.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, lerpedBorderSize);
        }
        rollTf.localEulerAngles = new Vector3(0f, 0f, 0f - flightInfo.roll);
        pitchTf.localPosition = new Vector3(0f, 10f * (0f - flightInfo.pitch) / 90f, 0f);
    }

    private void UpdateBearing()
    {
        if (opticalTargeter.locked)
        {
            bearingRotator.gameObject.SetActive(value: true);
            Vector3 toDirection = opticalTargeter.lockTransform.position - actor.position;
            _ = actor.position;
            toDirection.y = 0f;
            Vector3 forward = actor.transform.forward;
            forward.y = 0f;
            float num = VectorUtils.SignedAngle(forward, toDirection, Vector3.Cross(Vector3.up, forward));
            bearingRotator.localEulerAngles = new Vector3(0f, 0f, 0f - num);
        }
        else
        {
            bearingRotator.gameObject.SetActive(value: false);
        }
    }

    public void SendGPSTarget()
    {
        if (!started)
        {
            Setup();
        }
        if (!opticalTargeter)
        {
            return;
        }
        if (opticalTargeter.locked)
        {
            if (gpsSystem.noGroups)
            {
                wm.gpsSystem.CreateCustomGroup();
            }
            if ((bool)muvs && VTOLMPUtils.IsMultiplayer() && !muvs.isMine)
            {
                muvs.RemoteGPS_AddTarget("TGP", VTMapManager.WorldToGlobalPoint(opticalTargeter.lockTransform.position));
            }
            else
            {
                gpsSystem.AddTarget(opticalTargeter.lockTransform.position, "TGP");
            }
        }
        else
        {
            DisplayErrorMessage(s_tgp_noLock);
        }
    }

    private bool NoControlInMP()
    {
        if (!remoteOnly)
        {
            if (VTOLMPUtils.IsMultiplayer() && (bool)muvs)
            {
                return !muvs.IsLocalTGPControl();
            }
            return false;
        }
        return true;
    }

    public void ToGPSTarget()
    {
        if (!started)
        {
            Setup();
        }
        if (NoControlInMP())
        {
            DisplayErrorMessage(s_tgp_notSOI);
        }
        else if ((bool)opticalTargeter)
        {
            if (gpsSystem.noGroups)
            {
                DisplayErrorMessage(s_tgp_noGpsGroup);
            }
            else if (gpsSystem.currentGroup.targets.Count == 0)
            {
                DisplayErrorMessage(s_tgp_noTarget);
            }
            else
            {
                SlewAndLockPosition(gpsSystem.currentGroup.currentTarget.worldPosition, 360f);
            }
        }
    }

    private void DisplayErrorMessage(string message)
    {
        if ((bool)errorFlasher)
        {
            errorFlasher.DisplayError(message, 1.5f);
        }
        else if ((bool)errorText)
        {
            errorText.text = message;
            if (errorRoutine != null)
            {
                StopCoroutine(errorRoutine);
            }
            errorRoutine = StartCoroutine(ErrorRoutine());
        }
    }

    private IEnumerator ErrorRoutine()
    {
        for (int i = 0; i < 5; i++)
        {
            errorObject.SetActive(value: true);
            yield return new WaitForSeconds(0.2f);
            errorObject.SetActive(value: false);
            yield return new WaitForSeconds(0.1f);
        }
    }

    public void OnQuicksave(ConfigNode qsNode)
    {
        if ((bool)opticalTargeter)
        {
            ConfigNode configNode = new ConfigNode(qsNodeName);
            configNode.SetValue("powered", powered);
            configNode.SetValue("tgpMode", tgpMode);
            configNode.SetValue("lockGlobalPos", VTMapManager.WorldToGlobalPoint(opticalTargeter.lockTransform.position));
            configNode.AddNode(QuicksaveManager.SaveActorIdentifierToNode(opticalTargeter.lockedActor, "lockedActor"));
            configNode.SetValue("fovIdx", fovIdx);
            configNode.SetValue("sensorMode", sensorMode);
            configNode.SetValue("hmdView", hmdView);
            qsNode.AddNode(configNode);
        }
    }

    public void OnQuickload(ConfigNode qsNode)
    {
        if (qsNode.HasNode(qsNodeName))
        {
            myQNode = qsNode;
            QuicksaveManager.instance.OnQuickloadedMissiles += QuickloadAfterMissiles;
        }
    }

    private void QuickloadAfterMissiles(ConfigNode dummy)
    {
        QuicksaveManager.instance.OnQuickloadedMissiles -= QuickloadAfterMissiles;
        ConfigNode configNode = myQNode;
        Debug.Log("TargetingMFDPage OnQuickload");
        ConfigNode node = configNode.GetNode(qsNodeName);
        if (node.GetValue<bool>("powered"))
        {
            TGPPowerOn();
        }
        TGPModes value = node.GetValue<TGPModes>("tgpMode");
        SetMode(value);
        if (value == TGPModes.TGT)
        {
            Vector3 position = VTMapManager.GlobalToWorldPoint(node.GetValue<Vector3D>("lockGlobalPos"));
            Actor actor = QuicksaveManager.RetrieveActorFromNode(node.GetNode("lockedActor"));
            if ((bool)actor)
            {
                ForceLockActor(actor);
            }
            if (!actor || !locked)
            {
                AreaLock(position);
            }
        }
        fovIdx = node.GetValue<int>("fovIdx");
        targetingCamera.fieldOfView = fovs[fovIdx];
        bool value2 = node.GetValue<bool>("hmdView");
        if (!value2)
        {
            ToggleHMDView();
        }
        hmdView = value2;
        SetSensorMode(node.GetValue<SensorModes>("sensorMode"));
    }

    public void ToggleLimLineDisplay()
    {
        limLineVisible = !limLineVisible;
        UpdateLimLineVisibility();
    }

    private void UpdateLimLineVisibility()
    {
        limitLineDisplayObj.SetActive(limLineVisible);
    }

    private void SetupLimitLine()
    {
        if ((bool)opticalTargeter)
        {
            _ = opticalTargeter.sensorTurret;
            int num = lineLimitVertCount;
            Vector2[] array = new Vector2[num + 1];
            for (int i = 0; i <= num; i++)
            {
                float num2 = (float)i / (float)num * 360f;
                float vesselRelativePitchGimbalLimit = GetVesselRelativePitchGimbalLimit(num2);
                float num3 = Mathf.InverseLerp(-90f, vesselRelMaxPitch, vesselRelativePitchGimbalLimit);
                array[i] = Quaternion.AngleAxis(num2, Vector3.back) * new Vector2(0f, num3 * limitLineScale);
            }
            limitLineRenderer.Points = array;
        }
    }

    private Vector2 RadialLimitPosition(Vector3 direction, float scale)
    {
        _ = opticalTargeter.sensorTurret;
        Vector3 vector = actor.transform.InverseTransformDirection(direction);
        Vector3 vector2 = vector;
        vector2.y = 0f;
        float num = VectorUtils.SignedAngle(Vector3.forward, vector2, Vector3.right);
        if (num < 0f)
        {
            num += 360f;
        }
        float value = VectorUtils.SignedAngle(vector2, vector, Vector3.up);
        float num2 = Mathf.InverseLerp(-90f, vesselRelMaxPitch, value);
        return Quaternion.AngleAxis(num, Vector3.back) * new Vector2(0f, num2 * scale);
    }

    private float GetVesselRelativePitchGimbalLimit(float vesselYawAngle)
    {
        Vector3 vector = Quaternion.AngleAxis(vesselYawAngle, Vector3.up) * Vector3.forward;
        Vector3 axis = -Vector3.Cross(Vector3.up, vector);
        ModuleTurret sensorTurret = opticalTargeter.sensorTurret;
        float result = -90f;
        float num = -80f;
        bool flag = false;
        while (!flag)
        {
            Vector3 direction = Quaternion.AngleAxis(num, axis) * vector;
            Vector3 vector2 = sensorTurret.yawTransform.parent.InverseTransformDirection(actor.transform.TransformDirection(direction));
            Vector3 vector3 = vector2;
            vector3.y = 0f;
            float num2 = VectorUtils.SignedAngle(Vector3.forward, vector3, Vector3.right);
            if (num2 < 0f)
            {
                num2 += 360f;
            }
            float num3 = (sensorTurret.useMinPitchCurve ? sensorTurret.minPitchCurve.Evaluate(num2) : sensorTurret.minPitch);
            float num4 = (sensorTurret.useMaxPitchCurve ? sensorTurret.maxPitchCurve.Evaluate(num2) : sensorTurret.maxPitch);
            float num5 = VectorUtils.SignedAngle(vector3, vector2, Vector3.up);
            Vector3 direction2 = Quaternion.AngleAxis(Mathf.Clamp(num5, num3, num4), -Vector3.Cross(Vector3.up, vector3)) * vector3;
            Vector3 direction3 = sensorTurret.yawTransform.parent.TransformDirection(direction2);
            Vector3 vector4 = actor.transform.InverseTransformDirection(direction3);
            Vector3 fromDirection = vector4;
            fromDirection.y = 0f;
            result = VectorUtils.SignedAngle(fromDirection, vector4, Vector3.up);
            if (num5 < num3 || num5 > num4)
            {
                flag = true;
                continue;
            }
            num += 5f;
            if (!(num > 90f))
            {
                continue;
            }
            return 90f;
        }
        return result;
    }

    private void UpdateLimitPos()
    {
        limitPositionTf.localPosition = RadialLimitPosition(opticalTargeter.sensorTurret.pitchTransform.forward, limitLineScale);
    }

    public void OnSaveVehicleData(ConfigNode vDataNode)
    {
        vDataNode.AddOrGetNode("TargetingMFDPage").SetValue("limLineVisible", limLineVisible);
    }

    public void OnLoadVehicleData(ConfigNode vDataNode)
    {
        ConfigNode node = vDataNode.GetNode("TargetingMFDPage");
        if (node != null)
        {
            limLineVisible = ConfigNodeUtils.TryParseValue(node, "limLineVisible", ref limLineVisible);
            UpdateLimLineVisibility();
        }
    }
}