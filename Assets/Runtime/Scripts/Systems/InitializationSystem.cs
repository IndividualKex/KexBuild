using Unity.Entities;
using Unity.Collections;

namespace KexBuild {
    public partial class InitializationSystem : SystemBase {
        private bool _initialized;

        protected override void OnCreate() {
            RequireForUpdate<InitializeEvent>();
        }

        protected override void OnUpdate() {
            if (_initialized) {
                throw new System.Exception("Runtime already initialized");
            }

            using var ecb = new EntityCommandBuffer(Allocator.Temp);

            foreach (var (_, entity) in SystemAPI.Query<InitializeEvent>().WithEntityAccess()) {
                ecb.DestroyEntity(entity);
                if (_initialized) {
                    throw new System.Exception("Runtime already initialized");
                }
                _initialized = true;
            }

            UnityEngine.Debug.Log("Initializing runtime");

            ecb.Playback(EntityManager);
        }
    }
}
