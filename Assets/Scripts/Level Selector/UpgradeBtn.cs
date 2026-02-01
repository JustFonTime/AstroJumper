using UnityEngine;

public class UpgradeBtn : MonoBehaviour
{
     [SerializeField] GameObject upgradeUIPrefab;
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        
    }

    // Update is called once per frame
    void Update()
    {
        
    }

    public void onClick()
    {
        Vector3 parentTransform = new Vector3(0, 0, 0);
        GameObject UI = Instantiate(upgradeUIPrefab, parentTransform, Quaternion.identity);
        
        print(GameObject.FindGameObjectWithTag("Canvas").transform);

        UI.GetComponent<RectTransform>().SetParent(GameObject.FindGameObjectWithTag("Canvas").transform, false);
    }
}
