using UnityEngine;

public class UIGameAudioHandler : MonoBehaviour
{
    public static UIGameAudioHandler Instance { get; private set; }

    [Header("UI Sounds")]
    [SerializeField] private AudioSource deleteBlockSound;
    [SerializeField] private AudioSource placeBlockSound;
    [SerializeField] private AudioSource clickSound;
    [SerializeField] private AudioSource dragSound;
    [SerializeField] private AudioSource runSound;

    private void Awake()
    {
        Instance = this;
    }

    public void PlayDragSound()
    {
        if (dragSound != null)
        {
            dragSound.Play();
        }
    }

    public void PlayRunSound()
    {
        if (runSound != null)
        {
            runSound.Play();
        }
    }

    public void PlayClickSound()
    {
        if (clickSound != null)
        {
            clickSound.Play();
        }
    }

    public void PlayDeleteBlockSound()
    {
        if (deleteBlockSound != null)
        {
            deleteBlockSound.Play();
        }
    }

    public void PlayPlaceBlockSound()
    {
        if (placeBlockSound != null)
        {
            placeBlockSound.Play();
        }
    }

}