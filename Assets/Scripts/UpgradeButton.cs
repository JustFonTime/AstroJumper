using Unity.VisualScripting;
using UnityEngine;

public class UpgradeButton : MonoBehaviour
{
    public void PrintToConsole()
    {
        print($"Player purchased {this.name}.");
    }    
}
