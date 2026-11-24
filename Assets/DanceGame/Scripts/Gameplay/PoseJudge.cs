using UnityEngine;

[RequireComponent(typeof(KinectInput))]
public class PoseJudge : MonoBehaviour
{
    [SerializeField] private Vector2 targetCenter = new Vector2(0.7f, 0.6f);
    [SerializeField] private Vector2 targetRadii = new Vector2(0.12f, 0.16f);
    [SerializeField] private int points = 100;
    public int Score { get; private set; }
    public bool TargetHit { get; private set; }
    private KinectInput input;
    private Texture2D circle;
    private GameFlow flow;
    private AuditionFlow audition;

    private void Awake()
    {
        input = GetComponent<KinectInput>();
        flow = GetComponent<GameFlow>();
        audition = GetComponent<AuditionFlow>();
        circle = new Texture2D(64, 64, TextureFormat.RGBA32, false);
        for (int y = 0; y < 64; y++)
            for (int x = 0; x < 64; x++)
                circle.SetPixel(x, y, new Color(1, 1, 1,
                    Mathf.Clamp01(31.5f - new Vector2(x - 31.5f, y - 31.5f).magnitude)));
        circle.Apply();
    }
    private void LateUpdate()
    {
        if (flow != null && flow.isActiveAndEnabled) return;
        if (TargetHit || !input.IsRightHandTracked) return;
        if (!Contains(input.RightHandPosition, targetCenter, targetRadii)) return;
        TargetHit = true;
        Score += points;
        Debug.Log("Right hand hit! Score: " + Score, this);
    }
    public static bool Contains(Vector2 position, Vector2 center, Vector2 radii)
    {
        if (radii.x <= 0 || radii.y <= 0) return false;
        Vector2 delta = position - center;
        float x = delta.x / radii.x;
        float y = delta.y / radii.y;
        return x * x + y * y <= 1f;
    }
    private void OnGUI()
    {
        bool split = audition != null && audition.isActiveAndEnabled;
        if (split && audition.Stage != AuditionStage.Playing) return;
        if (flow != null && flow.isActiveAndEnabled)
        {
            float width = split ? Screen.width * .5f : Screen.width;
            GUI.BeginGroup(new Rect(0, 0, width, Screen.height));
            DrawChallengeTargets(width);
            GUI.EndGroup();
            return;
        }
        GUI.Box(new Rect(10, 10, 650, 115), "Kinect right-hand test");
        GUI.Label(new Rect(20, 35, 630, 25), input.Status);
        GUI.Label(new Rect(20, 60, 630, 25), "Score: " + Score + (TargetHit ? "  HIT!" : "  Touch the yellow target."));
        if (GUI.Button(new Rect(20, 90, 130, 25), "Reset target")) { Score = 0; TargetHit = false; }
        if (GUI.Button(new Rect(160, 90, 130, 25), "Retry receiver")) input.Connect();
        GUI.color = TargetHit ? Color.green : new Color(1, 0.85f, 0, 0.6f);
        GUI.DrawTexture(new Rect((targetCenter.x - targetRadii.x) * Screen.width,
            (1 - targetCenter.y - targetRadii.y) * Screen.height,
            targetRadii.x * 2 * Screen.width, targetRadii.y * 2 * Screen.height), circle);
        if (input.IsRightHandTracked)
        {
            GUI.color = Color.cyan;
            Vector2 hand = input.RightHandPosition;
            GUI.DrawTexture(new Rect(hand.x * Screen.width - 8, (1 - hand.y) * Screen.height - 8, 16, 16), circle);
        }
        GUI.color = Color.white;
    }
    private void OnDestroy() { if (circle != null) Destroy(circle); }

    private void DrawChallengeTargets(float width)
    {
        PoseDefinition pose = flow.CurrentPose;
        if (pose == null) return;
        Color savedColor = GUI.color;
        Matrix4x4 savedMatrix = GUI.matrix;
        GUI.color = flow.LastGrade == PoseGrade.Perfect || flow.LastGrade == PoseGrade.Good
            ? Color.green : new Color(1, 0.85f, 0, 0.6f);
        if (pose.joints != null)
            foreach (JointTarget target in pose.joints)
            {
                if (target == null) continue;
                GUI.DrawTexture(new Rect((target.center.x - target.radii.x) * width,
                    (1 - target.center.y - target.radii.y) * Screen.height,
                    target.radii.x * 2 * width, target.radii.y * 2 * Screen.height), circle);
                GUI.Label(new Rect(target.center.x * width,
                    (1 - target.center.y) * Screen.height, 180, 25), target.joint.ToString());
            }
        if (pose.bones != null)
            foreach (BoneTarget target in pose.bones)
            {
                if (target == null) continue;
                Vector2 start = new Vector2(target.start.x * width, (1 - target.start.y) * Screen.height);
                Vector2 end = new Vector2(target.end.x * width, (1 - target.end.y) * Screen.height);
                GUIUtility.RotateAroundPivot(Mathf.Atan2(end.y - start.y, end.x - start.x) * Mathf.Rad2Deg, start);
                GUI.DrawTexture(new Rect(start.x, start.y - 4, Vector2.Distance(start, end), 8), Texture2D.whiteTexture);
                GUI.matrix = savedMatrix;
                GUI.Label(new Rect(start.x, start.y + 10, 260, 25), target.startJoint + " - " + target.endJoint);
            }
        GUI.color = savedColor;
        GUI.matrix = savedMatrix;
    }
}
