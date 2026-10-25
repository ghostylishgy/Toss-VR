// Compile-only sample for the Unity .NET Standard 2.1 reference check. Never added to Assets/.
using UnityEngine;
using Toss.Experiments.PerceivedWeight;
using SN = System.Numerics;

namespace Toss.Experiments.AwrapGate
{
    public static class UnityNumericsBridgeSample
    {
        public static SN.Vector3 ToNumerics(Vector3 v) { return new SN.Vector3(v.x, v.y, v.z); }
        public static SN.Quaternion ToNumerics(Quaternion q) { return new SN.Quaternion(q.x, q.y, q.z, q.w); }
        public static Vector3 ToUnity(SN.Vector3 v) { return new Vector3(v.X, v.Y, v.Z); }
        public static Quaternion ToUnity(SN.Quaternion q) { return new Quaternion(q.X, q.Y, q.Z, q.W); }

        public static FollowPose Read(Transform t) { return new FollowPose(ToNumerics(t.position), ToNumerics(t.rotation)); }

        public static void Write(Transform t, FollowPose p) { t.SetPositionAndRotation(ToUnity(p.Position), ToUnity(p.Rotation)); }

        public static FollowStep StepFromTransform(PoseFollower follower, Transform t, FollowPose lastOutput, double dt)
        {
            return follower.Step(lastOutput, Read(t), dt);
        }
    }
}
