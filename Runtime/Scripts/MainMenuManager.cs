using System.Collections;
using System.Collections.Generic;
using System;
using UnityEngine;
using TMPro;
using UnityEngine.UI;

// Based on: https://github.com/google-ar/arcore-unity-extensions/tree/master/Samples~/PersistentCloudAnchors

public class MainMenuManager : MonoBehaviour
{

    //public TMP_Dropdown dropdown; // Dropdown listing all existing Maps
    [Header("Persistant AR Controllers")]
    public CloudAnchorController Controller;
    public MapObjectsManager mapObjectsManager;
    public ARCursor arCursor;

    // Start is called before the first frame update
    void Start()
    {
        
    }

    // Update is called once per frame
    void Update()
    {
        
    }

    public void FetchMaps(Action<CloudAnchorMapCollection> callback)
    {
        // Ask the server for the maps stored
        Controller.FetchCloudAnchorMapCollection((CloudAnchorMapCollection map) =>
        {
            callback.Invoke(map);
        });
    }

    // Load the map without the AR objects (used for anchors editing)
    public void LoadMap(string mapName)
    {
        //arCursor.ResetARSession();

        Controller.SetLoadObjectsWithAnchors(false);

        Controller.SetApplicationMode(CloudAnchorController.ApplicationMode.Resolving);

        Controller.LoadMap(mapName);
        Controller.SetEditingMap(true);

        arCursor.SwitchRaycastingModeToAnchorsAndSurfaces();
        arCursor.SetUseCursor(true);
    }

    // Load the map with the AR objects (used for AR objects editing)
    public void LoadObjectsWithAnchors(string mapName)
    {
        //arCursor.ResetARSession();

        //StartCoroutine(WaitForSessionReset());

        Controller.SetLoadObjectsWithAnchors(true);

        Controller.SetApplicationMode(CloudAnchorController.ApplicationMode.Resolving);

        Controller.LoadMap(mapName);
        Controller.SetEditingMap(true);

        arCursor.SwitchRaycastingModeToObjects();
        arCursor.SetUseCursor(false);

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

    }*/

    // Setup the modules to create a new Map
    public void StartNewMap()
    {
        //arCursor.ResetARSession();
        Controller.isClientApp = false;
        Controller.SetLoadObjectsWithAnchors(false);

        Controller.SetApplicationMode(CloudAnchorController.ApplicationMode.Hosting);
        Controller.SetEditingMap(false);

        arCursor.SwitchRaycastingModeToAnchorsAndSurfaces();
        arCursor.SetUseCursor(true);

    }

    // Load the map for the client experience using Bluetooth Beacons
    public void StartClientApp()
    {
        //arCursor.ResetARSession();
        Controller.isClientApp = true;
        Controller.SetLoadObjectsWithAnchors(true);

        // We don't want to see plane visualization in the client app
        arCursor.SetPlaneVisualization(false);

        Controller.SetApplicationMode(CloudAnchorController.ApplicationMode.Resolving);
        Controller.SetEditingMap(false);

        arCursor.SwitchRaycastingModeToAnchorsAndSurfaces();
        arCursor.SetUseCursor(false);
    }

    // Called by back buttons on the different menus to go back to main menu
    public void Back()
    {
        if(Controller.WorkInProgress())
        {
            Controller.ForceStop();
        }
        //beaconController.StopScanning();

        Controller.Reset(true);
        mapObjectsManager.Reset();
        arCursor.Reset();

        // Reload dropdown values from DB

        //canvasManager.showCanvasMainMenu();
    }

    private string FormatDateTime(DateTime time)
    {
        TimeSpan span = DateTime.Now.Subtract(time);
        return span.Hours == 0 ? span.Minutes == 0 ? "Just now" :
            string.Format("{0}m ago", span.Minutes) : string.Format("{0}h ago", span.Hours);
    }
}
