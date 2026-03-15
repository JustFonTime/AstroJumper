using UnityEngine;
public class FootstepSound : MonoBehaviour
{
   public AudioSource audioSource;
   public AudioClip[] footstepClips; // Add multiple sounds

   // Called by animation event
   public void PlayFootstep()
   {
      AudioClip clip = footstepClips[Random.Range(0, footstepClips.Length)];
      audioSource.PlayOneShot(clip);
   }
}