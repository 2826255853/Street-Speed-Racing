using NUnit.Framework;

public class VehicleRpmSettingsTests
{
    [Test]
    public void ExistingVehiclesUseRaceConfigAndCustomVehiclesOverrideIt()
    {
        var spec = new VehicleSpec();
        var config = new RaceConfig { launchRpmMin = 3200, launchRpmMax = 4500, launchRpmTarget = 4100, bestShiftRpmMin = 5000, bestShiftRpmMax = 6200 };
        RpmOperatingWindow fallback = spec.ResolveRpmSettings(config);
        Assert.AreEqual(3200, fallback.launchMin);
        Assert.AreEqual(4500, fallback.launchMax);
        Assert.AreEqual(4100, fallback.launchTarget);
        Assert.AreEqual(5000, fallback.shiftMin);
        Assert.AreEqual(6200, fallback.shiftMax);
        spec.useCustomRpmSettings = true;
        RpmOperatingWindow custom = spec.ResolveRpmSettings(config);
        Assert.AreEqual(spec.launchRpmMin, custom.launchMin);
        Assert.AreEqual(spec.launchRpmMax, custom.launchMax);
        Assert.AreEqual(spec.launchRpmTarget, custom.launchTarget);
        Assert.AreEqual(spec.bestShiftRpmMin, custom.shiftMin);
        Assert.AreEqual(spec.bestShiftRpmMax, custom.shiftMax);
    }

    [Test]
    public void RangesAreLimitedToVehicleEnvelopeAndReversedRangesCollapse()
    {
        var spec = new VehicleSpec { idleRpm = 1000, redlineRpm = 4000, useCustomRpmSettings = true,
            launchRpmMin = -50, launchRpmMax = 9000, launchRpmTarget = 8000,
            bestShiftRpmMin = 3800, bestShiftRpmMax = 2000 };
        RpmOperatingWindow range = spec.ResolveRpmSettings(new RaceConfig());
        Assert.AreEqual(1000, range.launchMin);
        Assert.AreEqual(4000, range.launchMax);
        Assert.AreEqual(4000, range.launchTarget);
        Assert.AreEqual(3800, range.shiftMin);
        Assert.AreEqual(3800, range.shiftMax);
        Assert.AreEqual(ShiftQuality.Good, RaceCarSimulator.EvaluateShift(3800, spec, new RaceConfig()));
    }

    [Test]
    public void SimulatorAndOpponentUseResolvedVehicleRanges()
    {
        var spec = new VehicleSpec { useCustomRpmSettings = true, launchRpmMin = 2000, launchRpmMax = 3000,
            launchRpmTarget = 2700, bestShiftRpmMin = 3500, bestShiftRpmMax = 4500 };
        var config = new RaceConfig();
        var state = new RaceCarState();
        state.Reset(spec);
        var opponent = new RaceOpponentController();
        Assert.IsTrue(opponent.ShouldHoldLaunch(spec, config, state));
        state.engineRpm = 2700;
        Assert.IsFalse(opponent.ShouldHoldLaunch(spec, config, state));
        RaceCarSimulator.BeginRace(spec, config, state);
        Assert.AreEqual(1f, state.launchQuality);
        Assert.AreEqual(ShiftQuality.TooEarly, RaceCarSimulator.EvaluateShift(3499, spec, config));
        Assert.AreEqual(ShiftQuality.Good, RaceCarSimulator.EvaluateShift(3500, spec, config));
        Assert.AreEqual(ShiftQuality.Good, RaceCarSimulator.EvaluateShift(4500, spec, config));
        Assert.AreEqual(ShiftQuality.TooLate, RaceCarSimulator.EvaluateShift(4501, spec, config));
        state.engineRpm = 4000;
        Assert.IsTrue(opponent.GetCommand(spec, config, state).shiftPressed);
        Assert.IsTrue(RaceCarSimulator.TryShift(spec, config, state));
        Assert.AreEqual(ShiftQuality.Good, state.lastShiftQuality);
        Assert.AreEqual(1, state.shiftSequence);
        state.Reset(spec);
        Assert.AreEqual(0, state.shiftSequence);
    }

    [Test]
    public void LegacyEvaluationEntryPointsRemainAvailable()
    {
        var config = new RaceConfig();
        Assert.AreEqual(1f, RaceCarSimulator.EvaluateLaunchQuality(config.launchRpmTarget, config));
        Assert.AreEqual(ShiftQuality.Good, RaceCarSimulator.EvaluateShift(6300, config));
    }
}
