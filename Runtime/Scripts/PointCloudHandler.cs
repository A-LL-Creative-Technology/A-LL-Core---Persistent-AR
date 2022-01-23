using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.XR.ARFoundation;

// Based on: https://tutorialsforar.com/accessing-and-saving-point-cloud-data-using-unity-and-ar-foundation/

public class PointCloudHandler : MonoBehaviour
{
    public ARPointCloudManager pointCloudManager;

    private List<ARPosition> positionsList;
    private List<float> identifiersList;
    private Dictionary<ulong, ARPoint> pointsDict;

    void Awake()
    {
        positionsList = new List<ARPosition>();
        identifiersList = new List<float>();
        pointsDict = new Dictionary<ulong, ARPoint>();
    }

    private void OnEnable()
    {
        pointCloudManager.pointCloudsChanged += PointCloudManager_pointCloudsChanged;
    }

    private void PointCloudManager_pointCloudsChanged(ARPointCloudChangedEventArgs obj)
    {
        // TODO: Is is important to separate saved ARPoints by separated PointCloud??
        //List<ARPoint> addedPoints = new List<ARPoint>();
        foreach (var pointCloud in obj.added) // Only once on android cause it uses only one cloud for all points
        {
            // We can't use indexes on parallel arrays because it is nullable -> foreach. OR test nullable with HasValue and get value with Value
            /*foreach (var pos in pointCloud.positions)
            {
                ARPosition newPosition = new ARPosition(pos);
                //addedPoints.Add(newPoint);

                positionsList.Add(newPosition);
            }*/

            /*for (int i = 0; i < pointCloud.positions.Length; i++)
            {
                //
                //pointCloud.positions[i]
            }*/

            // Check HasValue cause nullable
            if (pointCloud.positions.HasValue && pointCloud.identifiers.HasValue && pointCloud.confidenceValues.HasValue)
            {
                //foreach (var point in pointCloud.positions.Value)
                    //s_Vertices.Add(point);

                // Add ARPoints to Dictionary with identifies as Key
                for (int i = 0; i < pointCloud.positions.Value.Length; i++)
                {
                    //Dictonary.Add throws exception if key is taken. Maybe more optimised to use TryGetValue? -> https://stackoverflow.com/questions/1243717/how-to-update-the-value-stored-in-dictionary-in-c
                    //pointsDict.Add(pointCloud.identifiers.Value[i], new ARPoint(pointCloud.positions.Value[i], pointCloud.confidenceValues.Value[i]));
                    pointsDict[pointCloud.identifiers.Value[i]] = new ARPoint(pointCloud.positions.Value[i], pointCloud.confidenceValues.Value[i]);
                }
            }
        }


        List<ARPoint> updatedPoints = new List<ARPoint>();
        foreach (var pointCloud in obj.updated)
        {
            /*foreach (var pos in pointCloud.positions)
            {
                ARPoint newPoint = new ARPoint(pos);
                updatedPoints.Add(newPoint);

                pointsList.Add(newPoint);
            }*/

            // Check HasValue cause nullable
            if (pointCloud.positions.HasValue && pointCloud.identifiers.HasValue && pointCloud.confidenceValues.HasValue)
            {

                // Add ARPoints to Dictionary with identifies as Key
                for (int i = 0; i < pointCloud.positions.Value.Length; i++)
                {
                    pointsDict[pointCloud.identifiers.Value[i]] = new ARPoint(pointCloud.positions.Value[i], pointCloud.confidenceValues.Value[i]);
                }
            }
        }

        // TODO: check is obj.removed means points out of vision OR points that shall be discarded (bad confidence I guess?..)

        foreach (var pointCloud in obj.removed)
        {

            // Check HasValue cause nullable
            if (pointCloud.positions.HasValue && pointCloud.identifiers.HasValue && pointCloud.confidenceValues.HasValue)
            {

                // Add ARPoints to Dictionary with identifies as Key
                for (int i = 0; i < pointCloud.positions.Value.Length; i++)
                {
                    pointsDict.Remove(pointCloud.identifiers.Value[i]);
                }
            }
            Debug.Log("Removed called");
        }

        //Debug.Log("AR points count: " + pointsList.Count);
        //Debug.Log("AR points added: " + obj.added.Count);
    }

    public void ARPointCount()
    {
        Debug.Log("AR points count: " + pointsDict.Count);
    }

    public class ARPosition
    {
        public float x;
        public float y;
        public float z;

        public ARPosition(Vector3 pos)
        {
            x = pos.x;
            y = pos.y;
            z = pos.z;
        }
    }

    public class ARPoint
    {
        public float x;
        public float y;
        public float z;
        public float confidence;

        public ARPoint(Vector3 pos, float conf)
        {
            x = pos.x;
            y = pos.y;
            z = pos.z;
            confidence = conf;
        }
    }
}
