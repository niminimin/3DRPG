using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;


public class MouseManager : MonoBehaviour
{
    public static MouseManager instance;
    public RaycastHit hitInfo;
    public event Action<Vector3> OnMouseClicked;

    public Texture2D point, doorway, attack, target, arrow;

    private void Awake()
    {
        if(instance != null)
        {
            Destroy(gameObject);
        }
        else
        {
            instance = this;
        }
    }

    private void Update()
    {
        SetCursorTexture();
        MouseControl();
    }

    public void SetCursorTexture()
    {
        Ray ray = Camera.main.ScreenPointToRay(Input.mousePosition);

        if(Physics.Raycast(ray, out hitInfo))
        {
            //TODO 切换鼠标
            switch (hitInfo.collider.tag)
            {
                case "Ground":
                    Cursor.SetCursor(target, new Vector2(16, 16), CursorMode.Auto);
                    break;
            }
        }
    }

    public void MouseControl()
    {
        if(Input.GetMouseButtonDown(0) && hitInfo.collider != null)
        {
            if (hitInfo.collider.gameObject.CompareTag("Ground"))
            {
                OnMouseClicked?.Invoke(hitInfo.point);
            }
        } 
    }
}
