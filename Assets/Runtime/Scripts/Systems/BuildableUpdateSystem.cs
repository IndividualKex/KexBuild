using Unity.Entities;
using Unity.Burst;
using Unity.Mathematics;
using Unity.Transforms;

namespace KexBuild {
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    [UpdateAfter(typeof(BuildableSnappingSystem))]
    [BurstCompile]
    public partial struct BuildableUpdateSystem : ISystem {
        [BurstCompile]
        public void OnCreate(ref SystemState state) {
            state.RequireForUpdate<SnapSettings>();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state) {
            var snapSettings = SystemAPI.GetSingleton<SnapSettings>();
            float gridSize = snapSettings.GridSize;

            float deltaTime = SystemAPI.Time.DeltaTime;
            float t = math.saturate(deltaTime * 30f);
            foreach (var (buildable, transform) in SystemAPI.Query<RefRW<Buildable>, RefRW<LocalTransform>>()) {
                ref var buildableRef = ref buildable.ValueRW;
                ref var transformRef = ref transform.ValueRW;

                float3 resolvedPosition = buildableRef.TargetPosition;
                resolvedPosition.y += buildableRef.VerticalOffset * gridSize;

                quaternion buildYaw = quaternion.RotateY(math.radians(buildableRef.TargetYaw));
                float3 localForward = math.rotate(buildYaw, new float3(0, 0, 1));
                float3 localRight = math.rotate(buildYaw, new float3(1, 0, 0));

                float3 cameraForwardXZ = math.normalize(new float3(buildableRef.RayDirection.x, 0, buildableRef.RayDirection.z));
                float forwardDot = math.abs(math.dot(cameraForwardXZ, localForward));
                float rightDot = math.abs(math.dot(cameraForwardXZ, localRight));

                bool useForward = forwardDot > rightDot;
                float3 snapAxis = useForward ?
                    localForward * math.sign(math.dot(cameraForwardXZ, localForward)) :
                    localRight * math.sign(math.dot(cameraForwardXZ, localRight));
                float3 depthAdjustment = buildableRef.DepthOffset * gridSize * snapAxis;
                resolvedPosition += depthAdjustment;

                buildableRef.ResolvedTargetPosition = resolvedPosition;
                buildableRef.ResolvedTargetRotation = buildYaw;

                float3 position = transformRef.Position;
                quaternion rotation = transformRef.Rotation;

                if (position.y == -999f) {
                    transformRef.Position = buildableRef.ResolvedTargetPosition;
                    transformRef.Rotation = buildableRef.ResolvedTargetRotation;
                }
                else {
                    position = math.lerp(position, buildableRef.ResolvedTargetPosition, t);
                    rotation = math.slerp(rotation, buildableRef.ResolvedTargetRotation, t);
                    transformRef.Position = position;
                    transformRef.Rotation = rotation;
                }
            }
        }
    }
}
