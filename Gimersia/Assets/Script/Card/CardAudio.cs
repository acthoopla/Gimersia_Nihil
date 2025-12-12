using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class CardAudio : MonoBehaviour
{
    [Header("Audio Settings")]
    public AudioSource useAudio;
    public AudioSource hoverAudio;

    public void PlayUseAudio()
    {
        useAudio.Play();
    }

    public void PlayHoverAudio()
    {
        hoverAudio.Play();
    }
}
