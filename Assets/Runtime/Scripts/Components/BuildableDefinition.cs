using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;

namespace KexBuild {
    public struct BuildableDefinition : IComponentData {
        public FixedString64Bytes Name;
        public float3 Center;
        public float3 Size;
    }
}
