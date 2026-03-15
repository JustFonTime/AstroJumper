using UnityEngine;

public class RandomSoundPlayer : MonoBehaviour
{
   public AudioClip[] audioClips; // Array to hold your audio clips
   private AudioSource audioSource;

   void Start()
   {
      // Get the AudioSource component attached to this GameObject
      audioSource = GetComponent<AudioSource>();
      PlayRandomSound();
      Debug.Log("Played sound");
   }

   // Call this function to play a random sound
   public void PlayRandomSound()
   {
      if (audioClips.Length == 0) return; // Prevents errors if no clips are assigned

      // Choose a random index
      int randomIndex = Random.Range(0, audioClips.Length); // Random.Range for ints is max exclusive

      // Play the selected clip using PlayOneShot to avoid interrupting existing sounds
      audioSource.PlayOneShot(audioClips[randomIndex]);
   }
}