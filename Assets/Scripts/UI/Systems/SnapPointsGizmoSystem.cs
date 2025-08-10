using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using UnityEngine;

namespace KexBuild.UI {
    public partial class SnapPointsGizmoSystem : SystemBase {
        protected override void OnCreate() {
            RequireForUpdate<SnapSettings>();
        }

        protected override void OnUpdate() {
            var snapSettings = SystemAPI.GetSingleton<SnapSettings>();
            float gridSize = snapSettings.GridSize;

            float half = math.max(0.025f, gridSize * 0.15f);
            const float duration = 0.1f;
            const float MAX_RAY_DISTANCE = 10f;

            Color rayColor = new(1f, 0.5f, 0f, 0.8f);
            Color targetColor = new(0f, 1f, 0.2f, 1f);
            Color primaryBuildableColor = Color.white;
            Color secondaryBuildableColor = new(0.4f, 0.6f, 0.8f, 0.6f);
            Color primaryPlacedColor = new(0.9f, 0.9f, 0.9f, 0.8f);
            Color secondaryPlacedColor = new(0.5f, 0.5f, 0.5f, 0.4f);

            var snapLookup = SystemAPI.GetBufferLookup<SnapPosition>(true);

            foreach (var (localToWorld, buildable) in SystemAPI.Query<LocalToWorld, Buildable>()) {
                float3 rayOrigin = buildable.RayOrigin;
                float3 rayDirection = buildable.RayDirection;
                float3 targetPosition = buildable.TargetPosition;

                Debug.DrawRay(rayOrigin, rayDirection * MAX_RAY_DISTANCE, rayColor, duration, false);

                Debug.DrawLine(targetPosition + new float3(-half * 2, 0, 0), targetPosition + new float3(half * 2, 0, 0), targetColor, duration, false);
                Debug.DrawLine(targetPosition + new float3(0, -half * 2, 0), targetPosition + new float3(0, half * 2, 0), targetColor, duration, false);
                Debug.DrawLine(targetPosition + new float3(0, 0, -half * 2), targetPosition + new float3(0, 0, half * 2), targetColor, duration, false);

                if (buildable.Definition != Entity.Null && snapLookup.HasBuffer(buildable.Definition)) {
                    var buf = snapLookup[buildable.Definition];
                    quaternion yaw = quaternion.RotateY(math.radians(buildable.TargetYaw));
                    for (int i = 0; i < buf.Length; i++) {
                        var snapPoint = buf[i];
                        int3 cell = snapPoint.Value;
                        byte priority = snapPoint.Priority;
                        float3 local = new(cell.x * gridSize, cell.y * gridSize, cell.z * gridSize);
                        float3 world = targetPosition + math.rotate(yaw, local);

                        Color color = priority == 1 ? primaryBuildableColor : secondaryBuildableColor;
                        float size = priority == 1 ? half * 2f : half * 0.75f;

                        Debug.DrawLine(world + new float3(-size, 0, 0), world + new float3(size, 0, 0), color, duration, false);
                        Debug.DrawLine(world + new float3(0, -size, 0), world + new float3(0, size, 0), color, duration, false);
                        Debug.DrawLine(world + new float3(0, 0, -size), world + new float3(0, 0, size), color, duration, false);
                    }
                }

                foreach (var placed in SystemAPI.Query<PlacedBuildable>()) {
                    float3 toPlaced = placed.Position - rayOrigin;
                    float rayDistance = math.dot(toPlaced, rayDirection);

                    if (rayDistance < 0 || rayDistance > MAX_RAY_DISTANCE) continue;

                    float3 closestPointOnRay = rayOrigin + rayDirection * rayDistance;
                    float distanceFromRay = math.distance(closestPointOnRay, placed.Position);

                    if (distanceFromRay > 5f) continue;

                    if (placed.Definition == Entity.Null || !snapLookup.HasBuffer(placed.Definition)) continue;

                    var pbuf = snapLookup[placed.Definition];
                    quaternion pYaw = quaternion.RotateY(math.radians(placed.Yaw));

                    for (int i = 0; i < pbuf.Length; i++) {
                        var snapPoint = pbuf[i];
                        int3 cell = snapPoint.Value;
                        byte priority = snapPoint.Priority;
                        float3 local = new(cell.x * gridSize, cell.y * gridSize, cell.z * gridSize);
                        float3 world = placed.Position + math.rotate(pYaw, local);

                        Color color = priority == 1 ? primaryPlacedColor : secondaryPlacedColor;
                        float size = priority == 1 ? half * 1.5f : half * 0.6f;

                        Debug.DrawLine(world + new float3(-size, 0, 0), world + new float3(size, 0, 0), color, duration, false);
                        Debug.DrawLine(world + new float3(0, -size, 0), world + new float3(0, size, 0), color, duration, false);
                        Debug.DrawLine(world + new float3(0, 0, -size), world + new float3(0, 0, size), color, duration, false);
                    }
                }
            }
        }
    }
}
