using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Transforms;

namespace KexBuild {
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    [UpdateAfter(typeof(BuildableUpdateSystem))]
    [BurstCompile]
    public partial struct PlacementSystem : ISystem {
        [BurstCompile]
        public void OnUpdate(ref SystemState state) {
            using var ecb = new EntityCommandBuffer(Allocator.Temp);

            foreach (var (request, requestEntity) in SystemAPI
                .Query<PlacementRequest>()
                .WithEntityAccess()
            ) {
                ecb.DestroyEntity(requestEntity);

                var buildable = SystemAPI.GetComponent<Buildable>(request.BuildableEntity);
                ref var transform = ref SystemAPI.GetComponentRW<LocalTransform>(request.BuildableEntity).ValueRW;

                transform.Position = buildable.ResolvedTargetPosition;
                transform.Rotation = buildable.ResolvedTargetRotation;

                ecb.AddComponent(request.BuildableEntity, new PlacedBuildable {
                    CurrentEntity = request.BuildableEntity,
                    Definition = buildable.Definition,
                    Position = buildable.ResolvedTargetPosition,
                    Yaw = buildable.TargetYaw
                });

                ecb.RemoveComponent<Buildable>(request.BuildableEntity);
            }

            ecb.Playback(state.EntityManager);
        }
    }
}
