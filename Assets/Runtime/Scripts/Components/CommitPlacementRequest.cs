using Unity.Entities;

namespace KexBuild {
    public struct PlacementRequest : IComponentData {
        public Entity BuildableEntity;
    }
}
