namespace PopLife.DialogueBridge.UI
{
    /// <summary>
    /// Tooltip position relative to Spotlight
    /// </summary>
    public enum TooltipPosition
    {
        /// <summary>
        /// Automatically choose the best position based on available screen space
        /// </summary>
        Auto,

        /// <summary>
        /// Position to the left of the Spotlight
        /// </summary>
        Left,

        /// <summary>
        /// Position to the right of the Spotlight
        /// </summary>
        Right,

        /// <summary>
        /// Position above the Spotlight
        /// </summary>
        Top,

        /// <summary>
        /// Position below the Spotlight
        /// </summary>
        Bottom,

        /// <summary>
        /// Custom position using normalized screen coordinates (0-1)
        /// </summary>
        Custom
    }

    /// <summary>
    /// Direction the tooltip arrow should point
    /// </summary>
    public enum ArrowDirection
    {
        None,
        Left,
        Right,
        Up,
        Down
    }
}
