//#if UNITY_IOS

using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using Newtonsoft.Json;
using TMPro;
using Unity.Collections;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.XR.ARFoundation;
using UnityEngine.XR.ARKit;
using UnityEngine.XR.ARSubsystems;

public class ARWorldMapController : MonoBehaviour
{

    public ARSession arSession;
    public TextMeshProUGUI infosText;
    public TextMeshProUGUI logText;
    public TextMeshProUGUI mappingStatusText;
    public TMP_InputField inputFieldMapPrefabName;
    public Button saveButton;
    public Button loadButton;
    public Toggle overwriteFile;


    [SerializeField] private GameObject arModelToSpawn;

    private List<string> logs;

    private string rootPath;

    private string arModelTag = "WorldMapARModel";

    bool supported
    {
        get
        {
            return arSession.subsystem is ARKitSessionSubsystem && ARKitSessionSubsystem.worldMapSupported;
        }
    }

    private void Awake()
    {
        logs = new List<string>();

        rootPath = Path.Combine(Application.persistentDataPath, "AR Worldmaps");
        Directory.CreateDirectory(rootPath);
    }

    public void OnSaveButtonPressed()
    {
        StartCoroutine(Save());
    }

    public void OnLoadButtonPressed()
    {
        StartCoroutine(Load());
    }

    public void OnResetButtonPressed()
    {
        arSession.Reset();

        // remove all spawned models
        GameObject[] worldMapObjects = GameObject.FindGameObjectsWithTag(arModelTag);

        foreach (GameObject worldMapObject in worldMapObjects)
        {
            Destroy(worldMapObject);
        }
    }

    private IEnumerator Save()
    {
        if (inputFieldMapPrefabName.text == "")
        {
            SetText(infosText, "Error: Map and Prefab filename is empty.");

            yield break;
        }

        string filenameJson = "ARModels - " + inputFieldMapPrefabName.text + ".json";

        if (!overwriteFile.isOn && File.Exists(Path.Combine(rootPath, filenameJson)))
        {
            SetText(infosText, "Error: Map and Prefab filename already exists.");

            yield break;
        }

        ARKitSessionSubsystem sessionSubsystem = (ARKitSessionSubsystem)arSession.subsystem;

        if (sessionSubsystem == null)
        {
            Log("No session subsystem available. Could not save");
            yield break;
        }

        ARWorldMapRequest request = sessionSubsystem.GetARWorldMapAsync();

        while (!request.status.IsDone())
        {
            yield return null;
        }

        if (request.status.IsError())
        {
            Log($"Session serialization failed with status ${request.status}");
            yield break;
        }

        // Look for AR Models with the tag "WorldMapObject"
        GameObject[] worldMapObjects = GameObject.FindGameObjectsWithTag(arModelTag);

        if (worldMapObjects.Length == 0)
        {
            SetText(infosText, "Error: Cannot find AR Model with tag '" + arModelTag + "'.");

            yield break;
        }

        List<WorldMapObject> wObjects = new List<WorldMapObject>();
        foreach (GameObject worldMapObject in worldMapObjects)
        {
            WorldMapObject savedObject = new WorldMapObject();
            savedObject.prefabName = worldMapObject.name;

            savedObject.localPositionX = worldMapObject.transform.localPosition.x;
            savedObject.localPositionY = worldMapObject.transform.localPosition.y;
            savedObject.localPositionZ = worldMapObject.transform.localPosition.z;

            savedObject.localRotationX = worldMapObject.transform.localRotation.x;
            savedObject.localRotationY = worldMapObject.transform.localRotation.y;
            savedObject.localRotationZ = worldMapObject.transform.localRotation.z;
            savedObject.localRotationW = worldMapObject.transform.localRotation.w;

            savedObject.localScaleX = worldMapObject.transform.localScale.x;
            savedObject.localScaleY = worldMapObject.transform.localScale.y;
            savedObject.localScaleZ = worldMapObject.transform.localScale.z;

            wObjects.Add(savedObject);
        }

        string jsonStr = JsonConvert.SerializeObject(wObjects, Formatting.None, new JsonSerializerSettings()
        {
            ReferenceLoopHandling = ReferenceLoopHandling.Ignore
        });



        File.WriteAllText(Path.Combine(rootPath, filenameJson), jsonStr);


        ARWorldMap worldMap = request.GetWorldMap();
        request.Dispose();

        // Save the Worldmap
        Log("Serializing ARWOrldMap to byte Array");
        var data = worldMap.Serialize(Allocator.Temp);

        Log($"ARWorldMap has ${data.Length} bytes");

        string filenameWorlMap = "ARWorldMap - " + inputFieldMapPrefabName.text + ".worldmap";
        string filePath = Path.Combine(rootPath, filenameWorlMap);

        var file = File.Open(filePath, FileMode.Create);
        var writer = new BinaryWriter(file);
        writer.Write(data.ToArray());
        writer.Close();
        data.Dispose();
        worldMap.Dispose();
        Log($"ARWorldMap written to ${filePath}");

        SetText(infosText, "Success: File written to " + Path.Combine(rootPath, filenameJson));
    }

    private IEnumerator Load()
    {
        yield return null;

        ARKitSessionSubsystem sessionSubsystem = (ARKitSessionSubsystem)arSession.subsystem;

        if (sessionSubsystem == null)
        {
            Log("No session subsystem available. Could not load");
            yield break;
        }

        string filenameWorlMap = "ARWorldMap - " + inputFieldMapPrefabName.text + ".worldmap";
        string filePath = Path.Combine(rootPath, filenameWorlMap);

        var file = File.Open(filePath, FileMode.Open);
        if (file == null)
        {
            Log($"File ${filePath} doesn't exist");
            yield break;
        }

        Log($"Reading ${filePath}");

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


        string filenameJson = "ARModels - " + inputFieldMapPrefabName.text + ".json";
        string jsonContent = File.ReadAllText(Path.Combine(rootPath, filenameJson));
        List<WorldMapObject> worldMapObjects = JsonConvert.DeserializeObject<List<WorldMapObject>>(jsonContent);

        foreach (WorldMapObject wMapObject in worldMapObjects)
        {

            Log("Found " + wMapObject.prefabName + " in world objects data");

            GameObject arModelSpawned = Instantiate(arModelToSpawn, new Vector3(wMapObject.localPositionX, wMapObject.localPositionY, wMapObject.localPositionZ), new Quaternion(wMapObject.localRotationX, wMapObject.localRotationY, wMapObject.localRotationZ, wMapObject.localRotationW));

            arModelSpawned.transform.localScale = new Vector3(wMapObject.localScaleX, wMapObject.localScaleY, wMapObject.localScaleZ);
        }
    }

    public void Log(string text)
    {
        Debug.Log(text);
        logs.Add(text);

    }

    static void SetActive(GameObject go, bool active)
    {
        if (go != null)
        {
            go.gameObject.SetActive(active);
        }
    }

    static void SetText(TextMeshProUGUI text, string value)
    {
        if (text != null)
        {
            text.text = value;
        }
    }

    private void Update()
    {
        SetActive(infosText.gameObject, supported);
        SetActive(saveButton.gameObject, supported);
        SetActive(loadButton.gameObject, supported);
        SetActive(mappingStatusText.gameObject, supported);

        var sessionSubsystem = (ARKitSessionSubsystem)arSession.subsystem;

        if (sessionSubsystem == null)
        {
            return;
        }

        var numLogsToShow = 20;
        string msg = "";

        for (int i = Mathf.Max(0, logs.Count - numLogsToShow); i < logs.Count; ++i)
        {
            msg += logs[i];
            msg += "\n";
        }
        SetText(logText, msg);

        SetText(mappingStatusText, string.Format("Mapping Status: {0}", sessionSubsystem.worldMappingStatus));
    }




}

[Serializable]
public class WorldMapObject
{
    public string prefabName;

    public float localPositionX;
    public float localPositionY;
    public float localPositionZ;

    public float localRotationX;
    public float localRotationY;
    public float localRotationZ;
    public float localRotationW;

    public float localScaleX;
    public float localScaleY;
    public float localScaleZ;
}




//#endif