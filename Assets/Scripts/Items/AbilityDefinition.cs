using UnityEngine;

// Granted by whichever Weapon item is equipped (Ash-of-War style) — see
// PlayerCombat.TryUseAbility(), which fires an AOE burst hit using these
// values via the same windup/active-window pipeline as combo/heavy attacks
// (PlayerCombat.BeginAttack()/PerformAttack()).
[CreateAssetMenu(fileName = "NewAbility", menuName = "UnityGladiators/Ability")]
public class AbilityDefinition : ScriptableObject
{
    [SerializeField] private string abilityName;
    [SerializeField] private float cooldown = 5f;
    [SerializeField] private string animatorTrigger = "AbilityCast";

    [Header("Effect")]
    [Tooltip("A skill burst is a bigger, rarer hit than a normal swing — higher damage/stagger, wider radius, on a real cooldown.")]
    [SerializeField] private int damage = 30;
    [SerializeField] private float staggerAmount = 25f;
    [SerializeField] private float hitstunDuration = 0.3f;
    [SerializeField] private float windup = 0.25f;
    [SerializeField] private float activeDuration = 0.15f;
    [Tooltip("Independent of the weapon's normal attackRange — abilities can hit wider (or narrower) than a regular swing.")]
    [SerializeField] private float range = 2.5f;

    public string AbilityName => abilityName;
    public float Cooldown => cooldown;
    public string AnimatorTrigger => animatorTrigger;
    public int Damage => damage;
    public float StaggerAmount => staggerAmount;
    public float HitstunDuration => hitstunDuration;
    public float Windup => windup;
    public float ActiveDuration => activeDuration;
    public float Range => range;
}
