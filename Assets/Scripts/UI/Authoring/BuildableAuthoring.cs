using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;

namespace KexBuild.UI {
    public class BuildableAuthoring : MonoBehaviour {
        private class Baker : Baker<BuildableAuthoring> {
            public override void Bake(BuildableAuthoring authoring) {
                var entity = GetEntity(TransformUsageFlags.Dynamic);
                AddComponent(entity, new Buildable {
                    Definition = Entity.Null,
                    RayOrigin = float3.zero,
                    RayDirection = new float3(0, 0, 1),
                    TargetPosition = float3.zero,
                    TargetYaw = 0f
                });
            }
        }
    }
}
