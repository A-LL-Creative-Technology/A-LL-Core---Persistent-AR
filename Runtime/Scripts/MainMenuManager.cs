using System.Collections;
using System.Collections.Generic;
using System;
using UnityEngine;
using TMPro;
using UnityEngine.UI;

// Based on: https://github.com/google-ar/arcore-unity-extensions/tree/master/Samples~/PersistentCloudAnchors

public class MainMenuManager : MonoBehaviour
{

    public TMP_Dropdown dropdown; // Dropdown listing all existing Maps
    public CloudAnchorController Controller;
    public MapObjectsManager mapObjectsManager;
    //public CanvasManager canvasManager;
    public ARCursor arCursor;

    //public BeaconController beaconController;

    public Button editMapButton; // "Edit Map" button. Will be disabled when no map is selected
    public Button aRManagerButton; // "AR Manager" button. Will be disabled when no map is selected

    private CloudAnchorMapCollection mapCollection = new CloudAnchorMapCollection();

    // Start is called before the first frame update
    void Start()
    {
        
    }

    // Update is called once per frame
    void Update()
    {
        
    }

    // Called when dropdown selection change
    public void OnResolvingSelectionChanged()
    {
        // A map has been selected, the two buttons become available
        editMapButton.interactable = true;
        aRManagerButton.interactable = true;
    }

    public void OnEnable()
    {
        Debug.Log("MainMenuManager enabled");

        // Inform the user that the maps are loading
        var options = new List<TMP_Dropdown.OptionData>();
        options.Add(new TMP_Dropdown.OptionData("Loading maps..."));
        dropdown.options = options;

        // Ask the server for the maps stored
        Controller.FetchCloudAnchorMapCollection((CloudAnchorMapCollection map) =>
            {
                Debug.Log("OnEnable (MainMenuManager) callback returned");
                mapCollection = map;

                Debug.Log(JsonUtility.ToJson(mapCollection));

                var options = new List<TMP_Dropdown.OptionData>();

                if (mapCollection.Collection.Count > 0)
                {
                    // At least 1 map exists

                    dropdown.onValueChanged.AddListener(delegate
                    {
                        OnResolvingSelectionChanged();
                    });

                    foreach (var data in mapCollection.Collection)
                    {
                        //List maps in dropdown
                        options.Add(new TMP_Dropdown.OptionData(
                            data.name));
                    }
                }
                else
                {
                    // No map exists

                    options.Add(new TMP_Dropdown.OptionData("No map found"));
                }

                dropdown.options = options;
            });
    }

    public void OnDisable()
    {
        dropdown.onValueChanged.RemoveListener(delegate
        {
            OnResolvingSelectionChanged();
        });

        dropdown.ClearOptions();
        mapCollection.Collection.Clear();
    }

    // Load the map without the AR objects (used for anchors editing)
    public void LoadMap()
    {
        //arCursor.ResetARSession();

        Controller.SetLoadObjectsWithAnchors(false);

        Controller.SetApplicationMode(CloudAnchorController.ApplicationMode.Resolving);

        Debug.Log("dropdown.value = " + dropdown.value + ", mapCollectionSize = " + mapCollection.Collection.Count);
        Controller.LoadMap(mapCollection.Collection[dropdown.value].name);
        Controller.SetEditingMap(true);

        arCursor.SwitchRaycastingModeToAnchorsAndSurfaces();
        arCursor.SetUseCursor(true);

        //canvasManager.showPanelMapEditor();
    }

    // Load the map with the AR objects (used for AR objects editing)
    public void LoadObjectsWithAnchors()
    {
        //arCursor.ResetARSession();

        //StartCoroutine(WaitForSessionReset());

        Controller.SetLoadObjectsWithAnchors(true);

        Controller.SetApplicationMode(CloudAnchorController.ApplicationMode.Resolving);
        Debug.Log("dropdown.value = " + dropdown.value + ", mapCollectionSize = " + mapCollection.Collection.Count);
        Controller.LoadMap(mapCollection.Collection[dropdown.value].name);
        Controller.SetEditingMap(true);

        arCursor.SwitchRaycastingModeToObjects();
        arCursor.SetUseCursor(false);

        //canvasManager.showPanelSceneManager();
    }

    // When we want to reset the AR Session, he shall wait for the session to be initialised again...
    /*IEnumerator WaitForSessionReset()
    {
        yield return new WaitForSeconds(4f);

        Controller.SetLoadObjectsWithAnchors(true);

        Controller.SetApplicationMode(CloudAnchorController.ApplicationMode.Resolving);
        Debug.Log("dropdown.value = " + dropdown.value + ", mapCollectionSize = " + mapCollection.Collection.Count);
        Controller.LoadMap(mapCollection.Collection[dropdown.value].name);
        Controller.SetEditingMap(true);

        arCursor.SwitchRaycastingModeToObjects();
        arCursor.SetUseCursor(false);

        canvasManager.showPanelSceneManager();
    }*/

    // Setup the modules to create a new Map
    public void StartNewMap()
    {
        //arCursor.ResetARSession();

        Controller.SetLoadObjectsWithAnchors(false);

        Controller.SetApplicationMode(CloudAnchorController.ApplicationMode.Hosting);
        Controller.SetEditingMap(false);

        arCursor.SwitchRaycastingModeToAnchorsAndSurfaces();
        arCursor.SetUseCursor(true);

        //canvasManager.showPanelMapEditor();
    }

    // Load the map for the client experience using Bluetooth Beacons
    public void StartClientApp()
    {
        //arCursor.ResetARSession();

        Controller.SetLoadObjectsWithAnchors(true);

        // We don't want to see plane visualization in the client app
        arCursor.SetPlaneVisualization(false);

        Controller.SetApplicationMode(CloudAnchorController.ApplicationMode.Resolving);
        Controller.SetEditingMap(false);

        arCursor.SwitchRaycastingModeToAnchorsAndSurfaces();
        arCursor.SetUseCursor(false);

        //beaconController.StartScanning();

        //canvasManager.showPanelClientApp();
    }

    // Called by back buttons on the different menus to go back to main menu
    public void Back()
    {
        if(Controller.WorkInProgress())
        {
            Controller.ForceStop();
        }

        editMapButton.interactable = false;
        aRManagerButton.interactable = false;
        //beaconController.StopScanning();

        Controller.Reset(true);
        mapObjectsManager.Reset();
        arCursor.Reset();

        // Reload dropdown values from DB
        OnDisable();
        OnEnable();

        //canvasManager.showCanvasMainMenu();
    }

    private string FormatDateTime(DateTime time)
    {
        TimeSpan span = DateTime.Now.Subtract(time);
        return span.Hours == 0 ? span.Minutes == 0 ? "Just now" :
            string.Format("{0}m ago", span.Minutes) : string.Format("{0}h ago", span.Hours);
    }
}
