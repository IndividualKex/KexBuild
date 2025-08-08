using Unity.Collections;
using Unity.Entities;
using UnityEngine.InputSystem;

namespace KexBuild.UI {
    [UpdateInGroup(typeof(InitializationSystemGroup))]
    public partial class BuildableSystem : SystemBase {
        private FixedString64Bytes[] _names;
        private Entity _current;
        private int _currentIndex = -1;

        protected override void OnCreate() {
            _names = new FixedString64Bytes[] {
                new("Floor"),
                new("Wall")
            };

            RequireForUpdate<PrefabsSingleton>();
        }

        protected override void OnUpdate() {
            var definitionQuery = SystemAPI.QueryBuilder()
                .WithAll<BuildableDefinition>()
                .Build();
            int definitionCount = definitionQuery.CalculateEntityCount();
            using var map = new NativeHashMap<FixedString64Bytes, Entity>(definitionCount, Allocator.Temp);
            foreach (var (definition, entity) in SystemAPI.Query<BuildableDefinition>().WithEntityAccess()) {
                map.Add(definition.Name, entity);
            }

            var prefabs = SystemAPI.GetSingleton<PrefabsSingleton>();

            if (_current != Entity.Null &&
                !SystemAPI.HasComponent<Buildable>(_current)) {
                _current = Entity.Null;
            }

            if (Keyboard.current.digit1Key.wasPressedThisFrame) {
                bool isCurrent = _currentIndex == 0;
                Clear();
                if (!isCurrent) {
                    _current = EntityManager.Instantiate(prefabs.Floor);
                    EntityManager.SetComponentData(_current, new Buildable {
                        Definition = map[_names[0]]
                    });
                    _currentIndex = 0;
                }
            }

            if (Keyboard.current.digit2Key.wasPressedThisFrame) {
                bool isCurrent = _currentIndex == 1;
                Clear();
                if (!isCurrent) {
                    _current = EntityManager.Instantiate(prefabs.Wall);
                    EntityManager.SetComponentData(_current, new Buildable {
                        Definition = map[_names[1]]
                    });
                    _currentIndex = 1;
                }
            }

            if (Mouse.current.leftButton.wasPressedThisFrame &&
                _current != Entity.Null) {
                var requestEntity = EntityManager.CreateEntity();
                EntityManager.AddComponentData(requestEntity, new PlacementRequest {
                    BuildableEntity = _current
                });
                _current = Entity.Null;
                _currentIndex = -1;
            }
            else if (Mouse.current.rightButton.wasPressedThisFrame) {
                Clear();
            }
        }

        private void Clear() {
            if (_current != Entity.Null) {
                EntityManager.DestroyEntity(_current);
                _current = Entity.Null;
                _currentIndex = -1;
            }
        }
    }
}
