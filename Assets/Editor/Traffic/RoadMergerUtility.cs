using System.Collections.Generic;
using TrafficSystem.Spline;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace DeliveryDriver.EditorTools
{
    public static class RoadMergerUtility
    {
        private const float EndpointTolerance = 2.5f;

        /// <summary>
        /// Checks if the given intersection is a 2-way pass-through joint between two roads.
        /// </summary>
        public static bool IsTwoWayJoint(RoadIntersection intersection, out SplineRoad roadA, out SplineRoad roadB)
        {
            roadA = null;
            roadB = null;

            if (intersection == null) return false;

            if (intersection.connectedRoads == null)
            {
                intersection.connectedRoads = new List<SplineRoad>();
            }

            intersection.connectedRoads.RemoveAll(r => r == null);

            // Also check for roads physically meeting at intersection position
            SplineRoad[] allRoads = Object.FindObjectsByType<SplineRoad>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
            Vector3 jointPos = intersection.transform.position;
            float tolSqr = EndpointTolerance * EndpointTolerance;

            HashSet<SplineRoad> meetingRoads = new HashSet<SplineRoad>(intersection.connectedRoads);
            for (int i = 0; i < allRoads.Length; i++)
            {
                SplineRoad r = allRoads[i];
                if (r == null || !r.IsUsable) continue;

                float d0 = (r.GetPoint(0f) - jointPos).sqrMagnitude;
                float d1 = (r.GetPoint(1f) - jointPos).sqrMagnitude;
                if (d0 <= tolSqr || d1 <= tolSqr)
                {
                    meetingRoads.Add(r);
                }
            }

            meetingRoads.RemoveWhere(r => r == null || !r.IsUsable);

            if (meetingRoads.Count == 2)
            {
                var list = new List<SplineRoad>(meetingRoads);
                roadA = list[0];
                roadB = list[1];
                return roadA != roadB;
            }

            return false;
        }

        /// <summary>
        /// Merges roadA and roadB connected at 'joint' into a single smooth SplineRoad on roadA's GameObject.
        /// </summary>
        public static bool MergeTwoRoads(SplineRoad roadA, SplineRoad roadB, RoadIntersection joint)
        {
            if (roadA == null || roadB == null || roadA == roadB || !roadA.IsUsable || !roadB.IsUsable)
            {
                return false;
            }

            List<Vector3> anchorsA = roadA.GetAnchorPoints();
            List<Vector3> anchorsB = roadB.GetAnchorPoints();

            if (anchorsA.Count < 2 || anchorsB.Count < 2)
            {
                return false;
            }

            Vector3 aStart = roadA.GetPoint(0f);
            Vector3 aEnd = roadA.GetPoint(1f);
            Vector3 bStart = roadB.GetPoint(0f);
            Vector3 bEnd = roadB.GetPoint(1f);

            float dEndStart = Vector3.Distance(aEnd, bStart);
            float dEndEnd = Vector3.Distance(aEnd, bEnd);
            float dStartStart = Vector3.Distance(aStart, bStart);
            float dStartEnd = Vector3.Distance(aStart, bEnd);

            float minD = Mathf.Min(dEndStart, Mathf.Min(dEndEnd, Mathf.Min(dStartStart, dStartEnd)));

            List<Vector3> combinedAnchors = new List<Vector3>();
            RoadIntersection outerStartInter = null;
            RoadIntersection outerEndInter = null;

            if (Mathf.Approximately(minD, dEndStart))
            {
                // A -> B
                combinedAnchors.AddRange(anchorsA);
                for (int i = 1; i < anchorsB.Count; i++) combinedAnchors.Add(anchorsB[i]);
                outerStartInter = roadA.startIntersection;
                outerEndInter = roadB.endIntersection;
            }
            else if (Mathf.Approximately(minD, dEndEnd))
            {
                // A -> reverse(B)
                anchorsB.Reverse();
                combinedAnchors.AddRange(anchorsA);
                for (int i = 1; i < anchorsB.Count; i++) combinedAnchors.Add(anchorsB[i]);
                outerStartInter = roadA.startIntersection;
                outerEndInter = roadB.startIntersection;
            }
            else if (Mathf.Approximately(minD, dStartStart))
            {
                // reverse(A) -> B
                anchorsA.Reverse();
                combinedAnchors.AddRange(anchorsA);
                for (int i = 1; i < anchorsB.Count; i++) combinedAnchors.Add(anchorsB[i]);
                outerStartInter = roadA.endIntersection;
                outerEndInter = roadB.endIntersection;
            }
            else
            {
                // B -> A
                combinedAnchors.AddRange(anchorsB);
                for (int i = 1; i < anchorsA.Count; i++) combinedAnchors.Add(anchorsA[i]);
                outerStartInter = roadB.startIntersection;
                outerEndInter = roadA.endIntersection;
            }

            // Remove duplicated adjacent points
            for (int i = combinedAnchors.Count - 1; i > 0; i--)
            {
                if (Vector3.Distance(combinedAnchors[i], combinedAnchors[i - 1]) < 0.2f)
                {
                    combinedAnchors.RemoveAt(i);
                }
            }

            if (combinedAnchors.Count < 2) return false;

            Undo.IncrementCurrentGroup();
            Undo.SetCurrentGroupName("Merge Spline Roads");

            Undo.RecordObject(roadA, "Merge Spline Roads");
            roadA.SetSmoothPath(combinedAnchors);
            roadA.startIntersection = outerStartInter;
            roadA.endIntersection = outerEndInter;

            // Rewire outer intersections
            if (outerStartInter != null)
            {
                Undo.RecordObject(outerStartInter, "Update Intersection Connected Roads");
                outerStartInter.UnregisterConnectedRoad(roadB);
                outerStartInter.RegisterConnectedRoad(roadA);
                EditorUtility.SetDirty(outerStartInter);
            }

            if (outerEndInter != null)
            {
                Undo.RecordObject(outerEndInter, "Update Intersection Connected Roads");
                outerEndInter.UnregisterConnectedRoad(roadB);
                outerEndInter.RegisterConnectedRoad(roadA);
                EditorUtility.SetDirty(outerEndInter);
            }

            // Delete the joint and roadB
            if (joint != null && joint.gameObject != null)
            {
                Undo.DestroyObjectImmediate(joint.gameObject);
            }

            if (roadB.gameObject != null)
            {
                Undo.DestroyObjectImmediate(roadB.gameObject);
            }

            // Rebuild visual for the combined road
            SimpleRoadPlacementTool.RebuildSingleRoadVisual(roadA);
            EditorUtility.SetDirty(roadA);
            EditorUtility.SetDirty(roadA.gameObject);

            return true;
        }

        /// <summary>
        /// Recursively merges all consecutive 2-way road segments connected to the given road
        /// both in forward and backward directions, creating one seamless continuous road.
        /// </summary>
        public static SplineRoad MergeEntireChain(SplineRoad startRoad)
        {
            if (startRoad == null || !startRoad.IsUsable) return startRoad;

            SplineRoad currentRoad = startRoad;
            bool mergedAny = true;
            int maxIterations = 50;

            while (mergedAny && maxIterations-- > 0)
            {
                mergedAny = false;

                // Check end intersection
                if (currentRoad.endIntersection != null && IsTwoWayJoint(currentRoad.endIntersection, out SplineRoad r1, out SplineRoad r2))
                {
                    SplineRoad other = (r1 == currentRoad) ? r2 : r1;
                    if (other != null && other != currentRoad)
                    {
                        RoadIntersection joint = currentRoad.endIntersection;
                        if (MergeTwoRoads(currentRoad, other, joint))
                        {
                            mergedAny = true;
                            continue;
                        }
                    }
                }

                // Check start intersection
                if (currentRoad.startIntersection != null && IsTwoWayJoint(currentRoad.startIntersection, out SplineRoad s1, out SplineRoad s2))
                {
                    SplineRoad other = (s1 == currentRoad) ? s2 : s1;
                    if (other != null && other != currentRoad)
                    {
                        RoadIntersection joint = currentRoad.startIntersection;
                        if (MergeTwoRoads(currentRoad, other, joint))
                        {
                            mergedAny = true;
                            continue;
                        }
                    }
                }

                // Check by distance if intersections were not explicitly assigned
                SplineRoad[] allRoads = Object.FindObjectsByType<SplineRoad>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
                RoadIntersection[] allIntersections = Object.FindObjectsByType<RoadIntersection>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);

                for (int i = 0; i < allIntersections.Length; i++)
                {
                    RoadIntersection inter = allIntersections[i];
                    if (inter == null) continue;

                    if (IsTwoWayJoint(inter, out SplineRoad jA, out SplineRoad jB))
                    {
                        if (jA == currentRoad || jB == currentRoad)
                        {
                            SplineRoad other = (jA == currentRoad) ? jB : jA;
                            if (MergeTwoRoads(currentRoad, other, inter))
                            {
                                mergedAny = true;
                                break;
                            }
                        }
                    }
                }
            }

            return currentRoad;
        }

        /// <summary>
        /// Finds and merges ALL 2-way road joints in the active scene, turning fragmented roads into seamless ribbons.
        /// </summary>
        public static int MergeAllTwoWayRoadsInScene()
        {
            int totalMerged = 0;
            int passes = 30;
            bool foundAny = true;

            while (foundAny && passes-- > 0)
            {
                foundAny = false;
                RoadIntersection[] intersections = Object.FindObjectsByType<RoadIntersection>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);

                for (int i = 0; i < intersections.Length; i++)
                {
                    RoadIntersection inter = intersections[i];
                    if (inter == null) continue;

                    if (IsTwoWayJoint(inter, out SplineRoad roadA, out SplineRoad roadB))
                    {
                        if (MergeTwoRoads(roadA, roadB, inter))
                        {
                            totalMerged++;
                            foundAny = true;
                            break; // Collection modified, restart loop
                        }
                    }
                }
            }

            if (totalMerged > 0)
            {
                // Refresh network and rebuild intersections
                RoadNetworkManager networkManager = Object.FindFirstObjectByType<RoadNetworkManager>();
                if (networkManager != null)
                {
                    networkManager.RefreshNetwork();
                    EditorUtility.SetDirty(networkManager);
                }

                SimpleRoadPlacementTool.RebuildAllIntersectionsMenu();
                EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
                SceneView.RepaintAll();
            }

            return totalMerged;
        }
    }
}
