using System.Collections;
using System;
using System.Collections.Generic;
using UnityEngine.XR.ARFoundation;
using UnityEngine.XR.ARSubsystems;
using Google.XR.ARCoreExtensions;
using Google.XR.ARCoreExtensions.Samples.PersistentCloudAnchors;
using UnityEngine;
using TMPro;
using UnityEngine.UI;
using System.Linq;

// Based on: https://github.com/google-ar/arcore-unity-extensions/tree/master/Samples~/PersistentCloudAnchors

public class CloudAnchorController : MonoBehaviour
{
    public ARAnchorManager anchorManager;
    public ARSessionOrigin sessionOrigin;
    public GameObject anchorMarkerObject; // 3D object used to see where the anchors are
    //public TextMeshProUGUI debugText;
    public GameObject MapQualityIndicatorPrefab;
    //public TMP_InputField mapNameInput; // Map Editor Input
    //public TextMeshProUGUI mapNameSaveDisplay; // Name diplayed in Saved popup

    public MapObjectsManager mapObjectsManager;

    public GameObject localAnchorPrefab;

    //public Slider rotationSlider;
    //public TMP_InputField rotationInputField;

    //public TMP_InputField anchorNameInputField;

    // Closest plane detection
    public ARRaycastManager raycastManager; // Raycast manager from AR Session Origin used to raycast on AR trackables
    public ARPlaneManager planeManager;

    // API Module
    public APIRequestsManager requestsManager;
    public bool onlineMode; // true if using online requests, false if using local json data

    // Anchor resolving timeout variables
    public float timeoutDuration;
    private float timer;
    private bool hasTimedOut;

    public bool AttachAnchorToPlane = true;

    // The current application mode.
    [HideInInspector]
    public ApplicationMode mode;

    // Let us store our maps locally in PlayerPrefs
    private const string _persistentCloudAnchorsStorageKey = "PersistentAR";

    // Max number of maps
    private const int _storageLimit = 40;

    // The MapQualityIndicator that attaches to the placed object.
    private MapQualityIndicator _qualityIndicator = null;

    // Map currently used (may be a new map or an existing map being edited)
    CloudAnchorMap currentMap;

    // Collection of all maps
    CloudAnchorMapCollection existingMaps;

    private bool saveInProgress;

    private bool loadObjectsWithAnchors; // if false, only load anchors. If true, load AR objects with anchors

    private bool hideAnchorsModel; // if false, show anchors models (blue capsule + axis). If true, hide models

    private bool editingMap; // if false, it is a new map. If true, we are editing an existing map

    private bool anchorNameOrRotationChanged; // Flag used to know if something changed (and thus has to be saved)

    // We want to be able to rotate the anchor marker to use visual axis
    private GameObject selectedAnchorMarker;

    // Used with rotation slider
    private float previousRotationValue;


    List<ARAnchor> arAnchorsList; // Local anchors
    List<ARCloudAnchor> pendingCloudAnchors; // Anchors in progress of hosting or resolving (Depending of ApplicationMode)
    List<ARAnchor> arAnchorsHosted; // Anchors already hosted on Google Cloud
    List<ARCloudAnchor> stabilisingCloudAnchors; // Anchors in progress of stabilising (Resolving 'officially' ends when anchors are stabilised)

    Dictionary<string, Vector3> previousCloudAnchorsPositions; // Store previous Cloud Anchors positions to check stabilisation

    Dictionary<GameObject, string> anchorMarkerCloudId; // Link anchor markers gameobjects to corresponding Cloud ID
    Dictionary<GameObject, string> anchorMarkerToAnchorName; // Link anchor markers gameobjects to corresponding anchor name

    // Used because we don't have the cloud anchor id before cloud anchor has finished hosting
    // Will be used to add hosted anchors to anchorMarkerCloudId in order to be able to delete them right after hosting them
    Dictionary<ARCloudAnchor, GameObject> cloudAnchorToAnchorMarker; 


    public enum ApplicationMode
    {
        // Ready to host or resolve.
        Ready,

        // Hosting Cloud Anchors.
        Hosting,

        // Resolving Cloud Anchors.
        Resolving,
    }


    //************************************************************
    // Events
    //************************************************************

    //TimeOutEvent is send when 
    public delegate void TimeOutEvent();
    public static event TimeOutEvent OnTimeOut;

    private static void SendTimeOutEvent()
    {
        if (OnTimeOut != null)
            OnTimeOut();
    }

    //Event send when no map name was given.
    public delegate void EmptyMapNameEvent();
    public static event EmptyMapNameEvent OnEmptyMapName;

    private static void SendEmptyMapNameEvent()
    {
        if (OnEmptyMapName != null)
            OnEmptyMapName();
    }

    //Event send when map name is already taken.
    public delegate void MapNameTakenEvent();
    public static event MapNameTakenEvent OnMapNameTaken;

    private static void SendMapNameTakenEvent()
    {
        if (OnMapNameTaken != null)
            OnMapNameTaken();
    }

    //Event send when map is saving.
    public delegate void SavingMapEvent();
    public static event SavingMapEvent OnSavingMap;

    private static void SendSavingMapEvent()
    {
        if (OnSavingMap != null)
            OnSavingMap();
    }

    //Event send when map is saving.
    public delegate void MapSavedEvent();
    public static event MapSavedEvent OnMapSaved;

    private static void SendMapSavedEvent()
    {
        if (OnMapSaved != null)
            OnMapSaved();
    }

    //Event send when attempting to save a map but accuracy threshold is not yet met for every anchors.
    public delegate void ThresholdNotMetEvent();
    public static event ThresholdNotMetEvent OnThresholdNotMet;

    private static void SendThresholdNotMetEvent()
    {
        if (OnThresholdNotMet != null)
            OnThresholdNotMet();
    }

    //Event send when all ar objects on map have been saved successfully.
    public delegate void MapObjectsSavedEvent();
    public static event MapObjectsSavedEvent OnMapObjectsSaved;

    private static void SendMapObjectsSavedEvent()
    {
        if (OnMapObjectsSaved != null)
            OnMapObjectsSaved();
    }


    //************************************************************
    // Setup methods
    //************************************************************


    void Awake()
    {
        
    }

    // Start is called before the first frame update
    void Start()
    {
        Reset(false);
    }

    // Update is called once per frame
    void Update()
    {
        UpdatingMapQuality();

        UpdatePendingCloudAnchors();

        UpdateAnchorsHostingFinished();

        //LogFirstAnchorPosition();

        timer += Time.deltaTime;
    }

    private void LateUpdate()
    {
        if(hasTimedOut)
        {
            SendTimeOutEvent();
            hasTimedOut = false;
        }
    }

    public void Reset(bool trueReset)
    {
        if(trueReset)
        {
            RemoveAllCloudAnchors();
            StopCoroutine("UpdateStabilisingCloudAnchors");
        }    

        arAnchorsList = new List<ARAnchor>();
        pendingCloudAnchors = new List<ARCloudAnchor>();
        arAnchorsHosted = new List<ARAnchor>();
        stabilisingCloudAnchors = new List<ARCloudAnchor>();

        FetchCloudAnchorMapCollection((CloudAnchorMapCollection map) =>
        {
            Debug.Log("Reset (CloudAnchorController) callback returned");
            existingMaps = map;
        });

        saveInProgress = false;

        previousCloudAnchorsPositions = new Dictionary<string, Vector3>();

        anchorMarkerCloudId = new Dictionary<GameObject, string>();
        anchorMarkerToAnchorName = new Dictionary<GameObject, string>();

        cloudAnchorToAnchorMarker = new Dictionary<ARCloudAnchor, GameObject>();

        timer = 0f;
        hasTimedOut = false;

        mode = ApplicationMode.Ready;

        currentMap = null;

        anchorNameOrRotationChanged = false;

        //mapNameInput.text = "";

        previousRotationValue = 0f;
        //rotationSlider.value = 0f;
        //rotationInputField.text = "0";

        //anchorNameInputField.text = "";

        selectedAnchorMarker = null;


        // Debug: reset color of plane
        Gradient grad = new Gradient();
        GradientColorKey startGrad = new GradientColorKey(Color.black, 0);
        GradientColorKey endGrad = new GradientColorKey(Color.black, 1);
        grad.colorKeys = new GradientColorKey[] { startGrad, endGrad };

        foreach (ARPlane plane in planeManager.trackables)
        {
            plane.gameObject.GetComponent<LineRenderer>().colorGradient = grad;
        }
    }

    private void LogFirstAnchorPosition()
    {
        // At least 1 anchor
        if(arAnchorsList.Count > 0)
        {
            //debugText.text = arAnchorsList[0].transform.position.ToString();
            Debug.Log(arAnchorsList[0].transform.position.ToString());
        }
    }

    public bool WorkInProgress()
    {
        return pendingCloudAnchors.Count != 0 || stabilisingCloudAnchors.Count != 0 || saveInProgress;
    }

    public void ForceStop()
    {
        foreach (ARCloudAnchor cloudAnchor in pendingCloudAnchors)
        {
            Destroy(cloudAnchor.gameObject);
        }

        foreach (ARCloudAnchor cloudAnchor in stabilisingCloudAnchors)
        {
            Destroy(cloudAnchor.gameObject);
        }
    }


    //************************************************************
    // Variables setters
    //************************************************************


    public void AttachAnchorToggle(bool val)
    {
        AttachAnchorToPlane = val;
    }

    public void OnlineModeToggle(bool val)
    {
        onlineMode = val;
    }

    public void SetLoadObjectsWithAnchors(bool val)
    {
        loadObjectsWithAnchors = val;
    }

    public void SetHideAnchorsModel(bool val)
    {
        hideAnchorsModel = val;
    }

    public void SetEditingMap(bool val)
    {
        editingMap = val;
    }

    public bool SetApplicationMode(ApplicationMode mode)
    {
        if(!WorkInProgress())
        {
            Debug.Log("Change mode to " + mode);
            this.mode = mode;
            return true;
        }
        else
        {
            Debug.Log("pending anchor count: " + pendingCloudAnchors.Count);
            Debug.Log("stabilising anchor count: " + stabilisingCloudAnchors.Count);
            // TODO display "Resolution / Hosting in progress, cannot change mode"
            Debug.Log("Resolution / Hosting in progress, cannot change mode");
            return false;
        }
    }


    //************************************************************
    // Map functions
    //************************************************************


    public void LoadMap(string mapToLoadName)
    {
        // Check that there is no work in progress and change application mode
        if (!SetApplicationMode(ApplicationMode.Resolving)) return;

        timer = 0f; // Reset timer
        hasTimedOut = false;

        Debug.Log("LoadMap called with: " + mapToLoadName);

        if(existingMaps == null)
        {
            // Reload from API (Sometimes APIController is slow to init)
            FetchCloudAnchorMapCollection((CloudAnchorMapCollection map) =>
            {
                Debug.Log("Reset (CloudAnchorController) callback returned");
                existingMaps = map; // TODO new CloudAnchorMapCollection if null?

                currentMap = existingMaps.Collection.Find(x => x.name == mapToLoadName);

                // Set name in textfield
                //mapNameInput.text = currentMap.name;

                foreach (var anchor in currentMap.anchors)
                {
                    // Resolving all anchors of the map
                    ARCloudAnchor cloudAnchor =
                            anchorManager.ResolveCloudAnchorId(anchor.id);
                    if (cloudAnchor == null)
                    {
                        Debug.LogFormat("Failed to resolve Cloud Anchor " + anchor.id);
                    }
                    else
                    {
                        pendingCloudAnchors.Add(cloudAnchor);
                    }
                }

                StartCoroutine("UpdateStabilisingCloudAnchors"); // Note: don't forget to stop the coroutine before loading the next map
            });
        }
        else
        {
            currentMap = existingMaps.Collection.Find(x => x.name == mapToLoadName);

            // Set name in textfield
            //mapNameInput.text = currentMap.name;

            foreach (var anchor in currentMap.anchors)
            {
                // Resolving all anchors of the map
                ARCloudAnchor cloudAnchor =
                        anchorManager.ResolveCloudAnchorId(anchor.id);
                if (cloudAnchor == null)
                {
                    Debug.LogFormat("Failed to resolve Cloud Anchor " + anchor.id);
                }
                else
                {
                    pendingCloudAnchors.Add(cloudAnchor);
                }
            }

            StartCoroutine("UpdateStabilisingCloudAnchors"); // Note: don't forget to stop the coroutine before loading the next map
        }
    }

    // Reload the current map (for example after an anchor resolving timeout)
    public void ReloadMap()
    {
        String crtMapName = currentMap.name;
        Reset(true);
        LoadMap(crtMapName);
    }

    public void SaveMap(string mapName)
    {
        // Check that there is no work in progress and change application mode
        if (!SetApplicationMode(ApplicationMode.Hosting))
        {
            Debug.Log("Can't save, hosting / resolving in progress");
            return;
        }

        // If name textfield is empty
        if(string.IsNullOrEmpty(mapName))
        {
            Debug.LogError("Name empty");
            SendEmptyMapNameEvent();
            return;
        }

        if (existingMaps == null)
        {
            // Reload from API (Sometimes APIController is slow to init)
            FetchCloudAnchorMapCollection((CloudAnchorMapCollection map) =>
            {
                Debug.Log("Reset (CloudAnchorController) callback returned");
                if(map == null)
                {
                    existingMaps = new CloudAnchorMapCollection();
                }
                else
                {
                    existingMaps = map;
                }

                SaveMapRemaining(mapName);
            });
        }
        else
        {
            SaveMapRemaining(mapName);
        }
    }

    private void SaveMapRemaining(string mapName)
    {
        if (editingMap)
        {
            // Another map already has the same name than the one your are editing
            foreach (CloudAnchorMap map in existingMaps.Collection)
            {
                if (map.name.Equals(mapName) && map != currentMap) // TODO comparer statamic id? anciennement: !map.Equals(currentMap)
                {
                    // Name exists in DB and isn't current map
                    SendMapNameTakenEvent();
                    Debug.LogError("Name taken");
                    return;
                }
            }

            // Edit name
            if (!currentMap.name.Equals(mapName))
            {
                saveInProgress = true; // The name changed, we need to save
            }
            currentMap.name = mapName;

            if (anchorNameOrRotationChanged)
            {
                saveInProgress = true; // At least 1 anchor name changed, we need to save
                Debug.Log("anchor name or rotation changed, will save");
            }

            // Keep only the anchors that remain
            var currentMapAnchorsCleaned = currentMap.anchors.FindAll(anchor => anchorMarkerCloudId.ContainsValue(anchor.id));
            if (currentMap.anchors.Count != currentMapAnchorsCleaned.Count)
            {
                saveInProgress = true; // Some hosted anchors were removed, we need to save
            }
            currentMap.anchors = currentMapAnchorsCleaned;
        }
        else
        {
            List<String> mapNames = new List<string>();

            // Get all maps names
            foreach (var map in existingMaps.Collection)
            {
                mapNames.Add(map.name);
            }

            if (mapNames.Contains(mapName) || string.IsNullOrEmpty(mapName))
            {
                // Prepare currentMap in case we want to replace (unused)
                currentMap = existingMaps.Collection.Find(x => x.name == mapName);

                SendMapNameTakenEvent(); //Event name taken (should display popup name taken)
                return;
            }
            else
            {
                // Create empty map with name given
                currentMap = new CloudAnchorMap(mapName);
            }
        }


        // If all thresholds aren't reached, the map can't be saved
        if (!AllQualityThresholdReached())
        {
            SendThresholdNotMetEvent();
            return;
        }


        // Host all anchors
        foreach (var anchor in arAnchorsList)
        {
            // Creating a Cloud Anchor with lifetime = 1 day.
            // This is configurable up to 365 days when keyless authentication is used.
            ARCloudAnchor cloudAnchor = anchorManager.HostCloudAnchor(anchor, 1);
            if (cloudAnchor == null)
            {
                Debug.LogFormat("Failed to create a Cloud Anchor.");
                // TODO: show error message, hide "saving" overlay
            }
            else
            {
                cloudAnchorToAnchorMarker.Add(cloudAnchor, anchor.GetComponentInChildren<Marker>().gameObject);

                pendingCloudAnchors.Add(cloudAnchor);
                saveInProgress = true;
            }
        }

        // If nothing changed, nothing to save
        if (!saveInProgress)
        {
            Debug.Log("No change to save");
        }
        else
        {
            // Display saving infos
            SendSavingMapEvent();
        }
    }


    //************************************************************
    // Anchor functions
    //************************************************************


    public void UpdateAnchorName(string name)
    {
        if(selectedAnchorMarker != null)
        {
            if(anchorMarkerToAnchorName.ContainsKey(selectedAnchorMarker))
            {
                // Update name
                anchorMarkerToAnchorName[selectedAnchorMarker] = name;
                anchorNameOrRotationChanged = true;
            }
            else
            {
                // Add anchor and it's name. This shouldn't happend
                Debug.Log("Selected anchor was not in anchorMarkerToAnchorName. This shouldn't happend");
                anchorMarkerToAnchorName.Add(selectedAnchorMarker, name);
            }

            selectedAnchorMarker.GetComponentInChildren<TextMeshPro>().text = name;
        }
    }

    public void UpdateRotationValue(float value)
    {
        Debug.Log("rotation slider value: " + value);
        if (selectedAnchorMarker != null)
        {
            float delta = value - previousRotationValue;
            selectedAnchorMarker.transform.Rotate(Vector3.up * delta);
            anchorNameOrRotationChanged = true;
        }

        previousRotationValue = value;
        //rotationInputField.text = "" + value;
        //TODO Send event rotation changed
    }

    public void UpdateRotationInput(string value)
    {
        if(float.Parse(value) < 0)
        {
            //rotationInputField.text = "0";
            //TODO Send event update rotation
        }
        else if (float.Parse(value) > 360)
        {
            //rotationInputField.text = "360";
            //TODO Send event update rotation
        }

        if (selectedAnchorMarker != null)
        {
            float delta = float.Parse(value) - previousRotationValue;
            selectedAnchorMarker.transform.Rotate(Vector3.up * delta);
            anchorNameOrRotationChanged = true;
        }

        previousRotationValue = float.Parse(value);
        //rotationSlider.value = float.Parse(value);
        //TODO Send event update rotation
    }

    public void UpdateSelectedAnchor(GameObject anchorMarker)
    {
        selectedAnchorMarker = anchorMarker;

        Debug.Log(anchorMarker.transform.localRotation.eulerAngles);

        float updatedRotation = anchorMarker.transform.localRotation.eulerAngles.y;

        previousRotationValue = updatedRotation;
        //rotationSlider.value = updatedRotation;
        //TODO Send Event update rotation
        //rotationInputField.text = "" + updatedRotation;

        //anchorNameInputField.text = anchorMarkerToAnchorName[anchorMarker];
        //TODO Anchor name changed
    }

    public void PlaceCloudAnchor(ARPlane hitPlane, Pose hitPose)
    {
        ARAnchor anchor;

        if(AttachAnchorToPlane)
        {
            anchor = anchorManager.AttachAnchor(hitPlane, hitPose);
        }
        else
        {
            anchor = Instantiate(localAnchorPrefab, hitPose.position, hitPose.rotation).AddComponent<ARAnchor>();
        }

        var planeType = PlaneAlignment.HorizontalUp;

        if (anchor == null)
        {
            Debug.Log("Error creating anchor.");
        }
        else
        {
            // Parenting the anchorMarker to the prefab
            GameObject anchorMarkerPlaced =
                Instantiate(anchorMarkerObject, anchor.transform);

            // Stores the anchor so that it may be removed later.
            arAnchorsList.Add(anchor);

            // Link anchor name to new anchor
            anchorMarkerToAnchorName.Add(anchorMarkerPlaced, "New anchor");
            anchorMarkerPlaced.GetComponentInChildren<TextMeshPro>().text = "New anchor";

            planeType = hitPlane.alignment;

            // Attach map quality indicator to this anchor.
            var indicatorGO =
                Instantiate(MapQualityIndicatorPrefab, anchor.transform);
            _qualityIndicator = indicatorGO.GetComponent<MapQualityIndicator>();
            _qualityIndicator.DrawIndicator(planeType, sessionOrigin.camera);
        }
    }

    public void RemoveCloudAnchor(GameObject markerOfAnchorToRemove)
    {
        // TODO: Remove from Google Cloud with ID (when SAVING the map) -> if(arAnchorsHosted.contains...) -> 
        // export BEARER_TOKEN=`(oauth2l fetch --json creds.json arcore.management)`
        // curl - H "Authorization: Bearer $BEARER_TOKEN" - X "DELETE" https://arcorecloudanchor.googleapis.com/v1beta2/management/anchors/your-anchor-id-here
        // anchorMarkerCloudId[markerOfAnchorToRemove] -> gives anchor id

        anchorMarkerCloudId.Remove(markerOfAnchorToRemove);
        anchorMarkerToAnchorName.Remove(markerOfAnchorToRemove);

        arAnchorsHosted.Remove(markerOfAnchorToRemove.transform.parent.GetComponent<ARAnchor>());

        // Raycast hits the 3D object marker that is child of the anchor
        arAnchorsList.Remove(markerOfAnchorToRemove.transform.parent.GetComponent<ARAnchor>());
        Destroy(markerOfAnchorToRemove.transform.parent.gameObject);
    }

    // Remove all cloud anchors of the current map
    public void RemoveAllCloudAnchors()
    {
        foreach (var anchor in arAnchorsList)
        {
            Destroy(anchor.gameObject);
        }
        arAnchorsList.Clear();

        foreach (var anchor in arAnchorsHosted)
        {
            Destroy(anchor.gameObject);
        }
        arAnchorsHosted.Clear();
    }


    // Display the list of cloud anchors on the current map (used for debug only)
    public void ListCloudAnchors()
    {
        //if (debugText != null)
        //{
        //    var textList = "";
        //    foreach (var anchor in arAnchorsList)
        //    {
        //        textList += anchor.gameObject.name + "\n ";
        //    }

        //    foreach (var anchor in arAnchorsHosted)
        //    {
        //        textList += anchor.gameObject.name + "\n ";
        //    }
        //    debugText.text = textList;
        //}

        // AR Session State
        //Debug.Log("AR session state: " + ARSession.state.ToString());
        // Similar to Session State with 'Limited' and 'Tracking' values
        //Debug.Log("XRSessionSubsystem Tracking state: " + m_ARSession.subsystem.trackingState.ToString());
        // NotTrackingReason is more precise about why the tracking is not working
        //Debug.Log("XRSessionSubsystem NotTrackingReason: " + m_ARSession.subsystem.notTrackingReason.ToString());
    }

    public void FetchCloudAnchorMapCollection(System.Action<CloudAnchorMapCollection> callback)
    {
        if (onlineMode)
        {
            requestsManager.GetAllMaps((CloudAnchorMapCollection mapCollection) => {
                // Give result to right function
                Debug.Log("FetchCloudAnchorMapCollection callback returned");
                // mapCollection = CleanLoadedCloudAnchorMapCollection(mapCollection); // Bug: will block Invoke (no error appears...)
                callback.Invoke(mapCollection);
                //callback?.Invoke(CleanLoadedCloudAnchorMapCollection(mapCollection));
            });
        }
        else
        {
            CloudAnchorMapCollection mapCollection = JsonUtility.FromJson<CloudAnchorMapCollection>(
                    PlayerPrefs.GetString(_persistentCloudAnchorsStorageKey));
            // Give result to right function
            callback?.Invoke(CleanLoadedCloudAnchorMapCollection(mapCollection));
        }
    }

    // Called after mapCollection JSON is retrieved (either online or locally)
    private CloudAnchorMapCollection CleanLoadedCloudAnchorMapCollection(CloudAnchorMapCollection mapCollection)
    {
        if (mapCollection != null)
        {
            // Remove all anchors created more than 24 hours and update stored maps.
            DateTime current = DateTime.Now;
            foreach (CloudAnchorMap map in mapCollection.Collection)
                map.anchors.RemoveAll(
                data => current.Subtract(data.CreatedTime).Days > 0);

            // Remove all maps without anchors
            mapCollection.Collection.RemoveAll(
                data => data.anchors.Count == 0);
            PlayerPrefs.SetString(_persistentCloudAnchorsStorageKey,
                JsonUtility.ToJson(mapCollection));
        }
        else
        {
            mapCollection = new CloudAnchorMapCollection();
        }


        return mapCollection;
    }


    public void SaveCloudAnchorMapCollection(CloudAnchorMap data, System.Action callback) //TODO: make method private? TODO2 remove argument?
    {
        // Update anchors name with dictionary in currentMap
        foreach(GameObject anchorMarker in anchorMarkerCloudId.Keys)
        {
            CloudAnchorItem correspondingItem = currentMap.anchors.Find(x => x.id == anchorMarkerCloudId[anchorMarker]);
            Debug.Log("Updating '" + correspondingItem.name + "' with '" + anchorMarkerToAnchorName[anchorMarker] +"'");
            correspondingItem.name = anchorMarkerToAnchorName[anchorMarker];

            Quaternion rot = anchorMarker.transform.localRotation;
            correspondingItem.markerRotation = new SerializableQuaternion(rot.x, rot.y, rot.z, rot.w);
        }

        // Only for a new map in local mode
        if(!editingMap && !onlineMode)
        {
            // Sort the data from latest map to oldest map for the dropdown menu
            existingMaps.Collection.Add(currentMap);
            existingMaps.Collection.Sort((left, right) => right.CreatedTime.CompareTo(left.CreatedTime));

            // Remove the oldest maps if the capacity exceeds storage limit.
            if (existingMaps.Collection.Count > _storageLimit)
            {
                existingMaps.Collection.RemoveRange(
                    _storageLimit, existingMaps.Collection.Count - _storageLimit);
            }
        }

        Debug.Log("Current map: " + currentMap.ToString());
        if(existingMaps.Collection.Count > 0)
        {
            Debug.Log("Previously existing maps: " + existingMaps.Collection[0].ToString());
        }

        if(onlineMode)
        {
            if(editingMap)
            {
                requestsManager.EditMap(currentMap, () =>
                {
                    callback?.Invoke();
                });
            }
            else
            {
                // If we are not editing, it's a new map
                requestsManager.CreateMap(currentMap, () =>
                {
                    callback?.Invoke();
                });
            }
        }
        else
        {
            PlayerPrefs.SetString(_persistentCloudAnchorsStorageKey, JsonUtility.ToJson(existingMaps));
            callback?.Invoke();
        }
    }

    public void SaveCloudAnchorMapWithObjectsCollection(Dictionary<GameObject, List<GameObjectWithPrefabName>> dict) //TODO: make method private?
    {
        SendSavingMapEvent();

        foreach (CloudAnchorItem cloudAnchorItem in currentMap.anchors)
        {
            cloudAnchorItem.mapObjects.Clear(); // Clear all objects stored
            //TODO It is possible to only apply changes instead of replacing everything, for optimisation purpose only
        }

        foreach (var anchorMarker in dict)
        {
            Debug.Log("anchorMarkerCloudId: " + anchorMarkerCloudId.ToString());
            Debug.Log("anchorMarker.Key: " + anchorMarker.Key);

            var correspondingAnchorId = anchorMarkerCloudId[anchorMarker.Key];
            var correspondingStoredAnchor = currentMap.anchors.Find(x => x.id == correspondingAnchorId);

            foreach (var objectToAdd in anchorMarker.Value)
            {
                if(objectToAdd.gameObject != null)
                {
                    correspondingStoredAnchor.addObject(objectToAdd.prefabName, objectToAdd.gameObject);
                }
            }
        }

        Debug.Log(currentMap.ToString());

        if(onlineMode)
        {
            requestsManager.EditARObjects(currentMap, () =>
            {
                SendMapSavedEvent();
            });
        }
        else
        {
            PlayerPrefs.SetString(_persistentCloudAnchorsStorageKey, JsonUtility.ToJson(existingMaps));
            SendMapObjectsSavedEvent();
        }
    }

    // Get the camera pose for the current frame.
    public Pose GetCameraPose()
    {
        return new Pose(sessionOrigin.camera.transform.position,
            sessionOrigin.camera.transform.rotation);
    }

    private bool AllQualityThresholdReached()
    {
        foreach (var anchor in arAnchorsList)
        {
            // MapQualityIndicator is deactivated after the animation when Threshold is reached
            if (anchor.GetComponentInChildren<MapQualityIndicator>() != null && !anchor.GetComponentInChildren<MapQualityIndicator>().ReachQualityThreshold)
            {
                return false;
            }
        }

        return true;
    }

    private void UpdatingMapQuality()
    {
        // All anchors have already reached the required quality (or there is no anchor at all)
        if (AllQualityThresholdReached())
        {
            //Debug.Log("all quality threshold reached in Updating map quality");
            return;
        }

        // Update map quality:
        int qualityState = 2;
        // Can pass in ANY valid camera pose to the mapping quality API.
        // Ideally, the pose should represent users’ expected perspectives.
        FeatureMapQuality quality =
            anchorManager.EstimateFeatureMapQualityForHosting(GetCameraPose());
        //Debug.Log("Current mapping quality: " + quality);
        qualityState = (int)quality;
        _qualityIndicator.UpdateQualityState(qualityState);
    }

    private void UpdatePendingCloudAnchors()
    {
        foreach (var cloudAnchor in pendingCloudAnchors)
        {
            if (cloudAnchor.cloudAnchorState == CloudAnchorState.Success)
            {
                if (mode == ApplicationMode.Hosting)
                {
                    //Debug.LogFormat("Succeed to host the Cloud Anchor: {0}.",
                        //cloudAnchor.cloudAnchorId);
                    Debug.Log("...Succeed to host the Cloud Anchor: " + cloudAnchor.cloudAnchorId);

                    currentMap.addAnchor(cloudAnchor.cloudAnchorId, anchorMarkerToAnchorName[cloudAnchorToAnchorMarker[cloudAnchor]], cloudAnchorToAnchorMarker[cloudAnchor].transform.localRotation);

                    // Save ID so it is possible to remove it from the map later
                    anchorMarkerCloudId.Add(cloudAnchorToAnchorMarker[cloudAnchor], cloudAnchor.cloudAnchorId);

                    // Remove from list "to host" to avoid hosting the same anchor twice
                    arAnchorsList.Remove(cloudAnchorToAnchorMarker[cloudAnchor].transform.parent.GetComponent<ARAnchor>());

                    // Add to "hosted" list
                    arAnchorsHosted.Add(cloudAnchorToAnchorMarker[cloudAnchor].transform.parent.GetComponent<ARAnchor>());
                }
                else if (mode == ApplicationMode.Resolving)
                {
                    Debug.LogFormat("Succeed to resolve the Cloud Anchor: {0}",
                        cloudAnchor.cloudAnchorId);

                    // Display components
                    /*Debug.Log("Components of GameObject containing Cloud Anchor Before removing: ");
                    Component[] components = cloudAnchor.gameObject.GetComponents(typeof(Component));
                    foreach (Component component in components)
                    {
                        Debug.Log(component.ToString());
                    }*/

                    stabilisingCloudAnchors.Add(cloudAnchor);
                    previousCloudAnchorsPositions.Add(cloudAnchor.cloudAnchorId, new Vector3(0f, 0f, 0f)); // use (inf,inf,inf) as starting point ? (edge case)
                }
                else
                {
                    Debug.Log("Unknown Application Mode");
                }
            }
            else if (cloudAnchor.cloudAnchorState != CloudAnchorState.TaskInProgress)
            {
                if (mode == ApplicationMode.Hosting)
                {
                    //Debug.LogFormat("Failed to host the Cloud Anchor with error {0}.",
                        //cloudAnchor.cloudAnchorState);
                    Debug.Log("...Failed to host the Cloud Anchor with error: " + cloudAnchor.cloudAnchorState);
                }
                else if (mode == ApplicationMode.Resolving)
                {
                    Debug.LogFormat("Failed to resolve the Cloud Anchor {0} with error {1}.",
                        cloudAnchor.cloudAnchorId, cloudAnchor.cloudAnchorState);
                }
                else
                {
                    Debug.Log("Unknown Application Mode");
                }
            }
            else
            {
                Debug.Log("Other cloud anchor state: " + cloudAnchor.cloudAnchorState);
            }

            //---------------------
            Debug.Log("At least 1 element in _pendingCloudAnchor");
            //---------------------
        }

        pendingCloudAnchors.RemoveAll(
            x => x.cloudAnchorState != CloudAnchorState.TaskInProgress);

        if(mode == ApplicationMode.Resolving && pendingCloudAnchors.Count != 0)
        {
            if(timer > timeoutDuration)
            {
                Debug.Log("Timeout! Pending anchors destroyed: " + pendingCloudAnchors.Count);
                //debugText.text = "Pending anchors timeout";
                foreach (ARCloudAnchor cloudAnchor in pendingCloudAnchors)
                {
                    Destroy(cloudAnchor.gameObject);
                }
                pendingCloudAnchors = new List<ARCloudAnchor>();

                hasTimedOut = true;
            }
        }
    }

    private void UpdateAnchorsHostingFinished()
    {
        // Check if all pending anchors have been hosted
        if (saveInProgress && pendingCloudAnchors.Count == 0)
        {
            // Change before async method callback to avoid multiple saving with Update()
            saveInProgress = false;
            
            SaveCloudAnchorMapCollection(currentMap, () =>
            {
                SendMapSavedEvent();
                //mapNameSaveDisplay.text = mapNameInput.text;

                Debug.Log(currentMap.ToString() + ", number of anchors = " + currentMap.anchors.Count);

                //debugText.text = "UpdateAnchorsHostingFinished";
                
                anchorNameOrRotationChanged = false;

                // After saving a new map, if we continue we want to modify it
                editingMap = true;
            });
        }
    }

    private IEnumerator UpdateStabilisingCloudAnchors()
    {
        List<ARCloudAnchor> toRemove = new List<ARCloudAnchor>();
        while (true)
        {
            yield return new WaitForSeconds(1f); // Wait 1 second before each stability check

            Debug.Log("Stabilisation check");

            foreach (var cloudAnchor in stabilisingCloudAnchors)
            {
                // If stabilised (didn't move too far since last check)
                if (Vector3.Distance(cloudAnchor.pose.position, previousCloudAnchorsPositions[cloudAnchor.cloudAnchorId]) < 0.05) // Distance in meters
                {
                    Debug.Log(cloudAnchor.cloudAnchorId + " is stabilised");
                    Vector3 localAnchorPosition = cloneVector3(cloudAnchor.pose.position);
                    Quaternion localAnchorRotation = new Quaternion(cloudAnchor.pose.rotation.x, cloudAnchor.pose.rotation.y, cloudAnchor.pose.rotation.z, cloudAnchor.pose.rotation.w);
                    Pose localAnchorPose = new Pose(localAnchorPosition, localAnchorRotation);

                    // TODO destroy gameobject, not only component?
                    // Destroy before showing anchor prefab to be sure that the Cloud Anchor is destroyed
                    Destroy(cloudAnchor.gameObject.GetComponent<ARCloudAnchor>()); // Stops API calls

                    ARAnchor localAnchor = null;

                    if(AttachAnchorToPlane)
                    {
                        // TODO test the best
                        //localAnchor = AttachAnchorToClosestPlane(localAnchorPose);
                        localAnchor = AttachAnchorToClosestStablePlane(localAnchorPose);
                    }
                    else
                    {
                        localAnchor = Instantiate(localAnchorPrefab, localAnchorPosition, localAnchorRotation).AddComponent<ARAnchor>();
                    }

                    var anchorMarkerInstance = Instantiate(anchorMarkerObject, localAnchor.transform);
                    anchorMarkerInstance.transform.localRotation = currentMap.anchors.Find(x => x.id == cloudAnchor.cloudAnchorId).markerRotation;
                    anchorMarkerCloudId.Add(anchorMarkerInstance, cloudAnchor.cloudAnchorId);

                    // Anchor name management
                    string currentName = currentMap.anchors.Find(x => x.id == cloudAnchor.cloudAnchorId).name;
                    anchorMarkerToAnchorName.Add(anchorMarkerInstance, currentName);
                    anchorMarkerInstance.GetComponentInChildren<TextMeshPro>().text = currentName;

                    previousCloudAnchorsPositions.Remove(cloudAnchor.cloudAnchorId);
                    toRemove.Add(cloudAnchor); // Can't remove from stabilisingCloudAnchors while in a foreach -> add to a list and delete after

                    // Hide anchors marker and axis models
                    if (hideAnchorsModel)
                    {
                        //anchorMarkerInstance.GetComponent<Renderer>().enabled = false;
                        HideAnchorMarkerRender(anchorMarkerInstance);
                    }

                    // Load AR objects of each anchor
                    if (loadObjectsWithAnchors)
                    {
                        Debug.Log("loadedMap.anchors.Find(x => x.id == cloudAnchor.cloudAnchorId): " + currentMap.anchors.Find(x => x.id == cloudAnchor.cloudAnchorId));

                        foreach (MapObject obj in currentMap.anchors.Find(x => x.id == cloudAnchor.cloudAnchorId).mapObjects) // TODO optimisable storing mapObjects with ARCloudAnchors ?
                        {
                            Debug.Log("Before InstantiatePrefabOnAnchor");
                            mapObjectsManager.InstantiatePrefabOnAnchor(anchorMarkerInstance, obj.prefabName, obj.position, obj.rotation, obj.scale);
                            Debug.Log("After InstantiatePrefabOnAnchor");
                        }
                    }

                    // Add to "hosted" list
                    //arAnchorsHosted.Add(cloudAnchorToAnchorMarker[cloudAnchor].transform.parent.GetComponent<ARAnchor>());
                    arAnchorsHosted.Add(localAnchor.GetComponent<ARAnchor>());
                }
                else
                {
                    previousCloudAnchorsPositions[cloudAnchor.cloudAnchorId] = cloneVector3(cloudAnchor.pose.position);
                }
            }
            foreach (var cloudAnchor in toRemove)
            {
                stabilisingCloudAnchors.Remove(cloudAnchor);
            }
            toRemove.Clear();


            // Check timeout
            if (stabilisingCloudAnchors.Count != 0)
            {
                if (timer > timeoutDuration)
                {
                    Debug.Log("Timeout! Stabilising anchors destroyed: " + stabilisingCloudAnchors.Count);
                    //debugText.text = "Stabilising anchors timeout";
                    foreach (ARCloudAnchor cloudAnchor in stabilisingCloudAnchors)
                    {
                        Destroy(cloudAnchor.gameObject);
                    }
                    stabilisingCloudAnchors = new List<ARCloudAnchor>();

                    hasTimedOut = true;
                }
            }
        }
    }

    private ARAnchor AttachAnchorToClosestPlane(Pose localAnchorPose)
    {
        /*
        Ray ray = new Ray(localAnchorPose.position, -localAnchorPose.up);
        List<ARRaycastHit> hits = new List<ARRaycastHit>();
        raycastManager.Raycast(ray, hits, TrackableType.Planes); // TrackableType.AllTypes
        */


        ARPlane closestPlane = null;
        Dictionary<ARPlane, float> candidatePlanes = new Dictionary<ARPlane, float>();
        float minDist = Mathf.Infinity;
        Vector3 crtPosition = localAnchorPose.position;
        float candidateMaxDiff = 0.1f;  // TODO choose good param

        Debug.Log("planeManager.trackables count: " + planeManager.trackables.count);

        foreach (ARPlane plane in planeManager.trackables)
        {
            Debug.Log("Plane classification: " + plane.classification.ToString());
            float crtDist = Mathf.Abs(plane.infinitePlane.GetDistanceToPoint(crtPosition));
            // plane.infinitePlane.ClosestPointOnPlane(currentPosition);
            if (crtDist < minDist)
            {
                minDist = crtDist;
                //closestPlane = plane;

                // Remove candidates not in range anymore
                candidatePlanes = candidatePlanes.Where(i => i.Value-minDist <= candidateMaxDiff)
                    .ToDictionary(i => i.Key, i => i.Value);
                candidatePlanes.Add(plane,crtDist);
            }
            else if(crtDist-minDist < candidateMaxDiff)
            {
                candidatePlanes.Add(plane, crtDist);
            }
        }

        //candidatePlanes.Select(i => Vector3.Distance(i.Key.center,crtPosition)).Aggregate((l,r) =>  );
        closestPlane = candidatePlanes.Aggregate((l, r) => Vector3.Distance(l.Key.center, crtPosition) < Vector3.Distance(r.Key.center, crtPosition) ? l : r).Key;

        // Debug: change color of plane
        Gradient grad = new Gradient();
        GradientColorKey startGrad = new GradientColorKey(Color.blue, 0);
        GradientColorKey endGrad = new GradientColorKey(Color.blue, 1);
        grad.colorKeys = new GradientColorKey[] { startGrad, endGrad };

        closestPlane.gameObject.GetComponent<LineRenderer>().colorGradient = grad;

        return anchorManager.AttachAnchor(closestPlane, localAnchorPose);
    }

    private ARAnchor AttachAnchorToClosestStablePlane(Pose localAnchorPose)
    {
        ARPlane closestPlane = null;
        Dictionary<ARPlane, float> candidatePlanes = new Dictionary<ARPlane, float>();
        float minDist = Mathf.Infinity;
        Vector3 crtPosition = localAnchorPose.position;
        float candidateMaxDiff = 0.1f;  // TODO choose good param

        foreach (ARPlane plane in planeManager.trackables)
        {
            Debug.Log("Plane classification: " + plane.classification.ToString() + ", alignement: " + plane.alignment);
            if(plane.subsumedBy != null)
                Debug.Log("Plane subsumedBy classification: " + plane.subsumedBy.classification + ", alignement: " + plane.subsumedBy.alignment);

            /*if(plane.classification == PlaneClassification.None || plane.classification == PlaneClassification.Seat)
            {
                // The plane is not interesting / unstable and is ignored
                continue;
            }*/
            
            float crtDist = Mathf.Abs(plane.infinitePlane.GetDistanceToPoint(crtPosition));

            if (crtDist < minDist)
            {
                minDist = crtDist;

                // Remove candidates not in range anymore
                candidatePlanes = candidatePlanes.Where(i => i.Value - minDist <= candidateMaxDiff)
                    .ToDictionary(i => i.Key, i => i.Value);
                candidatePlanes.Add(plane, crtDist);
            }
            else if (crtDist - minDist < candidateMaxDiff)
            {
                candidatePlanes.Add(plane, crtDist);
            }
        }

        // TODO Maybe do a plane projection of the point to find if it is in the boundaries (plane.boudary -> "The plane's boundary points, in plane space, that is, relative to this ARPlane's local position and rotation")
        closestPlane = candidatePlanes.Aggregate((l, r) => Vector3.Distance(l.Key.center, crtPosition) < Vector3.Distance(r.Key.center, crtPosition) ? l : r).Key;

        // Debug: change color of plane
        Gradient grad = new Gradient();
        GradientColorKey startGrad = new GradientColorKey(Color.blue, 0);
        GradientColorKey endGrad = new GradientColorKey(Color.blue, 1);
        grad.colorKeys = new GradientColorKey[] { startGrad, endGrad };

        closestPlane.gameObject.GetComponent<LineRenderer>().colorGradient = grad;

        return anchorManager.AttachAnchor(closestPlane, localAnchorPose);
    }

    private void HideAnchorMarkerRender(GameObject anchorMarker)
    {
        anchorMarker.GetComponent<Renderer>().enabled = false;
        foreach(Renderer renderer in anchorMarker.GetComponentsInChildren<Renderer>())
        {
            renderer.enabled = false;
        }
        
    }


    private Vector3 cloneVector3(Vector3 vector)
    {
        return new Vector3(vector.x, vector.y, vector.z);
    }


}
