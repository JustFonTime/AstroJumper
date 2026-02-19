using UnityEngine;

public class DialougeError
{
    public Color Color { get; set; }

    public DialougeError()
    {
        GenerateRandomColor();
    }

    private void GenerateRandomColor()
    {
        Color = new Color32((byte)Random.Range(65, 256), (byte)Random.Range(50, 256), (byte)Random.Range(50, 256), 255);
    }
}
