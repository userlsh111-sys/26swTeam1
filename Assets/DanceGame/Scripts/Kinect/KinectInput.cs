using System;
using System.Net;
using System.Net.Sockets;
using UnityEngine;

public class KinectInput : MonoBehaviour
{
    public Vector2 RightHandPosition { get; private set; }
    public bool IsRightHandTracked { get; private set; }
    public string Status { get; private set; } = "Not connected";
    [SerializeField] private bool mirror = true;
    private UdpClient receiver;
    private float lastFrame;
    public long SampleVersion { get; private set; }
    private bool receivedFrame;
    public bool HasFreshFrame => isActiveAndEnabled && receivedFrame
        && Time.realtimeSinceStartup - lastFrame <= 0.3f;
    public Vector2[] JointPositions { get; } = new Vector2[20];
    public byte[] JointStates { get; } = new byte[20];
    private void OnEnable() { Connect(); }
    public void Connect()
    {
        Disconnect();
        try
        {
            receiver = new UdpClient(new IPEndPoint(IPAddress.Loopback, 39501));
            lastFrame = Time.realtimeSinceStartup;
            Status = "Run KinectBridge.exe, then stand in front of Kinect.";
        }
        catch (Exception error) { Status = error.Message; Debug.LogException(error, this); }
    }
    private void Update()
    {
        if (receiver == null) return;
        try
        {
            // Drain queued frames so judging uses the newest position.
            for (int i = 0; i < 120 && receiver.Available > 0; i++)
            {
                IPEndPoint sender = new IPEndPoint(IPAddress.Loopback, 0);
                byte[] data = receiver.Receive(ref sender);
                if (data.Length == 184 && BitConverter.ToInt32(data, 0) == 1802)
                {
                    for (int joint = 0; joint < 20; joint++)
                    {
                        int offset = 4 + joint * 9;
                        float jointX = BitConverter.ToSingle(data, offset + 1);
                        float jointY = BitConverter.ToSingle(data, offset + 5);
                        bool valid = !float.IsNaN(jointX) && !float.IsInfinity(jointX)
                            && !float.IsNaN(jointY) && !float.IsInfinity(jointY);
                        JointStates[joint] = valid && data[offset] <= 2 ? data[offset] : (byte)0;
                        JointPositions[joint] = valid ? new Vector2(mirror ? 1 - jointX : jointX, jointY) : Vector2.zero;
                    }
                    lastFrame = Time.realtimeSinceStartup;
                    receivedFrame = true;
                    SampleVersion++;
                    IsRightHandTracked = JointStates[11] == 2;
                    RightHandPosition = JointPositions[11];
                    Status = IsRightHandTracked ? "Skeleton received. Right hand tracked."
                        : "Bridge connected. Stand back so your body is visible.";
                    continue;
                }
                if (data.Length != 13 || BitConverter.ToInt32(data, 0) != 1801) continue;
                float x = BitConverter.ToSingle(data, 5);
                float y = BitConverter.ToSingle(data, 9);
                if (float.IsNaN(x) || float.IsInfinity(x) || float.IsNaN(y) || float.IsInfinity(y)) continue;
                lastFrame = Time.realtimeSinceStartup;
                receivedFrame = true;
                SampleVersion++;
                Array.Clear(JointStates, 0, JointStates.Length);
                IsRightHandTracked = data[4] == 1;
                RightHandPosition = new Vector2(mirror ? 1 - x : x, y);
                JointPositions[11] = RightHandPosition;
                JointStates[11] = IsRightHandTracked ? (byte)2 : (byte)0;
                Status = "Old bridge: right hand only. Restart the updated KinectBridge.exe.";
            }
            if (Time.realtimeSinceStartup - lastFrame > 0.3f)
            {
                IsRightHandTracked = false;
                Array.Clear(JointStates, 0, JointStates.Length);
                Status = "No fresh data. Check KinectBridge.exe and sensor.";
            }
        }
        catch (Exception error) { Disconnect(); Status = error.Message; Debug.LogException(error, this); }
    }
    private void OnDisable() { Disconnect(); }
    private void Disconnect()
    {
        receivedFrame = false;
        IsRightHandTracked = false;
        Array.Clear(JointStates, 0, JointStates.Length);
        if (receiver != null) receiver.Close();
        receiver = null;
    }
}
