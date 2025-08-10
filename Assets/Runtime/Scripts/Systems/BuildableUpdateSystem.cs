using Unity.Entities;
using Unity.Burst;
using Unity.Mathematics;
using Unity.Transforms;
using static KexBuild.Constants;

namespace KexBuild {
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    [UpdateAfter(typeof(BuildableSnappingSystem))]
    [BurstCompile]
    public partial struct BuildableUpdateSystem : ISystem {
        [BurstCompile]
        public void OnUpdate(ref SystemState state) {
            float deltaTime = SystemAPI.Time.DeltaTime;
            float t = math.saturate(deltaTime * 30f);
            foreach (var (buildableRW, transformRW) in SystemAPI.Query<RefRW<Buildable>, RefRW<LocalTransform>>()) {
                ref var buildable = ref buildableRW.ValueRW;
                ref var transform = ref transformRW.ValueRW;

                float3 resolvedPosition = buildable.TargetPosition;
                resolvedPosition.y += buildable.VerticalOffset * GRID_SIZE;

                quaternion buildYaw = quaternion.RotateY(math.radians(buildable.TargetYaw));
                float3 localForward = math.rotate(buildYaw, new float3(0, 0, 1));
                float3 localRight = math.rotate(buildYaw, new float3(1, 0, 0));

                float3 cameraForwardXZ = math.normalize(new float3(buildable.RayDirection.x, 0, buildable.RayDirection.z));
                float forwardDot = math.abs(math.dot(cameraForwardXZ, localForward));
                float rightDot = math.abs(math.dot(cameraForwardXZ, localRight));

                bool useForward = forwardDot > rightDot;
                float3 snapAxis = useForward ?
                    localForward * math.sign(math.dot(cameraForwardXZ, localForward)) :
                    localRight * math.sign(math.dot(cameraForwardXZ, localRight));
                float3 depthAdjustment = buildable.DepthOffset * GRID_SIZE * snapAxis;
                resolvedPosition += depthAdjustment;

                buildable.ResolvedTargetPosition = resolvedPosition;
                buildable.ResolvedTargetRotation = buildYaw;

                float3 position = transform.Position;
                quaternion rotation = transform.Rotation;

                if (position.y == -999f) {
                    transform.Position = buildable.ResolvedTargetPosition;
                    transform.Rotation = buildable.ResolvedTargetRotation;
                }
                else {
                    position = math.lerp(position, buildable.ResolvedTargetPosition, t);
                    rotation = math.slerp(rotation, buildable.ResolvedTargetRotation, t);
                    transform.Position = position;
                    transform.Rotation = rotation;
                }
            }
        }
    }
}
