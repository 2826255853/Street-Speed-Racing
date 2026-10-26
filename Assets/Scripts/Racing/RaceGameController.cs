using UnityEngine;
using UnityEngine.InputSystem;

public class RaceGameController : MonoBehaviour
{
    private static readonly Vector3 PlayerCameraOffset = new Vector3(3.5f, 2f, 3.5f);
    private static readonly Vector3 PlayerCameraLookOffset = new Vector3(0f, 0.85f, 1.9f);

    public RaceConfig config = new RaceConfig();
    public VehicleSpec playerVehicle = new VehicleSpec { displayName = "玩家车辆" };
    public VehicleSpec opponentVehicle = new VehicleSpec { displayName = "电脑车辆", peakTorque = 415f };

    [SerializeField] private RacePhase phase = RacePhase.Preparing;
    [SerializeField] private float countdownRemaining;
    [SerializeField] private float raceTime;

    private RaceCarState playerState;
    private RaceCarState opponentState;
    private RaceOpponentController opponentController;
    private RaceCarVisual playerVisual;
    private RaceCarVisual opponentVisual;
    private RaceHud hud;
    private Camera raceCamera;
    private float simulationAccumulator;
    private bool pendingShift;
    private bool pendingDownshift;
    private bool pendingNitrous;
    private bool startRequested;
    private bool initialized;
    private bool touchLaunchHeld;
    private bool hasInputFocus = true;
    private string result = string.Empty;

    public RacePhase Phase { get { return phase; } }
    public RaceCarState PlayerState { get { return playerState; } }
    public RaceCarState OpponentState { get { return opponentState; } }
    public RaceHud Hud { get { return hud; } }
    public bool TouchLaunchHeld { get { return touchLaunchHeld; } }

    private void Start()
    {
        Initialize();
        ResetRace();
    }

    private void Update()
    {
        if (!initialized)
        {
            Initialize();
        }

        Keyboard keyboard = Keyboard.current;
        bool launchHeld = hasInputFocus && (touchLaunchHeld || (keyboard != null && keyboard.spaceKey.isPressed));
        if (keyboard != null && hasInputFocus)
        {
            if (phase == RacePhase.Racing)
            {
                if (keyboard.eKey.wasPressedThisFrame) RequestUpshift();
                if (keyboard.qKey.wasPressedThisFrame) RequestDownshift();
                if (keyboard.leftShiftKey.wasPressedThisFrame) RequestNitrous();
            }
            if (phase == RacePhase.Preparing && keyboard.enterKey.wasPressedThisFrame)
            {
                RequestStart();
            }
            if (phase == RacePhase.Finished && keyboard.rKey.wasPressedThisFrame)
            {
                ResetRace();
            }
        }

        simulationAccumulator += Mathf.Min(Time.deltaTime, 0.1f);
        int ticks = 0;
        while (simulationAccumulator >= config.fixedStep && ticks < 8)
        {
            if (phase == RacePhase.Preparing)
            {
                SimulatePreparation(launchHeld);
            }
            else if (phase == RacePhase.Countdown)
            {
                SimulateCountdown(launchHeld);
            }
            else if (phase == RacePhase.Racing)
            {
                SimulateRace();
            }

            simulationAccumulator -= config.fixedStep;
            ticks++;
        }

        UpdatePresentation();
    }

    public void RequestStart()
    {
        if (phase == RacePhase.Preparing) startRequested = true;
    }

    public void SetLaunchHeld(bool held)
    {
        touchLaunchHeld = held && hasInputFocus && isActiveAndEnabled &&
            (phase == RacePhase.Preparing || phase == RacePhase.Countdown);
    }

    public void RequestUpshift()
    {
        if (CanDrive() && playerState.shiftCooldown <= 0f && playerState.gear < playerVehicle.gearRatios.Count)
            pendingShift = true;
    }

    public void RequestDownshift()
    {
        if (CanDrive() && playerState.shiftCooldown <= 0f && playerState.gear > 1)
            pendingDownshift = true;
    }

    public void RequestNitrous()
    {
        if (CanDrive() && playerState.nitrousChargesRemaining > 0 && !playerState.NitrousActive && playerVehicle.nitrousDuration > 0f)
            pendingNitrous = true;
    }

    public void RequestRestart()
    {
        if (phase == RacePhase.Finished) ResetRace();
    }

    private bool CanDrive()
    {
        return hasInputFocus && isActiveAndEnabled && phase == RacePhase.Racing && playerState != null && !playerState.finished;
    }

    private void OnApplicationFocus(bool focused)
    {
        hasInputFocus = focused;
        if (!focused) ClearInput();
    }

    private void OnApplicationPause(bool paused)
    {
        if (paused) ClearInput();
    }

    private void OnDisable()
    {
        ClearInput();
    }

    private void ClearInput()
    {
        touchLaunchHeld = false;
        pendingShift = pendingDownshift = pendingNitrous = startRequested = false;
        if (hud != null) hud.ReleaseControls();
    }

    private void Initialize()
    {
        config.EnsureValid();
        playerVehicle.EnsureValid();
        opponentVehicle.EnsureValid();
        playerState = new RaceCarState();
        opponentState = new RaceCarState();
        opponentController = new RaceOpponentController();
        BuildWorld();
        initialized = true;
    }

    public void ResetRace()
    {
        config.EnsureValid();
        playerVehicle.EnsureValid();
        opponentVehicle.EnsureValid();
        if (playerState == null)
        {
            playerState = new RaceCarState();
        }
        if (opponentState == null)
        {
            opponentState = new RaceCarState();
        }

        playerState.Reset(playerVehicle);
        opponentState.Reset(opponentVehicle);
        countdownRemaining = config.countdownSeconds;
        raceTime = 0f;
        simulationAccumulator = 0f;
        pendingShift = false;
        pendingDownshift = false;
        pendingNitrous = false;
        startRequested = false;
        touchLaunchHeld = false;
        result = string.Empty;
        phase = RacePhase.Preparing;
        if (hud != null) hud.ResetFeedback();
        if (playerVisual != null)
        {
            playerVisual.SetDistance(0f);
            opponentVisual.SetDistance(0f);
        }
    }

    public void BeginCountdown()
    {
        if (phase != RacePhase.Preparing)
        {
            return;
        }

        countdownRemaining = config.countdownSeconds;
        phase = RacePhase.Countdown;
        startRequested = false;
    }

    private void SimulatePreparation(bool launchHeld)
    {
        RaceCarSimulator.UpdateLaunchRpm(playerVehicle, config, playerState, launchHeld, config.fixedStep);
        RaceCarSimulator.UpdateLaunchRpm(opponentVehicle, config, opponentState, opponentController.ShouldHoldLaunch(opponentVehicle, config, opponentState), config.fixedStep);
        if (startRequested)
        {
            BeginCountdown();
        }
    }

    private void SimulateCountdown(bool launchHeld)
    {
        RaceCarSimulator.UpdateLaunchRpm(playerVehicle, config, playerState, launchHeld, config.fixedStep);
        RaceCarSimulator.UpdateLaunchRpm(opponentVehicle, config, opponentState, opponentController.ShouldHoldLaunch(opponentVehicle, config, opponentState), config.fixedStep);
        countdownRemaining -= config.fixedStep;
        if (countdownRemaining <= 0f)
        {
            RaceCarSimulator.BeginRace(playerVehicle, config, playerState);
            RaceCarSimulator.BeginRace(opponentVehicle, config, opponentState);
            phase = RacePhase.Racing;
            touchLaunchHeld = false;
            raceTime = 0f;
        }
    }

    private void SimulateRace()
    {
        DriverCommand playerCommand = new DriverCommand
        {
            shiftPressed = pendingShift,
            downshiftPressed = pendingDownshift,
            nitrousPressed = pendingNitrous
        };
        DriverCommand opponentCommand = opponentController.GetCommand(opponentVehicle, config, opponentState);
        pendingShift = false;
        pendingDownshift = false;
        pendingNitrous = false;

        float unusedCrossingTime;
        RaceCarSimulator.Step(playerVehicle, config, playerState, playerCommand, config.fixedStep, raceTime, out unusedCrossingTime);
        RaceCarSimulator.Step(opponentVehicle, config, opponentState, opponentCommand, config.fixedStep, raceTime, out unusedCrossingTime);
        raceTime += config.fixedStep;

        if (raceTime >= config.raceTimeoutSeconds)
        {
            if (!playerState.finished)
            {
                playerState.finished = true;
                playerState.finishTime = raceTime;
            }
            if (!opponentState.finished)
            {
                opponentState.finished = true;
                opponentState.finishTime = raceTime;
            }
        }

        if (playerState.finished && opponentState.finished)
        {
            phase = RacePhase.Finished;
            RaceOutcome outcome = RaceCarSimulator.ResolveOutcome(playerState, opponentState);
            string winner = outcome == RaceOutcome.Draw ? "平局" : outcome == RaceOutcome.PlayerWin ? "玩家获胜" : "电脑获胜";
            result = winner + "\n玩家 " + playerState.finishTime.ToString("0.000") + " 秒    电脑 " + opponentState.finishTime.ToString("0.000") + " 秒";
        }
    }

    private void BuildWorld()
    {
        GameObject track = GameObject.CreatePrimitive(PrimitiveType.Cube);
        track.name = "Straight Track";
        track.transform.position = new Vector3(0f, -0.15f, config.finishDistance * 0.5f);
        track.transform.localScale = new Vector3(12f, 0.3f, config.finishDistance + 20f);
        ApplyMaterial(track, new Color(0.08f, 0.09f, 0.11f));

        for (int i = 0; i <= config.finishDistance; i += 20)
        {
            GameObject marker = GameObject.CreatePrimitive(PrimitiveType.Cube);
            marker.name = "Distance Marker " + i;
            marker.transform.position = new Vector3(0f, 0.02f, i);
            marker.transform.localScale = new Vector3(0.12f, 0.02f, 5f);
            ApplyMaterial(marker, new Color(0.65f, 0.65f, 0.68f));
        }

        GameObject finish = GameObject.CreatePrimitive(PrimitiveType.Cube);
        finish.name = "Finish Line";
        finish.transform.position = new Vector3(0f, 0.05f, config.finishDistance);
        finish.transform.localScale = new Vector3(12f, 0.03f, 1.4f);
        ApplyMaterial(finish, new Color(1f, 0.8f, 0.1f));

        playerVisual = new RaceCarVisual("Player Car", new Color(0.1f, 0.45f, 1f), 2.4f, transform);
        opponentVisual = new RaceCarVisual("Opponent Car", new Color(0.95f, 0.15f, 0.1f), -2.4f, transform);
        raceCamera = Camera.main;
        if (raceCamera == null)
        {
            GameObject cameraObject = new GameObject("Race Camera");
            raceCamera = cameraObject.AddComponent<Camera>();
            cameraObject.tag = "MainCamera";
        }

        raceCamera.fieldOfView = 52f;
        raceCamera.farClipPlane = Mathf.Max(1000f, config.finishDistance + 100f);
        GameObject lightObject = new GameObject("Race Sun");
        Light sun = lightObject.AddComponent<Light>();
        sun.type = LightType.Directional;
        sun.intensity = 1.2f;
        lightObject.transform.rotation = Quaternion.Euler(45f, -25f, 0f);
        hud = RaceHud.Create(this);
    }

    private void UpdatePresentation()
    {
        if (playerVisual != null)
        {
            playerVisual.SetDistance(playerState.distance);
            opponentVisual.SetDistance(opponentState.distance);
        }

        if (raceCamera != null)
        {
            Transform playerTransform = playerVisual.Root;
            raceCamera.transform.position = playerTransform.TransformPoint(PlayerCameraOffset);
            raceCamera.transform.LookAt(playerTransform.TransformPoint(PlayerCameraLookOffset));
        }

        if (hud != null)
        {
            hud.Render(phase, countdownRemaining, raceTime, playerState, opponentState, playerVehicle, config, result);
        }
    }

    private static void ApplyMaterial(GameObject target, Color color)
    {
        Renderer renderer = target.GetComponent<Renderer>();
        if (renderer == null)
        {
            return;
        }

        Shader shader = Shader.Find("Universal Render Pipeline/Lit");
        if (shader == null)
        {
            shader = Shader.Find("Standard");
        }

        Material material = new Material(shader);
        material.color = color;
        renderer.sharedMaterial = material;
    }
}
