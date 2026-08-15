using UnityEngine;

// Granted by whichever Weapon item is equipped (Ash-of-War style) — see
// PlayerCombat.TryUseAbility(), which currently uses a fixed placeholder
// instead of reading this from the equipped weapon (wired in M4).
[CreateAssetMenu(fileName = "NewAbility", menuName = "UnityGladiators/Ability")]
public class AbilityDefinition : ScriptableObject
{
    [SerializeField] private string abilityName;
    [SerializeField] private float cooldown = 5f;
    [SerializeField] private string animatorTrigger = "AbilityCast";

    public string AbilityName => abilityName;
    public float Cooldown => cooldown;
    public string AnimatorTrigger => animatorTrigger;
}
