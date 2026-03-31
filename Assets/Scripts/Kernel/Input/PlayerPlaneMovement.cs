using Kernel;
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// 使用 PlayerControls 的 Movement/MovVector2 输入，在世界 XZ 平面移动玩家。
/// </summary>
[DisallowMultipleComponent]
public sealed class PlayerPlaneMovement : MonoBehaviour
{
    private const float AcceleratedSpeedMultiplier = 1.5f;

    [SerializeField, Min(0f)] private float moveSpeed = 5f;
    [SerializeField] private Rigidbody targetRigidbody;

    private void Awake()
    {
        if (targetRigidbody == null)
        {
            targetRigidbody = GetComponent<Rigidbody>();
        }
    }

    /// <summary>
    /// summary: 无 Rigidbody 时，按帧直接修改 Transform 的 XZ，保持 Y 不变。
    /// param: 无
    /// returns: 无
    /// </summary>
    private void Update()
    {
        if (targetRigidbody != null)
        {
            return;
        }

        Vector3 delta = GetMovementDelta(Time.deltaTime);
        Vector3 position = transform.position;
        position.x += delta.x;
        position.z += delta.z;
        transform.position = position;
    }

    /// <summary>
    /// summary: 有 Rigidbody 时，在 FixedUpdate 中推进玩家。
    /// param: 无
    /// returns: 无
    /// </summary>
    private void FixedUpdate()
    {
        if (targetRigidbody == null)
        {
            return;
        }

        if (targetRigidbody.isKinematic)
        {
            targetRigidbody.MovePosition(targetRigidbody.position + GetMovementDelta(Time.fixedDeltaTime));
            return;
        }

        ApplyDynamicRigidbodyVelocity();
    }

    /// <summary>
    /// summary: 对动态 Rigidbody 直接写入平面速度，让碰撞阻挡由物理系统自然处理。
    /// param: 无
    /// returns: 无
    /// </summary>
    private void ApplyDynamicRigidbodyVelocity()
    {
        Vector3 currentVelocity = targetRigidbody.linearVelocity;
        Vector3 desiredVelocity = GetMovementVelocity();
        currentVelocity.x = desiredVelocity.x;
        currentVelocity.z = desiredVelocity.z;
        targetRigidbody.linearVelocity = currentVelocity;
    }

    /// <summary>
    /// summary: 把输入系统的二维向量转换成世界 XZ 平面的平面速度。
    /// param: 无
    /// returns: 当前期望的世界平面速度
    /// </summary>
    private Vector3 GetMovementVelocity()
    {
        Vector2 input = ReadMoveInput();
        input = Vector2.ClampMagnitude(input, 1f);

        float currentMoveSpeed = moveSpeed * (IsAccelerating() ? AcceleratedSpeedMultiplier : 1f);
        return new Vector3(input.x, 0f, input.y) * currentMoveSpeed;
    }

    /// <summary>
    /// summary: 根据目标平面速度和时间步长计算本帧位移。
    /// param: deltaTime 本次移动使用的时间步长
    /// returns: 本帧或本物理帧应当累加的世界位移
    /// </summary>
    private Vector3 GetMovementDelta(float deltaTime)
    {
        return GetMovementVelocity() * deltaTime;
    }

    /// <summary>
    /// summary: 从 InputActionManager 读取 PlayerControls 的移动输入。
    /// param: 无
    /// returns: 当前移动输入；当输入系统未准备好时返回零向量
    /// </summary>
    private static Vector2 ReadMoveInput()
    {
        var inputManager = InputActionManager.Instance;
        if (inputManager == null || !inputManager.IsInitialized || inputManager.IsUnloaded || inputManager.Player == null)
        {
            return Vector2.zero;
        }

        return inputManager.Player.Movement.Move.ReadValue<Vector2>();
    }

    /// <summary>
    /// summary: 从 InputActionManager 读取 PlayerControls 的加速按钮状态。
    /// param: 无
    /// returns: 加速按钮当前按下时返回 true
    /// </summary>
    private static bool IsAccelerating()
    {
        var inputManager = InputActionManager.Instance;
        if (inputManager == null || !inputManager.IsInitialized || inputManager.IsUnloaded || inputManager.Player == null)
        {
            return false;
        }

        return inputManager.Player.Movement.Accelerate.IsPressed();
    }
}
