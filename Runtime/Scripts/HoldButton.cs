using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using UnityEngine.Events;


[System.Serializable]
public class HoldButton : Button
{
    [SerializeField]  public float factor;
    [SerializeField]  public ARCursor arCursor;
    [SerializeField]  public char axis;


    // Start is called before the first frame update
    void Start()
    {

    }

    // Update is called once per frame
    void Update()
    {
        
    }

    public override void OnPointerDown(PointerEventData eventData)
    {
        base.OnPointerDown(eventData);
        Debug.Log("Pressed");

        arCursor.StartTranslateSelectedObject(factor, axis);
            
    }

    public override void OnPointerUp(PointerEventData eventData)
    {
        base.OnPointerUp(eventData);
        Debug.Log("Released");

        arCursor.StopTranslateSelectedObject(factor, axis);
    }
}

