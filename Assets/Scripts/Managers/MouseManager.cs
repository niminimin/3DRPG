using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;


public class MouseManager : Singleton<MouseManager>
{
    public RaycastHit hitInfo;
    public event Action<Vector3> OnMouseClicked;
    public event Action<GameObject> OnEnemyClicked;

    /// <summary>点击"可打回的石头"（tag = Rock）</summary>
    public event Action<GameObject> OnRockClicked;

    /// <summary>点击传送门（tag = Portal）</summary>
    public event Action<GameObject> OnPortalClicked;

    public Texture2D point, doorway, attack, target, arrow;

    protected override void Awake()
    {
        base.Awake();       // ★ 必须调用：负责赋值 instance / 处理重复实例
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
                case "Enemy":
                case "Rock":                // 石头也能打，用同一个攻击光标
                    Cursor.SetCursor(attack, new Vector2(16, 16), CursorMode.Auto);
                    break;
                case "Portal":              // 传送门：门形光标
                    Cursor.SetCursor(doorway, new Vector2(16, 16), CursorMode.Auto);
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
            else if (hitInfo.collider.gameObject.CompareTag("Enemy"))
            {
                OnEnemyClicked?.Invoke(hitInfo.collider.gameObject);
            }
            else if (hitInfo.collider.gameObject.CompareTag("Rock"))
            {
                OnRockClicked?.Invoke(hitInfo.collider.gameObject);
            }
            else if (hitInfo.collider.gameObject.CompareTag("Portal"))
            {
                OnPortalClicked?.Invoke(hitInfo.collider.gameObject);
            }
        } 
    }
}
