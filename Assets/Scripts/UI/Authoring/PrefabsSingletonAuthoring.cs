using Unity.Entities;
using UnityEngine;

namespace KexBuild.UI {
    public class PrefabsSingletonAuthoring : MonoBehaviour {
        public GameObject Floor;
        public GameObject Wall;

        private class Baker : Baker<PrefabsSingletonAuthoring> {
            public override void Bake(PrefabsSingletonAuthoring authoring) {
                var entity = GetEntity(TransformUsageFlags.Dynamic);
                AddComponent(entity, new PrefabsSingleton {
                    Floor = GetEntity(authoring.Floor, TransformUsageFlags.Dynamic),
                    Wall = GetEntity(authoring.Wall, TransformUsageFlags.Dynamic)
                });
            }
        }
    }
}
