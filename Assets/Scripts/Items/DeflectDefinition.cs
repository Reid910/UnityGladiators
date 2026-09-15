using UnityEngine;

// Granted by whichever Gloves item is equipped — see
// PlayerCombat.TryDefendAgainst(). No gloves equipped still means the
// player can Block (see PlayerCombat's Deflect/Block header — Block is a
// universal ability, not gated by gear), just not the perfect-timing
// Deflect tier. See docs/combat-redesign-plan.md.
[CreateAssetMenu(fileName = "NewDeflect", menuName = "UnityGladiators/Deflect")]
public class DeflectDefinition : ScriptableObject
{
    [SerializeField] private string deflectName;
    [Tooltip("Overrides PlayerCombat's default Deflect Window when this pair of gloves is equipped — different gloves could make timing more/less forgiving later.")]
    [SerializeField] private float deflectWindow = 0.5f;

    public string DeflectName => deflectName;
    public float DeflectWindow => deflectWindow;
}
