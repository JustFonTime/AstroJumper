using UnityEngine;
public class JumpAudio : MonoBehaviour
{
   public AudioSource audioSource;
   public AudioClip[] jumpAudio; // Add multiple sounds

   // Called by animation event
   public void PlayJumpsound()
   {
      AudioClip clip = jumpAudio[Random.Range(0, jumpAudio.Length)];
      audioSource.PlayOneShot(clip);
   }
}