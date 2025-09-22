using UnityEngine;
using PopLife.Data;
using PopLife.Runtime;

public class PlaceButton : MonoBehaviour
{
    public ConstructionManager cm;
    public BuildingArchetype archetype;   // 拖拽你的 Vibrator(ShelfArchetype)

    public void OnClick()
    {
        cm.BeginPlace(archetype);
    }
}
