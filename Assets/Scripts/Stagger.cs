using System;
using UnityEngine;

public class Stagger : MonoBehaviour
{
    public event Action Broken;

    [Header("Stagger")]
    [SerializeField] private float maxStagger = 100f;
    [SerializeField] private float decayPerSecond = 15f;
    [SerializeField] private float decayDelayAfterHit = 1.5f;
    [SerializeField] private float brokenDuration = 2f;

    private float currentStagger;
    private float lastHitTime;
    private float brokenUntilTime;

    public float CurrentStagger => currentStagger;
    public float MaxStagger => maxStagger;
    public bool IsBroken => Time.time < brokenUntilTime;

    private void Update()
    {
        if (IsBroken || Time.time - lastHitTime < decayDelayAfterHit)
        {
            return;
        }

        currentStagger = Mathf.Max(0f, currentStagger - decayPerSecond * Time.deltaTime);
    }

    public void AddStagger(float amount)
    {
        if (IsBroken)
        {
            return;
        }

        lastHitTime = Time.time;
        currentStagger += amount;

        if (currentStagger >= maxStagger)
        {
            currentStagger = 0f;
            brokenUntilTime = Time.time + brokenDuration;
            Broken?.Invoke();
        }
    }
}
