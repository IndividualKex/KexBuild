using Unity.Entities;
using Unity.Collections;
using UnityEngine;

namespace KexBuild {
    public class KexBuildManager : MonoBehaviour {
        private void Start() {
            var entityManager = World.DefaultGameObjectInjectionWorld.EntityManager;
            using var ecb = new EntityCommandBuffer(Allocator.Temp);
            var entity = ecb.CreateEntity();
            ecb.AddComponent<InitializeEvent>(entity);
            ecb.Playback(entityManager);
        }
    }
}
