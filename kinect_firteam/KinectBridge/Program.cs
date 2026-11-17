using System;
using System.Net.Sockets;
using Microsoft.Kinect;

internal static class Program
{
    private static volatile bool running = true;
    private static int Main()
    {
        KinectSensor sensor = null;
        Console.CancelKeyPress += delegate(object sender, ConsoleCancelEventArgs e) { e.Cancel = true; running = false; };
        try
        {
            foreach (KinectSensor item in KinectSensor.KinectSensors)
                if (item.Status == KinectStatus.Connected) { sensor = item; break; }
            if (sensor == null) throw new InvalidOperationException("No connected Kinect.");
            sensor.SkeletonStream.Enable();
            sensor.Start();
            Skeleton[] bodies = new Skeleton[sensor.SkeletonStream.FrameSkeletonArrayLength];
            int playerId = 0;
            using (UdpClient sender = new UdpClient())
            {
                sender.Connect("127.0.0.1", 39501);
                Console.WriteLine("Kinect connected. Sending 20 joints to Unity. Ctrl+C to stop.");
                DateTime nextReport = DateTime.UtcNow;
                int sentFrames = 0;
                while (running && sensor.Status == KinectStatus.Connected)
                {
                    byte[] packet = new byte[184];
                    Buffer.BlockCopy(BitConverter.GetBytes(1802), 0, packet, 0, 4);
                    int trackedCount = 0;
                    using (SkeletonFrame frame = sensor.SkeletonStream.OpenNextFrame(40))
                    {
                        if (frame != null)
                        {
                            frame.CopySkeletonDataTo(bodies);
                            Skeleton player = null;
                            foreach (Skeleton body in bodies)
                            {
                                if (body.TrackingState != SkeletonTrackingState.Tracked) continue;
                                if (body.TrackingId == playerId) { player = body; break; }
                                if (player == null || body.Position.Z < player.Position.Z) player = body;
                            }
                            playerId = player == null ? 0 : player.TrackingId;
                            if (player != null)
                            {
                                foreach (Joint joint in player.Joints)
                                {
                                    if (joint.TrackingState == JointTrackingState.NotTracked || joint.Position.Z <= 0) continue;
                                    DepthImagePoint point = sensor.CoordinateMapper.MapSkeletonPointToDepthPoint(
                                        joint.Position, DepthImageFormat.Resolution640x480Fps30);
                                    int offset = 4 + (int)joint.JointType * 9;
                                    packet[offset] = (byte)joint.TrackingState;
                                    Buffer.BlockCopy(BitConverter.GetBytes(point.X / 640f), 0, packet, offset + 1, 4);
                                    Buffer.BlockCopy(BitConverter.GetBytes(1 - point.Y / 480f), 0, packet, offset + 5, 4);
                                    if (joint.TrackingState == JointTrackingState.Tracked) trackedCount++;
                                }
                            }
                        }
                    }
                    sender.Send(packet, packet.Length);
                    sentFrames++;
                    if (DateTime.UtcNow >= nextReport)
                    {
                        Console.WriteLine("Sent frames: {0}, tracked joints: {1}/20", sentFrames, trackedCount);
                        nextReport = DateTime.UtcNow.AddSeconds(2);
                    }
                }
            }
            return 0;
        }
        catch (Exception error)
        {
            Console.Error.WriteLine(error);
            Console.WriteLine("Press Enter to close.");
            Console.ReadLine();
            return 1;
        }
        finally { if (sensor != null && sensor.IsRunning) sensor.Stop(); }
    }
}
