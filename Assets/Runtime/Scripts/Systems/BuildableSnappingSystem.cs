using Unity.Burst;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Physics;
using static KexBuild.Constants;

namespace KexBuild {
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    [UpdateAfter(typeof(FixedStepSimulationSystemGroup))]
    [BurstCompile]
    public partial struct BuildableSnappingSystem : ISystem {
        private const float MAX_RAY_DISTANCE = 10f;
        private const float MIN_BUILD_DISTANCE = 1f;
        private const float DISTANCE_WEIGHT = 0.7f;
        private const float ANGLE_WEIGHT = 0.3f;

        [BurstCompile]
        public void OnUpdate(ref SystemState state) {
            var snapLookup = SystemAPI.GetBufferLookup<SnapPosition>(true);
            var collisionWorld = SystemAPI.GetSingleton<PhysicsWorldSingleton>().CollisionWorld;

            uint groundLayerMask = 1u << 6;
            uint buildableLayerMask = 1u << 7;

            foreach (var buildableRW in SystemAPI.Query<RefRW<Buildable>>()) {
                ref var buildable = ref buildableRW.ValueRW;

                float3 rayOrigin = buildable.RayOrigin;
                float3 rayDirection = buildable.RayDirection;
                quaternion buildYaw = quaternion.RotateY(math.radians(buildable.TargetYaw));

                var rayInput = new RaycastInput {
                    Start = rayOrigin,
                    End = rayOrigin + rayDirection * MAX_RAY_DISTANCE,
                    Filter = new CollisionFilter {
                        BelongsTo = ~0u,
                        CollidesWith = groundLayerMask | buildableLayerMask,
                        GroupIndex = 0
                    }
                };

                float3 defaultTarget;
                if (collisionWorld.CastRay(rayInput, out RaycastHit hit)) {
                    defaultTarget = hit.Position;
                }
                else {
                    defaultTarget = rayOrigin + rayDirection * MAX_RAY_DISTANCE;
                }

                float3 playerPos2D = new(rayOrigin.x, 0f, rayOrigin.z);
                float3 targetPos2D = new(defaultTarget.x, 0f, defaultTarget.z);
                float horizontalDistance = math.distance(playerPos2D, targetPos2D);

                if (horizontalDistance < MIN_BUILD_DISTANCE && horizontalDistance > 0.001f) {
                    float3 horizontalDirection = math.normalize(targetPos2D - playerPos2D);
                    float3 newTarget2D = playerPos2D + horizontalDirection * MIN_BUILD_DISTANCE;
                    defaultTarget = new float3(newTarget2D.x, defaultTarget.y, newTarget2D.z);
                }

                if (buildable.Definition != Entity.Null &&
                    SystemAPI.HasComponent<BuildableDefinition>(buildable.Definition)) {
                    var def = SystemAPI.GetComponent<BuildableDefinition>(buildable.Definition);
                    float yAdjustment = def.Size.y * 0.5f - def.Center.y;
                    defaultTarget.y += yAdjustment;
                }

                if (!snapLookup.TryGetBuffer(buildable.Definition, out var buildPoints)) {
                    buildable.TargetPosition = defaultTarget;
                    continue;
                }

                float3 bestOrigin = defaultTarget;
                float bestScore = float.MaxValue;
                bool foundSnap = false;

                foreach (var placed in SystemAPI.Query<PlacedBuildable>()) {
                    float3 toPlaced = placed.Position - rayOrigin;
                    float rayDistance = math.dot(toPlaced, rayDirection);

                    if (rayDistance < 0 || rayDistance > MAX_RAY_DISTANCE) continue;

                    if (!snapLookup.TryGetBuffer(placed.Definition, out var placePoints)) continue;

                    quaternion placedYaw = quaternion.RotateY(math.radians(placed.Yaw));

                    for (int bi = 0; bi < buildPoints.Length; bi++) {
                        var buildPoint = buildPoints[bi];
                        int3 bCell = buildPoint.Value;
                        byte bPriority = buildPoint.Priority;
                        float3 bLocal = new(bCell.x * GRID_SIZE, bCell.y * GRID_SIZE, bCell.z * GRID_SIZE);
                        float3 bLocalRot = math.rotate(buildYaw, bLocal);

                        for (int pi = 0; pi < placePoints.Length; pi++) {
                            var placePoint = placePoints[pi];
                            int3 pCell = placePoint.Value;
                            byte pPriority = placePoint.Priority;
                            float3 pLocal = new(pCell.x * GRID_SIZE, pCell.y * GRID_SIZE, pCell.z * GRID_SIZE);
                            float3 pWorld = placed.Position + math.rotate(placedYaw, pLocal);

                            float3 originCandidate = pWorld - bLocalRot;

                            float3 toCandidate = originCandidate - rayOrigin;
                            float projectionLength = math.dot(toCandidate, rayDirection);

                            if (projectionLength < 0 || projectionLength > MAX_RAY_DISTANCE) continue;

                            float3 closestPointOnRay = rayOrigin + rayDirection * projectionLength;
                            float distanceFromRay = math.distance(closestPointOnRay, originCandidate);

                            if (distanceFromRay > SNAP_THRESHOLD) continue;

                            float3 dirToCandidate = math.normalize(toCandidate);
                            float angleCosine = math.dot(rayDirection, dirToCandidate);
                            float angleScore = 1f - angleCosine;

                            float distanceScore = distanceFromRay / SNAP_THRESHOLD;
                            float combinedScore = DISTANCE_WEIGHT * distanceScore + ANGLE_WEIGHT * angleScore;
                            
                            float priorityMultiplier = (bPriority == 1 && pPriority == 1) ? 0.5f : 1.0f;
                            float finalScore = combinedScore * priorityMultiplier;

                            if (finalScore < bestScore) {
                                bestScore = finalScore;
                                bestOrigin = originCandidate;
                                foundSnap = true;
                            }
                        }
                    }
                }

                buildable.TargetPosition = foundSnap ? bestOrigin : defaultTarget;
            }
        }
    }
}
