using System;
using UnityEngine;

public class Stagger : MonoBehaviour
{
    public event Action Broken;

    [Header("Stagger")]
    [SerializeField] private float maxStagger = 100f;
    [Tooltip("Passive drain per second, always active — a slow constant recovery rather than a burst-safe grace window after the last hit.")]
    [SerializeField] private float decayPerSecond = 5f;
    [SerializeField] private float brokenDuration = 2f;
    [Tooltip("Scales how much of this Stagger meter a hit fills, relative to how much of this character's own max health that hit's damage represents. 1 = a hit dealing X% of max health fills X% of Stagger. Lower = harder to stagger relative to damage taken (e.g. a tankier enemy); higher = easier.")]
    [SerializeField] private float damageToStaggerMultiplier = 1f;

    [Header("References")]
    [Tooltip("Optional. Drives a 'Broken' bool each frame so a broken player/enemy visibly plays StunnedLoop (or whatever else the Controller has wired to it) instead of it being invisible except for the HUD.")]
    [SerializeField] private Animator animator;

    private float currentStagger;
    private float brokenUntilTime;

    public float CurrentStagger => currentStagger;
    public float MaxStagger => maxStagger;
    public bool IsBroken => Time.time < brokenUntilTime;

    private void Awake()
    {
        if (animator == null)
        {
            animator = GetComponentInChildren<Animator>();
        }
    }

    private void Update()
    {
        if (animator != null)
        {
            animator.SetBool("Broken", IsBroken);
        }

        if (IsBroken)
        {
            return;
        }

        currentStagger = Mathf.Max(0f, currentStagger - decayPerSecond * Time.deltaTime);
    }

    // Fills the meter by an amount proportional to how much of this
    // character's own max health the incoming damage represents, rather
    // than a flat hand-tuned number per move — a hit worth 10% of max
    // health fills 10% of Stagger (before damageToStaggerMultiplier). This
    // means a weapon/ability's damage number is the only thing that needs
    // tuning; stagger impact follows automatically and scales sensibly
    // across targets with very different health pools.
    public void AddStaggerFromDamage(int damage, int targetMaxHealth)
    {
        if (targetMaxHealth <= 0)
        {
            return;
        }

        float fractionOfHealth = (float)damage / targetMaxHealth;
        AddStagger(fractionOfHealth * maxStagger * damageToStaggerMultiplier);
    }

    public void AddStagger(float amount)
    {
        if (IsBroken)
        {
            return;
        }

        currentStagger += amount;

        if (currentStagger >= maxStagger)
        {
            currentStagger = 0f;
            brokenUntilTime = Time.time + brokenDuration;
            Broken?.Invoke();
        }
    }
}
