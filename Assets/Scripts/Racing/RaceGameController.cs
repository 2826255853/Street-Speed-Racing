using UnityEngine;
using UnityEngine.InputSystem;

public class RaceGameController : MonoBehaviour
{
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
    private string result = string.Empty;

    public RacePhase Phase { get { return phase; } }
    public RaceCarState PlayerState { get { return playerState; } }
    public RaceCarState OpponentState { get { return opponentState; } }

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
        bool launchHeld = keyboard != null && keyboard.spaceKey.isPressed;
        if (keyboard != null)
        {
            if (phase == RacePhase.Racing)
            {
                pendingShift |= keyboard.eKey.wasPressedThisFrame;
                pendingDownshift |= keyboard.qKey.wasPressedThisFrame;
                pendingNitrous |= keyboard.leftShiftKey.wasPressedThisFrame;
            }
            if (phase == RacePhase.Preparing && keyboard.enterKey.wasPressedThisFrame)
            {
                startRequested = true;
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

    private void OnGUI()
    {
        if (phase == RacePhase.Preparing)
        {
            Rect button = new Rect(Screen.width * 0.5f - 120f, Screen.height * 0.5f - 28f, 240f, 56f);
            if (GUI.Button(button, "准备比赛 / START"))
            {
                startRequested = true;
            }
        }
        else if (phase == RacePhase.Finished)
        {
            Rect button = new Rect(Screen.width * 0.5f - 100f, Screen.height * 0.72f, 200f, 48f);
            if (GUI.Button(button, "再来一局 / R"))
            {
                ResetRace();
            }
        }
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
        result = string.Empty;
        phase = RacePhase.Preparing;
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
        RaceCarSimulator.UpdateLaunchRpm(opponentVehicle, config, opponentState, opponentController.ShouldHoldLaunch(opponentState), config.fixedStep);
        if (startRequested)
        {
            BeginCountdown();
        }
    }

    private void SimulateCountdown(bool launchHeld)
    {
        RaceCarSimulator.UpdateLaunchRpm(playerVehicle, config, playerState, launchHeld, config.fixedStep);
        RaceCarSimulator.UpdateLaunchRpm(opponentVehicle, config, opponentState, opponentController.ShouldHoldLaunch(opponentState), config.fixedStep);
        countdownRemaining -= config.fixedStep;
        if (countdownRemaining <= 0f)
        {
            RaceCarSimulator.BeginRace(playerVehicle, config, playerState);
            RaceCarSimulator.BeginRace(opponentVehicle, config, opponentState);
            phase = RacePhase.Racing;
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

        playerVisual = new RaceCarVisual("Player Car", new Color(0.1f, 0.45f, 1f), -2.4f);
        opponentVisual = new RaceCarVisual("Opponent Car", new Color(0.95f, 0.15f, 0.1f), 2.4f);
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
        hud = RaceHud.Create();
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
            float midpoint = (playerState.distance + opponentState.distance) * 0.5f;
            float cameraZ = Mathf.Clamp(midpoint - 22f, -24f, Mathf.Max(-24f, config.finishDistance - 70f));
            raceCamera.transform.position = new Vector3(18f, 16f, cameraZ);
            raceCamera.transform.LookAt(new Vector3(0f, 0.6f, cameraZ + 28f));
        }

        if (hud != null)
        {
            hud.Render(phase, countdownRemaining, raceTime, playerState, opponentState, config, result);
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
