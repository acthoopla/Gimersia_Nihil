using UnityEngine;
using System.Collections;
using System;

public class BossAnimation : MonoBehaviour
{
    private Animator animator;

    private BossVisual bossVisual;

    private void Start()
    {
        if (animator == null)
            animator = GetComponentInChildren<Animator>();

        if (bossVisual == null)
            bossVisual = GetComponentInChildren<BossVisual>();
    }

    private void PlayAnimation(string animationName, bool useTransition = true, float transitionDuration = 0.1f)
    {
        if (animator == null) return;

        try
        {
            if (useTransition)
            {
                animator.CrossFade(animationName, transitionDuration);
            }
            else
            {
                animator.Play(animationName, -1, 0f);
            }
        }
        catch (Exception e)
        {
            Debug.LogError($"Error playing animation {animationName}: {e.Message}");
        }
    }

    private void PlayTrigger(string triggerName)
    {
        if (animator == null) return;

        try
        {
            animator.SetTrigger(triggerName);
        }
        catch (Exception e)
        {
            Debug.LogError($"Error playing trigger {triggerName}: {e.Message}");
        }
    }

    #region State Animations
    [ContextMenu("Idle")]
    public void PlayIdle()
    {
        PlayAnimation("Idle");
    }

    [ContextMenu("Attack_1")]
    public void PlayAttackOne()
    {
        // PlayAnimation("Attack_1");
        StartCoroutine(PlayInteractionWithReturn("Attack_1", "Idle"));
    }

    [ContextMenu("Attack_2")]
    public void PlayAttackTwo()
    {
        // PlayAnimation("Attack_2");
        StartCoroutine(PlayInteractionWithReturn("Attack_2", "Idle"));
    }

    [ContextMenu("Attack_3")]
    public void PlayAttackThree()
    {
        // PlayAnimation("Attack_3");
        StartCoroutine(PlayInteractionWithReturn("Attack_3", "Idle"));
    }

    [ContextMenu("Attack_4")]
    public void PlayAttackFour()
    {
        // PlayAnimation("Attack_4");
        StartCoroutine(PlayInteractionWithReturn("Attack_4", "Idle"));
    }

    [ContextMenu("Death")]
    public void PlayDeath()
    {
        bossVisual.SwitchToDeadMaterial();
        PlayAnimation("Death");
    }
    #endregion

    #region Animation Sequencing
    private IEnumerator PlayInteractionWithReturn(string interactionAnim, string returnAnim)
    {
        PlayAnimation(interactionAnim, false);

        yield return new WaitForSeconds(GetAnimationLength(interactionAnim));

        PlayAnimation(returnAnim);
    }
    #endregion

    #region Utility Methods
    public float GetAnimationLength(string animationName)
    {
        if (animator == null) return 1f;

        AnimationClip[] clips = animator.runtimeAnimatorController.animationClips;
        foreach (AnimationClip clip in clips)
        {
            if (clip.name == animationName)
            {
                return clip.length;
            }
        }
        return 1f;
    }

    public bool IsAnimationPlaying(string animationName)
    {
        if (animator == null) return false;

        AnimatorStateInfo stateInfo = animator.GetCurrentAnimatorStateInfo(0);
        return stateInfo.IsName(animationName);
    }

    public bool IsAnyInteractionPlaying()
    {
        return IsAnimationPlaying("Idle") ||
               IsAnimationPlaying("Attack_1") ||
               IsAnimationPlaying("Attack_2") ||
               IsAnimationPlaying("Attack_3") ||
               IsAnimationPlaying("Attack_4") ||
               IsAnimationPlaying("Death");
    }

    public void SetAnimationSpeed(float speed)
    {
        if (animator != null)
        {
            animator.speed = speed;
        }
    }

    public void PreviewAnimation(string animationName)
    {
        if (Application.isEditor)
        {
            PlayAnimation(animationName, false);
        }
    }
    #endregion
}
