using System;
using UnityEngine;
using UnityEngine.Video;

public enum AuditionStage { Menu, Intro, SongSelection, Confirmation, Playing, Result }

// Temporary presentation for integration testing; UI can subscribe to StageChanged.
[RequireComponent(typeof(KinectInput), typeof(GameFlow))]
public class AuditionFlow : MonoBehaviour
{
    [SerializeField] private VideoClip introClip;
    [SerializeField] private string songTitle = "RESCENE - Pretty Girl";
    public AuditionStage Stage { get; private set; }
    public event Action<AuditionStage> StageChanged;
    public bool SplitScreen => Stage >= AuditionStage.SongSelection;
    public bool ShowSkeleton => SplitScreen;
    public bool IsHandMenu => Stage == AuditionStage.SongSelection || Stage == AuditionStage.Confirmation;
    private KinectInput input;
    private GameFlow game;
    private VideoPlayer video;
    private AudioSource voice;
    private readonly HandTouchGate rightTouch = new HandTouchGate();
    private readonly HandTouchGate leftTouch = new HandTouchGate();
    private long lastSample = -1;
    private string videoError;

    private void Awake()
    {
        input = GetComponent<KinectInput>();
        game = GetComponent<GameFlow>();
        voice = gameObject.AddComponent<AudioSource>();
        voice.playOnAwake = false;
        voice.spatialBlend = 0;
        video = gameObject.AddComponent<VideoPlayer>();
        video.playOnAwake = false;
        video.isLooping = false;
        video.renderMode = VideoRenderMode.APIOnly;
        video.audioOutputMode = VideoAudioOutputMode.AudioSource;
        video.controlledAudioTrackCount = 1;
        video.SetTargetAudioSource(0, voice);
        video.loopPointReached += OnVideoEnded;
        video.errorReceived += OnVideoError;
        video.prepareCompleted += OnVideoPrepared;
    }
    private void OnEnable() { game.ResetGame(); ChangeStage(AuditionStage.Menu); }
    public void PlayIntro()
    {
        if (Stage != AuditionStage.Menu) return;
        videoError = null;
        ChangeStage(AuditionStage.Intro);
        if (introClip == null) return;
        video.clip = introClip;
        video.Prepare();
    }
    private void OnVideoPrepared(VideoPlayer player)
    { if (isActiveAndEnabled && Stage == AuditionStage.Intro) player.Play(); }
    private void OnVideoEnded(VideoPlayer player)
    { if (isActiveAndEnabled && Stage == AuditionStage.Intro) ChangeStage(AuditionStage.SongSelection); }
    private void OnVideoError(VideoPlayer player, string message)
    { videoError = message; }
    private void ChangeStage(AuditionStage next)
    {
        if (next != AuditionStage.Intro) video.Stop();
        Stage = next;
        rightTouch.Reset(); leftTouch.Reset();
        lastSample = input.SampleVersion;
        StageChanged?.Invoke(next);
    }
    private Rect SongButton => new Rect(Screen.width * .58f, Screen.height * .35f, Screen.width * .34f, Screen.height * .18f);
    private Rect StartButton => new Rect(Screen.width * .62f, Screen.height * .40f, Screen.width * .28f, Screen.height * .18f);
    private Rect BackButton => new Rect(Screen.width * .62f, Screen.height * .67f, Screen.width * .28f, Screen.height * .18f);
    private bool TryHand(DanceJoint joint, out Vector2 point)
    {
        Vector2 normalized = input.JointPositions[(int)joint];
        // Menu hit testing uses the same full-screen coordinates as the skeleton.
        point = new Vector2(Screen.width * normalized.x, Screen.height * (1 - normalized.y));
        return input.HasFreshFrame && input.JointStates[(int)joint] == 2
            && normalized.x >= 0 && normalized.x <= 1 && normalized.y >= 0 && normalized.y <= 1;
    }
    private void LateUpdate()
    {
        if (Stage == AuditionStage.Playing && game.State == DanceGameState.Finished)
        { ChangeStage(AuditionStage.Result); return; }
        if (Stage != AuditionStage.SongSelection && Stage != AuditionStage.Confirmation) return;
        if (!input.HasFreshFrame) { rightTouch.Reset(); leftTouch.Reset(); return; }
        if (lastSample == input.SampleVersion) return;
        lastSample = input.SampleVersion;
        Vector2 right, left;
        bool r = TryHand(DanceJoint.HandRight, out right);
        bool l = TryHand(DanceJoint.HandLeft, out left);
        if (Stage == AuditionStage.SongSelection)
        {
            bool pickedRight = rightTouch.Observe(r, SongButton.Contains(right));
            bool pickedLeft = leftTouch.Observe(l, SongButton.Contains(left));
            if (pickedRight || pickedLeft) ChangeStage(AuditionStage.Confirmation);
            return;
        }
        bool back = leftTouch.Observe(l, BackButton.Contains(left));
        bool start = rightTouch.Observe(r, StartButton.Contains(right));
        // Back wins if both valid hands touch in the same frame.
        if (back) ChangeStage(AuditionStage.SongSelection);
        else if (start)
        {
            game.StartGame();
            if (game.Error == null) ChangeStage(AuditionStage.Playing);
        }
    }
    private void OnGUI()
    {
        int depth = GUI.depth;
        GUI.depth = -10;
        if (Stage == AuditionStage.Menu)
        {
            GUI.Box(new Rect(Screen.width * .25f, Screen.height * .25f, Screen.width * .5f, Screen.height * .5f), "BECOME AN IDOL");
            if (GUI.Button(new Rect(Screen.width * .4f, Screen.height * .5f, Screen.width * .2f, 60), "플레이")) PlayIntro();
        }
        else if (Stage == AuditionStage.Intro)
        {
            GUI.DrawTexture(new Rect(0, 0, Screen.width, Screen.height), Texture2D.blackTexture);
            if (video.texture != null) GUI.DrawTexture(new Rect(0, 0, Screen.width, Screen.height), video.texture, ScaleMode.ScaleToFit);
            if (introClip == null || videoError != null)
            {
                GUI.Label(new Rect(30, 30, Screen.width - 60, 80), videoError ?? "도입 영상 미등록: Inspector의 Intro Clip에 영상을 넣어주세요.");
                if (GUI.Button(new Rect(30, 120, 250, 50), "테스트: 곡 선택으로 이동")) ChangeStage(AuditionStage.SongSelection);
            }
            else if (!video.isPrepared) GUI.Label(new Rect(30, 30, 300, 40), "영상 준비 중...");
        }
        else
        {
            GUI.Box(new Rect(Screen.width * .5f, 0, Screen.width * .5f, Screen.height), "BECOME AN IDOL");
            GUI.Label(new Rect(15, 15, Screen.width * .48f, 50), input.Status);
            if (Stage == AuditionStage.SongSelection)
            {
                GUI.Label(new Rect(Screen.width * .55f, 70, Screen.width * .4f, 65), "손 커서로 곡을 선택하세요.\n버튼 밖에서 안으로 손을 이동하세요.");
                GUI.Box(SongButton, songTitle + "\n왼손 / 오른손");
            }
            else if (Stage == AuditionStage.Confirmation)
            {
                GUI.Label(new Rect(Screen.width * .55f, 80, Screen.width * .4f, 90), songTitle + "\n선택한 곡으로 시작할까요?\n첫 카운트다운 후 음악과 3포즈 테스트가 시작됩니다.");
                GUI.Box(StartButton, "시작\n오른손 (Right Hand)");
                GUI.Box(BackButton, "이전\n왼손 (Left Hand)");
                if (game.Error != null) GUI.Label(new Rect(Screen.width * .55f, 180, Screen.width * .4f, 70), game.Error);
            }
            else if (Stage == AuditionStage.Playing)
            {
                GUI.Label(new Rect(Screen.width * .55f, 80, Screen.width * .4f, 80), "포즈 이미지 준비 중\n" + game.State + "  " + game.RemainingSeconds.ToString("F1"));
                if (game.State == DanceGameState.Feedback)
                    GUI.Box(new Rect(Screen.width * .4f, Screen.height * .45f, Screen.width * .2f, 60), game.LastGrade.ToString().ToUpperInvariant());
            }
            else if (Stage == AuditionStage.Result)
            {
                GUI.Label(new Rect(Screen.width * .55f, 80, Screen.width * .4f, 100), "테스트 종료\n총점: " + game.Score);
                if (GUI.Button(new Rect(Screen.width * .6f, Screen.height * .6f, Screen.width * .3f, 60), "곡 선택으로"))
                { game.ResetGame(); ChangeStage(AuditionStage.SongSelection); }
            }
            if (Stage == AuditionStage.SongSelection || Stage == AuditionStage.Confirmation)
            { DrawHand(DanceJoint.HandRight, "R", Color.cyan); DrawHand(DanceJoint.HandLeft, "L", Color.yellow); }
        }
        GUI.depth = depth;
    }
    private void DrawHand(DanceJoint joint, string label, Color color)
    {
        Vector2 point;
        if (!TryHand(joint, out point)) return;
        Color previous = GUI.color;
        GUI.color = color;
        GUI.DrawTexture(new Rect(point.x - 8, point.y - 8, 16, 16), Texture2D.whiteTexture);
        GUI.color = previous;
        GUI.Label(new Rect(point.x + 10, point.y - 12, 30, 30), label);
    }
    private void OnDisable()
    {
        if (video != null) video.Stop();
        if (game != null) game.ResetGame();
        rightTouch.Reset(); leftTouch.Reset();
    }
    private void OnDestroy()
    {
        if (video != null)
        {
            video.loopPointReached -= OnVideoEnded;
            video.errorReceived -= OnVideoError;
            video.prepareCompleted -= OnVideoPrepared;
            Destroy(video);
        }
        if (voice != null) Destroy(voice);
    }
}

