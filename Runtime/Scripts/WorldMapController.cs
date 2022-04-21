//#if UNITY_IOS

using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using Unity.Collections;
using Unity.Plastic.Newtonsoft.Json;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.XR.ARFoundation;
using UnityEngine.XR.ARKit;
using UnityEngine.XR.ARSubsystems;

public class WorldMapController : MonoBehaviour
{




    public ARSession arSession;
    public Text errorText;
    public Text logText;
    public Text mappingStatusText;
    public Button saveButton;
    public Button loadButton;

    public List<string> messages;

    public string path;

    bool supported
    {
        get
        {
#if UNITY_IOS
            return arSession.subsystem is ARKitSessionSubsystem && ARKitSessionSubsystem.worldMapSupported;
#else
            return false;
#endif
        }
    }

    private void Awake()
    {
        messages = new List<string>();
        path = Path.Combine(Application.persistentDataPath, "my_session.worldmap");
    }



    public void OnSaveButtonPressed()
    {
#if UNITY_IOS
        StartCoroutine(Save());
#endif
    }


    public void OnLoadButtonPressed()
    {
#if UNITY_IOS
        StartCoroutine(Load());
#endif
    }

    public void OnResetButtonPressed()
    {
#if UNITY_IOS
        arSession.Reset();
#endif
    }


    private IEnumerator Save()
    {
        var sessionSubsystem = (ARKitSessionSubsystem)arSession.subsystem;

        if (sessionSubsystem == null)
        {
            Log("No session subsystem available. Could not save");
            yield break;
        }

        var request = sessionSubsystem.GetARWorldMapAsync();

        while (!request.status.IsDone())
        {
            yield return null;
        }

        if (request.status.IsError())
        {
            Log($"Session serialization failed with status ${request.status}");
            yield break;
        }


        GameObject[] worldObjects = GameObject.FindGameObjectsWithTag("WorldObject");

        List<WorldObject> wObjects = new List<WorldObject>();
        foreach (GameObject worldObject in worldObjects)
        {
            WorldObject savedObject = new WorldObject();
            savedObject.prefabName = worldObject.name;
            savedObject.position = worldObject.transform.position;
            savedObject.rotation = worldObject.transform.rotation;
            savedObject.scale = worldObject.transform.localScale;

            wObjects.Add(savedObject);
        }

        string jsonStr = JsonUtility.ToJson(wObjects);
        File.WriteAllText(Path.Combine(Application.persistentDataPath, "WorldObjectsData.json"), jsonStr);

        var worldMap = request.GetWorldMap();
        request.Dispose();

        SaveAndDisposeWorldMap(worldMap);
    }

    private IEnumerator Load()
    {

        var sessionSubsystem = (ARKitSessionSubsystem)arSession.subsystem;

        if (sessionSubsystem == null)
        {
            Log("No session subsystem available. Could not load");
            yield break;
        }

        var file = File.Open(path, FileMode.Open);
        if (file == null)
        {
            Log($"File ${path} doesn't exist");
            yield break;
        }

        Log($"Reading ${path}");

        int bytesPerFrame = 1024 * 10;
        long bytesRemaining = file.Length;
        var binaryReader = new BinaryReader(file);
        var allBytes = new List<byte>();
        while (bytesRemaining > 0)
        {
            var bytes = binaryReader.ReadBytes(bytesPerFrame);
            allBytes.AddRange(bytes);
            bytesRemaining -= bytesPerFrame;
            yield return null;
        }

        var data = new NativeArray<byte>(allBytes.Count, Allocator.Temp);
        data.CopyFrom(allBytes.ToArray());

        Log("Deserializing to ARWorldMap");
        ARWorldMap aRWorldMap;
        if (ARWorldMap.TryDeserialize(data, out aRWorldMap))
        {
            data.Dispose();
        }

        if (aRWorldMap.valid)
        {
            Log("Deserialization successful");
        }
        else
        {
            Log("Data is not a valid ARWorldMap");
            yield break;
        }

        Log("Apply worldmap to current session");
        sessionSubsystem.ApplyWorldMap(aRWorldMap);


        string jsonContent = File.ReadAllText(Path.Combine(Application.persistentDataPath, "WorldObjectsData.json"));
        List<WorldObject> worldObjects = JsonConvert.DeserializeObject<List<WorldObject>>(jsonContent);

        foreach (WorldObject wObject in worldObjects)
        {
            Debug.Log("Found " + wObject.prefabName + " in world objects data");
            Log("Found " + wObject.prefabName + " in world objects data");
        }
    }

    public void Log(string text)
    {
        Debug.Log(text);
        messages.Add(text);

    }

    static void SetActive(GameObject go, bool active)
    {
        if (go != null)
        {
            go.gameObject.SetActive(active);
        }
    }

    static void SetText(Text text, string value)
    {
        if (text != null)
        {
            text.text = value;
        }
    }

    private void Update()
    {
        SetActive(errorText.gameObject, !supported);
        SetActive(saveButton.gameObject, supported);
        SetActive(loadButton.gameObject, supported);
        SetActive(mappingStatusText.gameObject, supported);

#if UNITY_IOS
        var sessionSubsystem = (ARKitSessionSubsystem)arSession.subsystem;
#else
        XRSessionSubsystem sessionSubsystem = null;
#endif

        if (sessionSubsystem == null)
        {
            return;
        }

        var numLogsToShow = 20;
        string msg = "";

        for (int i = Mathf.Max(0, messages.Count - numLogsToShow); i < messages.Count; ++i)
        {
            msg += messages[i];
            msg += "\n";
        }
        SetText(logText, msg);

#if UNITY_IOS
        SetText(mappingStatusText, string.Format("Mapping Status: {0}", sessionSubsystem.worldMappingStatus));
#endif
    }


    private void SaveAndDisposeWorldMap(ARWorldMap worldMap)
    {
        Log("Serializing ARWOrldMap to byte Array");
        var data = worldMap.Serialize(Allocator.Temp);

        Log($"ARWorldMap has ${data.Length} bytes");

        var file = File.Open(path, FileMode.Create);
        var writer = new BinaryWriter(file);
        writer.Write(data.ToArray());
        writer.Close();
        data.Dispose();
        worldMap.Dispose();
        Log($"ARWorldMap written to ${path}");
    }

}

[System.Serializable]
public class WorldObject
{
    public string prefabName;
    public Vector3 position;
    public Quaternion rotation;
    public Vector3 scale;
}




//#endif