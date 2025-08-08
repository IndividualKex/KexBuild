using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using static KexBuild.Constants;

namespace KexBuild.UI {
    [CustomEditor(typeof(BuildableDefinitionAuthoring))]
    public class BuildableDefinitionAuthoringEditor : Editor {

        public override void OnInspectorGUI() {
            DrawDefaultInspector();

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Snap Positions Utilities", EditorStyles.boldLabel);

            using (new EditorGUILayout.HorizontalScope()) {
                if (GUILayout.Button("Populate From Bounds")) {
                    PopulateFromBounds();
                }
                if (GUILayout.Button("Clear")) {
                    ClearSnapPositions();
                }
            }
        }

        private void PopulateFromBounds() {
            var authoring = (BuildableDefinitionAuthoring)target;
            var center = authoring.Center;
            var size = authoring.Size;

            float minX = center.x - size.x * 0.5f;
            float maxX = center.x + size.x * 0.5f;
            float minY = center.y - size.y * 0.5f;
            float maxY = center.y + size.y * 0.5f;
            float minZ = center.z - size.z * 0.5f;
            float maxZ = center.z + size.z * 0.5f;

            int xMin = Mathf.CeilToInt(minX / GRID_SIZE);
            int xMax = Mathf.FloorToInt(maxX / GRID_SIZE);
            int yMin = Mathf.CeilToInt(minY / GRID_SIZE);
            int yMax = Mathf.FloorToInt(maxY / GRID_SIZE);
            int zMin = Mathf.CeilToInt(minZ / GRID_SIZE);
            int zMax = Mathf.FloorToInt(maxZ / GRID_SIZE);

            if (xMin > xMax) xMin = xMax = Mathf.RoundToInt(center.x / GRID_SIZE);
            if (yMin > yMax) yMin = yMax = Mathf.RoundToInt(center.y / GRID_SIZE);
            if (zMin > zMax) zMin = zMax = Mathf.RoundToInt(center.z / GRID_SIZE);

            var snapPoints = new List<BuildableDefinitionAuthoring.SnapPointData>();
            
            for (int x = xMin; x <= xMax; x++) {
                for (int y = yMin; y <= yMax; y++) {
                    for (int z = zMin; z <= zMax; z++) {
                        bool isCorner = (x == xMin || x == xMax) && 
                                       (y == yMin || y == yMax) && 
                                       (z == zMin || z == zMax);
                        
                        bool isPrimary = isCorner;
                        
                        snapPoints.Add(new BuildableDefinitionAuthoring.SnapPointData {
                            Position = new Vector3Int(x, y, z),
                            IsPrimary = isPrimary
                        });
                    }
                }
            }

            var so = new SerializedObject(authoring);
            var prop = so.FindProperty("SnapPoints");
            so.Update();
            prop.arraySize = snapPoints.Count;
            for (int i = 0; i < snapPoints.Count; i++) {
                var element = prop.GetArrayElementAtIndex(i);
                element.FindPropertyRelative("Position").vector3IntValue = snapPoints[i].Position;
                element.FindPropertyRelative("IsPrimary").boolValue = snapPoints[i].IsPrimary;
            }
            so.ApplyModifiedProperties();
            Undo.RecordObject(authoring, "Populate Snap Positions");
            EditorUtility.SetDirty(authoring);
        }

        private void ClearSnapPositions() {
            var authoring = (BuildableDefinitionAuthoring)target;
            var so = new SerializedObject(authoring);
            var prop = so.FindProperty("SnapPoints");
            so.Update();
            prop.arraySize = 0;
            so.ApplyModifiedProperties();
            Undo.RecordObject(authoring, "Clear Snap Positions");
            EditorUtility.SetDirty(authoring);
        }
    }
}
