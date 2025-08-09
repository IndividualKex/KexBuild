using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Rendering;

namespace KexBuild {
    [UpdateInGroup(typeof(PresentationSystemGroup))]
    public partial class SnapPointRenderSystem : SystemBase {
        private const int MAX_SNAP_POINTS = 2048;
        private const float MAX_RENDER_DISTANCE = 50f;
        private const float UNIFORM_SIZE = 0.25f;

        private GraphicsBuffer _matricesBuffer;
        private GraphicsBuffer _visualizationBuffer;
        private GraphicsBuffer _argsBuffer;

        private NativeList<float4x4> _matrices;
        private NativeList<float4> _visualizationData;

        private Mesh _quadMesh;

        protected override void OnCreate() {
            _matricesBuffer = new GraphicsBuffer(GraphicsBuffer.Target.Structured, MAX_SNAP_POINTS, sizeof(float) * 16);
            _visualizationBuffer = new GraphicsBuffer(GraphicsBuffer.Target.Structured, MAX_SNAP_POINTS, sizeof(float) * 4);
            _argsBuffer = new GraphicsBuffer(GraphicsBuffer.Target.IndirectArguments, 5, sizeof(uint));

            _matrices = new NativeList<float4x4>(MAX_SNAP_POINTS, Allocator.Persistent);
            _visualizationData = new NativeList<float4>(MAX_SNAP_POINTS, Allocator.Persistent);

            CreateQuadMesh();

            RequireForUpdate<GlobalSettings>();
            RequireForUpdate<SnapPointSettings>();
        }

        protected override void OnDestroy() {
            _matricesBuffer?.Dispose();
            _visualizationBuffer?.Dispose();
            _argsBuffer?.Dispose();

            if (_matrices.IsCreated) _matrices.Dispose();
            if (_visualizationData.IsCreated) _visualizationData.Dispose();

            if (_quadMesh != null) Object.DestroyImmediate(_quadMesh);
        }

        protected override void OnUpdate() {
            var settings = SystemAPI.ManagedAPI.GetSingleton<GlobalSettings>();
            if (settings.SnapPointMaterial == null || _quadMesh == null) return;

            _matrices.Clear();
            _visualizationData.Clear();

            float cellSize = Constants.GRID_SIZE;
            const float ALIGNMENT_THRESHOLD = 0.01f;

            var snapLookup = SystemAPI.GetBufferLookup<SnapPosition>(true);
            var snapPointSettings = SystemAPI.GetSingleton<SnapPointSettings>();
            
            if (snapPointSettings.Mode == SnapMode.None) return;

            // Find alignments only between buildables and placed buildables
            var alignedPoints = new NativeHashSet<float3>(256, Allocator.Temp);
            var buildableSnapPoints = new NativeList<float3>(256, Allocator.Temp);
            var buildablePositions = new NativeList<float3>(32, Allocator.Temp);
            const float NEARBY_DISTANCE = 5.0f; // Only show snap points within this distance

            // Collect all buildable snap points and positions
            foreach (var buildable in SystemAPI.Query<Buildable>()) {
                if (buildable.Definition == Entity.Null || !snapLookup.HasBuffer(buildable.Definition)) continue;

                buildablePositions.Add(buildable.TargetPosition);
                var buf = snapLookup[buildable.Definition];
                quaternion yaw = quaternion.RotateY(math.radians(buildable.TargetYaw));

                for (int i = 0; i < buf.Length; i++) {
                    var snapPoint = buf[i];
                    int3 cell = snapPoint.Value;
                    byte priority = snapPoint.Priority;
                    
                    if (snapPointSettings.Mode == SnapMode.Simple && priority != 1) continue;
                    
                    float3 local = new(cell.x * cellSize, cell.y * cellSize, cell.z * cellSize);
                    float3 world = buildable.TargetPosition + math.rotate(yaw, local);
                    buildableSnapPoints.Add(world);
                }
            }

            // Check for alignments with placed buildables (only near buildables)
            foreach (var placed in SystemAPI.Query<PlacedBuildable>()) {
                if (placed.Definition == Entity.Null || !snapLookup.HasBuffer(placed.Definition)) continue;

                // Check if this placed buildable is near any active buildable
                bool isNearBuildable = false;
                for (int b = 0; b < buildablePositions.Length; b++) {
                    if (math.distance(placed.Position, buildablePositions[b]) < NEARBY_DISTANCE) {
                        isNearBuildable = true;
                        break;
                    }
                }
                
                if (!isNearBuildable) continue;

                var buf = snapLookup[placed.Definition];
                quaternion yaw = quaternion.RotateY(math.radians(placed.Yaw));

                for (int i = 0; i < buf.Length; i++) {
                    var snapPoint = buf[i];
                    int3 cell = snapPoint.Value;
                    byte priority = snapPoint.Priority;
                    
                    if (snapPointSettings.Mode == SnapMode.Simple && priority != 1) continue;
                    
                    float3 local = new(cell.x * cellSize, cell.y * cellSize, cell.z * cellSize);
                    float3 placedWorld = placed.Position + math.rotate(yaw, local);
                    
                    // Check if this placed snap point aligns with any buildable snap point
                    for (int j = 0; j < buildableSnapPoints.Length; j++) {
                        if (math.distance(placedWorld, buildableSnapPoints[j]) < ALIGNMENT_THRESHOLD) {
                            alignedPoints.Add(placedWorld);
                            alignedPoints.Add(buildableSnapPoints[j]);
                        }
                    }
                }
            }

            // Draw buildable snap points
            foreach (var buildable in SystemAPI.Query<Buildable>()) {
                if (buildable.Definition == Entity.Null || !snapLookup.HasBuffer(buildable.Definition)) continue;

                var buf = snapLookup[buildable.Definition];
                quaternion yaw = quaternion.RotateY(math.radians(buildable.TargetYaw));

                for (int i = 0; i < buf.Length; i++) {
                    var snapPoint = buf[i];
                    int3 cell = snapPoint.Value;
                    byte priority = snapPoint.Priority;
                    
                    if (snapPointSettings.Mode == SnapMode.Simple && priority != 1) continue;
                    
                    float3 local = new(cell.x * cellSize, cell.y * cellSize, cell.z * cellSize);
                    float3 world = buildable.TargetPosition + math.rotate(yaw, local);

                    bool isAligned = alignedPoints.Contains(world);
                    bool isPrimary = priority == 1;

                    AddSnapPoint(world, isAligned, isPrimary);
                }
            }

            // Draw placed buildable snap points (only if near an active buildable)
            foreach (var placed in SystemAPI.Query<PlacedBuildable>()) {
                if (placed.Definition == Entity.Null || !snapLookup.HasBuffer(placed.Definition)) continue;

                // Check if this placed buildable is near any active buildable
                bool isNearBuildable = false;
                for (int b = 0; b < buildablePositions.Length; b++) {
                    if (math.distance(placed.Position, buildablePositions[b]) < NEARBY_DISTANCE) {
                        isNearBuildable = true;
                        break;
                    }
                }
                
                if (!isNearBuildable) continue;

                var buf = snapLookup[placed.Definition];
                quaternion yaw = quaternion.RotateY(math.radians(placed.Yaw));

                for (int i = 0; i < buf.Length; i++) {
                    var snapPoint = buf[i];
                    int3 cell = snapPoint.Value;
                    byte priority = snapPoint.Priority;
                    
                    if (snapPointSettings.Mode == SnapMode.Simple && priority != 1) continue;
                    
                    float3 local = new(cell.x * cellSize, cell.y * cellSize, cell.z * cellSize);
                    float3 world = placed.Position + math.rotate(yaw, local);

                    bool isAligned = alignedPoints.Contains(world);
                    bool isPrimary = priority == 1;

                    AddSnapPoint(world, isAligned, isPrimary);
                }
            }

            alignedPoints.Dispose();
            buildableSnapPoints.Dispose();
            buildablePositions.Dispose();

            if (_matrices.Length > 0) {
                RenderSnapPoints(settings.SnapPointMaterial);
            }
        }

        private void RenderSnapPoints(Material material) {
            if (_matrices.Length == 0) return;

            _matricesBuffer.SetData(_matrices.AsArray(), 0, 0, _matrices.Length);
            _visualizationBuffer.SetData(_visualizationData.AsArray(), 0, 0, _visualizationData.Length);

            var matProps = new MaterialPropertyBlock();
            matProps.SetBuffer("_Matrices", _matricesBuffer);
            matProps.SetBuffer("_VisualizationData", _visualizationBuffer);

            var bounds = new Bounds(Vector3.zero, 2 * MAX_RENDER_DISTANCE * Vector3.one);
            var renderParams = new RenderParams(material) {
                worldBounds = bounds,
                matProps = matProps,
                shadowCastingMode = ShadowCastingMode.Off,
                receiveShadows = false
            };

            var args = new uint[5] { _quadMesh.GetIndexCount(0), (uint)_matrices.Length, 0, 0, 0 };
            _argsBuffer.SetData(args);

            Graphics.RenderMeshIndirect(renderParams, _quadMesh, _argsBuffer);
        }

        private void CreateQuadMesh() {
            _quadMesh = new Mesh {
                name = "SnapPointQuad"
            };

            var vertices = new Vector3[] {
                new(-0.5f, -0.5f, 0),
                new(0.5f, -0.5f, 0),
                new(0.5f, 0.5f, 0),
                new(-0.5f, 0.5f, 0)
            };

            var uvs = new Vector2[] {
                new(0, 0),
                new(1, 0),
                new(1, 1),
                new(0, 1)
            };

            var indices = new int[] { 0, 1, 2, 0, 2, 3 };

            _quadMesh.vertices = vertices;
            _quadMesh.uv = uvs;
            _quadMesh.SetIndices(indices, MeshTopology.Triangles, 0);
            _quadMesh.bounds = new Bounds(Vector3.zero, Vector3.one * 100f);
        }

        private void AddSnapPoint(float3 position, bool isAligned, bool isPrimary) {
            var matrix = float4x4.TRS(position, quaternion.identity, new float3(UNIFORM_SIZE, UNIFORM_SIZE, 1));

            var data = new float4(
                isAligned ? 1.0f : 0.0f,
                isPrimary ? 1.0f : 0.0f,
                0.0f,
                0.0f
            );

            _matrices.Add(matrix);
            _visualizationData.Add(data);
        }
    }
}
