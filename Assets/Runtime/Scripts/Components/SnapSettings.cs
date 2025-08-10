using Unity.Entities;

namespace KexBuild {
    public struct SnapSettings : IComponentData {
        public SnapMode Mode;
        public float GridSize;
    }
}
