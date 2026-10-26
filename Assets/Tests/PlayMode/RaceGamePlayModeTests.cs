using System.Collections;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using UnityEngine.TestTools;

public class RaceGamePlayModeTests
{
    [UnityTest]
    public IEnumerator RaceHudReflectsResolvedRangesAndDriverInputs()
    {
        GameObject host = new GameObject("Race HUD Interaction Test");
        RaceGameController controller = host.AddComponent<RaceGameController>();
        controller.playerVehicle.useCustomRpmSettings = true;
        controller.playerVehicle.launchRpmMin = 2400;
        controller.playerVehicle.launchRpmMax = 3600;
        controller.playerVehicle.launchRpmTarget = 3000;
        controller.playerVehicle.bestShiftRpmMin = 4000;
        controller.playerVehicle.bestShiftRpmMax = 5000;
        controller.config.countdownSeconds = .5f;
        yield return null;

        RaceHud hud = controller.Hud;
        Assert.IsNotNull(hud);
        Assert.AreEqual(2400, hud.CurrentWindow.launchMin);
        Assert.AreEqual(3000, hud.CurrentWindow.launchTarget);
        Assert.IsTrue(hud.ShowingLaunchWindow);
        Assert.IsNotNull(hud.GetComponent<Canvas>());
        Assert.IsNotNull(EventSystem.current);
        Assert.IsNotNull(EventSystem.current.GetComponent<InputSystemUIInputModule>());
        Assert.IsNotNull(hud.transform.Find("Safe Area/Tachometer/Dial"));
        Assert.IsNotNull(hud.transform.Find("Safe Area/Tachometer/Needle"));
        Assert.AreEqual(controller.playerVehicle.redlineRpm, hud.MaximumRpm);
        Assert.IsNotNull(Resources.Load<Sprite>("UI/Icons/panel"));
        Assert.AreEqual("准备起步", hud.transform.Find("Safe Area/Race Header/Phase").GetComponent<Text>().text);
        Assert.IsNotNull(hud.transform.Find("Safe Area/Right Controls/Throttle/Icon"));

        controller.PlayerState.engineRpm = 3000;
        hud.Render(RacePhase.Preparing, 0, 0, controller.PlayerState, controller.OpponentState,
            controller.playerVehicle, controller.config, string.Empty);
        Assert.IsTrue(hud.InBestRange);
        Button throttle = FindButton(hud.transform, "Throttle");
        RaceHoldControl throttleHold = throttle.GetComponent<RaceHoldControl>();
        throttleHold.OnPointerDown(new PointerEventData(EventSystem.current) { pointerId = 101 });
        Assert.IsTrue(controller.TouchLaunchHeld);
        throttleHold.OnPointerExit(new PointerEventData(EventSystem.current) { pointerId = 101 });
        Assert.IsFalse(controller.TouchLaunchHeld);
        hud.Render(RacePhase.Countdown, .3f, 0, controller.PlayerState, controller.OpponentState,
            controller.playerVehicle, controller.config, string.Empty);
        controller.PlayerState.started = true;
        controller.PlayerState.engineRpm = 4500;
        hud.Render(RacePhase.Racing, 0, .25f, controller.PlayerState, controller.OpponentState,
            controller.playerVehicle, controller.config, string.Empty);
        Assert.IsFalse(hud.ShowingLaunchWindow);
        Assert.IsTrue(hud.InBestRange);

        Button upshift = FindButton(hud.transform, "Upshift");
        Button downshift = FindButton(hud.transform, "Downshift");
        Button nitrous = FindButton(hud.transform, "Nitrous");
        Assert.IsTrue(upshift.interactable);
        Assert.IsFalse(downshift.interactable);
        Assert.IsTrue(nitrous.interactable);
        controller.PlayerState.gear = controller.playerVehicle.gearRatios.Count;
        hud.Render(RacePhase.Racing, 0, .3f, controller.PlayerState, controller.OpponentState,
            controller.playerVehicle, controller.config, string.Empty);
        Assert.IsFalse(upshift.interactable);

        int hudCount = Object.FindObjectsByType<RaceHud>(FindObjectsSortMode.None).Length;
        Assert.AreEqual(1, hudCount);
        controller.ResetRace();
        Assert.IsFalse(controller.TouchLaunchHeld);
        Assert.IsTrue(string.IsNullOrEmpty(hud.Feedback));
        Assert.AreEqual(hudCount, Object.FindObjectsByType<RaceHud>(FindObjectsSortMode.None).Length);

        Object.Destroy(host);
        yield return null;
        Assert.AreEqual(hudCount - 1, Object.FindObjectsByType<RaceHud>(FindObjectsSortMode.None).Length);
    }

    [UnityTest]
    public IEnumerator ThrottleHoldReleasesOnPointerExitAndComponentDisable()
    {
        GameObject item = new GameObject("Throttle Hold Test", typeof(RectTransform), typeof(Image), typeof(Button), typeof(RaceHoldControl));
        RaceHoldControl control = item.GetComponent<RaceHoldControl>();
        bool held = false;
        control.Changed = value => held = value;
        control.OnPointerDown(new PointerEventData(EventSystem.current) { pointerId = 17 });
        Assert.IsTrue(held);
        control.OnPointerExit(new PointerEventData(EventSystem.current) { pointerId = 17 });
        Assert.IsFalse(held);
        control.OnPointerDown(new PointerEventData(EventSystem.current) { pointerId = 18 });
        item.SetActive(false);
        Assert.IsFalse(held);
        Object.Destroy(item);
        yield return null;
    }

    private static Button FindButton(Transform root, string name)
    {
        foreach (Button button in root.GetComponentsInChildren<Button>(true))
            if (button.name == name) return button;
        Assert.Fail("Missing HUD control " + name);
        return null;
    }

    [UnityTest]
    public IEnumerator RacePrototypeSceneStartsAndCreatesController()
    {
        SceneManager.LoadScene("RacePrototype", LoadSceneMode.Single);
        yield return null;

        RaceGameController controller = Object.FindFirstObjectByType<RaceGameController>();
        Assert.IsNotNull(controller);
        Assert.AreEqual(RacePhase.Preparing, controller.Phase);
        Assert.IsNotNull(controller.PlayerState);
        Assert.IsNotNull(controller.OpponentState);

        Transform player = controller.transform.Find("Player Car");
        AssertSedan(player, 2.4f, new Color(0.1f, 0.45f, 1f));
        AssertSedan(controller.transform.Find("Opponent Car"), -2.4f, new Color(0.95f, 0.15f, 0.1f));

        Camera raceCamera = Camera.main;
        Assert.IsNotNull(raceCamera);
        Vector3 localCameraPosition = player.InverseTransformPoint(raceCamera.transform.position);
        Assert.That(localCameraPosition.x, Is.GreaterThan(0f), "The camera should be on the car's right side.");
        Assert.That(localCameraPosition.z, Is.GreaterThan(0f), "The camera should be in front of the car.");
        Assert.That(localCameraPosition.x, Is.EqualTo(localCameraPosition.z).Within(0.001f),
            "The camera should sit at a 45-degree front-right angle.");
        Assert.That(localCameraPosition.magnitude, Is.LessThan(6f), "The camera should stay close to the car.");
        Vector3 carFront = player.TransformPoint(new Vector3(0f, 0.85f, 1.9f));
        Assert.That(Vector3.Angle(raceCamera.transform.forward, carFront - raceCamera.transform.position),
            Is.LessThan(0.1f), "The camera should look at the front of the player's car.");
    }

    [UnityTest]
    public IEnumerator SedanModelsFollowDistanceAndResetWheelsAcrossAllLods()
    {
        SceneManager.LoadScene("RacePrototype", LoadSceneMode.Single);
        yield return null;

        RaceGameController controller = Object.FindFirstObjectByType<RaceGameController>();
        Transform player = controller.transform.Find("Player Car");
        Transform opponent = controller.transform.Find("Opponent Car");
        Transform[] playerWheels = Wheels(player);
        Transform[] opponentWheels = Wheels(opponent);
        Assert.AreEqual(12, playerWheels.Length);
        Assert.AreEqual(12, opponentWheels.Length);
        Quaternion[] playerRest = playerWheels.Select(w => w.localRotation).ToArray();
        Quaternion[] opponentRest = opponentWheels.Select(w => w.localRotation).ToArray();
        float[] widths = playerWheels.Select(w => w.GetComponent<Renderer>().bounds.size.x).ToArray();

        controller.PlayerState.distance = 0.325f * Mathf.PI * 0.5f;
        controller.OpponentState.distance = 0.325f * Mathf.PI;
        yield return null;

        Assert.That(player.position.z, Is.EqualTo(controller.PlayerState.distance).Within(0.001f));
        Assert.That(opponent.position.z, Is.EqualTo(controller.OpponentState.distance).Within(0.001f));
        Assert.That(player.position.x, Is.EqualTo(2.4f));
        Assert.That(opponent.position.x, Is.EqualTo(-2.4f));
        for (int i = 0; i < playerWheels.Length; i++)
        {
            Assert.That(Quaternion.Angle(playerRest[i], playerWheels[i].localRotation), Is.EqualTo(90f).Within(0.01f));
            Assert.That(Quaternion.Angle(opponentRest[i], opponentWheels[i].localRotation), Is.EqualTo(180f).Within(0.01f));
            Assert.That(playerWheels[i].GetComponent<Renderer>().bounds.size.x, Is.EqualTo(widths[i]).Within(0.01f),
                "Wheel rotation must preserve the axle width.");
        }

        controller.ResetRace();
        Assert.That(player.position.z, Is.EqualTo(0f));
        Assert.That(opponent.position.z, Is.EqualTo(0f));
        for (int i = 0; i < playerWheels.Length; i++)
        {
            Assert.That(Quaternion.Angle(playerRest[i], playerWheels[i].localRotation), Is.LessThan(0.01f));
            Assert.That(Quaternion.Angle(opponentRest[i], opponentWheels[i].localRotation), Is.LessThan(0.01f));
        }

        Object.Destroy(controller.gameObject);
        yield return null;
        Assert.IsTrue(player == null && opponent == null, "Vehicle instances must be destroyed with their controller.");
    }

    [UnityTest]
    public IEnumerator CountdownRaceFinishAndRestartWorkInPlayMode()
    {
        GameObject host = new GameObject("Race Play Mode Test");
        RaceGameController controller = host.AddComponent<RaceGameController>();
        controller.config.finishDistance = 400f;
        controller.config.countdownSeconds = 0.5f;
        controller.config.raceTimeoutSeconds = 35f;
        yield return null;

        Assert.AreEqual(RacePhase.Preparing, controller.Phase);
        controller.BeginCountdown();
        Assert.AreEqual(RacePhase.Countdown, controller.Phase);

        float deadline = Time.realtimeSinceStartup + 32f;
        while (controller.Phase != RacePhase.Finished && Time.realtimeSinceStartup < deadline)
        {
            yield return null;
        }

        Assert.AreEqual(RacePhase.Finished, controller.Phase, "Both racers should resolve a finish or timeout.");
        Assert.IsTrue(controller.PlayerState.finished);
        Assert.IsTrue(controller.OpponentState.finished);
        Assert.That(controller.PlayerState.distance, Is.EqualTo(400f).Within(0.001f));
        Assert.That(controller.OpponentState.distance, Is.EqualTo(400f).Within(0.001f));

        controller.ResetRace();
        Assert.AreEqual(RacePhase.Preparing, controller.Phase);
        Assert.AreEqual(0f, controller.PlayerState.distance);
        Assert.AreEqual(1, controller.PlayerState.nitrousChargesRemaining);

        Object.Destroy(host);
    }

    private static Transform[] Wheels(Transform car)
    {
        return car.GetComponentsInChildren<Transform>(true).Where(t => t.name.StartsWith("Wheel_")).ToArray();
    }

    private static void AssertSedan(Transform car, float laneX, Color paintColor)
    {
        Assert.IsNotNull(car);
        Assert.That(car.position, Is.EqualTo(new Vector3(laneX, 0f, 0f)));
        Assert.That(car.localScale, Is.EqualTo(Vector3.one));
        Assert.That(Vector3.Dot(car.forward, Vector3.forward), Is.GreaterThan(0.999f));
        Assert.IsNull(car.Find("Cabin"), "The primitive placeholder must be replaced.");
        LODGroup group = car.GetComponent<LODGroup>();
        Assert.IsNotNull(group);
        LOD[] lods = group.GetLODs();
        Assert.AreEqual(3, lods.Length);
        Assert.That(lods[2].screenRelativeTransitionHeight, Is.LessThanOrEqualTo(0.0031f),
            "A racer must remain visible when the cars separate along the 400 metre track.");
        var block = new MaterialPropertyBlock();
        foreach (LOD lod in lods)
        {
            Assert.AreEqual(6, lod.renderers.Length);
            int paintSlots = 0;
            foreach (Renderer renderer in lod.renderers)
            {
                Assert.IsNotNull(renderer.GetComponent<MeshFilter>().sharedMesh);
                Material[] materials = renderer.sharedMaterials;
                for (int slot = 0; slot < materials.Length; slot++)
                {
                    Material material = materials[slot];
                    Assert.IsNotNull(material);
                    Assert.IsTrue(material.shader.isSupported);
                    block.Clear();
                    renderer.GetPropertyBlock(block, slot);
                    if (material.name == "Sedan01_SilverPaint")
                    {
                        paintSlots++;
                        Assert.That(Vector4.Distance(block.GetColor("_BaseColor"), paintColor), Is.LessThan(0.0001f));
                        Assert.That(material.GetColor("_BaseColor"), Is.Not.EqualTo(paintColor),
                            "Per-car paint must not modify the shared source material.");
                    }
                    else
                    {
                        Assert.IsTrue(block.isEmpty, "Only the paint material may be tinted.");
                    }
                }
            }
            Assert.Greater(paintSlots, 0, "Each LOD must receive the driver's paint colour.");
        }

        Renderer[] near = lods[0].renderers;
        Bounds bounds = near[0].bounds;
        foreach (Renderer renderer in near.Skip(1)) bounds.Encapsulate(renderer.bounds);
        Assert.That(bounds.min.y, Is.EqualTo(0f).Within(0.02f), "Tyres must meet the track surface.");
        Assert.That(bounds.size.z, Is.InRange(4.3f, 4.9f));
    }
}
