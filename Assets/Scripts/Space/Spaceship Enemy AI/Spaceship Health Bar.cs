using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class SpaceshipHealthBar : MonoBehaviour
{
    private RectTransform rectTransform;
    private Transform _cam;

    [SerializeField] private SpaceshipHealthComponent healthComponent;
    [SerializeField] private Image fillImage;
    [SerializeField] private bool faceCamera = true;
    [SerializeField] private float durationOfFlash = 1.5f;


    void Awake()
    {
        rectTransform = GetComponent<RectTransform>();
        if (Camera.main) _cam = Camera.main.transform;
        if (healthComponent)
        {
            healthComponent.HealthChanged += SetHealth;
        }

        rectTransform.localScale = Vector3.zero;
    }


    void LateUpdate()
    {
        if (faceCamera && _cam)
            transform.forward = _cam.forward; // billboard
    }

    public void SetHealth(int health, int maxHealth)
    {
        StopAllCoroutines();

        rectTransform.localScale =
            new Vector3(1f, 1f, 1f); // reset scale to show the bar (in case it was hidden by flash)

        float percent = (float)health / maxHealth;
        float current = Mathf.Clamp01(percent);
        var rt = fillImage.rectTransform;
        rt.localScale = new Vector3(current, 1f, 1f);


        StartCoroutine(FlashBar());
    }

    IEnumerator FlashBar()
    {
        yield return new WaitForSeconds(durationOfFlash);
        rectTransform.localScale = Vector3.zero;
    }
}