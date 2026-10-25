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

    public DriverCommand GetCommand(VehicleSpec spec, RaceConfig config, RaceCarState state)
    {
        DriverCommand command = new DriverCommand();
        if (!state.started || state.finished)
        {
            return command;
        }

        command.shiftPressed = state.gear < spec.gearRatios.Count && state.engineRpm >= shiftRpm;
        command.nitrousPressed = state.nitrousChargesRemaining > 0 && state.distance >= nitrousStartDistance;
        return command;
    }
}
