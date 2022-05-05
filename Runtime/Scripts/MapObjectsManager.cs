using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using TMPro;
using UnityEngine.UI;

public class MapObjectsManager : MonoBehaviour
{
    public List<GameObject> availableGameobjectsList;
    //public TextMeshProUGUI infoText;
    //public TMP_Dropdown dropdown; // Dropdown of all available AR objects that can be placed
    //public TextMeshProUGUI dropdownPlaceholder;
    public CloudAnchorController cloudAnchorController; // used to save map objects

    //public Slider rotationSlider;
    //public TMP_InputField rotationInputField;

    //public Slider scaleSlider;
    //public TMP_InputField scaleInputField;

    //public TMP_InputField xInputField;
    //public TMP_InputField yInputField;
    //public TMP_InputField zInputField;

    private GameObject selectedObject;
    private GameObject placingOrMovingObject; // AR object currently being placed or moved
    private GameObject anchorObjectSelected; // Used to attach GameObject to anchor
    private float placementDist; // Distance in front of the user where the object will be placed (old system, now using raycasting)
    private Dictionary<GameObject, List<GameObjectWithPrefabName>> dictAnchorGameObjectList;

    private float previousRotationValue; // Used with rotation slider

    private bool stickToPlane; // if true, raycasting to plane for placement. if false, use placementDist
    private Vector3 stickToPlanePosition;

    // The current application mode.
    [HideInInspector]
    public SceneManagementMode mode;

    public enum SceneManagementMode
    {
        // Selecting anchor to attach the new object
        SelectingAnchor,

        // Placing the object
        PlacingObject,

        // Selecting object to edit or remove
        SelectingObject,

        // Moving selected object
        MovingObject,

        // Saving the scene to database
        Saving,

        // Error mode
        Error
    }


    //************************************************************
    // Setup methods
    //************************************************************


    // Start is called before the first frame update
    void Start()
    {
        placementDist = 1.5f;

        dictAnchorGameObjectList = new Dictionary<GameObject, List<GameObjectWithPrefabName>>();

        stickToPlane = true;

        stickToPlanePosition = new Vector3();
    }

    // Update is called once per frame
    void Update()
    {
        // If we are placing or moving a AR object, we need to update the position
        if(mode == SceneManagementMode.PlacingObject || mode == SceneManagementMode.MovingObject)
        {
            UpdatePlacingOrMovingObjectPosition();
        }
    }

    public void OnEnable()
    {
        Debug.Log("MapObjectManager enabled");

        //var options = new List<TMP_Dropdown.OptionData>();

        //if (availableGameobjectsList.Count > 0)
        //{
        //    dropdown.onValueChanged.AddListener(delegate
        //    {
        //        OnResolvingSelectionChanged();
        //    });

        //    foreach (var data in availableGameobjectsList)
        //    {
        //        options.Add(new TMP_Dropdown.OptionData(
        //            data.name));
        //    }
        //}
        //else
        //{
        //    options.Add(new TMP_Dropdown.OptionData("No object list found"));
        //    changeSceneMode(SceneManagementMode.Error);
        //}

        //dropdown.options = options;
        //dropdown.placeholder = dropdownPlaceholder; // Set a placeholder to tell the user to select a AR object to place

        //dropdown.SetValueWithoutNotify(-1);

        //changeSceneMode(SceneManagementMode.SelectingAnchor);
    }

    public void OnDisable()
    {
        //dropdown.onValueChanged.RemoveListener(delegate
        //{
        //    OnResolvingSelectionChanged();
        //});

        //dropdown.ClearOptions();
    }

    public void Reset()
    {
        placementDist = 1.5f;

        dictAnchorGameObjectList = new Dictionary<GameObject, List<GameObjectWithPrefabName>>();

        placingOrMovingObject = null;
        selectedObject = null;
        anchorObjectSelected = null;

        previousRotationValue = 0f;
        //rotationSlider.value = 0f;
        //rotationInputField.text = "0";

        //scaleSlider.value = 1f;
        //scaleInputField.text = "1";

        changeSceneMode(SceneManagementMode.SelectingAnchor);
    }

    public void OnResolvingSelectionChanged()
    {
        if (placingOrMovingObject != null)
        {
            GameObject.Destroy(placingOrMovingObject);
        }

        if(mode == SceneManagementMode.PlacingObject)
        {
            //placingOrMovingObject = Instantiate(availableGameobjectsList[dropdown.value]);
        }

        resetSlidersAndInputFields();
    }


    //************************************************************
    // Variables setters
    //************************************************************


    public void SetInfoText(string txt)
    {
        //infoText.text = txt;
    }


    public void SwitchStickToPlane()
    {
        stickToPlane = !stickToPlane;
    }

    public void UpdateStickToPlanePosition(Vector3 pos)
    {
        stickToPlanePosition = cloneVector3(pos);
    }


    // Called by AR Cursor to set anchor selected
    public void AnchorSelect(GameObject anchorSelected)
    {
        anchorObjectSelected = anchorSelected;
        selectedObject = null; // Unselect object

        changeSceneMode(SceneManagementMode.PlacingObject);

        OnResolvingSelectionChanged();
    }

    // Called by UI
    public void ValidateAnchorSelected()
    {
        // TODO unused? delete?

        changeSceneMode(SceneManagementMode.PlacingObject);

        OnResolvingSelectionChanged();
    }

    public void UpdateSelectedObjects(GameObject objectSelected)
    {
        selectedObject = objectSelected;

        resetSlidersAndInputFields();
    }

    public void SaveScene()
    {
        cloudAnchorController.SaveCloudAnchorMapWithObjectsCollection(dictAnchorGameObjectList);
    }


    //************************************************************
    // AR Objects operations
    //************************************************************


    private void UpdatePlacingOrMovingObjectPosition()
    {
        if(stickToPlane)
        {
            placingOrMovingObject.transform.position = stickToPlanePosition;
        }
        else
        {
            placingOrMovingObject.transform.position = Camera.main.transform.position + Camera.main.transform.forward * placementDist;
        }

        // TODO check distance with selected anchor and display message if too far (> 8 meters)
    }

    public void PlaceObject()
    {
        if (availableGameobjectsList.Count > 0)
        {
            if (mode == SceneManagementMode.PlacingObject)
            {
                // If it is the first object attached to this anchor
                if (!dictAnchorGameObjectList.ContainsKey(anchorObjectSelected))
                {

                    dictAnchorGameObjectList.Add(anchorObjectSelected, new List<GameObjectWithPrefabName>());
                }

                //dictAnchorGameObjectList[anchorObjectSelected].Add(new GameObjectWithPrefabName(availableGameobjectsList[dropdown.value].name, placingOrMovingObject));

                // Next item ready to be placed
                placingOrMovingObject.transform.SetParent(anchorObjectSelected.transform);
                placingOrMovingObject = null;

                changeSceneMode(SceneManagementMode.SelectingAnchor);
            }
            else if (mode == SceneManagementMode.MovingObject)
            {
                // No need to update list cause it contains a reference to the object (so already up to date)

                placingOrMovingObject = null;

                changeSceneMode(SceneManagementMode.SelectingObject);
            }

            resetSlidersAndInputFields();
        }
        else
        {
            //infoText.text = "No objects available...";
        }

    }

    public void MoveObject()
    {
        placingOrMovingObject = null;

        changeSceneMode(SceneManagementMode.SelectingObject);
    }

    public void SwitchSelectingMode()
    {
        if (mode == SceneManagementMode.SelectingAnchor || mode == SceneManagementMode.PlacingObject)
        {
            changeSceneMode(SceneManagementMode.SelectingObject);
        }
        else if (mode == SceneManagementMode.SelectingObject)
        {
            changeSceneMode(SceneManagementMode.SelectingAnchor);
        }
    }

    public void EditSelectedObject(GameObject selected)
    {
        placingOrMovingObject = selected; // TODO use selecteObject instead of passing selected again?
        // TODO reset selectedAnchor? Assign selectedAnchor to selected anchor? change color (require shader)?

        Debug.Log("Parent of selected is: " + selected.transform.parent.gameObject.name);

        changeSceneMode(SceneManagementMode.MovingObject);

    }

    public void RemoveSelectedObject(GameObject selected)
    {
        GameObject.Destroy(selected);

        // Objects aren't deleted from dictAnchorGameObjectList. Null objects will be managed during saving
    }

    public void TranslateSelectedObjectX(GameObject selected, float factor)
    {
        selected.transform.localPosition = new Vector3(selected.transform.localPosition.x + 0.1f * factor, selected.transform.localPosition.y, selected.transform.localPosition.z);
        //xInputField.text = "" + selected.transform.localPosition.x;
    }

    public void TranslateSelectedObjectY(GameObject selected, float factor)
    {
        selected.transform.localPosition = new Vector3(selected.transform.localPosition.x, selected.transform.localPosition.y + 0.1f * factor, selected.transform.localPosition.z);
        //yInputField.text = "" + selected.transform.localPosition.y;
    }

    public void TranslateSelectedObjectZ(GameObject selected, float factor)
    {
        selected.transform.localPosition = new Vector3(selected.transform.localPosition.x, selected.transform.localPosition.y, selected.transform.localPosition.z + 0.1f * factor);
        //zInputField.text = "" + selected.transform.localPosition.z;
    }


    public void TranslateSelectedObject(GameObject selected, float factor, char axis)
    {
        if(axis == 'x')
        {
            selected.transform.localPosition = new Vector3(selected.transform.localPosition.x + 0.0025f * factor, selected.transform.localPosition.y, selected.transform.localPosition.z);
            //xInputField.text = "" + selected.transform.localPosition.x;
        }
        else if (axis == 'y')
        {
            selected.transform.localPosition = new Vector3(selected.transform.localPosition.x, selected.transform.localPosition.y + 0.0025f * factor, selected.transform.localPosition.z);
            //yInputField.text = "" + selected.transform.localPosition.y;
        }
        else
        {
            selected.transform.localPosition = new Vector3(selected.transform.localPosition.x, selected.transform.localPosition.y, selected.transform.localPosition.z + 0.0025f * factor);
            //zInputField.text = "" + selected.transform.localPosition.z;
        }
    }

    public void OnPositionInputChanged(GameObject selected, string c)
    {
        if (c == "x")
        {
            //selected.transform.localPosition = new Vector3(float.Parse(xInputField.text), selected.transform.localPosition.y, selected.transform.localPosition.z);
        }
        else if (c == "y")
        {
            //selected.transform.localPosition = new Vector3(selected.transform.localPosition.x, float.Parse(yInputField.text), selected.transform.localPosition.z);
        }
        else
        {
            //selected.transform.localPosition = new Vector3(selected.transform.localPosition.x, selected.transform.localPosition.y, float.Parse(zInputField.text));
        }
    }


    //************************************************************
    // Sliders and options values
    //************************************************************


    public void UpdateRotationValue(float value)
    {
        Debug.Log("rotation slider value: " + value);
        if(placingOrMovingObject != null)
        {
            float delta = value - previousRotationValue;
            placingOrMovingObject.transform.Rotate(-Vector3.up * delta);
        }
        else if(selectedObject != null)
        {
            float delta = value - previousRotationValue;
            selectedObject.transform.Rotate(-Vector3.up * delta);
        }

        previousRotationValue = value;
        //rotationInputField.text = "" + value;
    }

    public void UpdateRotationInput(string value)
    {
        if (float.Parse(value) < 0)
        {
            //rotationInputField.text = "0";
        }
        else if (float.Parse(value) > 360)
        {
            //rotationInputField.text = "360";
        }

        if (placingOrMovingObject != null)
        {
            float delta = float.Parse(value) - previousRotationValue;
            placingOrMovingObject.transform.Rotate(Vector3.up * delta);
        }
        else if (selectedObject != null)
        {
            float delta = float.Parse(value) - previousRotationValue;
            selectedObject.transform.Rotate(Vector3.up * delta);
        }

        previousRotationValue = float.Parse(value);
        //rotationSlider.value = float.Parse(value);
    }



    public void UpdateScalingValue(float value)
    {
        Debug.Log("scaling slider value: " + value);
        if (placingOrMovingObject != null)
        {
            Vector3 newScale = new Vector3(value, value, value);
            placingOrMovingObject.transform.localScale = newScale;
        }
        else if (selectedObject != null)
        {
            Vector3 newScale = new Vector3(value, value, value);
            selectedObject.transform.localScale = newScale;
        }

        //scaleInputField.text = "" + value;
    }

    public void UpdateScaleInput(string value)
    {
        if (float.Parse(value) < 0.01)
        {
            //scaleInputField.text = "0.01";
        }
        else if (float.Parse(value) > 5)
        {
            //scaleInputField.text = "5";
        }

        if (placingOrMovingObject != null)
        {
            Vector3 newScale = new Vector3(float.Parse(value), float.Parse(value), float.Parse(value));
            placingOrMovingObject.transform.localScale = newScale;
        }
        else if(selectedObject != null)
        {
            Vector3 newScale = new Vector3(float.Parse(value), float.Parse(value), float.Parse(value));
            selectedObject.transform.localScale = newScale;
        }

        //scaleSlider.value = float.Parse(value);
    }



    public void UpdateDistanceValue(float value)
    {
        placementDist = value;
    }

    public void InstantiatePrefabOnAnchor(GameObject anchorMarker, string prefabName, SerializableVector3 position, SerializableQuaternion rotation, SerializableVector3 scale)
    {
        Debug.Log("In InstantiatePrefabOnAnchor");
        var obj = Instantiate(Resources.Load<GameObject>("Persistent/" + prefabName), anchorMarker.transform); // TODO remove parent?
        // var obj = Instantiate(availableGameobjectsList.Find(x => x.name == prefabName), anchorMarker.transform); // TODO remove parent?
        obj.transform.localPosition = position;
        obj.transform.localRotation = rotation;
        obj.transform.localScale = scale;

        // If it is the first object attached to this anchor
        if (!dictAnchorGameObjectList.ContainsKey(anchorMarker))
        {

            dictAnchorGameObjectList.Add(anchorMarker, new List<GameObjectWithPrefabName>());
        }

        //dictAnchorGameObjectList[anchorMarker].Add(new GameObjectWithPrefabName(prefabName, obj));
    }

    private void changeSceneMode(SceneManagementMode newMode)
    {
        mode = newMode;

        //switch (newMode)
        //{
        //    case SceneManagementMode.SelectingAnchor:
        //        infoText.text = "Select anchor";
        //        break;
        //    case SceneManagementMode.PlacingObject:
        //        resetSlidersAndInputFields();
        //        infoText.text = "Place object";
        //        break;
        //    case SceneManagementMode.SelectingObject:
        //        infoText.text = "Select object to edit or remove";
        //        break;
        //    case SceneManagementMode.MovingObject:
        //        infoText.text = "Moving selected object";
        //        resetSlidersAndInputFields();
        //        break;
        //    case SceneManagementMode.Saving:
        //        // TODO
        //        infoText.text = "Saving";
        //        break;
        //    case SceneManagementMode.Error:
        //        infoText.text = "An error happend";
        //        break;
        //    default:
        //        infoText.text = "Unknown mode";
        //        break;
        //}
    }

    private void resetSlidersAndInputFields()
    {
        if (placingOrMovingObject != null)
        {
            float updatedRotation = placingOrMovingObject.transform.localRotation.eulerAngles.y;

            previousRotationValue = updatedRotation;
            //rotationSlider.value = updatedRotation;
            //rotationInputField.text = "" + updatedRotation;

            //scaleSlider.value = placingOrMovingObject.transform.localScale.x;
            //scaleInputField.text = "" + placingOrMovingObject.transform.localScale.x;

            //xInputField.text = "" + placingOrMovingObject.transform.localPosition.x;
            //yInputField.text = "" + placingOrMovingObject.transform.localPosition.y;
            //zInputField.text = "" + placingOrMovingObject.transform.localPosition.z;
        }
        else if(selectedObject != null)
        {
            float updatedRotation = selectedObject.transform.localRotation.eulerAngles.y;

            previousRotationValue = updatedRotation;
            //rotationSlider.value = updatedRotation;
            //rotationInputField.text = "" + updatedRotation;

            //scaleSlider.value = selectedObject.transform.localScale.x;
            //scaleInputField.text = "" + selectedObject.transform.localScale.x;

            //Debug.Log("local pos x: " + selectedObject.transform.localPosition.x.ToString());
            //Debug.Log("input field x: " + xInputField.text);

            //xInputField.text = "" + selectedObject.transform.localPosition.x;
            //yInputField.text = "" + selectedObject.transform.localPosition.y;
            //zInputField.text = "" + selectedObject.transform.localPosition.z;
        }
        else
        {
            // Reset rotation slider
            previousRotationValue = 0f;
            //rotationSlider.value = 0f;
            //rotationInputField.text = "0";

            //// Reset scale slider
            //scaleSlider.value = 1f;
            //scaleInputField.text = "1";
        }

    }


    private Vector3 cloneVector3(Vector3 vector)
    {
        return new Vector3(vector.x, vector.y, vector.z);
    }
}


public struct GameObjectWithPrefabName
{
    public string prefabName; // Cloud Anchor ID used for resolving
    public GameObject gameObject; // Creation time of anchor. Used to find expired anchors

    public GameObjectWithPrefabName(string prefabName, GameObject gameObject)
    {
        this.prefabName = prefabName;
        this.gameObject = gameObject;
    }
}