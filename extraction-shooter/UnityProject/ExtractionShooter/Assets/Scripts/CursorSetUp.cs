using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class CursorSetUp : MonoBehaviour
{
    public Texture2D cursor;
    // Start is called before the first frame update
    void Start()
    {
        Cursor.SetCursor(cursor,new Vector2(-2f,-2f),CursorMode.ForceSoftware);
    }

    // Update is called once per frame
    void Update()
    {
        
    }
}
