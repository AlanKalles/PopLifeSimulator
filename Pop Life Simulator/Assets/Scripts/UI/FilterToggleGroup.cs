using System.Collections.Generic;
using UnityEngine;

namespace PopLife.UI
{
    /// <summary>
    /// Manages a group of IFilterButton instances with single-selection behavior
    /// Supports both SelectPageButton (Alpha Hit Test) and FilterToggleButton (traditional Button)
    /// Ensures only one button is selected at a time
    /// </summary>
    public class FilterToggleGroup : MonoBehaviour
    {
        [Header("Settings")]
        [SerializeField] private bool allowNoneSelected = false; // If false, always have one selected

        private List<IFilterButton> toggleButtons = new List<IFilterButton>();
        private IFilterButton currentlySelected;

        /// <summary>
        /// Event fired when selection changes
        /// Parameter is the selected FilterValue (can be null for "All")
        /// </summary>
        public System.Action<object> OnSelectionChanged;

        /// <summary>
        /// Register a toggle button to this group
        /// </summary>
        public void RegisterToggle(IFilterButton toggle)
        {
            if (toggle == null || toggleButtons.Contains(toggle))
                return;

            toggleButtons.Add(toggle);

            // If this is the first button and we don't allow none selected, select it
            if (toggleButtons.Count == 1 && !allowNoneSelected)
            {
                SelectToggle(toggle, notifyChange: false);
            }
        }

        /// <summary>
        /// Unregister a toggle button from this group
        /// </summary>
        public void UnregisterToggle(IFilterButton toggle)
        {
            if (toggle == null) return;

            toggleButtons.Remove(toggle);

            if (currentlySelected == toggle)
            {
                currentlySelected = null;

                // Select first available if we don't allow none
                if (!allowNoneSelected && toggleButtons.Count > 0)
                {
                    SelectToggle(toggleButtons[0], notifyChange: true);
                }
            }
        }

        /// <summary>
        /// Handle toggle button click
        /// </summary>
        public void OnToggleClicked(IFilterButton clickedToggle)
        {
            if (clickedToggle == null) return;

            // If already selected and we allow none, deselect
            if (clickedToggle == currentlySelected && allowNoneSelected)
            {
                currentlySelected.SetSelected(false);
                currentlySelected = null;
                OnSelectionChanged?.Invoke(null);
                return;
            }

            // Select the clicked toggle
            SelectToggle(clickedToggle, notifyChange: true);
        }

        /// <summary>
        /// Programmatically select a toggle
        /// </summary>
        private void SelectToggle(IFilterButton toggle, bool notifyChange)
        {
            if (toggle == null) return;

            // Deselect previous
            if (currentlySelected != null)
            {
                currentlySelected.SetSelected(false);
            }

            // Select new
            currentlySelected = toggle;
            currentlySelected.SetSelected(true);

            // Notify listeners
            if (notifyChange)
            {
                OnSelectionChanged?.Invoke(currentlySelected.FilterValue);
            }
        }

        /// <summary>
        /// Select toggle by filter value (useful for initialization)
        /// </summary>
        public void SelectByValue(object filterValue)
        {
            IFilterButton targetToggle = toggleButtons.Find(t =>
                (t.FilterValue == null && filterValue == null) ||
                (t.FilterValue != null && t.FilterValue.Equals(filterValue))
            );

            if (targetToggle != null)
            {
                SelectToggle(targetToggle, notifyChange: true);
            }
        }

        /// <summary>
        /// Select the "All" button (FilterValue == null)
        /// </summary>
        public void SelectAll()
        {
            SelectByValue(null);
        }

        /// <summary>
        /// Get currently selected filter value
        /// </summary>
        public object GetSelectedValue()
        {
            return currentlySelected?.FilterValue;
        }

        /// <summary>
        /// Clear all toggles (useful when destroying the group)
        /// </summary>
        public void Clear()
        {
            foreach (var toggle in toggleButtons)
            {
                if (toggle != null)
                {
                    toggle.SetSelected(false);
                }
            }

            toggleButtons.Clear();
            currentlySelected = null;
        }
    }
}
