using UnityEngine;
using UnityEngine.UI;

public class RaceHud : MonoBehaviour
{
    private Text phaseText;
    private Text countdownText;
    private Text playerText;
    private Text opponentText;
    private Text controlsText;
    private Text resultText;

    public static RaceHud Create()
    {
        GameObject canvasObject = new GameObject("Race HUD");
        Canvas canvas = canvasObject.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvasObject.AddComponent<CanvasScaler>();
        canvasObject.AddComponent<GraphicRaycaster>();
        RaceHud hud = canvasObject.AddComponent<RaceHud>();
        hud.Build(canvas.transform);
        return hud;
    }

    private void Build(Transform parent)
    {
        Font font;
        try
        {
            font = Resources.GetBuiltinResource<Font>("Arial.ttf");
        }
        catch
        {
            font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        }

        phaseText = CreateText(parent, "Phase", font, 26, TextAnchor.UpperLeft, new Vector2(0.03f, 0.9f), new Vector2(0.45f, 0.98f));
        playerText = CreateText(parent, "Player", font, 20, TextAnchor.UpperLeft, new Vector2(0.03f, 0.72f), new Vector2(0.49f, 0.88f));
        opponentText = CreateText(parent, "Opponent", font, 20, TextAnchor.UpperRight, new Vector2(0.51f, 0.72f), new Vector2(0.97f, 0.88f));
        countdownText = CreateText(parent, "Countdown", font, 72, TextAnchor.MiddleCenter, new Vector2(0.3f, 0.38f), new Vector2(0.7f, 0.65f));
        controlsText = CreateText(parent, "Controls", font, 18, TextAnchor.LowerCenter, new Vector2(0.12f, 0.02f), new Vector2(0.88f, 0.12f));
        resultText = CreateText(parent, "Result", font, 34, TextAnchor.MiddleCenter, new Vector2(0.2f, 0.28f), new Vector2(0.8f, 0.72f));
        resultText.color = new Color(1f, 0.85f, 0.25f);
    }

    public void Render(RacePhase phase, float countdownRemaining, float raceTime, RaceCarState player, RaceCarState opponent, RaceConfig config, string result)
    {
        phaseText.text = phase == RacePhase.Countdown ? "准备起步" : phase == RacePhase.Racing ? "竞速中" : phase == RacePhase.Finished ? "比赛结束" : "准备中";
        playerText.text = FormatCar("玩家", player, config.finishDistance);
        opponentText.text = FormatCar("电脑", opponent, config.finishDistance);
        string launchRange = "起步最佳区间 " + config.launchRpmMin.ToString("0") + "–" + config.launchRpmMax.ToString("0") + " RPM";
        controlsText.text = phase == RacePhase.Finished
            ? "按 R 重赛"
            : phase == RacePhase.Preparing
                ? launchRange + "    空格控制转速，按 Enter 或点击按钮开始"
                : launchRange + "    空格：转速    Q：降挡    E：升挡    左 Shift：氮气";

        if (phase == RacePhase.Countdown)
        {
            countdownText.gameObject.SetActive(true);
            countdownText.text = countdownRemaining > 0.05f ? Mathf.CeilToInt(countdownRemaining).ToString() : "GO!";
        }
        else
        {
            countdownText.gameObject.SetActive(false);
        }

        resultText.gameObject.SetActive(phase == RacePhase.Finished);
        resultText.text = phase == RacePhase.Finished ? result + "\n总用时 " + raceTime.ToString("0.000") + " 秒" : string.Empty;
    }

    private static Text CreateText(Transform parent, string name, Font font, int size, TextAnchor anchor, Vector2 minimum, Vector2 maximum)
    {
        GameObject item = new GameObject(name);
        item.transform.SetParent(parent, false);
        RectTransform rect = item.AddComponent<RectTransform>();
        rect.anchorMin = minimum;
        rect.anchorMax = maximum;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
        Text text = item.AddComponent<Text>();
        text.font = font;
        text.fontSize = size;
        text.alignment = anchor;
        text.color = Color.white;
        text.horizontalOverflow = HorizontalWrapMode.Wrap;
        text.verticalOverflow = VerticalWrapMode.Overflow;
        return text;
    }

    private static string FormatCar(string label, RaceCarState state, float finishDistance)
    {
        string nitrous = state.nitrousChargesRemaining > 0 || state.NitrousActive ? "可用" : "已用";
        string shift = state.lastShiftQuality == ShiftQuality.Downshift ? "降挡" : state.lastShiftQuality == ShiftQuality.Good ? "完美" : state.lastShiftQuality == ShiftQuality.TooEarly ? "过早" : "过晚";
        float remaining = Mathf.Max(0f, finishDistance - state.distance);
        return label + "\n速度 " + (state.speed * 3.6f).ToString("0") + " km/h    剩余 " + remaining.ToString("0.0") + " m\n挡位 " + state.gear + "    转速 " + state.engineRpm.ToString("0") + "    换挡 " + shift + "    氮气 " + nitrous;
    }
}
