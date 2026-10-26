using UnityEngine;

public class RaceOpponentController
{
    public float targetLaunchRpm = 5300f;
    public float shiftRpm = 6550f;
    public float nitrousStartDistance = 130f;

    public bool ShouldHoldLaunch(RaceCarState state)
    {
        return state.engineRpm < targetLaunchRpm;
    }

    public bool ShouldHoldLaunch(VehicleSpec spec, RaceConfig config, RaceCarState state)
    {
        return state.engineRpm < spec.ResolveRpmSettings(config).launchTarget;
    }

    public DriverCommand GetCommand(VehicleSpec spec, RaceConfig config, RaceCarState state)
    {
        DriverCommand command = new DriverCommand();
        if (!state.started || state.finished)
        {
            return command;
        }

        RpmOperatingWindow window = spec.ResolveRpmSettings(config);
        command.shiftPressed = state.gear < spec.gearRatios.Count && state.engineRpm >= (window.shiftMin + window.shiftMax) * 0.5f;
        command.nitrousPressed = state.nitrousChargesRemaining > 0 && state.distance >= nitrousStartDistance;
        return command;
    }
}
