using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.XR.ARFoundation;
using UnityEngine.XR.ARSubsystems;
using UnityEngine.EventSystems;
using TMPro;
using UnityEngine.UI;
//using UnityEngine.InputSystem;

// Based on: https://www.youtube.com/watch?v=R3OCUE9TwZk and https://github.com/google-ar/arcore-unity-extensions/tree/master/Samples~/PersistentCloudAnchors


public class ARCursor : MonoBehaviour
{
    public GameObject cursorChildObject; // Red target used to place anchors
    public ARRaycastManager raycastManager; // Raycast manager from AR Session Origin used to raycast on AR trackables
    public ARSession m_ARSession; // AR Session used to reset session
    public ARPlaneManager m_PlaneManager; // Used to get AR plane hit by raycasting
    public CloudAnchorController controller; // The cloud anchor controller manages the AR anchors 
    public MapObjectsManager objectManager; // The map objects manager manages the AR objects

    public bool useCursor = true; // True if using the red target cursor to place anchors, False otherwise
    public bool touchToSelect = true; // True if using to controls to select anchors and objects

    //public TMP_Dropdown trackableTypeDropdown; // Dropdown of trackable types for AR Raycasting (placing anchors)

    // UI for AR Manager Page
    //public Button addARObjectButton;
    //public Button placeARObjectButton;
    //public Button editObjectButton;
    //public Button removeObjectButton;
    //public GameObject buttonPanel1; // Panel for add, place, edit and remove AR objects, as well as Save AR scene.
    //public GameObject buttonPanel2; // Panel for editing scale, rotation and position of AR objects
    //public TMP_Dropdown objectDropdown; // Dropdown with the list of all 3D objects that can be added to an AR scene

    private ARPlane hitPlane; // Store AR plane hit by raycasting
    private Pose hitPose; // Store position and rotation of the hit on AR plane
    private GameObject anchorMarkerSelected; // 3D object representing an anchor that is currently selected
    private GameObject mapObjectSelected; // 3D object of the AR scene currently selected

    private TrackableType currentTrackableType; // Trackable type targeted by AR raycasting for placing anchors

    private bool dropdownUsed; // AR Objects Dropdown is initialised with a placeholder value (index == -1). Application behavior changes after at least one selection

    private bool planeVisualization; // True if we want to see the planes detected by ARPlaneManager

    private static ARCursor instance; // Store instance of current ARCursor

    private IEnumerator currentTranslationcoroutine; // Coroutine of translation currently active


    // Return instance of current ARCursor
    public static ARCursor GetInstance()
    {
        return instance;
    }


    // The current raytracing mode.
    [HideInInspector]
    public RaycastingMode mode;

    public enum RaycastingMode
    {
        // Anchors and AR Trackables Surfaces. Used in Map Editor Page
        AnchorsAndSurfaces,

        // Map objects linked to anchors. Used in AR Manager Page
        Objects,

        // No raytracing
        None,
    }


    //************************************************************
    // Setup methods
    //************************************************************


    void Awake()
    {
        instance = this;
    }

    // Start is called before the first frame update
    void Start()
    {
        // Init value depends on Unity Editor settings
        cursorChildObject.SetActive(useCursor); 

        // Init default values
        changeRaycastingMode(RaycastingMode.AnchorsAndSurfaces);
        currentTrackableType = TrackableType.Planes;

        // Deactivate buttons
        //addARObjectButton.interactable = false;
        //placeARObjectButton.interactable = false;
        //editObjectButton.interactable = false;
        //removeObjectButton.interactable = false;

        // Deactivate AR Objects Dropdown
        //objectDropdown.interactable = false;

        // Reset AR Objects Dropdown usage state
        dropdownUsed = false;

        // Active by default
        planeVisualization = true;

        // Empty by default
        currentTranslationcoroutine = null;
    }

    // Update is called once per frame
    void Update()
    {
        // Update cursor (red target) position and raycast values every frame
        UpdateCursorAndRaycast();

        // Only check touch if activated and application not currently placing or moving and object
        if(touchToSelect && objectManager.mode != MapObjectsManager.SceneManagementMode.PlacingObject && objectManager.mode != MapObjectsManager.SceneManagementMode.MovingObject)
        {
            // If the player has not touched the screen then the update is complete.
            Touch touch;
            if (Input.touchCount < 1 ||
                (touch = Input.GetTouch(0)).phase != TouchPhase.Began)
            {
                return;
            }

            // Ignore the touch if it's pointing on UI objects.
            if (IsPointerOverUIObject())
            {
                Debug.Log("Blocked by UI");
                return;
            }

            // Perform hit test and apply correct interactions
            PerformHitTest(touch.position);
        }
    }

    public void Reset()
    {
        //// Reset Dropdown index
        //trackableTypeDropdown.value = 0;

        // Reset to default values
        currentTrackableType = TrackableType.PlaneWithinPolygon;
        changeRaycastingMode(RaycastingMode.AnchorsAndSurfaces);
        SetUseCursor(false);

        // Deactivate buttons
        //addARObjectButton.interactable = false;
        //placeARObjectButton.interactable = false;
        //editObjectButton.interactable = false;
        //removeObjectButton.interactable = false;

        // Deactivate AR Objects Dropdown
        //objectDropdown.interactable = false;

        // Reset AR Objects Dropdown usage state
        dropdownUsed = false;

        // If coroutine currently active, stop it
        if(currentTranslationcoroutine != null)
        {
            StopCoroutine(currentTranslationcoroutine);
            currentTranslationcoroutine = null;
        }
    }

    // Called by OnValueChanged of Trackable Dropdown (assigned in Unity Editor)
    public void UpdateTrackableType(int index)
    {
        switch(index)
        {
            case 0:
                currentTrackableType = TrackableType.PlaneWithinPolygon;
                break;
            case 1:
                currentTrackableType = TrackableType.PlaneWithinBounds;
                break;
            case 2:
                currentTrackableType = TrackableType.PlaneEstimated;
                break;
            case 3:
                currentTrackableType = TrackableType.PlaneWithinInfinity;
                break;
            case 4:
                currentTrackableType = TrackableType.Planes;
                break;
        }
    }

    // Change red target cursor usage
    public void SetUseCursor(bool val)
    {
        useCursor = val;
        cursorChildObject.SetActive(useCursor);
    }

    // Detect if raycasting at touch position is on a UI object
    private bool IsPointerOverUIObject()
    {
        PointerEventData eventDataCurrentPosition = new PointerEventData(EventSystem.current);
        if(eventDataCurrentPosition == null)
        {
            Debug.Log("Event data current position is null!");
        }
        eventDataCurrentPosition.position = new Vector2(Input.mousePosition.x, Input.mousePosition.y);
        List<RaycastResult> results = new List<RaycastResult>();
        EventSystem.current.RaycastAll(eventDataCurrentPosition, results);

        if(results.Count == 0)
        {
            Debug.Log("nothing hit");
        }

        for (int i = 0; i < results.Count; i++)
        {
            if (results[i].gameObject.layer == 5) //layer number 5 is the UI layer
            {
                Debug.Log("Blocked by UI: " + results[i].gameObject.name);
                return true;
            }
        }

        return false;
    }

    // Raycast from touch position. Apply correct interactions depending of what is hit (anchor or AR object)
    private void PerformHitTest(Vector2 touchPos)
    {
        Debug.Log("Hit test at: " + touchPos);
        Debug.Log("Current mode: " + mode);

        // Construct a ray from the current touch coordinates
        Ray ray = Camera.main.ScreenPointToRay(touchPos);

        RaycastHit hitAnchor; // Anchor hit by raycast

        // If an anchor is hit by the raycast
        if (Physics.Raycast(ray, out hitAnchor, Mathf.Infinity, LayerMask.GetMask("MapAnchors")))
        {
            // If the anchor hit is not already selected
            if (anchorMarkerSelected != hitAnchor.transform.gameObject)
            {
                // If another anchor was selected before
                if (anchorMarkerSelected != null)
                {
                    // Reset color of unselected object
                    anchorMarkerSelected.GetComponent<MeshRenderer>().material.color = Color.cyan;
                }
                
                anchorMarkerSelected = hitAnchor.transform.gameObject;

                // Set color to red to show that the anchor is selected
                anchorMarkerSelected.GetComponent<MeshRenderer>().material.color = Color.red;

                controller.UpdateSelectedAnchor(anchorMarkerSelected.transform.parent.gameObject);

                // Update buttons and dropdown interactable
                //if(dropdownUsed)
                //    addARObjectButton.interactable = true;
                //placeARObjectButton.interactable = false;
                //editObjectButton.interactable = false;
                //removeObjectButton.interactable = false;
                //objectDropdown.interactable = true;

                if (mode == RaycastingMode.Objects)
                {
                    // Unselect object
                    mapObjectSelected = null;
                    //objectManager.infoText.text = "Anchor selected";

                    // TODO Use if we remove the "Add AR object" button
                    //SelectAnchorForObject();
                }
            }
        }

        RaycastHit hitObject; // AR Object hit by raycast

        // If an AR object is hit by the raycast
        if (Physics.Raycast(ray, out hitObject, Mathf.Infinity, LayerMask.GetMask("MapObjects")))
        {
            // Display on screen the name of selected object
            //objectManager.infoText.text = hitObject.transform.gameObject.name + " selected";

            // Unselect anchor
            if (anchorMarkerSelected != null)
                anchorMarkerSelected.GetComponent<MeshRenderer>().material.color = Color.cyan;
            anchorMarkerSelected = null;

            // Select AR object
            mapObjectSelected = hitObject.transform.gameObject;
            objectManager.UpdateSelectedObjects(mapObjectSelected);

            // Update buttons and dropdown interactable
            //addARObjectButton.interactable = false;
            //placeARObjectButton.interactable = false;
            //editObjectButton.interactable = true;
            //removeObjectButton.interactable = true;
            //objectDropdown.interactable = false;
        }
    }

    // Called when dropdown is used
    //public void UpdateDropdownUsed()
    //{
    //    dropdownUsed = true;
    //    if(anchorMarkerSelected != null && objectManager.mode != MapObjectsManager.SceneManagementMode.PlacingObject)
    //        //addARObjectButton.interactable = true;
    //}

    // Called by switch button to swap between [Add, Place, Edit, Remove, Save] and [Scale, Rotation, Position]
    //public void SwitchOptionPanel()
    //{
    //    buttonPanel1.SetActive(!buttonPanel1.activeSelf);
    //    buttonPanel2.SetActive(!buttonPanel2.activeSelf);
    //}

    // Called by button to display or not the planes detected by ARPlaneManager
    public void TogglePlaneVisualization()
    {
        planeVisualization = !planeVisualization;

        // Hide or display already existing planes
        foreach (var plane in m_PlaneManager.trackables)
        {
            plane.gameObject.SetActive(planeVisualization);
        }

        if (!planeVisualization)
        {
            // On new planes added, hide them directly
            m_PlaneManager.planesChanged += HidePlanesAdded;
        }
        else
        {
            m_PlaneManager.planesChanged -= HidePlanesAdded;
        }
    }

    // Called on planes changed event
    void HidePlanesAdded(ARPlanesChangedEventArgs args)
    {
        foreach (var plane in args.added)
        {
            plane.gameObject.SetActive(false);
        }
    }

    public void SetPlaneVisualization(bool value)
    {
        if(planeVisualization != value)
        {
            TogglePlaneVisualization();
        }
    }

    //************************************************************
    // Cloud Anchors Section
    //************************************************************

    // Asks Cloud Anchor Controller to create an anchor at raycast hit position on trackable
    public void PlaceAnchor()
    {
        if (useCursor)
        {
            controller.PlaceCloudAnchor(hitPlane, hitPose);
        }
    }

    // Asks Cloud Anchor Controller to remove the selected anchor
    public void RemoveSelectedAnchor()
    {
        if (anchorMarkerSelected != null)
        {
            controller.RemoveCloudAnchor(anchorMarkerSelected.transform.parent.gameObject);
        }
    }

    //************************************************************
    // AR Objects Section
    //************************************************************

    // Confirm selected anchor, starts placing object in scene
    public void SelectAnchorForObject()
    {
        if (anchorMarkerSelected != null)
        {
            //addARObjectButton.interactable = false;
            //placeARObjectButton.interactable = true;

            objectManager.AnchorSelect(anchorMarkerSelected.transform.parent.gameObject);
            // Change color of selected anchor from red to yellow (anchor selected and object being placed)
            anchorMarkerSelected.GetComponent<MeshRenderer>().material.color = Color.yellow;
        }
        else
        {
            objectManager.SetInfoText("No anchor selected, " + DateTime.Now.ToString());
        }
    }

    // Place object at his current position
    public void PlaceObject()
    {
        objectManager.PlaceObject();
        //placeARObjectButton.interactable = false;

        // If placed object is an edited object
        //if (mapObjectSelected != null)
        //{
        //    editObjectButton.interactable = true;
        //    removeObjectButton.interactable = true;
        //}
        //// If placed object is a newly added one
        //else
        //{
        //    addARObjectButton.interactable = true;
        //}

        // Change color of selected anchor from yellow to red (anchor selected but no object being placed)
        if (objectManager.mode == MapObjectsManager.SceneManagementMode.SelectingAnchor)
        {
            anchorMarkerSelected.GetComponent<MeshRenderer>().material.color = Color.red;
        }
    }

    // Put selected object in placing state (follows camera)
    public void EditSelectedObjects()
    {
        if (mapObjectSelected != null)
        {
            //placeARObjectButton.interactable = true;
            //editObjectButton.interactable = false;
            //removeObjectButton.interactable = false;
            objectManager.EditSelectedObject(mapObjectSelected);
        }
    }

    // Asks Map Objects Manager to remove the object
    public void RemoveSelectedObjects()
    {
        if (mapObjectSelected != null)
        {
            //editObjectButton.interactable = false;
            //removeObjectButton.interactable = false;
            objectManager.RemoveSelectedObject(mapObjectSelected);
        }
    }

    // Move selected AR object along X axis by a set value
    public void TranslateSelectedObjectX(float factor)
    {
        if (mapObjectSelected != null)
        {
            objectManager.TranslateSelectedObjectX(mapObjectSelected, factor);
        }
    }

    // Move selected AR object along Y axis by a set value
    public void TranslateSelectedObjectY(float factor)
    {
        if (mapObjectSelected != null)
        {
            objectManager.TranslateSelectedObjectY(mapObjectSelected, factor);
        }
    }

    // Move selected AR object along Z axis by a set value
    public void TranslateSelectedObjectZ(float factor)
    {
        if (mapObjectSelected != null)
        {
            objectManager.TranslateSelectedObjectZ(mapObjectSelected, factor);
        }
    }



    // Move selected AR object along given axis by a set value
    public void StartTranslateSelectedObject(float factor, char axis)
    {
        if (mapObjectSelected != null)
        {
            // Stop currently active coroutine
            if(currentTranslationcoroutine != null)
            {
                StopCoroutine(currentTranslationcoroutine);
                currentTranslationcoroutine = null;
            }

            currentTranslationcoroutine = ContinuousTranslation(factor, axis);
            StartCoroutine(currentTranslationcoroutine);
        }
    }

    public void StopTranslateSelectedObject(float factor, char axis)
    {
        StopCoroutine(currentTranslationcoroutine);
        currentTranslationcoroutine = null;
    }

    IEnumerator ContinuousTranslation(float factor, char axis)
    {
        while(true)
        {
            objectManager.TranslateSelectedObject(mapObjectSelected, factor, axis);
            //yield return new WaitForSeconds(0.1f); // Wait 0.1 second
            yield return null;
        }
    }

    // When Input Field values change for position X,Y or Z
    public void OnPositionInputChanged(string c)
    {
        if (mapObjectSelected != null)
        {
            objectManager.OnPositionInputChanged(mapObjectSelected, c);
        }
    }

    //************************************************************
    // Raycasting settings
    //************************************************************

    // Used by Main Menu Manager
    public void SwitchRaycastingModeToAnchorsAndSurfaces()
    {
        changeRaycastingMode(RaycastingMode.AnchorsAndSurfaces);
    }

    // Used by Main Menu Manager
    public void SwitchRaycastingModeToObjects()
    {
        changeRaycastingMode(RaycastingMode.Objects);
    }

    // Used by Main Menu Manager
    public void SwitchRaycastingModeToNone()
    {
        changeRaycastingMode(RaycastingMode.None);
    }

    // Used by switch trackable type button (Unused at the moment)
    public void SwitchTrackableType()
    {
        if (currentTrackableType == TrackableType.PlaneWithinPolygon)
        {
            currentTrackableType = TrackableType.Planes;
        }
        else
        {
            currentTrackableType = TrackableType.PlaneWithinPolygon;
        }
    }

    // Keeps coherance with Map Objects Manager modes
    public void MatchModeWithObjectsManager()
    {
        if(objectManager.mode == MapObjectsManager.SceneManagementMode.SelectingAnchor)
        {
            changeRaycastingMode(RaycastingMode.AnchorsAndSurfaces);
        }
        else if (objectManager.mode == MapObjectsManager.SceneManagementMode.SelectingObject)
        {
            changeRaycastingMode(RaycastingMode.Objects);
        }
    }

    // Unused at the moment
    public void ResetARSession()
    {
        //Debug.Log("Before: " + m_ARSession.subsystem;

        m_ARSession.Reset();

        //Debug.Log("After: " + m_ARSession.subsystem.currentConfiguration.ToString());
    }

    // Update cursor (red target) position and raycast values depending of raycasting mode, cursor use and touch control activated
    private void UpdateCursorAndRaycast()
    {
        Vector2 screenPosition = Camera.main.ViewportToScreenPoint(new Vector2(0.5f, 0.5f));
        List<ARRaycastHit> hits = new List<ARRaycastHit>();
        raycastManager.Raycast(screenPosition, hits, currentTrackableType);

        if (hits.Count > 0) // At least 1 hit with raycast
        {
            objectManager.UpdateStickToPlanePosition(hits[0].pose.position);
        }

        if (mode == RaycastingMode.AnchorsAndSurfaces)
        {
            // Raycasting on anchors
            if (!touchToSelect)
            {
                RaycastHit hit;
                if (Physics.Raycast(Camera.main.transform.position, Camera.main.transform.forward, out hit, Mathf.Infinity, LayerMask.GetMask("MapAnchors")))
                {
                    //debugText.text = "Hit! " + hit.transform.gameObject.name + ", " + hit.transform.parent.gameObject.name;
                    if (anchorMarkerSelected != hit.transform.gameObject)
                    {
                        if (anchorMarkerSelected != null)
                            anchorMarkerSelected.GetComponent<MeshRenderer>().material.color = Color.cyan; // Reset color of unselected object
                        anchorMarkerSelected = hit.transform.gameObject;
                        anchorMarkerSelected.GetComponent<MeshRenderer>().material.color = Color.red;
                    }
                }
                else
                {
                    if (anchorMarkerSelected != null)
                        anchorMarkerSelected.GetComponent<MeshRenderer>().material.color = Color.cyan; // Reset color of unselected object
                    anchorMarkerSelected = null;
                    // debugText.text = "Did not hit!";
                }
            }

            // Raycasting on AR Trackables Surfaces
            if (hits.Count > 0 && useCursor) // At least 1 hit with raycast
            {
                transform.position = hits[0].pose.position;
                transform.rotation = hits[0].pose.rotation;
                hitPose = hits[0].pose;

                var hitTrackableId = hits[0].trackableId;
                hitPlane = m_PlaneManager.GetPlane(hitTrackableId);
            }
        }
        else if (mode == RaycastingMode.Objects)
        {
            // Raycasting on AR objects
            if(!touchToSelect && useCursor)
            {
                RaycastHit hit;
                if (Physics.Raycast(Camera.main.transform.position, Camera.main.transform.forward, out hit, Mathf.Infinity, LayerMask.GetMask("MapObjects")))
                {
                    // Move cursor on objects
                    transform.position = hit.point;
                    transform.up = hit.normal; // Rotation

                    if (mapObjectSelected != hit.transform.gameObject)
                    {
                        mapObjectSelected = hit.transform.gameObject;
                        // TODO highlight ? Change info text? warn MapObject?
                    }
                }
                else
                {
                    mapObjectSelected = null;
                }
            }
            
        }
    }

    // Change raycasting mode and set some variables according to selected mode
    private void changeRaycastingMode(RaycastingMode newMode)
    {
        mode = newMode;

        switch (newMode)
        {
            case RaycastingMode.AnchorsAndSurfaces:
                cursorChildObject.SetActive(true);
                break;
            case RaycastingMode.Objects:
                cursorChildObject.SetActive(true);
                break;
            case RaycastingMode.None:
                cursorChildObject.SetActive(false);
                mapObjectSelected = null;
                //anchorMarkerSelected = null;
                break;
            default:
                Debug.Log("unknown raycasting mode");
                break;
        }
    }
}
