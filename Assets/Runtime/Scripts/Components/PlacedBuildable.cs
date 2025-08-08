using Unity.Entities;
using Unity.Mathematics;

namespace KexBuild {
    public struct PlacedBuildable : IComponentData {
        public Entity Definition;
        public float3 Position;
        public float Yaw;
    }
}
