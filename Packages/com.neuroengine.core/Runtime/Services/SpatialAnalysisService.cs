using System;
using System.Collections.Generic;
using NeuroEngine.Core;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace NeuroEngine.Services
{
    /// <summary>
    /// Detects spatial anomalies in the scene.
    /// This is Layer 2 (Observation) - finds off-screen objects, scale issues, and collider overlaps.
    /// </summary>
    public class SpatialAnalysisService : ISpatialAnalysis
    {
        // Layers to ignore for off-screen detection (UI, effects, etc.)
        private readonly HashSet<string> _ignoredLayers = new HashSet<string> { "UI", "Ignore Raycast" };

        public SpatialReport AnalyzeScene()
        {
            var report = new SpatialReport
            {
                Timestamp = DateTime.UtcNow.ToString("o")
            };

            report.OffScreenObjects = FindOffScreenObjects();
            report.ScaleAnomalies = FindScaleAnomalies();
            report.Overlaps = FindOverlappingColliders();

            // Count total objects
            var scene = SceneManager.GetActiveScene();
            var roots = scene.GetRootGameObjects();
            foreach (var root in roots)
            {
                report.TotalObjectsAnalyzed += CountObjects(root.transform);
            }

            return report;
        }

        public SpatialReport AnalyzeObject(GameObject target)
        {
            var report = new SpatialReport
            {
                Timestamp = DateTime.UtcNow.ToString("o")
            };

            report.TotalObjectsAnalyzed = CountObjects(target.transform);

            // Check this object and children for issues
            CheckObjectRecursive(target.transform, report);

            return report;
        }

        public List<OffScreenObject> FindOffScreenObjects()
        {
            var results = new List<OffScreenObject>();
            var camera = Camera.main;

            if (camera == null)
            {
                Debug.LogWarning("[SpatialAnalysis] No main camera found");
                return results;
            }

            var frustumPlanes = GeometryUtility.CalculateFrustumPlanes(camera);
            var scene = SceneManager.GetActiveScene();
            var roots = scene.GetRootGameObjects();

            foreach (var root in roots)
            {
                CheckOffScreenRecursive(root.transform, camera, frustumPlanes, results);
            }

            return results;
        }

        public List<ScaleAnomaly> FindScaleAnomalies(float minScale = 0.01f, float maxScale = 100f)
        {
            var results = new List<ScaleAnomaly>();
            var scene = SceneManager.GetActiveScene();
            var roots = scene.GetRootGameObjects();

            foreach (var root in roots)
            {
                CheckScaleRecursive(root.transform, minScale, maxScale, results);
            }

            return results;
        }

        public List<ColliderOverlap> FindOverlappingColliders()
        {
            var results = new List<ColliderOverlap>();
            var colliders = UnityEngine.Object.FindObjectsByType<Collider>(FindObjectsSortMode.None);

            for (int i = 0; i < colliders.Length; i++)
            {
                for (int j = i + 1; j < colliders.Length; j++)
                {
                    var col1 = colliders[i];
                    var col2 = colliders[j];

                    // Skip triggers and disabled colliders
                    if (col1.isTrigger || col2.isTrigger) continue;
                    if (!col1.enabled || !col2.enabled) continue;

                    // Check for overlap using Physics.ComputePenetration
                    if (Physics.ComputePenetration(
                        col1, col1.transform.position, col1.transform.rotation,
                        col2, col2.transform.position, col2.transform.rotation,
                        out Vector3 direction, out float distance))
                    {
                        if (distance > 0.001f) // Small threshold
                        {
                            results.Add(new ColliderOverlap
                            {
                                Object1Path = GetObjectPath(col1.gameObject),
                                Object2Path = GetObjectPath(col2.gameObject),
                                ColliderType1 = col1.GetType().Name,
                                ColliderType2 = col2.GetType().Name,
                                PenetrationDepth = distance
                            });
                        }
                    }
                }
            }

            return results;
        }

        private void CheckObjectRecursive(Transform transform, SpatialReport report)
        {
            var camera = Camera.main;
            if (camera != null)
            {
                var frustumPlanes = GeometryUtility.CalculateFrustumPlanes(camera);
                CheckOffScreenSingle(transform, camera, frustumPlanes, report.OffScreenObjects);
            }

            CheckScaleSingle(transform, 0.01f, 100f, report.ScaleAnomalies);

            foreach (Transform child in transform)
            {
                CheckObjectRecursive(child, report);
            }
        }

        private void CheckOffScreenRecursive(Transform transform, Camera camera, Plane[] frustumPlanes, List<OffScreenObject> results)
        {
            CheckOffScreenSingle(transform, camera, frustumPlanes, results);

            foreach (Transform child in transform)
            {
                CheckOffScreenRecursive(child, camera, frustumPlanes, results);
            }
        }

        private void CheckOffScreenSingle(Transform transform, Camera camera, Plane[] frustumPlanes, List<OffScreenObject> results)
        {
            // Skip ignored layers
            if (_ignoredLayers.Contains(LayerMask.LayerToName(transform.gameObject.layer)))
                return;

            // Skip inactive objects
            if (!transform.gameObject.activeInHierarchy)
                return;

            // Check if object has a renderer
            var renderer = transform.GetComponent<Renderer>();
            if (renderer == null)
                return;

            // Test against frustum
            if (!GeometryUtility.TestPlanesAABB(frustumPlanes, renderer.bounds))
            {
                var position = transform.position;
                var viewportPoint = camera.WorldToViewportPoint(position);

                string reason;
                float distance;

                if (viewportPoint.x < 0)
                {
                    reason = "x < camera.left";
                    distance = -viewportPoint.x;
                }
                else if (viewportPoint.x > 1)
                {
                    reason = "x > camera.right";
                    distance = viewportPoint.x - 1;
                }
                else if (viewportPoint.y < 0)
                {
                    reason = "y < camera.bottom";
                    distance = -viewportPoint.y;
                }
                else if (viewportPoint.y > 1)
                {
                    reason = "y > camera.top";
                    distance = viewportPoint.y - 1;
                }
                else if (viewportPoint.z < camera.nearClipPlane)
                {
                    reason = "z < camera.near";
                    distance = camera.nearClipPlane - viewportPoint.z;
                }
                else if (viewportPoint.z > camera.farClipPlane)
                {
                    reason = "z > camera.far";
                    distance = viewportPoint.z - camera.farClipPlane;
                }
                else
                {
                    reason = "outside frustum";
                    distance = 0;
                }

                results.Add(new OffScreenObject
                {
                    ObjectPath = GetObjectPath(transform.gameObject),
                    WorldPosition = new[] { position.x, position.y, position.z },
                    Reason = reason,
                    DistanceFromView = distance
                });
            }
        }

        private void CheckScaleRecursive(Transform transform, float minScale, float maxScale, List<ScaleAnomaly> results)
        {
            CheckScaleSingle(transform, minScale, maxScale, results);

            foreach (Transform child in transform)
            {
                CheckScaleRecursive(child, minScale, maxScale, results);
            }
        }

        private void CheckScaleSingle(Transform transform, float minScale, float maxScale, List<ScaleAnomaly> results)
        {
            var scale = transform.localScale;
            string reason = null;

            // Check for too small
            if (scale.x < minScale || scale.y < minScale || scale.z < minScale)
            {
                reason = $"scale < {minScale}";
            }
            // Check for too large
            else if (scale.x > maxScale || scale.y > maxScale || scale.z > maxScale)
            {
                reason = $"scale > {maxScale}";
            }
            // Check for extreme non-uniform scale
            else
            {
                float maxComponent = Mathf.Max(scale.x, scale.y, scale.z);
                float minComponent = Mathf.Min(scale.x, scale.y, scale.z);
                if (minComponent > 0 && maxComponent / minComponent > 100)
                {
                    reason = "non-uniform extreme (ratio > 100)";
                }
            }

            if (reason != null)
            {
                results.Add(new ScaleAnomaly
                {
                    ObjectPath = GetObjectPath(transform.gameObject),
                    Scale = new[] { scale.x, scale.y, scale.z },
                    Reason = reason
                });
            }
        }

        private int CountObjects(Transform transform)
        {
            int count = 1;
            foreach (Transform child in transform)
            {
                count += CountObjects(child);
            }
            return count;
        }

        private string GetObjectPath(GameObject go)
        {
            var path = go.name;
            var parent = go.transform.parent;

            while (parent != null)
            {
                path = parent.name + "/" + path;
                parent = parent.parent;
            }

            return path;
        }
    }
}
