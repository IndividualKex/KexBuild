using Unity.Entities;
using Unity.Burst;
using Unity.Mathematics;
using Unity.Transforms;

namespace KexBuild {
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    [UpdateAfter(typeof(FixedStepSimulationSystemGroup))]
    [BurstCompile]
    public partial struct BuildableUpdateSystem : ISystem {
        [BurstCompile]
        public void OnUpdate(ref SystemState state) {
            float deltaTime = SystemAPI.Time.DeltaTime;
            float t = math.saturate(deltaTime * 30f);
            foreach (var (buildable, transform) in SystemAPI.Query<Buildable, RefRW<LocalTransform>>()) {
                float3 position = transform.ValueRO.Position;
                if (position.Equals(float3.zero)) {
                    transform.ValueRW.Position = buildable.TargetPosition;
                }
                else {
                    position = math.lerp(position, buildable.TargetPosition, t);
                    transform.ValueRW.Position = position;
                }
            }
        }
    }
}
