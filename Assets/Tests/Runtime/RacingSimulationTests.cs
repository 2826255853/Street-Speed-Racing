using NUnit.Framework;
using UnityEngine;
using UnityEditor.SceneManagement;

public class RacingSimulationTests
{
    private VehicleSpec spec;
    private RaceConfig config;
    private RaceCarState state;

    [SetUp]
    public void SetUp()
    {
        spec = new VehicleSpec();
        config = new RaceConfig();
        state = new RaceCarState();
        state.Reset(spec);
    }

    [Test]
    public void LaunchRpmIncreasesWhileHoldingSpace()
    {
        float before = state.engineRpm;
        RaceCarSimulator.UpdateLaunchRpm(spec, config, state, true, 0.5f);
        Assert.Greater(state.engineRpm, before);
    }

    [Test]
    public void LaunchInBestRangeGetsFullQuality()
    {
        state.engineRpm = config.launchRpmTarget;
        RaceCarSimulator.BeginRace(spec, config, state);
        Assert.That(state.launchQuality, Is.EqualTo(1f).Within(0.001f));
    }

    [Test]
    public void ShiftQualityAndGearAreUpdated()
    {
        state.engineRpm = 6300f;
        RaceCarSimulator.BeginRace(spec, config, state);
        bool shifted = RaceCarSimulator.TryShift(spec, config, state);
        Assert.IsTrue(shifted);
        Assert.AreEqual(2, state.gear);
        Assert.AreEqual(ShiftQuality.Good, state.lastShiftQuality);
    }

    [Test]
    public void EarlyShiftAppliesPowerPenalty()
    {
        state.engineRpm = 4200f;
        RaceCarSimulator.BeginRace(spec, config, state);
        Assert.IsTrue(RaceCarSimulator.TryShift(spec, config, state));
        Assert.AreEqual(ShiftQuality.TooEarly, state.lastShiftQuality);
        Assert.That(state.shiftPenalty, Is.EqualTo(config.earlyShiftPenalty).Within(0.001f));
    }

    [Test]
    public void DownshiftRecoversFromEarlyUpshift()
    {
        state.engineRpm = 4200f;
        RaceCarSimulator.BeginRace(spec, config, state);
        Assert.IsTrue(RaceCarSimulator.TryShift(spec, config, state));
        state.shiftCooldown = 0f;
        state.speed = 12f;
        state.distance = 25f;

        Assert.IsTrue(RaceCarSimulator.TryDownshift(spec, state));

        Assert.AreEqual(1, state.gear);
        Assert.That(state.engineRpm, Is.EqualTo(4200f).Within(0.001f));
        Assert.AreEqual(1f, state.shiftPenalty);
        Assert.AreEqual(ShiftQuality.Downshift, state.lastShiftQuality);
        Assert.AreEqual(spec.shiftDuration, state.shiftCooldown);
        Assert.AreEqual(12f, state.speed);
        Assert.AreEqual(25f, state.distance);
    }

    [TestCase(2)]
    [TestCase(3)]
    [TestCase(6)]
    public void DownshiftLowersExactlyOneGear(int gear)
    {
        RaceCarSimulator.BeginRace(spec, config, state);
        state.gear = gear;
        state.engineRpm = 3000f;
        float expectedRpm = state.engineRpm * spec.GetRatio(gear - 1) / spec.GetRatio(gear);

        Assert.IsTrue(RaceCarSimulator.TryDownshift(spec, state));

        Assert.AreEqual(gear - 1, state.gear);
        Assert.That(state.engineRpm, Is.EqualTo(expectedRpm).Within(0.001f));
    }

    [TestCase(false, false, 2, 0f)]
    [TestCase(true, true, 2, 0f)]
    [TestCase(true, false, 1, 0f)]
    [TestCase(true, false, 2, 0.1f)]
    public void DownshiftIsRejectedWithoutChangingState(bool started, bool finished, int gear, float cooldown)
    {
        state.started = started;
        state.finished = finished;
        state.gear = gear;
        state.engineRpm = 3000f;
        state.shiftCooldown = cooldown;
        state.shiftPenalty = config.earlyShiftPenalty;
        state.lastShiftQuality = ShiftQuality.TooEarly;

        Assert.IsFalse(RaceCarSimulator.TryDownshift(spec, state));

        Assert.AreEqual(gear, state.gear);
        Assert.AreEqual(3000f, state.engineRpm);
        Assert.AreEqual(cooldown, state.shiftCooldown);
        Assert.AreEqual(config.earlyShiftPenalty, state.shiftPenalty);
        Assert.AreEqual(ShiftQuality.TooEarly, state.lastShiftQuality);
    }

    [Test]
    public void DownshiftCapsRpmAtRedline()
    {
        RaceCarSimulator.BeginRace(spec, config, state);
        state.gear = 2;
        state.engineRpm = 6000f;

        Assert.IsTrue(RaceCarSimulator.TryDownshift(spec, state));

        Assert.AreEqual(1, state.gear);
        Assert.AreEqual(spec.redlineRpm, state.engineRpm);
    }

    [Test]
    public void DownshiftCommandUsesCooldownForBothDirections()
    {
        RaceCarSimulator.BeginRace(spec, config, state);
        state.gear = 3;
        DriverCommand downshift = new DriverCommand { downshiftPressed = true };
        DriverCommand upshift = new DriverCommand { shiftPressed = true };

        RaceCarSimulator.Step(spec, config, state, downshift, config.fixedStep, 0f, out _);
        Assert.AreEqual(2, state.gear);
        Assert.IsFalse(RaceCarSimulator.TryDownshift(spec, state));
        Assert.IsFalse(RaceCarSimulator.TryShift(spec, config, state));

        RaceCarSimulator.Step(spec, config, state, upshift, config.fixedStep, config.fixedStep, out _);
        Assert.AreEqual(2, state.gear);

        RaceCarSimulator.Step(spec, config, state, upshift, spec.shiftDuration, 2f * config.fixedStep, out _);
        Assert.AreEqual(3, state.gear);
        Assert.IsFalse(RaceCarSimulator.TryDownshift(spec, state));
    }

    [TestCase(1)]
    [TestCase(3)]
    [TestCase(6)]
    public void SimultaneousShiftCommandsDoNotChangeGear(int gear)
    {
        spec.shiftDuration = 0f;
        RaceCarSimulator.BeginRace(spec, config, state);
        state.gear = gear;
        DriverCommand command = new DriverCommand { shiftPressed = true, downshiftPressed = true };

        RaceCarSimulator.Step(spec, config, state, command, config.fixedStep, 0f, out _);

        Assert.AreEqual(gear, state.gear);
        Assert.AreEqual(1f, state.shiftPenalty);
        Assert.AreEqual(0f, state.shiftCooldown);
        Assert.AreEqual(ShiftQuality.Good, state.lastShiftQuality);
    }

    [Test]
    public void AccurateLaunchProducesMoreEarlyDistance()
    {
        RaceCarState accurate = new RaceCarState();
        RaceCarState poor = new RaceCarState();
        accurate.Reset(spec);
        poor.Reset(spec);
        accurate.engineRpm = config.launchRpmTarget;
        poor.engineRpm = config.launchRpmMin - 500f;
        RaceCarSimulator.BeginRace(spec, config, accurate);
        RaceCarSimulator.BeginRace(spec, config, poor);

        for (int i = 0; i < 30; i++)
        {
            RaceCarSimulator.Step(spec, config, accurate, new DriverCommand(), config.fixedStep, i * config.fixedStep, out _);
            RaceCarSimulator.Step(spec, config, poor, new DriverCommand(), config.fixedStep, i * config.fixedStep, out _);
        }

        Assert.Greater(accurate.distance, poor.distance);
    }

    [Test]
    public void NitrousCanOnlyBeConsumedOnce()
    {
        RaceCarSimulator.BeginRace(spec, config, state);
        Assert.IsTrue(RaceCarSimulator.TryActivateNitrous(spec, state));
        Assert.IsFalse(RaceCarSimulator.TryActivateNitrous(spec, state));
        Assert.AreEqual(0, state.nitrousChargesRemaining);
    }

    [Test]
    public void NitrousExpiresAfterItsConfiguredDuration()
    {
        spec.nitrousDuration = 0.1f;
        RaceCarSimulator.BeginRace(spec, config, state);
        Assert.IsTrue(RaceCarSimulator.TryActivateNitrous(spec, state));
        for (int i = 0; i < 8; i++)
        {
            RaceCarSimulator.Step(spec, config, state, new DriverCommand(), config.fixedStep, i * config.fixedStep, out _);
        }
        Assert.IsFalse(state.NitrousActive);
        Assert.AreEqual(0, state.nitrousChargesRemaining);
    }

    [Test]
    public void SimulationMovesCarAndReportsFinishTime()
    {
        config.finishDistance = 12f;
        state.engineRpm = config.launchRpmTarget;
        RaceCarSimulator.BeginRace(spec, config, state);
        bool crossed = false;
        float time = 0f;
        for (int i = 0; i < 2000 && !crossed; i++)
        {
            crossed = RaceCarSimulator.Step(spec, config, state, new DriverCommand(), config.fixedStep, time, out _);
            time += config.fixedStep;
        }

        Assert.Greater(state.distance, 0f);
        Assert.IsTrue(crossed);
        Assert.That(state.finishTime, Is.GreaterThan(0f));
    }

    [Test]
    public void FinishTimesWithinOneMillisecondAreTied()
    {
        RaceCarState player = new RaceCarState { finishTime = 10f };
        RaceCarState opponent = new RaceCarState { finishTime = 10.0005f };
        Assert.AreEqual(RaceOutcome.Draw, RaceCarSimulator.ResolveOutcome(player, opponent));
    }

    [Test]
    public void FasterFinishingCarWins()
    {
        RaceCarState player = new RaceCarState { finishTime = 9.5f };
        RaceCarState opponent = new RaceCarState { finishTime = 10f };
        Assert.AreEqual(RaceOutcome.PlayerWin, RaceCarSimulator.ResolveOutcome(player, opponent));
    }

    [Test]
    public void ResetClearsRaceProgressAndNitrous()
    {
        state.distance = 120f;
        state.speed = 52f;
        state.started = true;
        state.nitrousChargesRemaining = 0;
        state.finishTime = 11f;
        state.Reset(spec);
        Assert.AreEqual(0f, state.distance);
        Assert.AreEqual(0f, state.speed);
        Assert.AreEqual(1, state.nitrousChargesRemaining);
        Assert.AreEqual(-1f, state.finishTime);
        Assert.IsFalse(state.started);
    }

    [Test]
    public void RacePrototypeSceneContainsController()
    {
        var scene = EditorSceneManager.OpenScene("Assets/Scenes/RacePrototype.unity", OpenSceneMode.Single);
        Assert.IsTrue(scene.IsValid());
        RaceGameController controller = null;
        foreach (GameObject root in scene.GetRootGameObjects())
        {
            controller = root.GetComponent<RaceGameController>();
            if (controller != null)
            {
                break;
            }
        }
        Assert.IsNotNull(controller);
    }
}
