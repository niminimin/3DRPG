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

    /// <summary>
    /// 离开游戏场景（回主菜单）时清掉自定义光标。
    /// Cursor.SetCursor 是全局状态，MouseManager 被销毁后不会自动恢复，
    /// 不清的话主菜单会一直显示游戏里的"攻击/移动"图标。
    /// </summary>
    private void OnDisable()
    {
        Cursor.SetCursor(null, Vector2.zero, CursorMode.Auto);
        Cursor.visible = true;
        Cursor.lockState = CursorLockMode.None;
    }

    private void Update()
    {
        // 暂停（timeScale = 0）时不处理鼠标：菜单要能用鼠标点按钮
        if (Time.timeScale <= 0f) return;

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
