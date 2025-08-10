using Unity.Entities;
using Unity.Mathematics;

namespace KexBuild {
    public struct SnapPosition : IBufferElementData {
        public int3 Value;
        public byte Priority;
    }
}
