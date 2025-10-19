using UnityEngine;
using TMPro;

public class BuildingClickHandler : MonoBehaviour
{
    public TMP_Text infoText;

    void Update()
    {
        Vector3 worldPos = Camera.main.ScreenToWorldPoint(Input.mousePosition);
        worldPos.z = 0;

        if (Input.GetMouseButton(0)) // held down
        {
            RaycastHit2D hit = Physics2D.Raycast(worldPos, Vector2.zero);
            if (hit.collider != null)
            {
                BuildingInfo info = hit.collider.GetComponent<BuildingInfo>();
                if (info != null)
                {
                    infoText.text = $"Date: {info.builtDate}\nSpent On: {info.spentOn}\nSpent: £{info.spentAmount}";
                    return; // stop here so it doesn't clear text instantly
                }
            }
        }

        // clear if not holding or not hovering a valid building
        infoText.text = "";
    }
}