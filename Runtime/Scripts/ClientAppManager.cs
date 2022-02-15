using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using TMPro;

public class ClientAppManager : MonoBehaviour
{
    //public TextMeshProUGUI beaconNameDisplayTMP; // UI that display the beacons info (closest beacon)

    private CloudAnchorController cloudAnchorController;
    private string closestBeaconName;

    // Start is called before the first frame update
    void Start()
    {
        cloudAnchorController = gameObject.GetComponent<CloudAnchorController>();
        closestBeaconName = "";
        //BeaconController.ClosestBeaconChangedEvent += OnClosestBeaconChanged;
    }

    // Update is called once per frame
    void Update()
    {
        
    }

    void OnDestroy()
    {
        //BeaconController.ClosestBeaconChangedEvent -= OnClosestBeaconChanged;
    }

    public void LoadMapClosestBeacon()
    {
        // Reset cloud anchor controller (will remove previously found anchors)
        cloudAnchorController.Reset(true);

        // Setup because we just reset the cloud controller
        cloudAnchorController.SetLoadObjectsWithAnchors(true);
        cloudAnchorController.SetApplicationMode(CloudAnchorController.ApplicationMode.Resolving);
        cloudAnchorController.SetEditingMap(false);

        // Load map (map name correspond to beacon name)
        cloudAnchorController.LoadMap(closestBeaconName);
    }

    private void OnClosestBeaconChanged(string beaconName)
    {
        closestBeaconName = beaconName;
        //beaconNameDisplayTMP.text = "Closest beacon is: " + beaconName;
    }
}
