using System.Collections;
using UnityEngine;

public class BossVisual : MonoBehaviour
{
    [Header("Material Settings")]
    [SerializeField] private Material aliveMaterial;
    [SerializeField] private Material deadMaterial;

    [Header("Damage Blink Settings")]
    [SerializeField] private Material blinkMaterial;
    [SerializeField] private float blinkDuration = 0.1f;
    [SerializeField] private int blinkCount = 3;

    [Header("Particle")]
    [SerializeField] private GameObject bossAttackParticle;
    [SerializeField] private Transform attackOnePoint;
    [SerializeField] private Transform attackTwoPoint;
    [SerializeField] private Transform attackThreePoint;
    [SerializeField] private Transform attackFourPoint;

    private Renderer[] bossRenderers;
    private Material[][] originalMaterials;
    private bool isBlinking = false;

    #region Unity Methods
    private void Awake()
    {
        InitializeComponents();
        StoreMaterials();
    }

    private void Start()
    {
        SetAliveMaterial();
    }
    #endregion

    #region Initialization
    private void InitializeComponents()
    {
        bossRenderers = GetComponentsInChildren<Renderer>();
    }

    private void StoreMaterials()
    {
        if (bossRenderers == null || bossRenderers.Length == 0) return;

        originalMaterials = new Material[bossRenderers.Length][];

        for (int i = 0; i < bossRenderers.Length; i++)
        {
            if (bossRenderers[i] != null)
            {
                originalMaterials[i] = bossRenderers[i].materials;
            }
        }
    }

    private void SetAliveMaterial()
    {
        if (aliveMaterial != null)
        {
            ApplyMaterialToAll(aliveMaterial);
        }
    }
    #endregion

    #region Damage Blink System
    [ContextMenu("PlayDamageBlink")]
    public void PlayDamageBlink()
    {
        if (isBlinking) return;

        StartCoroutine(DamageBlinkSequence());
    }

    private IEnumerator DamageBlinkSequence()
    {
        isBlinking = true;

        for (int i = 0; i < blinkCount; i++)
        {
            ApplyBlinkMaterial(true);

            yield return new WaitForSeconds(blinkDuration);

            RestoreOriginalMaterials();

            yield return new WaitForSeconds(blinkDuration);
        }

        isBlinking = false;
    }

    private void ApplyBlinkMaterial(bool useBlink)
    {
        if (bossRenderers == null || blinkMaterial == null) return;

        for (int i = 0; i < bossRenderers.Length; i++)
        {
            if (bossRenderers[i] != null)
            {
                if (useBlink)
                {
                    Material[] blinkMaterials = new Material[originalMaterials[i].Length];
                    for (int j = 0; j < blinkMaterials.Length; j++)
                    {
                        blinkMaterials[j] = blinkMaterial;
                    }
                    bossRenderers[i].materials = blinkMaterials;
                }
                else
                {
                    if (originalMaterials[i] != null)
                    {
                        bossRenderers[i].materials = originalMaterials[i];
                    }
                }
            }
        }
    }

    private void RestoreOriginalMaterials()
    {
        if (bossRenderers == null || originalMaterials == null) return;

        for (int i = 0; i < bossRenderers.Length; i++)
        {
            if (bossRenderers[i] != null && originalMaterials[i] != null)
            {
                bossRenderers[i].materials = originalMaterials[i];
            }
        }
    }
    #endregion

    #region Death Material System
    [ContextMenu("SwitchToDeadMaterial")]
    public void SwitchToDeadMaterial()
    {
        if (deadMaterial != null)
        {
            ApplyMaterialToAll(deadMaterial);

            StoreMaterials();
        }
    }

    private void ApplyMaterialToAll(Material material)
    {
        if (bossRenderers == null) return;

        for (int i = 0; i < bossRenderers.Length; i++)
        {
            if (bossRenderers[i] != null)
            {
                Material[] materials = new Material[bossRenderers[i].materials.Length];
                for (int j = 0; j < materials.Length; j++)
                {
                    materials[j] = material;
                }
                bossRenderers[i].materials = materials;
            }
        }
    }
    #endregion

    #region Boss Attack Particle
    public void AttackOne()
    {
        GameObject particle = Instantiate(bossAttackParticle, attackOnePoint.position, Quaternion.identity);

        Destroy(particle, 2f);
    }

    public void AttackTwo()
    {
        GameObject particle = Instantiate(bossAttackParticle, attackTwoPoint.position, Quaternion.identity);

        Destroy(particle, 2f);
    }

    public void AttackThree()
    {
        GameObject particle = Instantiate(bossAttackParticle, attackThreePoint.position, Quaternion.identity);

        Destroy(particle, 2f);
    }

    public void AttackFour()
    {
        GameObject particle = Instantiate(bossAttackParticle, attackFourPoint.position, Quaternion.identity);

        Destroy(particle, 2f);
    }
    #endregion

    private void OnDrawGizmos()
    {
        Gizmos.color = Color.green;
        Gizmos.DrawWireSphere(attackOnePoint.position, 0.2f);
        Gizmos.DrawWireSphere(attackTwoPoint.position, 0.2f);
        Gizmos.DrawWireSphere(attackThreePoint.position, 0.2f);
        Gizmos.DrawWireSphere(attackFourPoint.position, 0.2f);
    }
}
