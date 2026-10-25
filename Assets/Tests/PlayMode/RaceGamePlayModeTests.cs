using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

public class RaceGamePlayModeTests
{
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
}
