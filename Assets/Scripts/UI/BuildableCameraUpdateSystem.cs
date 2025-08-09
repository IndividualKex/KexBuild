using Unity.Entities;
using Unity.Mathematics;

namespace KexBuild.UI {
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    [UpdateBefore(typeof(BuildableSnappingSystem))]
    public partial class BuildableCameraUpdateSystem : SystemBase {
        private UnityEngine.Transform _camera;

        protected override void OnCreate() {
            _camera = UnityEngine.Camera.main.transform;
        }

        protected override void OnUpdate() {
            foreach (var buildableRW in SystemAPI.Query<RefRW<Buildable>>()) {
                ref var buildable = ref buildableRW.ValueRW;

                float3 cameraPos = _camera.position;
                float3 cameraForward = _camera.forward;
                float3 forward = math.normalize(cameraForward + math.down() * 0.2f);

                buildable.RayOrigin = cameraPos;
                buildable.RayDirection = forward;
            }
        }
    }
}
