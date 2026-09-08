using UnityEngine;

// Granted by whichever Boots item is equipped (Risk of Rain shift-style) — see
// PlayerCombat.TryDash(). No boots equipped means no dash at all.
[CreateAssetMenu(fileName = "NewDash", menuName = "UnityGladiators/Dash")]
public class DashDefinition : ScriptableObject
{
    [SerializeField] private string dashName;
    [SerializeField] private float distance = 4f;
    [SerializeField] private float cooldown = 1.5f;
    [Tooltip("How long after dashing the player takes zero damage/effects at all — a real i-frame window, not just poise. A correctly-timed dash dodges even a would-be finisher.")]
    [SerializeField] private float invulnerabilityDuration = 0.2f;
    [SerializeField] private bool dealsDamage;
    [SerializeField] private int damage;

    public string DashName => dashName;
    public float Distance => distance;
    public float Cooldown => cooldown;
    public float InvulnerabilityDuration => invulnerabilityDuration;
    public bool DealsDamage => dealsDamage;
    public int Damage => damage;
}
