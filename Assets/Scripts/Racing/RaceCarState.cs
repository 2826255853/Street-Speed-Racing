using System;
using UnityEngine;

[Serializable]
public class RaceCarState
{
    public float distance;
    public float speed;
    public float engineRpm;
    public int gear = 1;
    public bool started;
    public bool finished;
    public float finishTime = -1f;
    public float launchQuality;
    public ShiftQuality lastShiftQuality = ShiftQuality.Good;
    public int shiftSequence;
    public float shiftCooldown;
    public float shiftPenalty = 1f;
    public float launchBoostTime;
    public int nitrousChargesRemaining = 1;
    public float nitrousTimeRemaining;

    public bool NitrousActive
    {
        get { return nitrousTimeRemaining > 0f; }
    }

    public void Reset(VehicleSpec spec)
    {
        spec.EnsureValid();
        distance = 0f;
        speed = 0f;
        engineRpm = spec.idleRpm;
        gear = 1;
        started = false;
        finished = false;
        finishTime = -1f;
        launchQuality = 0f;
        lastShiftQuality = ShiftQuality.Good;
        shiftSequence = 0;
        shiftCooldown = 0f;
        shiftPenalty = 1f;
        launchBoostTime = 0f;
        nitrousChargesRemaining = 1;
        nitrousTimeRemaining = 0f;
    }
}

public static class RaceCarSimulator
{
    private const float AirDensity = 1.225f;
    private const float Gravity = 9.81f;

    public static void UpdateLaunchRpm(VehicleSpec spec, RaceConfig config, RaceCarState state, bool launchHeld, float deltaTime)
    {
        spec.EnsureValid();
        config.EnsureValid();
        if (state.started || state.finished)
        {
            return;
        }

        float direction = launchHeld ? 1f : -1f;
        float rate = launchHeld ? 3600f : 2800f;
        state.engineRpm = Mathf.Clamp(state.engineRpm + direction * rate * Mathf.Max(0f, deltaTime), spec.idleRpm, spec.redlineRpm);
    }

    public static float EvaluateLaunchQuality(float rpm, RaceConfig config)
    {
        config.EnsureValid();
        return EvaluateLaunchQuality(rpm, config.launchRpmMin, config.launchRpmMax, config.launchRpmTarget);
    }

    public static float EvaluateLaunchQuality(float rpm, VehicleSpec spec, RaceConfig config)
    {
        RpmOperatingWindow window = spec.ResolveRpmSettings(config);
        return EvaluateLaunchQuality(rpm, window.launchMin, window.launchMax, window.launchTarget);
    }

    private static float EvaluateLaunchQuality(float rpm, float min, float max, float target)
    {
        if (rpm < min || rpm > max)
        {
            return 0f;
        }

        float sideRange = rpm < target ? target - min : max - target;
        if (sideRange <= 0f)
        {
            return 1f;
        }

        float distanceFromTarget = Mathf.Abs(rpm - target);
        return Mathf.Clamp01(1f - distanceFromTarget / sideRange);
    }

    public static void BeginRace(VehicleSpec spec, RaceConfig config, RaceCarState state)
    {
        spec.EnsureValid();
        config.EnsureValid();
        if (state.started)
        {
            return;
        }

        state.started = true;
        state.launchQuality = EvaluateLaunchQuality(state.engineRpm, spec, config);
        state.launchBoostTime = config.launchBoostDuration;
        state.speed = 0f;
        state.gear = 1;
    }

    public static ShiftQuality EvaluateShift(float rpm, RaceConfig config)
    {
        config.EnsureValid();
        return EvaluateShift(rpm, config.bestShiftRpmMin, config.bestShiftRpmMax);
    }

    public static ShiftQuality EvaluateShift(float rpm, VehicleSpec spec, RaceConfig config)
    {
        RpmOperatingWindow window = spec.ResolveRpmSettings(config);
        return EvaluateShift(rpm, window.shiftMin, window.shiftMax);
    }

    private static ShiftQuality EvaluateShift(float rpm, float min, float max)
    {
        if (rpm < min)
        {
            return ShiftQuality.TooEarly;
        }

        if (rpm > max)
        {
            return ShiftQuality.TooLate;
        }

        return ShiftQuality.Good;
    }

    public static bool TryShift(VehicleSpec spec, RaceConfig config, RaceCarState state)
    {
        spec.EnsureValid();
        config.EnsureValid();
        if (!state.started || state.finished || state.shiftCooldown > 0f || state.gear >= spec.gearRatios.Count)
        {
            return false;
        }

        float oldRatio = spec.GetRatio(state.gear);
        state.lastShiftQuality = EvaluateShift(state.engineRpm, spec, config);
        state.shiftSequence++;
        switch (state.lastShiftQuality)
        {
            case ShiftQuality.TooEarly:
                state.shiftPenalty = config.earlyShiftPenalty;
                break;
            case ShiftQuality.TooLate:
                state.shiftPenalty = config.lateShiftPenalty;
                break;
            default:
                state.shiftPenalty = 1f;
                break;
        }

        state.gear++;
        float newRatio = spec.GetRatio(state.gear);
        state.engineRpm = Mathf.Clamp(state.engineRpm * newRatio / oldRatio, spec.idleRpm, spec.redlineRpm);
        state.shiftCooldown = spec.shiftDuration;
        return true;
    }

    public static bool TryDownshift(VehicleSpec spec, RaceCarState state)
    {
        spec.EnsureValid();
        if (!state.started || state.finished || state.shiftCooldown > 0f || state.gear <= 1)
        {
            return false;
        }

        float oldRatio = spec.GetRatio(state.gear);
        state.gear--;
        float newRatio = spec.GetRatio(state.gear);
        state.engineRpm = Mathf.Clamp(state.engineRpm * newRatio / oldRatio, spec.idleRpm, spec.redlineRpm);
        state.lastShiftQuality = ShiftQuality.Downshift;
        state.shiftSequence++;
        state.shiftPenalty = 1f;
        state.shiftCooldown = spec.shiftDuration;
        return true;
    }

    public static bool TryActivateNitrous(VehicleSpec spec, RaceCarState state)
    {
        spec.EnsureValid();
        if (!state.started || state.finished || state.nitrousChargesRemaining <= 0 || state.NitrousActive || spec.nitrousDuration <= 0f)
        {
            return false;
        }

        state.nitrousChargesRemaining--;
        state.nitrousTimeRemaining = spec.nitrousDuration;
        return true;
    }

    public static bool Step(VehicleSpec spec, RaceConfig config, RaceCarState state, DriverCommand command, float deltaTime, float raceTime, out float crossingTime)
    {
        spec.EnsureValid();
        config.EnsureValid();
        crossingTime = -1f;
        if (!state.started || state.finished || deltaTime <= 0f)
        {
            return false;
        }

        if (state.shiftCooldown > 0f)
        {
            state.shiftCooldown = Mathf.Max(0f, state.shiftCooldown - deltaTime);
        }

        if (command.shiftPressed && !command.downshiftPressed)
        {
            TryShift(spec, config, state);
        }
        else if (command.downshiftPressed && !command.shiftPressed)
        {
            TryDownshift(spec, state);
        }

        if (command.nitrousPressed)
        {
            TryActivateNitrous(spec, state);
        }

        float previousDistance = state.distance;
        float ratio = spec.GetRatio(state.gear);
        float wheelCircumference = 2f * Mathf.PI * spec.wheelRadius;
        float wheelRpm = state.speed / wheelCircumference * 60f;
        float rpmTarget = Mathf.Clamp(Mathf.Max(spec.idleRpm, wheelRpm * ratio * spec.finalDrive + 850f), spec.idleRpm, spec.redlineRpm);
        state.engineRpm = Mathf.MoveTowards(state.engineRpm, rpmTarget, spec.rpmResponse * deltaTime);

        float normalizedRpm = Mathf.InverseLerp(spec.idleRpm, spec.redlineRpm, state.engineRpm);
        float torqueFactor = Mathf.Max(0.1f, spec.torqueCurve.Evaluate(normalizedRpm));
        float driveForce = spec.peakTorque * torqueFactor * ratio * spec.finalDrive * spec.drivelineEfficiency / spec.wheelRadius;
        float tractionLimit = spec.mass * Gravity * spec.gripCoefficient;
        driveForce = Mathf.Min(driveForce, tractionLimit);
        if (state.launchBoostTime > 0f)
        {
            float launchMultiplier = Mathf.Lerp(config.launchGripPenalty, 1f, state.launchQuality);
            driveForce *= launchMultiplier;
            state.launchBoostTime = Mathf.Max(0f, state.launchBoostTime - deltaTime);
        }

        driveForce *= state.shiftPenalty;
        state.shiftPenalty = Mathf.MoveTowards(state.shiftPenalty, 1f, deltaTime * 1.8f);
        float dragForce = 0.5f * AirDensity * spec.dragCoefficient * spec.frontalArea * state.speed * state.speed;
        float rollingForce = spec.rollingResistance;
        float nitrousForce = 0f;
        if (state.NitrousActive)
        {
            nitrousForce = spec.nitrousForce;
            state.nitrousTimeRemaining = Mathf.Max(0f, state.nitrousTimeRemaining - deltaTime);
        }

        float acceleration = (driveForce + nitrousForce - dragForce - rollingForce) / spec.mass;
        state.speed = Mathf.Max(0f, state.speed + acceleration * deltaTime);
        state.distance += state.speed * deltaTime;

        if (state.distance >= config.finishDistance)
        {
            float distanceTravelled = state.distance - previousDistance;
            float fraction = distanceTravelled > 0f
                ? Mathf.Clamp01((config.finishDistance - previousDistance) / distanceTravelled)
                : 1f;
            crossingTime = raceTime + fraction * deltaTime;
            state.distance = config.finishDistance;
            state.finished = true;
            state.finishTime = crossingTime;
            return true;
        }

        return false;
    }

    public static RaceOutcome ResolveOutcome(RaceCarState player, RaceCarState opponent, float tieTolerance = 0.001f)
    {
        float difference = Mathf.Abs(player.finishTime - opponent.finishTime);
        if (difference <= Mathf.Max(0f, tieTolerance))
        {
            return RaceOutcome.Draw;
        }

        return player.finishTime < opponent.finishTime ? RaceOutcome.PlayerWin : RaceOutcome.OpponentWin;
    }
}
