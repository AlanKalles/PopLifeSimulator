using System;
using System.Collections.Generic;
using UnityEngine;

namespace PopLife.Manager
{
    /// <summary>
    /// 根据教程 Marker 控制一组对象的可见性。
    /// 需要挂在始终保持 active 的外部对象上，才能唤醒 inactive 的目标对象。
    /// </summary>
    [DefaultExecutionOrder(-10000)]
    public class MarkerActivatedVisibility : MonoBehaviour
    {
        [Serializable]
        public class Binding
        {
            public TutorialMarker marker;
            public GameObject target;
        }

        [SerializeField] private List<Binding> bindings = new List<Binding>();

        private void Awake()
        {
            ApplyAllBindings();
        }

        private void OnEnable()
        {
            TutorialEventBus.OnMarkerTriggered += HandleMarkerTriggered;
            ApplyAllBindings();
        }

        private void OnDisable()
        {
            TutorialEventBus.OnMarkerTriggered -= HandleMarkerTriggered;
        }

        private void HandleMarkerTriggered(TutorialMarker marker)
        {
            for (int i = 0; i < bindings.Count; i++)
            {
                Binding binding = bindings[i];
                if (binding == null || binding.marker != marker)
                {
                    continue;
                }

                SetTargetActive(binding.target, true);
            }
        }

        private void ApplyAllBindings()
        {
            for (int i = 0; i < bindings.Count; i++)
            {
                Binding binding = bindings[i];
                if (binding == null)
                {
                    continue;
                }

                bool shouldBeActive = ShouldTargetBeActive(binding.target);
                SetTargetActive(binding.target, shouldBeActive);
            }
        }

        private bool ShouldTargetBeActive(GameObject target)
        {
            if (target == null)
            {
                return false;
            }

            for (int i = 0; i < bindings.Count; i++)
            {
                Binding binding = bindings[i];
                if (binding != null
                    && binding.target == target
                    && TutorialEventBus.IsMarkerTriggered(binding.marker))
                {
                    return true;
                }
            }

            return false;
        }

        private void SetTargetActive(GameObject target, bool isActive)
        {
            if (target == null)
            {
                Debug.LogWarning($"[{nameof(MarkerActivatedVisibility)}] Binding target is missing.", this);
                return;
            }

            if (target.activeSelf != isActive)
            {
                target.SetActive(isActive);
            }
        }
    }
}
