using UnityEngine;

/// <summary>
/// 简单的第三人称跟随相机（给简化场景用，不依赖 Cinemachine）。
/// 放在 LateUpdate 里跟随，避免和角色移动打架。
/// </summary>
public class CameraFollow : MonoBehaviour
{
    public Transform target;

    [Tooltip("相对目标的偏移（世界坐标）")]
    public Vector3 offset = new Vector3(0f, 12f, -12f);

    [Tooltip("跟随平滑度，越大越跟手")]
    public float smooth = 8f;

    [Tooltip("看向目标上方多少米")]
    public float lookHeight = 1.2f;

    private void Start()
    {
        if (target == null)
        {
            PlayerController pc = FindObjectOfType<PlayerController>();
            if (pc != null) target = pc.transform;
        }

        if (target != null) SnapToTarget();
    }

    private void LateUpdate()
    {
        if (target == null) return;

        Vector3 want = target.position + offset;
        float k = 1f - Mathf.Exp(-smooth * Time.deltaTime);

        transform.position = Vector3.Lerp(transform.position, want, k);
        transform.LookAt(target.position + Vector3.up * lookHeight);
    }

    private void SnapToTarget()
    {
        transform.position = target.position + offset;
        transform.LookAt(target.position + Vector3.up * lookHeight);
    }
}
