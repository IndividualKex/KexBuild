using Unity.Entities;
using Unity.Collections;
using UnityEngine;

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

            var duplicationMaterial = Resources.Load<Material>("Duplication");

            var settingsEntity = ecb.CreateEntity();
            ecb.AddComponent(settingsEntity, new GlobalSettings {
                DuplicationMaterial = duplicationMaterial
            });
            ecb.SetName(settingsEntity, "KexBuild Global Settings");

            ecb.Playback(EntityManager);
        }
    }
}
