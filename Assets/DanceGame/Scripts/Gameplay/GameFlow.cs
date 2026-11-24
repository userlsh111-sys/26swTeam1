using System;
using System.Collections.Generic;
using UnityEngine;

public enum DanceGameState { Idle, Countdown, Judging, Feedback, Finished }

[RequireComponent(typeof(KinectInput), typeof(PoseJudge))]
public class GameFlow : MonoBehaviour
{
    [SerializeField] private float countdownSeconds = 3f;
    [SerializeField] private float feedbackSeconds = 1f;
    [SerializeField] private int perfectPoints = 100;
    [SerializeField] private int goodPoints = 70;
    [SerializeField] private bool showDebugOverlay = true;
    [SerializeField] private PoseDefinition[] poses = {
        new PoseDefinition {
            id = "test-right", displayName = "Right hand target", imageIndex = 0,
            joints = new[] { new JointTarget() }
        },
        new PoseDefinition {
            id = "test-left", displayName = "Left hand target", imageIndex = 1,
            joints = new[] { new JointTarget {
                joint = DanceJoint.HandLeft, center = new Vector2(0.3f, 0.6f)
            }}
        },
        new PoseDefinition {
            id = "test-arm", displayName = "Right forearm horizontal", imageIndex = 2,
            bones = new[] { new BoneTarget() }
        }
    };

    public DanceGameState State { get; private set; }
    public int PoseIndex { get; private set; } = -1;
    public int PoseCount => poses == null ? 0 : poses.Length;
    public PoseDefinition CurrentPose => PoseIndex >= 0 && PoseIndex < PoseCount ? poses[PoseIndex] : null;
    public int Score { get; private set; }
    public int PerfectCount { get; private set; }
    public int GoodCount { get; private set; }
    public int MissCount { get; private set; }
    public float RemainingSeconds { get; private set; }
    public float HoldProgress => CurrentPose == null ? 0 : Mathf.Clamp01(hold.HeldSeconds / CurrentPose.holdSeconds);
    public PoseGrade LastGrade { get; private set; }
    public PoseEvaluation CurrentEvaluation { get; private set; }
    public string Error { get; private set; }
    // UI subscribes to these; no image files or scene references are needed here.
    public event Action StateChanged;
    public event Action<int, PoseGrade, int> PoseJudged;

    private KinectInput input;
    private readonly PoseHoldTracker hold = new PoseHoldTracker();
    private float phaseEnd;
    private float lastSampleTime;
    private long lastSampleVersion;

    private void Awake() { input = GetComponent<KinectInput>(); }

    public void StartGame()
    {
        ResetGame();
        if (!ValidateConfiguration()) return;
        PoseIndex = 0;
        SetPhase(DanceGameState.Countdown, countdownSeconds);
    }

    public void ResetGame()
    {
        State = DanceGameState.Idle;
        PoseIndex = -1;
        Score = PerfectCount = GoodCount = MissCount = 0;
        RemainingSeconds = 0;
        LastGrade = PoseGrade.None;
        CurrentEvaluation = default(PoseEvaluation);
        Error = null;
        hold.Reset();
        StateChanged?.Invoke();
    }

    private bool ValidateConfiguration()
    {
        if (PoseCount == 0) Error = "Add at least one pose.";
        else if (!FiniteNonnegative(countdownSeconds) || !FiniteNonnegative(feedbackSeconds))
            Error = "Countdown and feedback duration must be finite and nonnegative.";
        else if (goodPoints < 0 || perfectPoints < goodPoints) Error = "Invalid score settings.";
        if (Error == null)
        {
            var ids = new HashSet<string>();
            for (int i = 0; i < poses.Length; i++)
            {
                string reason = "Pose is null.";
                if (poses[i] == null || !poses[i].IsValid(out reason))
                { Error = "Pose " + (i + 1) + ": " + reason; break; }
                if (!ids.Add(poses[i].id)) { Error = "Duplicate pose ID: " + poses[i].id; break; }
            }
        }
        if (Error != null) { Debug.LogError(Error, this); StateChanged?.Invoke(); }
        return Error == null;
    }

    private static bool FiniteNonnegative(float value)
    { return value >= 0 && !float.IsInfinity(value); }

    private void LateUpdate()
    {
        if (State == DanceGameState.Idle || State == DanceGameState.Finished) return;
        float now = Time.realtimeSinceStartup;
        RemainingSeconds = Mathf.Max(0, phaseEnd - now);
        if (State == DanceGameState.Countdown)
        {
            if (now >= phaseEnd)
            {
                hold.Reset();
                CurrentEvaluation = default(PoseEvaluation);
                lastSampleTime = now;
                lastSampleVersion = input.SampleVersion;
                SetPhase(DanceGameState.Judging, CurrentPose.durationSeconds);
            }
            return;
        }
        if (State == DanceGameState.Feedback)
        {
            if (now < phaseEnd) return;
            PoseIndex++;
            if (PoseIndex >= PoseCount) SetPhase(DanceGameState.Finished, 0);
            else
            {
                hold.Reset();
                LastGrade = PoseGrade.None;
                CurrentEvaluation = default(PoseEvaluation);
                SetPhase(DanceGameState.Countdown, countdownSeconds);
            }
            return;
        }
        // A late frame cannot score after the deadline.
        if (now >= phaseEnd) { CompletePose(PoseGrade.Miss); return; }
        if (!input.HasFreshFrame)
        {
            hold.Reset();
            CurrentEvaluation = default(PoseEvaluation);
            lastSampleTime = now;
            return;
        }
        if (lastSampleVersion == input.SampleVersion) return;
        CurrentEvaluation = PoseEvaluator.Evaluate(CurrentPose, input.JointPositions, input.JointStates);
        float interval = now - lastSampleTime;
        lastSampleTime = now;
        lastSampleVersion = input.SampleVersion;
        PoseGrade result = hold.Observe(CurrentEvaluation, interval, CurrentPose.holdSeconds);
        if (result != PoseGrade.None) CompletePose(result);
    }

    private void CompletePose(PoseGrade grade)
    {
        if (State != DanceGameState.Judging) return;
        LastGrade = grade;
        int awarded = 0;
        switch (grade)
        {
            case PoseGrade.Perfect: PerfectCount++; awarded = perfectPoints; break;
            case PoseGrade.Good: GoodCount++; awarded = goodPoints; break;
            default: MissCount++; break;
        }
        Score += awarded;
        int completedIndex = PoseIndex;
        SetPhase(DanceGameState.Feedback, feedbackSeconds);
        PoseJudged?.Invoke(completedIndex, grade, awarded);
    }

    private void SetPhase(DanceGameState state, float duration)
    {
        State = state;
        RemainingSeconds = duration;
        phaseEnd = Time.realtimeSinceStartup + duration;
        StateChanged?.Invoke();
    }

    private void OnDisable() { ResetGame(); }

    private void OnGUI()
    {
        AuditionFlow audition = GetComponent<AuditionFlow>();
        if (audition != null && audition.isActiveAndEnabled) return;
        if (!showDebugOverlay) return;
        GUI.Box(new Rect(10, 10, 680, 180), "Pose challenge - temporary test data");
        GUI.Label(new Rect(20, 35, 660, 22), input.Status);
        GUI.Label(new Rect(20, 58, 660, 22), "State: " + State + " | Pose: "
            + (CurrentPose == null ? "-" : (PoseIndex + 1) + "/" + PoseCount + " " + CurrentPose.displayName));
        GUI.Label(new Rect(20, 81, 660, 22), "Time: " + RemainingSeconds.ToString("F1")
            + " | Hold: " + (HoldProgress * 100).ToString("F0") + "% | Result: " + LastGrade);
        GUI.Label(new Rect(20, 104, 660, 22), "Score: " + Score + " | Perfect: " + PerfectCount
            + " Good: " + GoodCount + " Miss: " + MissCount);
        if (GUI.Button(new Rect(20, 133, 130, 25), "Start / Restart")) StartGame();
        if (GUI.Button(new Rect(160, 133, 130, 25), "Stop / Reset")) ResetGame();
        if (GUI.Button(new Rect(300, 133, 130, 25), "Retry receiver")) input.Connect();
        if (Error != null) GUI.Label(new Rect(20, 163, 660, 22), Error);
    }
}
