using Unity.Entities;

namespace KexBuild.UI {
    public struct PrefabsSingleton : IComponentData {
        public Entity Floor;
        public Entity Wall;
    }
}
