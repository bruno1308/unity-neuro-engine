using System.Collections.Generic;
using UnityEngine;

namespace NeuroEngine.Core
{
    /// <summary>
    /// Interface for spatial anomaly detection.
    /// This is Layer 2 (Observation) - detects spatial issues like off-screen objects,
    /// unusual scales, and collider overlaps that may indicate bugs.
    /// </summary>
    public interface ISpatialAnalysis
    {
        /// <summary>
        /// Perform a full spatial analysis of the current scene.
        /// </summary>
        SpatialReport AnalyzeScene();

        /// <summary>
        /// Analyze a single GameObject for spatial anomalies.
        /// </summary>
        SpatialReport AnalyzeObject(GameObject target);

        /// <summary>
        /// Find all objects outside the main camera's view frustum.
        /// </summary>
        List<OffScreenObject> FindOffScreenObjects();

        /// <summary>
        /// Find objects with unusual scale values.
        /// </summary>
        List<ScaleAnomaly> FindScaleAnomalies(float minScale = 0.01f, float maxScale = 100f);

        /// <summary>
        /// Find colliders that are overlapping/penetrating each other.
        /// </summary>
        List<ColliderOverlap> FindOverlappingColliders();
    }

    /// <summary>
    /// Complete spatial analysis report.
    /// Note: Not marked [Serializable] - we use Newtonsoft.Json for serialization.
    /// </summary>
    public class SpatialReport
    {
        public List<OffScreenObject> OffScreenObjects = new List<OffScreenObject>();
        public List<ScaleAnomaly> ScaleAnomalies = new List<ScaleAnomaly>();
        public List<ColliderOverlap> Overlaps = new List<ColliderOverlap>();
        public string Timestamp;
        public int TotalObjectsAnalyzed;
        public int IssuesFound => OffScreenObjects.Count + ScaleAnomalies.Count + Overlaps.Count;
    }

    /// <summary>
    /// An object detected outside the camera's view frustum.
    /// </summary>
    public class OffScreenObject
    {
        public string ObjectPath;
        public float[] WorldPosition;
        public string Reason;
        public float DistanceFromView;
    }

    /// <summary>
    /// An object with unusual scale.
    /// </summary>
    public class ScaleAnomaly
    {
        public string ObjectPath;
        public float[] Scale;
        public string Reason;
    }

    /// <summary>
    /// Two colliders that are overlapping.
    /// </summary>
    public class ColliderOverlap
    {
        public string Object1Path;
        public string Object2Path;
        public string ColliderType1;
        public string ColliderType2;
        public float PenetrationDepth;
    }
}
