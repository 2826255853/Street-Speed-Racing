using System;
using System.Collections.Generic;
using UnityEngine;

public enum RacePhase
{
    Preparing,
    Countdown,
    Racing,
    Finished
}

public enum ShiftQuality
{
    TooEarly,
    Good,
    TooLate,
    Downshift
}

[Serializable]
public struct DriverCommand
{
    public bool launchHeld;
    public bool shiftPressed;
    public bool downshiftPressed;
    public bool nitrousPressed;
}

[Serializable]
public class VehicleSpec
{
    public string displayName = "Street Prototype";
    public float mass = 1200f;
    public float peakTorque = 420f;
    public float idleRpm = 900f;
    public float redlineRpm = 7600f;
    public float rpmResponse = 4200f;
    public float finalDrive = 3.55f;
    public float wheelRadius = 0.34f;
    public float rollingResistance = 145f;
    public float dragCoefficient = 0.42f;
    public float frontalArea = 2.1f;
    public float drivelineEfficiency = 0.86f;
    public float gripCoefficient = 1.2f;
    public float shiftDuration = 0.18f;
    public float nitrousForce = 3200f;
    public float nitrousDuration = 1.5f;
    public AnimationCurve torqueCurve = new AnimationCurve(
        new Keyframe(0f, 0.72f),
        new Keyframe(0.68f, 1f),
        new Keyframe(1f, 0.72f));
    public List<float> gearRatios = new List<float> { 3.10f, 2.15f, 1.55f, 1.20f, 0.95f, 0.78f };

    public void EnsureValid()
    {
        mass = Mathf.Max(1f, mass);
        peakTorque = Mathf.Max(1f, peakTorque);
        idleRpm = Mathf.Max(100f, idleRpm);
        redlineRpm = Mathf.Max(idleRpm + 500f, redlineRpm);
        finalDrive = Mathf.Max(0.1f, finalDrive);
        wheelRadius = Mathf.Max(0.05f, wheelRadius);
        drivelineEfficiency = Mathf.Clamp01(drivelineEfficiency);
        gripCoefficient = Mathf.Max(0.1f, gripCoefficient);
        shiftDuration = Mathf.Max(0f, shiftDuration);
        nitrousDuration = Mathf.Max(0f, nitrousDuration);
        if (torqueCurve == null || torqueCurve.length == 0)
        {
            torqueCurve = new AnimationCurve(new Keyframe(0f, 0.72f), new Keyframe(0.68f, 1f), new Keyframe(1f, 0.72f));
        }

        if (gearRatios == null || gearRatios.Count == 0)
        {
            gearRatios = new List<float> { 3.10f, 2.15f, 1.55f, 1.20f, 0.95f, 0.78f };
        }

        for (int i = 0; i < gearRatios.Count; i++)
        {
            gearRatios[i] = Mathf.Max(0.1f, gearRatios[i]);
        }
    }

    public float GetRatio(int gear)
    {
        EnsureValid();
        int index = Mathf.Clamp(gear - 1, 0, gearRatios.Count - 1);
        return gearRatios[index];
    }
}

public enum RaceOutcome
{
    PlayerWin,
    OpponentWin,
    Draw
}

[Serializable]
public class RaceConfig
{
    public float finishDistance = 400f;
    public float countdownSeconds = 3f;
    public float fixedStep = 0.02f;
    public float raceTimeoutSeconds = 30f;
    public float launchRpmMin = 4300f;
    public float launchRpmMax = 6000f;
    public float launchRpmTarget = 5300f;
    public float bestShiftRpmMin = 5600f;
    public float bestShiftRpmMax = 7000f;
    public float launchGripPenalty = 0.65f;
    public float earlyShiftPenalty = 0.72f;
    public float lateShiftPenalty = 0.78f;
    public float launchBoostDuration = 0.65f;

    public void EnsureValid()
    {
        finishDistance = Mathf.Max(1f, finishDistance);
        countdownSeconds = Mathf.Max(0.5f, countdownSeconds);
        fixedStep = Mathf.Clamp(fixedStep, 0.005f, 0.1f);
        raceTimeoutSeconds = Mathf.Max(1f, raceTimeoutSeconds);
        launchRpmMin = Mathf.Clamp(launchRpmMin, 0f, 20000f);
        launchRpmMax = Mathf.Max(launchRpmMin + 1f, launchRpmMax);
        launchRpmTarget = Mathf.Clamp(launchRpmTarget, launchRpmMin, launchRpmMax);
        bestShiftRpmMin = Mathf.Max(0f, bestShiftRpmMin);
        bestShiftRpmMax = Mathf.Max(bestShiftRpmMin + 1f, bestShiftRpmMax);
        launchGripPenalty = Mathf.Clamp01(launchGripPenalty);
        earlyShiftPenalty = Mathf.Clamp01(earlyShiftPenalty);
        lateShiftPenalty = Mathf.Clamp01(lateShiftPenalty);
        launchBoostDuration = Mathf.Max(0f, launchBoostDuration);
    }
}
