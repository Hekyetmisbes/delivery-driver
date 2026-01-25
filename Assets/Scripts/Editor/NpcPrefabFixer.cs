using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using TrafficSystem;

namespace TrafficSystemEditor
{
    public static class NpcPrefabFixer
    {
        private const string NpcPrefabsPath = "Assets/Prefabs/NPCs";

        [MenuItem("Tools/Traffic System/Fix Selected NPC Prefabs")]
        private static void FixSelectedPrefabs()
        {
            Object[] selection = Selection.objects;
            if (selection == null || selection.Length == 0)
            {
                Debug.LogWarning("[NpcPrefabFixer] No prefabs selected.");
                return;
            }

            List<string> paths = new List<string>();
            foreach (Object obj in selection)
            {
                string path = AssetDatabase.GetAssetPath(obj);
                if (string.IsNullOrEmpty(path))
                    continue;

                if (path.EndsWith(".prefab", System.StringComparison.OrdinalIgnoreCase))
                    paths.Add(path);
            }

            if (paths.Count == 0)
            {
                Debug.LogWarning("[NpcPrefabFixer] Selection contains no prefab assets.");
                return;
            }

            int fixedCount = FixPrefabs(paths);
            Debug.Log($"[NpcPrefabFixer] Fixed {fixedCount} prefab(s) from selection.");
        }

        [MenuItem("Tools/Traffic System/Fix All NPC Prefabs")]
        private static void FixAllNpcPrefabs()
        {
            string[] guids = AssetDatabase.FindAssets("t:Prefab", new[] { NpcPrefabsPath });
            if (guids == null || guids.Length == 0)
            {
                Debug.LogWarning("[NpcPrefabFixer] No NPC prefabs found.");
                return;
            }

            List<string> paths = new List<string>();
            foreach (string guid in guids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                if (!string.IsNullOrEmpty(path))
                    paths.Add(path);
            }

            int fixedCount = FixPrefabs(paths);
            Debug.Log($"[NpcPrefabFixer] Fixed {fixedCount} NPC prefab(s).");
        }

        private static int FixPrefabs(List<string> prefabPaths)
        {
            int fixedCount = 0;
            foreach (string path in prefabPaths)
            {
                if (TryFixPrefab(path))
                    fixedCount++;
            }
            return fixedCount;
        }

        private static bool TryFixPrefab(string path)
        {
            GameObject root = PrefabUtility.LoadPrefabContents(path);
            if (root == null)
                return false;

            bool changed = false;
            NpcCarAgent agent = root.GetComponentInChildren<NpcCarAgent>(true);
            if (agent == null)
            {
                PrefabUtility.UnloadPrefabContents(root);
                return false;
            }

            SerializedObject so = new SerializedObject(agent);
            SerializedProperty frontLeft = so.FindProperty("frontLeftCollider");
            SerializedProperty frontRight = so.FindProperty("frontRightCollider");
            SerializedProperty rearLeft = so.FindProperty("rearLeftCollider");
            SerializedProperty rearRight = so.FindProperty("rearRightCollider");
            SerializedProperty autoDetect = so.FindProperty("autoDetectModelForward");
            SerializedProperty modelForward = so.FindProperty("modelForwardLocal");
            SerializedProperty groundMask = so.FindProperty("groundMask");

            WheelCollider fl = frontLeft != null ? frontLeft.objectReferenceValue as WheelCollider : null;
            WheelCollider fr = frontRight != null ? frontRight.objectReferenceValue as WheelCollider : null;
            WheelCollider rl = rearLeft != null ? rearLeft.objectReferenceValue as WheelCollider : null;
            WheelCollider rr = rearRight != null ? rearRight.objectReferenceValue as WheelCollider : null;

            if (autoDetect != null && modelForward != null)
            {
                autoDetect.boolValue = true;

                Vector3 localForward = ComputeLocalForward(agent.transform, fl, fr, rl, rr);
                if (localForward.sqrMagnitude > 0.001f)
                {
                    modelForward.vector3Value = localForward.normalized;
                    changed = true;
                }
            }

            if (ReassignWheelReferences(so, agent.transform))
            {
                changed = true;
            }

            if (NormalizeWheelColliderHeights(agent.transform, fl, fr, rl, rr))
            {
                changed = true;
            }

            if (groundMask != null && groundMask.intValue == ~0)
            {
                int roadLayer = LayerMask.NameToLayer("Road");
                if (roadLayer >= 0)
                {
                    groundMask.intValue = 1 << roadLayer;
                    changed = true;
                }
            }

            if (changed)
            {
                so.ApplyModifiedPropertiesWithoutUndo();
                PrefabUtility.SaveAsPrefabAsset(root, path);
            }

            PrefabUtility.UnloadPrefabContents(root);
            return changed;
        }

        private static Vector3 ComputeLocalForward(Transform root, WheelCollider fl, WheelCollider fr, WheelCollider rl, WheelCollider rr)
        {
            if (root == null || fl == null || fr == null || rl == null || rr == null)
                return Vector3.forward;

            Vector3 frontAxle = (fl.transform.position + fr.transform.position) * 0.5f;
            Vector3 rearAxle = (rl.transform.position + rr.transform.position) * 0.5f;
            Vector3 frontLocal = root.InverseTransformPoint(frontAxle);
            Vector3 rearLocal = root.InverseTransformPoint(rearAxle);

            Vector3 localForward = frontLocal - rearLocal;
            if (localForward.sqrMagnitude < 0.001f)
                return Vector3.forward;

            return localForward.normalized;
        }

        private static bool ReassignWheelReferences(SerializedObject so, Transform root)
        {
            SerializedProperty frontLeft = so.FindProperty("frontLeftCollider");
            SerializedProperty frontRight = so.FindProperty("frontRightCollider");
            SerializedProperty rearLeft = so.FindProperty("rearLeftCollider");
            SerializedProperty rearRight = so.FindProperty("rearRightCollider");

            WheelCollider fl = frontLeft != null ? frontLeft.objectReferenceValue as WheelCollider : null;
            WheelCollider fr = frontRight != null ? frontRight.objectReferenceValue as WheelCollider : null;
            WheelCollider rl = rearLeft != null ? rearLeft.objectReferenceValue as WheelCollider : null;
            WheelCollider rr = rearRight != null ? rearRight.objectReferenceValue as WheelCollider : null;

            if (root == null || fl == null || fr == null || rl == null || rr == null)
                return false;

            WheelCollider[] wheels = { fl, fr, rl, rr };
            Vector3[] localPositions = new Vector3[wheels.Length];
            for (int i = 0; i < wheels.Length; i++)
                localPositions[i] = root.InverseTransformPoint(wheels[i].transform.position);

            float minX = localPositions[0].x;
            float maxX = localPositions[0].x;
            float minZ = localPositions[0].z;
            float maxZ = localPositions[0].z;
            for (int i = 1; i < localPositions.Length; i++)
            {
                Vector3 p = localPositions[i];
                if (p.x < minX) minX = p.x;
                if (p.x > maxX) maxX = p.x;
                if (p.z < minZ) minZ = p.z;
                if (p.z > maxZ) maxZ = p.z;
            }

            float spanX = maxX - minX;
            float spanZ = maxZ - minZ;
            Vector3 axis = spanZ >= spanX ? Vector3.forward : Vector3.right;

            List<(WheelCollider wheel, Vector3 localPos, float proj)> list = new List<(WheelCollider, Vector3, float)>();
            foreach (WheelCollider wheel in wheels)
            {
                Vector3 lp = root.InverseTransformPoint(wheel.transform.position);
                list.Add((wheel, lp, Vector3.Dot(lp, axis)));
            }

            list.Sort((a, b) => b.proj.CompareTo(a.proj));
            var frontA = list[0];
            var frontB = list[1];
            var rearA = list[2];
            var rearB = list[3];

            Vector3 frontAvg = (frontA.localPos + frontB.localPos) * 0.5f;
            float sign = Vector3.Dot(frontAvg, axis) >= 0f ? 1f : -1f;
            Vector3 forwardAxis = axis * sign;
            Vector3 rightAxis = Vector3.Cross(Vector3.up, forwardAxis).normalized;
            if (rightAxis.sqrMagnitude < 0.001f)
                rightAxis = Vector3.right;

            float frontADot = Vector3.Dot(frontA.localPos, rightAxis);
            float frontBDot = Vector3.Dot(frontB.localPos, rightAxis);
            float rearADot = Vector3.Dot(rearA.localPos, rightAxis);
            float rearBDot = Vector3.Dot(rearB.localPos, rightAxis);

            WheelCollider newFrontLeft = frontADot <= frontBDot ? frontA.wheel : frontB.wheel;
            WheelCollider newFrontRight = frontADot <= frontBDot ? frontB.wheel : frontA.wheel;
            WheelCollider newRearLeft = rearADot <= rearBDot ? rearA.wheel : rearB.wheel;
            WheelCollider newRearRight = rearADot <= rearBDot ? rearB.wheel : rearA.wheel;

            bool changed = false;
            if (frontLeft != null && frontLeft.objectReferenceValue != newFrontLeft)
            {
                frontLeft.objectReferenceValue = newFrontLeft;
                changed = true;
            }
            if (frontRight != null && frontRight.objectReferenceValue != newFrontRight)
            {
                frontRight.objectReferenceValue = newFrontRight;
                changed = true;
            }
            if (rearLeft != null && rearLeft.objectReferenceValue != newRearLeft)
            {
                rearLeft.objectReferenceValue = newRearLeft;
                changed = true;
            }
            if (rearRight != null && rearRight.objectReferenceValue != newRearRight)
            {
                rearRight.objectReferenceValue = newRearRight;
                changed = true;
            }

            return changed;
        }

        private static bool NormalizeWheelColliderHeights(Transform root, WheelCollider fl, WheelCollider fr, WheelCollider rl, WheelCollider rr)
        {
            if (root == null || fl == null || fr == null || rl == null || rr == null)
                return false;

            WheelCollider[] wheels = { fl, fr, rl, rr };
            float minContactY = float.MaxValue;

            foreach (WheelCollider wheel in wheels)
            {
                Vector3 localPos = root.InverseTransformPoint(wheel.transform.position);
                float contactY = localPos.y - wheel.radius;
                if (contactY < minContactY)
                    minContactY = contactY;
            }

            if (Mathf.Abs(minContactY) < 0.001f)
                return false;

            foreach (WheelCollider wheel in wheels)
            {
                Transform wt = wheel.transform;
                Vector3 localPos = wt.localPosition;
                localPos.y -= minContactY;
                wt.localPosition = localPos;
            }

            return true;
        }
    }
}
