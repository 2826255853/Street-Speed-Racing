using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

public sealed class RaceHud : MonoBehaviour
{
    private static readonly Color White = new Color(.94f, .95f, .95f);
    private static readonly Color Muted = new Color(.58f, .63f, .65f);
    private static readonly Color Cyan = new Color(.27f, .83f, .94f);
    private static readonly Sprite UiSprite = Resources.Load<Sprite>("UI/Icons/panel");
    private RaceGameController owner;
    private Font font, numbers;
    private RectTransform safe, gaugeRoot, leftControls, rightControls, center;
    private RaceGaugeGraphic dial, needle;
    private Text phaseText, timerText, distanceText, playerText, opponentText, speedText, gearText, rpmText;
    private Text rangeText, cueText, countdownText, feedbackText, resultText, tooltipText, maxText, nitrousText;
    private Image playerProgress, opponentProgress;
    private readonly List<Text> scaleLabels = new List<Text>();
    private Button startButton, restartButton, throttleButton, upButton, downButton, nitrousButton;
    private RaceHoldControl holdControl;
    private float displayedRpm, cachedMaximum = -1f, feedbackUntil, goUntil;
    private int lastShiftSequence;
    private RacePhase previousPhase;
    private Vector2 previousSize;
    private Rect previousSafeArea;

    public RpmOperatingWindow CurrentWindow { get; private set; }
    public bool ShowingLaunchWindow { get; private set; }
    public bool InBestRange { get; private set; }
    public float MaximumRpm { get; private set; }
    public string Feedback { get { return feedbackText.text; } }

    public static RaceHud Create(RaceGameController controller)
    {
        GameObject item = new GameObject("Race HUD", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        item.transform.SetParent(controller.transform, false);
        Canvas canvas = item.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 20;
        CanvasScaler scaler = item.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);
        scaler.matchWidthOrHeight = .5f;
        RaceHud hud = item.AddComponent<RaceHud>();
        hud.owner = controller;
        hud.Build();
        hud.EnsureEventSystem();
        return hud;
    }

    private void EnsureEventSystem()
    {
        EventSystem events = EventSystem.current;
        if (events == null) events = FindFirstObjectByType<EventSystem>();
        if (events == null)
        {
            GameObject item = new GameObject("Race UI Event System", typeof(EventSystem), typeof(InputSystemUIInputModule));
            item.transform.SetParent(transform, false);
            return;
        }
        InputSystemUIInputModule module = events.GetComponent<InputSystemUIInputModule>();
        if (module == null) module = events.gameObject.AddComponent<InputSystemUIInputModule>();
        foreach (BaseInputModule old in events.GetComponents<BaseInputModule>())
            if (old != module) old.enabled = false;
        module.enabled = true;
    }

    private void Build()
    {
        font = Resources.Load<Font>("UI/Fonts/NotoSansSC-Regular");
        numbers = Resources.Load<Font>("UI/Fonts/BarlowCondensed-SemiBold");
        if (font == null) font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        if (numbers == null) numbers = font;
        safe = Rect("Safe Area", transform);
        Stretch(safe);
        RectTransform header = Rect("Race Header", safe);
        header.anchorMin = new Vector2(0, 1);
        header.anchorMax = Vector2.one;
        header.pivot = new Vector2(.5f, 1);
        header.sizeDelta = new Vector2(0, 170);
        Panel(header, "Header Backdrop", new Color(.02f, .025f, .03f, .88f));
        phaseText = Label(header, "Phase", "准备起步", 23, new Vector2(.5f, 1), new Vector2(0, -32), new Vector2(280, 34));
        timerText = Label(header, "Timer", "0.000", 46, new Vector2(.5f, 1), new Vector2(0, -80), new Vector2(240, 56), true);
        distanceText = Label(header, "Race Distance", "400 m", 16, new Vector2(.5f, 1), new Vector2(0, -132), new Vector2(260, 28));
        distanceText.color = Muted;
        playerText = Label(header, "Player", "玩家", 20, new Vector2(.25f, 1), new Vector2(0, -40), new Vector2(320, 34));
        opponentText = Label(header, "Opponent", "对手", 20, new Vector2(.75f, 1), new Vector2(0, -40), new Vector2(320, 34));
        playerProgress = Progress(header, "Player Progress", .25f, Cyan);
        opponentProgress = Progress(header, "Opponent Progress", .75f, new Color(1f, .39f, .30f));

        gaugeRoot = Rect("Tachometer", safe);
        Place(gaugeRoot, new Vector2(.5f, 0), new Vector2(0, 22), new Vector2(600, 535));
        gaugeRoot.pivot = new Vector2(.5f, 0);
        dial = Graphic("Dial", gaugeRoot, RaceGaugeGraphic.Layer.Dial);
        rangeText = Label(gaugeRoot, "Operating Range", "", 17, new Vector2(.5f, 0), new Vector2(0, 425), new Vector2(600, 30));
        rangeText.color = Muted;
        cueText = Label(gaugeRoot, "Driving Cue", "", 24, new Vector2(.5f, 0), new Vector2(0, 465), new Vector2(480, 36));
        gearText = Label(gaugeRoot, "Gear", "1 / 6", 65, new Vector2(.5f, 0), new Vector2(0, 218), new Vector2(220, 84), true);
        speedText = Label(gaugeRoot, "Speed", "0", 49, new Vector2(.5f, 0), new Vector2(0, 146), new Vector2(190, 60), true);
        Label(gaugeRoot, "Speed Unit", "km/h", 16, new Vector2(.5f, 0), new Vector2(0, 108), new Vector2(140, 28), true).color = Muted;
        rpmText = Label(gaugeRoot, "RPM", "900 RPM", 22, new Vector2(.5f, 0), new Vector2(0, 66), new Vector2(240, 32), true);
        maxText = Label(gaugeRoot, "Maximum RPM", "", 15, new Vector2(.5f, 0), new Vector2(0, 31), new Vector2(320, 28));
        maxText.color = Muted;
        for (int i = 0; i < 13; i++)
            scaleLabels.Add(Label(gaugeRoot, "Scale " + i, "", 18, new Vector2(.5f, 0), Vector2.zero, new Vector2(52, 28), true));
        needle = Graphic("Needle", gaugeRoot, RaceGaugeGraphic.Layer.Needle);
        Debug.Log($"[RaceHud] 转速表指针加载完成：已创建={needle != null}，激活={needle != null && needle.gameObject.activeInHierarchy}，启用={needle != null && needle.enabled}，尺寸={(needle != null ? needle.rectTransform.rect.size.ToString() : "N/A")}，Canvas={(needle != null && needle.canvas != null ? needle.canvas.name : "null")}", needle);

        leftControls = Rect("Left Controls", safe);
        rightControls = Rect("Right Controls", safe);
        Place(leftControls, new Vector2(.5f, 0), new Vector2(-475, 112), new Vector2(240, 160));
        Place(rightControls, new Vector2(.5f, 0), new Vector2(475, 112), new Vector2(240, 160));
        nitrousButton = IconButton(leftControls, "Nitrous", "zap", new Vector2(-58, 0), "氮气 · Left Shift", owner.RequestNitrous);
        downButton = IconButton(leftControls, "Downshift", "arrow-down", new Vector2(58, 0), "降挡 · Q", owner.RequestDownshift);
        throttleButton = IconButton(rightControls, "Throttle", "gauge", new Vector2(-58, 0), "油门 · Space", null);
        holdControl = throttleButton.gameObject.AddComponent<RaceHoldControl>();
        holdControl.Changed = owner.SetLaunchHeld;
        holdControl.Supported = () => throttleButton.interactable;
        upButton = IconButton(rightControls, "Upshift", "arrow-up", new Vector2(58, 0), "升挡 · E", owner.RequestUpshift);
        nitrousText = Label(leftControls, "Nitrous State", "氮气就绪", 16, new Vector2(.5f, .5f), new Vector2(-58, -68), new Vector2(128, 28));
        nitrousText.color = Cyan;
        center = Rect("Race Messages", safe);
        Place(center, new Vector2(.5f, .56f), Vector2.zero, new Vector2(660, 300));
        countdownText = Label(center, "Countdown", "", 108, new Vector2(.5f, .5f), Vector2.zero, new Vector2(600, 150), true);
        feedbackText = Label(center, "Feedback", "", 32, new Vector2(.5f, .5f), new Vector2(0, -100), new Vector2(620, 64));
        resultText = Label(center, "Result", "", 26, new Vector2(.5f, .5f), new Vector2(0, 38), new Vector2(640, 190));
        startButton = ActionButton(center, "Start Race", "开始比赛", owner.RequestStart, Vector2.zero);
        restartButton = ActionButton(center, "Restart Race", "再次比赛", owner.RequestRestart, new Vector2(0, -110));
        tooltipText = Label(safe, "Tooltip", "", 18, new Vector2(.5f, 0), new Vector2(0, 8), new Vector2(650, 30));
        tooltipText.color = Muted;
        ResetFeedback();
    }

    public void Render(RacePhase phase, float countdownRemaining, float raceTime, RaceCarState player,
        RaceCarState opponent, VehicleSpec vehicle, RaceConfig config, string result)
    {
        UpdateLayout();
        CurrentWindow = vehicle.ResolveRpmSettings(config);
        MaximumRpm = vehicle.redlineRpm;
        ShowingLaunchWindow = phase == RacePhase.Preparing || phase == RacePhase.Countdown;
        bool driving = phase == RacePhase.Racing && !player.finished;
        bool canShift = driving && player.gear < vehicle.gearRatios.Count;
        InBestRange = ShowingLaunchWindow ? CurrentWindow.ContainsLaunch(player.engineRpm) : canShift && CurrentWindow.ContainsShift(player.engineRpm);
        float min = ShowingLaunchWindow ? CurrentWindow.launchMin : CurrentWindow.shiftMin;
        float max = ShowingLaunchWindow ? CurrentWindow.launchMax : CurrentWindow.shiftMax;
        if (phase == RacePhase.Preparing && previousPhase != phase) ResetFeedback();
        if (phase == RacePhase.Racing && previousPhase == RacePhase.Countdown)
        {
            goUntil = Time.unscaledTime + .65f;
            feedbackUntil = Time.unscaledTime + 1.1f;
            feedbackText.text = player.launchQuality >= .9f ? "最佳起步" : player.launchQuality > 0f ? "良好起步" : "起步转速偏离";
            feedbackText.color = player.launchQuality > 0f ? RaceGaugeGraphic.Green : RaceGaugeGraphic.Red;
        }
        if (player.shiftSequence != lastShiftSequence)
        {
            lastShiftSequence = player.shiftSequence;
            feedbackUntil = Time.unscaledTime + .9f;
            feedbackText.text = player.lastShiftQuality == ShiftQuality.Good ? "最佳换挡" :
                player.lastShiftQuality == ShiftQuality.TooEarly ? "换挡过早" : player.lastShiftQuality == ShiftQuality.TooLate ? "换挡过晚" : "降挡";
            feedbackText.color = player.lastShiftQuality == ShiftQuality.Good ? RaceGaugeGraphic.Green :
                player.lastShiftQuality == ShiftQuality.Downshift ? White : RaceGaugeGraphic.Red;
        }
        if (Time.unscaledTime >= feedbackUntil || phase == RacePhase.Finished) feedbackText.text = "";
        displayedRpm = Mathf.Lerp(displayedRpm, player.engineRpm, 1f - Mathf.Exp(-22f * Time.unscaledDeltaTime));
        bool showRange = ShowingLaunchWindow || canShift;
        dial.SetData(vehicle.redlineRpm, showRange ? min : 0, showRange ? max : 0, CurrentWindow.launchTarget, ShowingLaunchWindow, InBestRange, displayedRpm);
        needle.SetData(vehicle.redlineRpm, min, max, CurrentWindow.launchTarget, ShowingLaunchWindow, InBestRange, displayedRpm);
        UpdateScale(vehicle.redlineRpm);
        speedText.text = (player.speed * 3.6f).ToString("0");
        gearText.text = player.gear + " <size=26><color=#929C9F>/ " + vehicle.gearRatios.Count + "</color></size>";
        rpmText.text = player.engineRpm.ToString("0") + " RPM";
        rpmText.color = player.engineRpm >= vehicle.redlineRpm * .98f ? RaceGaugeGraphic.Red : White;
        maxText.text = "最大转速  " + vehicle.redlineRpm.ToString("0") + " RPM";
        rangeText.text = showRange ? (ShowingLaunchWindow ? "最佳起步  " : "最佳换挡  ") + min.ToString("0") + " - " + max.ToString("0") + " RPM" : "";
        cueText.text = ShowingLaunchWindow ? (InBestRange ? "保持转速" : player.engineRpm < min ? "提升转速" : "降低转速") :
            !driving ? "" : !canShift ? "最高挡" : player.shiftCooldown > 0f ? "换挡中" : InBestRange ? "换挡" : player.engineRpm > max ? "立即换挡" : "";
        cueText.color = InBestRange ? RaceGaugeGraphic.Green : player.engineRpm > max ? RaceGaugeGraphic.Red : White;
        phaseText.text = phase == RacePhase.Preparing ? "准备起步" : phase == RacePhase.Countdown ? "即将发车" : phase == RacePhase.Racing ? "竞速中" : "比赛结束";
        timerText.text = raceTime.ToString("0.000");
        distanceText.text = config.finishDistance.ToString("0") + " m  ·  直线竞速";
        playerText.text = "玩家  ·  " + Mathf.Max(0, config.finishDistance - player.distance).ToString("0") + " m";
        opponentText.text = "对手  ·  " + Mathf.Max(0, config.finishDistance - opponent.distance).ToString("0") + " m";
        playerProgress.fillAmount = Mathf.Clamp01(player.distance / config.finishDistance);
        opponentProgress.fillAmount = Mathf.Clamp01(opponent.distance / config.finishDistance);
        countdownText.text = phase == RacePhase.Countdown ? Mathf.Max(1, Mathf.CeilToInt(countdownRemaining)).ToString() :
            phase == RacePhase.Racing && Time.unscaledTime < goUntil ? "GO" : "";
        startButton.gameObject.SetActive(phase == RacePhase.Preparing);
        restartButton.gameObject.SetActive(phase == RacePhase.Finished);
        resultText.gameObject.SetActive(phase == RacePhase.Finished);
        resultText.text = phase == RacePhase.Finished ? result : "";
        throttleButton.interactable = ShowingLaunchWindow;
        if (!ShowingLaunchWindow) holdControl.Release();
        upButton.interactable = canShift && player.shiftCooldown <= 0f;
        downButton.interactable = driving && player.gear > 1 && player.shiftCooldown <= 0f;
        nitrousButton.interactable = driving && player.nitrousChargesRemaining > 0 && !player.NitrousActive && vehicle.nitrousDuration > 0f;
        nitrousText.text = player.NitrousActive ? player.nitrousTimeRemaining.ToString("0.0") + " s" : player.nitrousChargesRemaining > 0 && vehicle.nitrousDuration > 0 ? "氮气就绪" : "氮气已用尽";
        nitrousText.color = player.NitrousActive ? White : nitrousButton.interactable ? Cyan : Muted;
        previousPhase = phase;
    }

    private void UpdateScale(float maximum)
    {
        if (cachedMaximum == maximum) return;
        cachedMaximum = maximum;
        float step = maximum > 12000f ? 2000f : maximum < 3500f ? 500f : 1000f;
        int count = Mathf.Min(12, Mathf.FloorToInt(maximum / step));
        int index = 0;
        for (int i = 0; i <= count && index < scaleLabels.Count - 1; i++)
        {
            float value = i * step;
            if (maximum - value < step * .55f) break;
            PositionScale(scaleLabels[index++], value, maximum);
        }
        PositionScale(scaleLabels[index++], maximum, maximum);
        for (; index < scaleLabels.Count; index++) scaleLabels[index].gameObject.SetActive(false);
    }

    private static void PositionScale(Text label, float value, float maximum)
    {
        label.gameObject.SetActive(true);
        label.text = (value / 1000f).ToString("0.#");
        Vector2 direction = RaceGaugeGraphic.Direction(value / maximum);
        label.rectTransform.anchoredPosition = new Vector2(direction.x * 207, 136.425f + direction.y * 207);
        label.color = value >= maximum * .95f ? RaceGaugeGraphic.Red : Muted;
    }

    private void UpdateLayout()
    {
        Rect screenSafe = Screen.safeArea;
        Vector2 screen = new Vector2(Screen.width, Screen.height);
        if (screen != previousSize || screenSafe != previousSafeArea)
        {
            safe.anchorMin = new Vector2(screenSafe.xMin / screen.x, screenSafe.yMin / screen.y);
            safe.anchorMax = new Vector2(screenSafe.xMax / screen.x, screenSafe.yMax / screen.y);
            safe.offsetMin = safe.offsetMax = Vector2.zero;
            previousSize = screen;
            previousSafeArea = screenSafe;
            Canvas.ForceUpdateCanvases();
        }
        float width = safe.rect.width;
        float height = safe.rect.height;
        bool portrait = height > width;
        float scale = portrait ? Mathf.Min(1, width * .80f / 600f) : Mathf.Min(1, width * .30f / 600f);
        gaugeRoot.localScale = Vector3.one * scale;
        gaugeRoot.anchoredPosition = new Vector2(0, portrait ? 225 : 22);
        float offset = portrait ? Mathf.Min(width * .41f, 340) : 300 * scale + 160;
        leftControls.anchoredPosition = new Vector2(-offset, portrait ? 100 : 115 * scale);
        rightControls.anchoredPosition = new Vector2(offset, portrait ? 100 : 115 * scale);
        float controlsScale = Mathf.Min(1f, width / (portrait ? 850f : 1450f));
        leftControls.localScale = rightControls.localScale = Vector3.one * controlsScale;
        center.localScale = Vector3.one * Mathf.Min(1, width / 720f);
        center.anchoredPosition = new Vector2(0, portrait ? 90 : 35);
        float labelWidth = Mathf.Min(320, width * .22f);
        playerText.rectTransform.sizeDelta = opponentText.rectTransform.sizeDelta = new Vector2(labelWidth, 34);
        playerText.fontSize = opponentText.fontSize = width < 1200 ? 16 : 20;
    }

    public void ResetFeedback()
    {
        lastShiftSequence = 0;
        feedbackUntil = goUntil = 0;
        displayedRpm = owner != null ? owner.playerVehicle.idleRpm : 0;
        if (feedbackText != null) feedbackText.text = "";
        ReleaseControls();
    }
    public void ReleaseControls() { if (holdControl != null) holdControl.Release(); }
    private void OnDisable() { ReleaseControls(); }

    private static RectTransform Rect(string name, Transform parent)
    {
        GameObject item = new GameObject(name, typeof(RectTransform));
        item.transform.SetParent(parent, false);
        return (RectTransform)item.transform;
    }
    private static void Place(RectTransform rect, Vector2 anchor, Vector2 position, Vector2 size)
    {
        rect.anchorMin = rect.anchorMax = anchor;
        rect.anchoredPosition = position;
        rect.sizeDelta = size;
    }
    private static void Stretch(RectTransform rect)
    {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = rect.offsetMax = Vector2.zero;
    }
    private static Image Panel(Transform parent, string name, Color color)
    {
        RectTransform rect = Rect(name, parent);
        Stretch(rect);
        Image image = rect.gameObject.AddComponent<Image>();
        image.color = color;
        image.sprite = UiSprite;
        image.type = Image.Type.Sliced;
        image.raycastTarget = false;
        return image;
    }
    private Text Label(Transform parent, string name, string caption, int size, Vector2 anchor, Vector2 position, Vector2 dimensions, bool numeric = false)
    {
        RectTransform rect = Rect(name, parent);
        Place(rect, anchor, position, dimensions);
        Text label = rect.gameObject.AddComponent<Text>();
        label.font = numeric ? numbers : font;
        label.fontSize = size;
        label.text = caption;
        label.color = White;
        label.alignment = TextAnchor.MiddleCenter;
        label.horizontalOverflow = HorizontalWrapMode.Wrap;
        label.verticalOverflow = VerticalWrapMode.Truncate;
        label.raycastTarget = false;
        return label;
    }
    private static RaceGaugeGraphic Graphic(string name, Transform parent, RaceGaugeGraphic.Layer layer)
    {
        RectTransform rect = Rect(name, parent);
        Stretch(rect);
        rect.gameObject.AddComponent<CanvasRenderer>();
        RaceGaugeGraphic graphic = rect.gameObject.AddComponent<RaceGaugeGraphic>();
        graphic.layer = layer;
        graphic.raycastTarget = false;
        return graphic;
    }
    private Image Progress(Transform parent, string name, float x, Color tint)
    {
        RectTransform rect = Rect(name, parent);
        rect.anchorMin = new Vector2(x - .13f, 1);
        rect.anchorMax = new Vector2(x + .13f, 1);
        rect.anchoredPosition = new Vector2(0, -84);
        rect.sizeDelta = new Vector2(0, 4);
        Panel(rect, "Track", new Color(.25f, .28f, .29f));
        Image progress = Panel(rect, "Fill", tint);
        progress.type = Image.Type.Filled;
        progress.fillMethod = Image.FillMethod.Horizontal;
        progress.fillAmount = 0;
        return progress;
    }
    private Button ActionButton(Transform parent, string name, string caption, UnityEngine.Events.UnityAction action, Vector2 position)
    {
        Button button = BaseButton(parent, name, position, new Vector2(250, 68));
        button.image.color = new Color(.16f, .24f, .17f, .98f);
        Outline outline = button.gameObject.AddComponent<Outline>();
        outline.effectColor = new Color(.48f, .95f, .30f, .6f);
        outline.effectDistance = new Vector2(1, -1);
        Label(button.transform, "Label", caption, 24, new Vector2(.5f, .5f), Vector2.zero, new Vector2(230, 50));
        button.onClick.AddListener(action);
        return button;
    }
    private Button IconButton(Transform parent, string name, string icon, Vector2 position, string tooltip, UnityEngine.Events.UnityAction action)
    {
        Button button = BaseButton(parent, name, position, new Vector2(96, 96));
        Outline outline = button.gameObject.AddComponent<Outline>();
        outline.effectColor = new Color(.52f, .57f, .58f, .5f);
        outline.effectDistance = new Vector2(1, -1);
        Sprite iconSprite = Resources.Load<Sprite>("UI/Icons/" + icon);
        if (iconSprite != null)
        {
            Image image = Panel(button.transform, "Icon", White);
            image.rectTransform.anchorMin = image.rectTransform.anchorMax = new Vector2(.5f, .5f);
            image.rectTransform.sizeDelta = new Vector2(38, 38);
            image.sprite = iconSprite;
            image.type = Image.Type.Simple;
        }
        RaceHudTooltip tip = button.gameObject.AddComponent<RaceHudTooltip>();
        tip.Caption = tooltip;
        tip.Show = caption => { if (tooltipText != null) tooltipText.text = caption; };
        if (action != null) button.onClick.AddListener(action);
        return button;
    }
    private static Button BaseButton(Transform parent, string name, Vector2 position, Vector2 size)
    {
        RectTransform rect = Rect(name, parent);
        Place(rect, new Vector2(.5f, .5f), position, size);
        Image image = rect.gameObject.AddComponent<Image>();
        image.color = new Color(.055f, .067f, .073f, .94f);
        image.sprite = UiSprite;
        image.type = Image.Type.Sliced;
        Button button = rect.gameObject.AddComponent<Button>();
        button.targetGraphic = image;
        Navigation navigation = button.navigation;
        navigation.mode = Navigation.Mode.None;
        button.navigation = navigation;
        ColorBlock colors = button.colors;
        colors.highlightedColor = new Color(.8f, 1f, .8f);
        colors.pressedColor = new Color(.48f, .95f, .3f);
        colors.disabledColor = new Color(.30f, .32f, .33f, .55f);
        button.colors = colors;
        return button;
    }
}
