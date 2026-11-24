using System;
using UnityEngine;

// Matches Kinect SDK 1.8 indices without referencing its DLL in Unity.
public enum DanceJoint
{
    HipCenter, Spine, ShoulderCenter, Head,
    ShoulderLeft, ElbowLeft, WristLeft, HandLeft,
    ShoulderRight, ElbowRight, WristRight, HandRight,
    HipLeft, KneeLeft, AnkleLeft, FootLeft,
    HipRight, KneeRight, AnkleRight, FootRight
}

public enum PoseGrade { None, Miss, Good, Perfect }

[Serializable]
public class JointTarget
{
    public DanceJoint joint = DanceJoint.HandRight;
    public Vector2 center = new Vector2(0.7f, 0.6f);
    public Vector2 radii = new Vector2(0.12f, 0.16f);
}

[Serializable]
public class BoneTarget
{
    public DanceJoint startJoint = DanceJoint.ElbowRight;
    public DanceJoint endJoint = DanceJoint.WristRight;
    public Vector2 start = new Vector2(0.55f, 0.6f);
    public Vector2 end = new Vector2(0.8f, 0.6f);
    public Vector2 endpointRadii = new Vector2(0.15f, 0.2f);
    public float angleToleranceDegrees = 25f;
}

[Serializable]
public class PoseDefinition
{
    public string id = "pose-01";
    public string displayName = "Right hand";
    public int imageIndex;
    public float durationSeconds = 6f;
    public float holdSeconds = 0.5f;
    // Perfect uses this fraction of each target's Good tolerance.
    public float perfectToleranceScale = 0.5f;
    public JointTarget[] joints = new JointTarget[0];
    public BoneTarget[] bones = new BoneTarget[0];

    public bool IsValid(out string reason)
    {
        reason = null;
        if (string.IsNullOrWhiteSpace(id)) reason = "Pose ID is required.";
        else if (!Positive(durationSeconds) || !Positive(holdSeconds) || holdSeconds > durationSeconds)
            reason = "Duration and hold time must be positive; hold must fit within duration.";
        else if (!Positive(perfectToleranceScale) || perfectToleranceScale > 1f)
            reason = "Perfect tolerance scale must be between 0 and 1.";
        else if ((joints == null || joints.Length == 0) && (bones == null || bones.Length == 0))
            reason = "At least one joint or bone target is required.";
        if (reason != null) return false;
        if (joints != null)
            foreach (JointTarget target in joints)
                if (target == null || !ValidJoint(target.joint) || !Point(target.center) || !Radii(target.radii))
                { reason = "Invalid joint target."; return false; }
        if (bones != null)
            foreach (BoneTarget target in bones)
                if (target == null || !ValidJoint(target.startJoint) || !ValidJoint(target.endJoint)
                    || target.startJoint == target.endJoint || !Point(target.start) || !Point(target.end)
                    || target.start.Equals(target.end) || !Radii(target.endpointRadii)
                    || !Positive(target.angleToleranceDegrees) || target.angleToleranceDegrees > 180f)
                { reason = "Invalid bone target."; return false; }
        return true;
    }

    private static bool Positive(float value) { return !float.IsInfinity(value) && value > 0f; }
    private static bool Radii(Vector2 value) { return Positive(value.x) && Positive(value.y); }
    private static bool Point(Vector2 value)
    { return value.x >= 0 && value.x <= 1 && value.y >= 0 && value.y <= 1; }
    private static bool ValidJoint(DanceJoint joint) { return (int)joint >= 0 && (int)joint < 20; }
}
