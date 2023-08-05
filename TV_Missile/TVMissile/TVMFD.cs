using System;
using System.Collections;
using System.Collections.Generic;
using OC;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.UI.Extensions;
using UnityStandardAssets.ImageEffects;
using VTOLVR.Multiplayer;

public class TVMFDPage : MonoBehaviour, IQSVehicleComponent, IPersistentVehicleData, ILocalizationUser
{
    public enum SensorModes
    {
        DAY = 0,
        NIGHT = 1,
        COLOR = 2
    }

    // TODO: replace with headActive
    public enum TGPModes
    {
        HEAD = 0,
        FWD = 1
    }

    public HelmetController helmet;

    public GameObject targetRT;

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

    private float lockedTime;

    public Text sensorModeText;

    public Text rangeText;

    public GameObject rangeObject;

    public GameObject lockDisplayObject;

    public GameObject actorLockDisplayObject;

    public FlightInfo flightInfo;

    public Transform rollTf;

    public Transform pitchTf;

    public GameObject headModeDisplayObj;

    public GameObject friendObject;

    public GameObject foeObject;

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

    private string s_hmd;

    private string s_tgp_notSOI = "NOT SOI";

    private bool started = false;

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

    private List<Camera> TV_Cameras = new List<Camera>();
    private int activeCamera = 0;

    // TODO: Rename to CurActiveTVCamera or something
    public Camera TVCamera;

    public int fovIdx { get; private set; }

    public bool remoteOnly { get; set; }

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
            return _switchedOn;
        }
        set
        {
            _switchedOn = value;
        }
    }

    private string qsNodeName => base.gameObject.name + "_TVMFDPage";

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
        // TODO: Maybe implement
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
        //SetSensorMode(SensorModes.DAY);
        //targetRT.SetActive(value: false);
        //headModeDisplayObj.SetActive(value: false);
//        errorObject.SetActive(value: false);
        //rangeObject.SetActive(value: false);
        Debug.Log("TVMFD::Awake");
    }


    private void Setup()
    {
        if (started)
        {
            Debug.Log("TVMFD::Setup: early exit");
            return;
        }
        Debug.Log("TVMFD::Setup: doing setup");

        if (!wm)
        {
            Debug.Log("TVMFD::Setup: finding wm");
            wm = GetComponentInParent<WeaponManager>();
            if(!wm)
            {
                Debug.LogError("TVMFD::Setup: Didn't find wm");
            }
        }
        if(!targetRT)
        {
            Debug.Log("TVMFD::Setup: finding targetRT");
            targetRT = transform.Find("TV_MFD_RT").gameObject;
        }
        if (!TVCamera)
        {
            Debug.Log("TVMFD::Setup: finding cameras");
            FindTVCameras();
        }
        var t = transform;
        transform.localScale = new Vector3( 1, 1, 1);
        transform.localRotation = Quaternion.identity;
        transform.localPosition = new Vector3( 0, 0, 0);
        Debug.Log($"TVMFD::Setup: positioning {t.localPosition}, {t.localRotation}, {t.localScale}");
        started = true;
        if ((bool)wm.opticalTargeter)
        {
            if ((bool)TVCamera)
            {
                // TODO: Replace
                // LODManager.instance.tcam = TVCamera;
            }
        }
        if ((bool)mfdPage)
        {
            mfdPage.SetText("tgpMode", tgpModeLabels[(int)tgpMode]);
        }
        else if ((bool)portalPage)
        {
            portalPage.SetText("tgpMode", tgpModeLabels[(int)tgpMode]);
        }
        //UpdateLimLineVisibility();
    }
    void FindTVCameras()
    {
        Debug.Log($"TVMFD::FindTVCameras(): wm.equipCount: {wm.equipCount} ");
        for(int i = 0; i < wm.equipCount; i++)
        {
            var wep = wm.GetEquip(i);
            if((bool)wep)
            {
                // TODO: Enforce this is a tv missile
                Debug.Log($"Checking weapon {i}: {wep.name} for camera. active: {wep.gameObject.activeSelf },  activeInHierarchy {wep.gameObject.activeInHierarchy }");

                Camera camera = wep.GetComponentInChildren<Camera>();
                if(camera )
                {
                    Debug.Log("Found Camera on weapon " + i);
                    if(wep.name == "fa26_tv161")
                        TV_Cameras.Add(camera);
                }
            }
        }

        if(TV_Cameras.Count > 0) {
            Debug.Log("TVMFD::FindTVCameras: Found camera, enabling");
            TVCamera = TV_Cameras[0];
            TVCamera.enabled = true;
            if(!targetRT)
            {
                Debug.LogError("TVMFD::FindTVCameras: targetRT not set");
            }
            if(!TVCamera.targetTexture)
            {
                Debug.LogError("TVMFD::FindTVCameras: TVCamera.targetTexture");
            }

            targetRT.SetActive(true);
        } else {
            Debug.Log("TVMFD::FindTVCameras: no cameras, disabling targetRT");
            targetRT.SetActive(false);
        }
    }

    private void OnDeactivatePage()
    {
        //errorObject.SetActive(value: false);
        if((bool)TVCamera)
        {
            TVCamera.enabled = false;
            targetRT.SetActive(false);
        }
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
    }

    private void SetSensorMode(SensorModes sensorMode, bool sendEvent = true)
    {
        this.sensorMode = sensorMode;
        if ((bool)TVCamera)
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
        if ((bool)TVCamera)
        {
            MeshRenderer component = helmet.displayQuad.GetComponent<MeshRenderer>();
            if ((bool)component)
            {
                MaterialPropertyBlock materialPropertyBlock = new MaterialPropertyBlock();
                materialPropertyBlock.SetTexture("_MainTex", TVCamera.targetTexture);
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

    // TODO: not sure if we will use this
    public void SetTVCamera(Camera t)
    {
        if (!started)
        {
            Setup();
        }
        TVCamera = t;
        if((bool)TVCamera)
        {
            TVCamera.fieldOfView = fovs[fovIdx];
            // TODO: 
            // LODManager.instance.tcam = TVCamera;
            if ((bool)helmet)
            {
                MeshRenderer component = helmet.displayQuad.GetComponent<MeshRenderer>();
                if ((bool)component)
                {
                    MaterialPropertyBlock materialPropertyBlock = new MaterialPropertyBlock();
                    materialPropertyBlock.SetTexture("_MainTex", TVCamera.targetTexture);
                    component.SetPropertyBlock(materialPropertyBlock);
                }
            }
            if (VTResources.useOverCloud && !TVCamera.gameObject.GetComponent<OverCloudCamera>())
            {
                OverCloudCamera overCloudCamera = TVCamera.gameObject.AddComponent<OverCloudCamera>();
                overCloudCamera.lightSampleCount = SampleCount.Low;
                overCloudCamera.scatteringMaskSamples = SampleCount.Low;
                overCloudCamera.includeCascadedShadows = false;
                overCloudCamera.renderScatteringMask = false;
                overCloudCamera.renderVolumetricClouds = false;
                overCloudCamera.downsample2DClouds = true;
            }
            // TODO: Think about if we want these
            //targetIlluminator = opticalTargeter.GetComponentInChildren<IlluminateVesselsOnRender>();
            //cameraFog = opticalTargeter.GetComponentInChildren<CameraFogSettings>();
            //grayScaleEffect = opticalTargeter.GetComponentInChildren<Grayscale>();
            SetSensorMode(sensorMode);
        }
        else
        {
            targetIlluminator = null;
            cameraFog = null;
            grayScaleEffect = null;
        }
    }

    private void LateUpdate()
    {
        if ((bool)wm.battery && !wm.battery.Drain(0.01f * Time.deltaTime) && powered)
        {
            //TGPPowerOff();
        }
        //UpdateDisplay();
        if (tgpMode != TGPModes.HEAD && tsButtonDownTime > 0f)
        {
            tsButtonDownTime += Time.deltaTime;
            if (tsButtonDownTime > 1f)
            {
                MFDHeadButton();
                tsButtonDownTime = -1f;
            }
        }
    }

    private void OnRenderCamera(Camera c)
    {
        // TODO: We probably don't want this
        //if (tgpMode == TGPModes.HEAD && (bool)TVCamera && TVCamera.fieldOfView > 30f && !IsGimbalLimit() && !remoteOnly)
        {
            //TVCamera.transform.rotation = VRHead.instance.cam.transform.rotation;
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

        // TODO: Maybe we gotta do something here
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

    private void TGPPowerOn()
    {
        if ((bool)TVCamera)
        {
            powered = true;
            TVCamera.enabled = true;
            targetRT.SetActive(value: true);
        }
    }

    private void TGPPowerOff()
    {
        targetRT.SetActive(value: false);
        if (helmet.tgpDisplayEnabled)
        {
            ToggleHelmetDisplay();
        }
        helmet.displayQuad.SetActive(value: false);
        helmet.gimbalLimitObj.SetActive(value: false);
        helmet.lockTransform = null;
        rangeObject.SetActive(value: false);
        powered = false;
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
        Debug.Log("TVMFD::OpenPage");
        if (!started)
        {
            Debug.Log("TVMFD::OpenPage: Setup()");
            Setup();
        }
        if ((bool)TVCamera)
        {
            Debug.Log("TVMFD::OpenPage: Activating Camera");
            TVCamera.enabled = true;
            targetRT.SetActive(true);
            CurrentCameraEvents.OnCameraPreCull -= OnRenderCamera;
            CurrentCameraEvents.OnCameraPreCull += OnRenderCamera;
        }
    }

    public void CloseOut()
    {
        if ((bool)TVCamera)
        {
            TVCamera.enabled = false;
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
        hasResetThumbStick = false;
    }

    public void OnResetThumbstick()
    {
        hasResetThumbStick = true;
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
            TVCamera.fieldOfView = fovs[fovIdx];
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
            TVCamera.fieldOfView = fovs[fovIdx];
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
                TVCamera.fieldOfView = fovs[fovIdx];
            }
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

    private void UpdateDisplay()
    {
        if (rangeObject.activeInHierarchy)
        {
            rangeText.text = measurements.FormattedDistance(targetRange);
        }
        actorLockDisplayObject.SetActive(value: false);
        if (lerpZoom)
        {
            TVCamera.fieldOfView = Mathf.Lerp(TVCamera.fieldOfView, fovs[fovIdx], zoomLerpRate * Time.deltaTime);
        }
        // TODO: Think about what this is doing
        float num = 1;
        if (borderTf.gameObject.activeSelf)
        {
            lerpedBorderSize = Mathf.Lerp(lerpedBorderSize, num, 10f * Time.deltaTime);
            borderTf.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, lerpedBorderSize);
            borderTf.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, lerpedBorderSize);
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

    // TODO: Maybe think about saving active missiles
    public void OnQuicksave(ConfigNode qsNode)
    {
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
        fovIdx = node.GetValue<int>("fovIdx");
        TVCamera.fieldOfView = fovs[fovIdx];
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

    // TODO: Think about this
    public void OnSaveVehicleData(ConfigNode vDataNode)
    {
    }

    public void OnLoadVehicleData(ConfigNode vDataNode)
    {
        ConfigNode node = vDataNode.GetNode("TVMFDPage");
    }
}