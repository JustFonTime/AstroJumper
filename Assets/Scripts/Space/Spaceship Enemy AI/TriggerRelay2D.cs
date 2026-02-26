using System;
using UnityEngine;

public class TriggerRelay2D : MonoBehaviour
{
    public event Action<Collider2D> Enter;
    public event Action<Collider2D> Exit;

    private void OnTriggerEnter2D(Collider2D other) => Enter?.Invoke(other);
    private void OnTriggerExit2D(Collider2D other)  => Exit?.Invoke(other);
}