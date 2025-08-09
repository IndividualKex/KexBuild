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

            uint groundLayerMask = 0;

            foreach (var (evt, entity) in SystemAPI.Query<InitializeEvent>().WithEntityAccess()) {
                ecb.DestroyEntity(entity);
                if (_initialized) {
                    throw new System.Exception("Runtime already initialized");
                }
                groundLayerMask = (uint)evt.GroundLayerMask.value;
                _initialized = true;
            }

            var snapPointMaterial = Resources.Load<Material>("SnapPoint");

            var settingsEntity = ecb.CreateEntity();
            ecb.AddComponent(settingsEntity, new GlobalSettings {
                SnapPointMaterial = snapPointMaterial
            });
            ecb.AddComponent(settingsEntity, new LayerMaskSettings {
                GroundMask = groundLayerMask,
            });
            ecb.SetName(settingsEntity, "KexBuild Global Settings");

            ecb.Playback(EntityManager);
        }
    }
}
