using Unity.Entities;
using Unity.Mathematics;

namespace KexBuild {
    public struct Buildable : IComponentData {
        public Entity Definition;
        public float3 RayOrigin;
        public float3 RayDirection;
        public float3 TargetPosition;
        public float TargetYaw;
    }
}
