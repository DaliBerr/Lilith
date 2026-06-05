using UnityEngine;

/// <summary>
/// Provides a small absorb layer that can be shared by players and enemies before health damage is applied.
/// </summary>
[DisallowMultipleComponent]
public sealed class DamageShieldController : MonoBehaviour
{
    [SerializeField, Min(0f)] private float currentShield;
    [SerializeField, Min(0f)] private float remainingDuration;

    public float CurrentShield => currentShield;
    public float RemainingDuration => remainingDuration;
    public bool HasShield => currentShield > 0f && remainingDuration > 0f;

    public static DamageShieldController GetOrAdd(Component target)
    {
        if (target == null)
        {
            return null;
        }

        return GetOrAdd(target.transform);
    }

    public static DamageShieldController GetOrAdd(Transform targetRoot)
    {
        if (targetRoot == null)
        {
            return null;
        }

        if (!targetRoot.TryGetComponent(out DamageShieldController shield))
        {
            shield = targetRoot.gameObject.AddComponent<DamageShieldController>();
        }

        return shield;
    }

    public void AddShield(float amount, float duration)
    {
        if (amount <= 0f || duration <= 0f)
        {
            return;
        }

        currentShield = Mathf.Max(0f, currentShield) + amount;
        remainingDuration = Mathf.Max(remainingDuration, duration);
    }

    public bool TryAbsorbDamage(float incomingDamage, out float remainingDamage, out float absorbedDamage)
    {
        remainingDamage = Mathf.Max(0f, incomingDamage);
        absorbedDamage = 0f;
        if (remainingDamage <= 0f || !HasShield)
        {
            return false;
        }

        absorbedDamage = Mathf.Min(currentShield, remainingDamage);
        currentShield = Mathf.Max(0f, currentShield - absorbedDamage);
        remainingDamage = Mathf.Max(0f, remainingDamage - absorbedDamage);
        if (currentShield <= 0f)
        {
            remainingDuration = 0f;
        }

        return absorbedDamage > 0f;
    }

    private void Update()
    {
        if (remainingDuration <= 0f)
        {
            currentShield = 0f;
            return;
        }

        remainingDuration = Mathf.Max(0f, remainingDuration - Time.deltaTime);
        if (remainingDuration <= 0f)
        {
            currentShield = 0f;
        }
    }

    private void OnValidate()
    {
        currentShield = Mathf.Max(0f, currentShield);
        remainingDuration = Mathf.Max(0f, remainingDuration);
    }
}
