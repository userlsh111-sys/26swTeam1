using UnityEngine;

// Joint indices follow Kinect SDK 1.8's JointType order.
[RequireComponent(typeof(KinectInput))]
public class skeletonTest : MonoBehaviour
{
    private KinectInput input;
    private static readonly int[,] Bones = {
        {0,1},{1,2},{2,3},{2,4},{4,5},{5,6},{6,7},
        {2,8},{8,9},{9,10},{10,11},{0,12},{12,13},{13,14},{14,15},
        {0,16},{16,17},{17,18},{18,19}
    };
    private void Awake() { input = GetComponent<KinectInput>(); }
    private Vector2 ScreenPoint(int index)
    {
        Vector2 point = input.JointPositions[index];
        return new Vector2(point.x * Screen.width, (1 - point.y) * Screen.height);
    }
    private void OnGUI()
    {
        Color savedColor = GUI.color;
        Matrix4x4 savedMatrix = GUI.matrix;
        for (int i = 0; i < Bones.GetLength(0); i++)
        {
            int a = Bones[i,0], b = Bones[i,1];
            if (input.JointStates[a] == 0 || input.JointStates[b] == 0) continue;
            Vector2 start = ScreenPoint(a), end = ScreenPoint(b);
            GUI.color = input.JointStates[a] == 2 && input.JointStates[b] == 2 ? Color.green : Color.yellow;
            GUIUtility.RotateAroundPivot(Mathf.Atan2(end.y - start.y, end.x - start.x) * Mathf.Rad2Deg, start);
            GUI.DrawTexture(new Rect(start.x, start.y - 2, Vector2.Distance(start, end), 4), Texture2D.whiteTexture);
            GUI.matrix = savedMatrix;
        }
        for (int i = 0; i < 20; i++)
        {
            if (input.JointStates[i] == 0) continue;
            Vector2 point = ScreenPoint(i);
            GUI.color = input.JointStates[i] == 2 ? Color.cyan : Color.yellow;
            GUI.DrawTexture(new Rect(point.x - 4, point.y - 4, 8, 8), Texture2D.whiteTexture);
        }
        GUI.color = savedColor;
        GUI.matrix = savedMatrix;
    }
}
