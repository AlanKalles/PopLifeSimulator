using UnityEngine;
using PopLife.Data;

namespace PopLife.Runtime
{
    public class ConstructionManager : MonoBehaviour
    {
        public enum Mode { None, Place, Move }

        [Header("状态")]
        public Mode mode = Mode.None;
        public BuildingArchetype selectedArchetype;
        public BuildingInstance selectedInstance;

        [Header("预览")]
        public GameObject previewPrefab;
        private GameObject preview;
        private SpriteRenderer previewSR;
        private int previewRot; // 0/1/2/3

        [Header("引用")]
        public FloorManager floorManager;        // 需由你项目提供：GetActiveFloor(), GetFloor(int)
        public BlueprintManager blueprintManager;// 需由你项目提供
        public ResourceManager resourceManager;  // 需由你项目提供

        void Update()
        {
            if (mode == Mode.Place) { UpdatePlacePreview(); HandlePlaceInput(); }
            else if (mode == Mode.Move) { UpdateMovePreview(); HandleMoveInput(); }
        }

        // —— 放置模式 ——
        public void BeginPlace(BuildingArchetype arch)
        {
            // 资源校验
            if (arch.requiresBlueprint && !blueprintManager.HasBlueprint(arch.archetypeId))
            {
                UIManager.Instance.ShowMessage("需要蓝图");
                return;
            }
            if (!resourceManager.CanAfford(arch.buildCost, 0))
            {
                UIManager.Instance.ShowMessage("资金不足");
                return;
            }

            selectedArchetype = arch;
            previewRot = 0;
            mode = Mode.Place;
            CreatePreview(arch);
        }

        private void CreatePreview(BuildingArchetype arch)
        {
            if (preview) Destroy(preview);
            preview = Instantiate(previewPrefab);
            previewSR = preview.GetComponent<SpriteRenderer>();

            var srcSR = arch.prefab.GetComponent<SpriteRenderer>();
            if (srcSR && previewSR) previewSR.sprite = srcSR.sprite;

            if (previewSR) { var c = previewSR.color; previewSR.color = new Color(c.r, c.g, c.b, 0.5f); }
        }

        private void UpdatePlacePreview()
        {
            if (!preview) return;
            var floor = floorManager.GetActiveFloor();
            var mouse = Camera.main.ScreenToWorldPoint(Input.mousePosition); mouse.z = 0;
            var gridPos = floor.WorldToGrid(mouse);

            preview.transform.SetPositionAndRotation(floor.GridToWorld(gridPos), Quaternion.Euler(0, 0, previewRot * 90));

            bool ok = floor.CanPlaceFootprint(selectedArchetype.GetRotatedFootprint(previewRot), gridPos)
                      && selectedArchetype.ValidatePlacement(floor, gridPos, previewRot);

            if (previewSR) previewSR.color = ok ? new Color(0, 1, 0, 0.5f) : new Color(1, 0, 0, 0.5f);
        }

        private void HandlePlaceInput()
        {
            if (Input.GetKeyDown(KeyCode.R) && selectedArchetype.canRotate)
                previewRot = (previewRot + 1) % 4;

            if (Input.GetMouseButtonDown(0))
            {
                var floor = floorManager.GetActiveFloor();
                var mouse = Camera.main.ScreenToWorldPoint(Input.mousePosition); mouse.z = 0;
                var gp = floor.WorldToGrid(mouse);

                var inst = floor.PlaceBuildingTransactional(selectedArchetype, gp, previewRot);
                if (inst)
                {
                    AudioManager.Instance.PlaySound("BuildingPlaced");
                    if (!Input.GetKey(KeyCode.LeftShift)) Cancel();
                }
                else UIManager.Instance.ShowMessage("放置失败");
            }

            if (Input.GetMouseButtonDown(1) || Input.GetKeyDown(KeyCode.Escape)) Cancel();
        }

        // —— 移动模式 ——
        public void BeginMove(BuildingInstance bi)
        {
            selectedInstance = bi;
            previewRot = bi.rotation;
            mode = Mode.Move;
            CreatePreview(bi.archetype);
        }

        private void UpdateMovePreview()
        {
            if (!preview || !selectedInstance) return;
            var floor = floorManager.GetFloor(selectedInstance.floorId);
            var mouse = Camera.main.ScreenToWorldPoint(Input.mousePosition); mouse.z = 0;
            var gp = floor.WorldToGrid(mouse);

            preview.transform.SetPositionAndRotation(floor.GridToWorld(gp), Quaternion.Euler(0, 0, previewRot * 90));

            bool ok = floor.CanPlaceFootprintAllowSelf(selectedInstance.archetype.GetRotatedFootprint(previewRot), gp, selectedInstance.instanceId);
            if (previewSR) previewSR.color = ok ? new Color(0, 1, 0, 0.5f) : new Color(1, 0, 0, 0.5f);
        }

        private void HandleMoveInput()
        {
            if (Input.GetKeyDown(KeyCode.R) && selectedInstance.archetype.canRotate)
                previewRot = (previewRot + 1) % 4;

            if (Input.GetMouseButtonDown(0))
            {
                var floor = floorManager.GetFloor(selectedInstance.floorId);
                var mouse = Camera.main.ScreenToWorldPoint(Input.mousePosition); mouse.z = 0;
                var gp = floor.WorldToGrid(mouse);

                if (floor.MoveBuilding(selectedInstance, gp, previewRot))
                {
                    AudioManager.Instance.PlaySound("BuildingMoved");
                    Cancel();
                }
                else UIManager.Instance.ShowMessage("移动失败");
            }

            if (Input.GetMouseButtonDown(1) || Input.GetKeyDown(KeyCode.Escape)) Cancel();
        }

        public void DestroyBuilding(BuildingInstance bi)
        {
            var floor = floorManager.GetFloor(bi.floorId);
            floor.RemoveBuilding(bi, refundBlueprint: true);
            Destroy(bi.gameObject);
            AudioManager.Instance.PlaySound("BuildingDestroyed");
        }

        public void Cancel()
        {
            mode = Mode.None;
            selectedArchetype = null;
            selectedInstance = null;
            if (preview) Destroy(preview);
        }
    }
}
