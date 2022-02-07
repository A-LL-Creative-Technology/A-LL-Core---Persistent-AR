using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class APIRequestsManager : MonoBehaviour
{
    public string apiEndpoint;
    public string apiToken; // Used as basic authentication token to add security

#if USE_ALL_CORE_UTILS
    public APIController apiController; // Using GetInstance() in Start() sometimes returns null. Drag and Drop in Unity causes no problem
#endif
    private Dictionary<string, string> parameters; // Contains header params

    // Start is called before the first frame update
    void Start()
    {
        parameters = new Dictionary<string, string>();
        parameters.Add("APIToken", apiToken);
    }

    // Update is called once per frame
    void Update()
    {
        
    }

    // Get the list of all maps
    public void GetAllMaps(System.Action<CloudAnchorMapCollection> callback)
    {
#if USE_ALL_CORE_UTILS
        if(apiController == null)
        {
            Debug.Log("apiController is null");
        }
        apiController.Get<CloudAnchorMapCollection>("allMaps", parameters, (CloudAnchorMapCollection mapCollection) =>
        {
            //Debug.Log("existing maps: " + existingMaps.Collection[0].ToString());
            if (mapCollection == null)
            {
                Debug.Log("GetAllMaps returned null");
            }
            else
            {
                Debug.Log("GetAllMaps result: " + mapCollection.Collection[0].ToString());
            }

            callback?.Invoke(mapCollection);
        }, null, null, null, false);
#else
        Debug.LogError("APIController missing, please install A-LL Core Utils to use this functionnality !");
#endif
    }

    public void CreateMap(CloudAnchorMap map, System.Action callback)
    {
#if USE_ALL_CORE_UTILS
        apiController.Post<CloudAnchorMap, string>(map, "createMap", parameters, (string successMessage) =>
        {
            callback?.Invoke();
        }, null, null, null, false);
#else
        Debug.LogError("APIController missing, please install A-LL Core Utils to use this functionnality !");
#endif
    }

    public void EditMap(CloudAnchorMap map, System.Action callback)
    {
#if USE_ALL_CORE_UTILS
        apiController.Post<CloudAnchorMap, string>(map, "editMap", parameters, (string successMessage) =>
        {
            callback?.Invoke();
        }, null, null, null, false);
#else
        Debug.LogError("APIController missing, please install A-LL Core Utils to use this functionnality !");
#endif
    }

    public void EditARObjects(CloudAnchorMap map, System.Action callback)
    {
#if USE_ALL_CORE_UTILS
        apiController.Post<CloudAnchorMap, string>(map, "editArObjects", parameters, (string successMessage) =>
        {
            callback?.Invoke();
        }, null, null, null, false);
#else
        Debug.LogError("APIController missing, please install A-LL Core Utils to use this functionnality !");
#endif
    }
}
