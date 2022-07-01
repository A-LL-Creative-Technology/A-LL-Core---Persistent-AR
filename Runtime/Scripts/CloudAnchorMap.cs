using System.Collections;
using System;
using System.Collections.Generic;
using UnityEngine;

// Based on:
// https://github.com/google-ar/arcore-unity-extensions/tree/master/Samples~/PersistentCloudAnchors
// https://answers.unity.com/questions/956047/serialize-quaternion-or-vector3.html

[Serializable]
public class CloudAnchorMap
{
    public string name; //Map name. Correspond to beacon region name
    public List<CloudAnchorItem> anchors; // TODO transform to dictionary <id, CloudAnchorItem> ?
    public string SerializedTime; // Creation time of map. Used to order
    public string statamicId; // Id of the map in the Statamic DB

    public CloudAnchorMap(string name)
    {
        this.name = name;
        anchors = new List<CloudAnchorItem>();
        SerializedTime = DateTime.Now.ToString();
    }

    public void addAnchor(string id, string anchorName, Quaternion rot)
    {
        anchors.Add(new CloudAnchorItem(id, anchorName, rot));
    }

    public DateTime CreatedTime
    {
        get
        {
            return Convert.ToDateTime(SerializedTime);
        }
    }

    // Return the json string
    public override string ToString()
    {
        return JsonUtility.ToJson(this);
    }
}

[Serializable]
public class CloudAnchorItem
{
    public string id; // Cloud Anchor ID used for resolving
    public string name; // Name given to the anchor
    public List<MapObject> mapObjects;
    public string SerializedTime; // Creation time of anchor. Used to find expired anchors
    public SerializableQuaternion markerRotation; // Rotation of axis marker
    public string statamicId; // Id of the anchor in the Statamic DB

    public CloudAnchorItem(string id, string name, Quaternion rot)
    {
        this.id = id;
        this.name = name;
        mapObjects = new List<MapObject>();
        SerializedTime = DateTime.Now.ToString();
        this.markerRotation = new SerializableQuaternion(rot.x, rot.y, rot.z, rot.w);
    }

    public void addObject(string prefabName, GameObject mapObject)
    {
        mapObjects.Add(new MapObject(prefabName, mapObject));
    }

    public DateTime CreatedTime
    {
        get
        {
            return Convert.ToDateTime(SerializedTime);
        }
    }

    // Return the json string
    public override string ToString()
    {
        return JsonUtility.ToJson(this);
    }
}

[Serializable]
public struct MapObject
{
    public string prefabName;
    public SerializableVector3 position;
    public SerializableQuaternion rotation;
    public SerializableVector3 scale;
    public List<ARAsset> assets;

    public MapObject(string prefabName, GameObject mapObject, List<ARAsset> assets = null)
    {
        this.prefabName = prefabName;

        var mapPos = mapObject.transform.localPosition;
        this.position = new SerializableVector3(mapPos.x, mapPos.y, mapPos.z);

        var mapRot = mapObject.transform.localRotation;
        this.rotation = new SerializableQuaternion(mapRot.x, mapRot.y, mapRot.z, mapRot.w);

        var mapScale = mapObject.transform.localScale;
        this.scale = new SerializableVector3(mapScale.x, mapScale.y, mapScale.z);

        this.assets = assets;
    }

    // Return the json string
    public override string ToString()
    {
        return JsonUtility.ToJson(this);
    }
}

[Serializable]
public struct ARAsset
{
    public string uri;
    public int version;

    public ARAsset(string uri, int version)
    {
        this.uri = uri;
        this.version = version;
    }
}


[Serializable]
public struct SerializableVector3
{
    public float x;
    public float y;
    public float z;

    public SerializableVector3(float x, float y, float z)
    {
        this.x = x;
        this.y = y;
        this.z = z;
    }

    // Implicite conversion to Vector3
    public static implicit operator Vector3(SerializableVector3 val)
    {
        return new Vector3(val.x, val.y, val.z);
    }
}

[Serializable]
public struct SerializableQuaternion
{
    public float x;
    public float y;
    public float z;
    public float w;

    public SerializableQuaternion(float x, float y, float z, float w)
    {
        this.x = x;
        this.y = y;
        this.z = z;
        this.w = w;
    }

    // Implicite conversion to Quaternion
    public static implicit operator Quaternion(SerializableQuaternion val)
    {
        return new Quaternion(val.x, val.y, val.z, val.w);
    }
}

// Wrapper for serialization
[Serializable]
public class CloudAnchorMapCollection
{
    public List<CloudAnchorMap> Collection = new List<CloudAnchorMap>();
}