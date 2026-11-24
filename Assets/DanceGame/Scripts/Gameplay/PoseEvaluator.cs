using System;
using UnityEngine;

public struct PoseEvaluation
{
    public bool tracked;
    public PoseGrade grade;
    public float accuracy;
}

// Pure calculations. Coordinates are normalized within the Kinect image,
// independent of where the frontend displays the player panel.
public static class PoseEvaluator
{
    public static PoseEvaluation Evaluate(PoseDefinition pose, Vector2[] positions, byte[] states)
    {
        string reason;
        if (pose == null || !pose.IsValid(out reason) || positions == null || states == null
            || positions.Length < 20 || states.Length < 20) return default(PoseEvaluation);
        double worstError = 0;
        if (pose.joints != null)
            foreach (JointTarget target in pose.joints)
            {
                int index = (int)target.joint;
                if (!Tracked(index, positions, states)) return default(PoseEvaluation);
                worstError = Math.Max(worstError, PositionError(positions[index], target.center, target.radii));
            }
        if (pose.bones != null)
            foreach (BoneTarget target in pose.bones)
            {
                int a = (int)target.startJoint, b = (int)target.endJoint;
                if (!Tracked(a, positions, states) || !Tracked(b, positions, states))
                    return default(PoseEvaluation);
                worstError = Math.Max(worstError, PositionError(positions[a], target.start, target.endpointRadii));
                worstError = Math.Max(worstError, PositionError(positions[b], target.end, target.endpointRadii));
                // Correct for the 640x480 source aspect ratio when comparing angles.
                double ax = (positions[b].x - positions[a].x) * 640;
                double ay = (positions[b].y - positions[a].y) * 480;
                double bx = (target.end.x - target.start.x) * 640;
                double by = (target.end.y - target.start.y) * 480;
                double lengths = Math.Sqrt((ax * ax + ay * ay) * (bx * bx + by * by));
                if (lengths < 0.000001)
                    return new PoseEvaluation { tracked = true, grade = PoseGrade.Miss };
                double cosine = Math.Max(-1, Math.Min(1, (ax * bx + ay * by) / lengths));
                double angle = Math.Acos(cosine) * 180 / Math.PI;
                worstError = Math.Max(worstError, angle / target.angleToleranceDegrees);
            }
        return new PoseEvaluation
        {
            tracked = true,
            grade = worstError <= pose.perfectToleranceScale ? PoseGrade.Perfect
                : worstError <= 1 ? PoseGrade.Good : PoseGrade.Miss,
            accuracy = (float)Math.Max(0, 1 - worstError)
        };
    }

    private static bool Tracked(int index, Vector2[] positions, byte[] states)
    {
        Vector2 point = positions[index];
        return states[index] == 2 && !float.IsNaN(point.x) && !float.IsInfinity(point.x)
            && !float.IsNaN(point.y) && !float.IsInfinity(point.y);
    }

    private static double PositionError(Vector2 point, Vector2 center, Vector2 radii)
    {
        double x = (point.x - center.x) / radii.x, y = (point.y - center.y) / radii.y;
        return Math.Sqrt(x * x + y * y);
    }
}

// Only continuous, fresh samples count toward the hold.
public class PoseHoldTracker
{
    public float HeldSeconds { get; private set; }
    public float PerfectSeconds { get; private set; }
    public PoseGrade Result { get; private set; }
    private bool previousMatch;
    private bool previousPerfect;

    public void Reset()
    {
        HeldSeconds = PerfectSeconds = 0;
        Result = PoseGrade.None;
        previousMatch = previousPerfect = false;
    }

    public PoseGrade Observe(PoseEvaluation sample, float elapsedSeconds, float requiredSeconds)
    {
        if (Result != PoseGrade.None) return Result;
        if (elapsedSeconds < 0 || elapsedSeconds > 0.3f || float.IsNaN(elapsedSeconds)
            || !sample.tracked || sample.grade == PoseGrade.Miss || sample.grade == PoseGrade.None)
        { Reset(); return Result; }
        bool perfect = sample.grade == PoseGrade.Perfect;
        if (previousMatch) HeldSeconds += elapsedSeconds;
        PerfectSeconds = perfect && previousPerfect ? PerfectSeconds + elapsedSeconds : 0;
        previousMatch = true;
        previousPerfect = perfect;
        if (HeldSeconds >= requiredSeconds)
            Result = PerfectSeconds >= requiredSeconds ? PoseGrade.Perfect : PoseGrade.Good;
        return Result;
    }
}
