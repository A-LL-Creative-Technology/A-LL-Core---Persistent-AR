//#if UNITY_IOS

using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using Unity.Collections;
using UnityEngine;
using UnityEngine.XR.ARFoundation;
using UnityEngine.XR.ARKit;
using UnityEngine.XR.ARSubsystems;
using System.Runtime.ExceptionServices;
using Unity.Plastic.Newtonsoft.Json;

public class ARWorldMapController : MonoBehaviour
{

    private static ARWorldMapController instance;

    public static ARWorldMapController GetInstance()
    {
        return instance;
    }

    public static event EventHandler OnARWorldmapSaved;
    public static event EventHandler OnARWorldmapLoaded;
    public static event EventHandler OnARWorldmapRelocalized;

    public ARSession arSession;

    private ARKitSessionSubsystem arSessionSubsystem;
    private ARWorldMap aRWorldMap;

    [HideInInspector] public List<string> logs;

    [HideInInspector] public string rootPath;

    private string arModelTag = "WorldMapARModel";

    [HideInInspector] public ARWorldMappingStatus arWorldmapMappingStatus;

    private IEnumerator tryToRelocalizeWorlmapCoroutine;

    private bool isLoadingARWorldmap = false;
    private bool isSavingARWorldmap = false;

    private void Awake()
    {
        instance = this;

        logs = new List<string>();

        rootPath = Path.Combine(Application.persistentDataPath, "AR Worldmaps");
        Directory.CreateDirectory(rootPath);

        arSessionSubsystem = (ARKitSessionSubsystem)arSession.subsystem;
    }


    private void Update()
    {
        arWorldmapMappingStatus = arSessionSubsystem.worldMappingStatus;
    }



    public IEnumerator SaveARWorldmap(string arWorldmapName)
    {
        Log("Saving AR Worldmap");

        if (isSavingARWorldmap)
            yield break;

        isSavingARWorldmap = true;

        if (arSessionSubsystem == null)
        {
            Log("No session subsystem available. Could not save");
            yield break;
        }

        ARWorldMapRequest request = arSessionSubsystem.GetARWorldMapAsync();

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
            Log("Error: Cannot find AR Model with tag '" + arModelTag + "'.");

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

        string filenameJson = GetJsonARWorldmapFilename(arWorldmapName); ;

        File.WriteAllText(Path.Combine(rootPath, filenameJson), jsonStr);


        ARWorldMap worldMap = request.GetWorldMap();
        request.Dispose();

        // Save the Worldmap
        Log("Serializing ARWorldMap to byte Array");
        var data = worldMap.Serialize(Allocator.Temp);

        Log($"ARWorldMap has ${data.Length} bytes");

        string filenameWorlMap = GetARWorldmapFilename(arWorldmapName);
        string filePath = Path.Combine(rootPath, filenameWorlMap);

        var file = File.Open(filePath, FileMode.Create);
        var writer = new BinaryWriter(file);
        writer.Write(data.ToArray());
        writer.Close();
        data.Dispose();
        worldMap.Dispose();

        Log($"ARWorldMap saved to ${filePath}");

        // fires the event to notify that ARWorldmap has been loaded
        if (OnARWorldmapSaved != null)
        {
            OnARWorldmapSaved(this, EventArgs.Empty);
        }

        isSavingARWorldmap = false;
    }

    public IEnumerator LoadARWorldmap(string arWorldmapName)
    {
        Log("Loading AR Worldmap");

        if (isLoadingARWorldmap)
            yield break;

        isLoadingARWorldmap = true;

        if (arSessionSubsystem == null)
        {
            Log("No session subsystem available. Could not load");
            yield break;
        }

        string filenameWorlMap = GetARWorldmapFilename(arWorldmapName);
        string filePath = Path.Combine(rootPath, filenameWorlMap);

        var file = File.Open(filePath, FileMode.Open);
        if (file == null)
        {
            Log($"File ${filePath} doesn't exist");
            yield break;
        }

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

        if (ARWorldMap.TryDeserialize(data, out aRWorldMap))
        {
            data.Dispose();
        }

        if (aRWorldMap.valid)
        {
            Log("Worldmap loaded successfully");
        }
        else
        {
            Log("Data is not a valid ARWorldMap");
            yield break;
        }


        // fires the event to notify that ARWorldmap has been loaded
        if (OnARWorldmapLoaded != null)
        {
            OnARWorldmapLoaded(this, EventArgs.Empty);
        }

        isLoadingARWorldmap = false;

    }

    public void RelocalizeLoadedARWorldmap()
    {
        // first kill any coroutine stuck in trying to relocalize
        if (tryToRelocalizeWorlmapCoroutine != null)
        {
            Log("Killing existing running relocalization coroutine");

            StopCoroutine(tryToRelocalizeWorlmapCoroutine);
        }


        arSessionSubsystem.ApplyWorldMap(aRWorldMap);

        tryToRelocalizeWorlmapCoroutine = TryToRelocalizedWorldmap();

        Log("Applied worldmap to current session");

        StartCoroutine(tryToRelocalizeWorlmapCoroutine);

    }

    private IEnumerator TryToRelocalizedWorldmap()
    {
        // before checking for mapped status, we make sure it goes through not available (successfull reset of arsessionorigin)
        while (arWorldmapMappingStatus != ARWorldMappingStatus.NotAvailable)
            yield return null;

        while (arWorldmapMappingStatus != ARWorldMappingStatus.Mapped)
            yield return null;

        // fires the event 
        if (OnARWorldmapRelocalized != null)
        {
            OnARWorldmapRelocalized(this, EventArgs.Empty);
        }

        tryToRelocalizeWorlmapCoroutine = null;

        Log("AR worldmap relocalized");
    }

    public string GetARWorldmapFilename(string arWorldmapName)
    {
        return "ARWorldMap - " + arWorldmapName + ".worldmap";
    }

    public string GetJsonARWorldmapFilename(string arWorldmapName)
    {
        return "ARModels - " + arWorldmapName + ".json";
    }

    public void Log(string text)
    {
        Debug.Log(text);
        logs.Add(text);

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