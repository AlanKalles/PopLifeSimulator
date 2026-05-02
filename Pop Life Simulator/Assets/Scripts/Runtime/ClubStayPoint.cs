using UnityEngine;

namespace PopLife.Runtime
{
    public class ClubStayPoint : MonoBehaviour
    {
        [SerializeField] private StoreIdKind storeId = StoreIdKind.Club;
        [SerializeField] private Vector2 stayDurationRange = new(3f, 5f);

        public string StoreId => StoreIds.ToId(storeId);

        public float SampleDuration()
        {
            float min = Mathf.Min(stayDurationRange.x, stayDurationRange.y);
            float max = Mathf.Max(stayDurationRange.x, stayDurationRange.y);
            return Mathf.Max(0f, Random.Range(min, max));
        }
    }
}
