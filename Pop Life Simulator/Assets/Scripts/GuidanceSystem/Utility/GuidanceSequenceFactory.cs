using UnityEngine;

namespace PopLife.GuidanceSystem.Utility
{
    /// <summary>
    /// 工厂类，用于创建预定义的指引序列
    /// Factory class for creating predefined guidance sequences
    /// </summary>
    public static class GuidanceSequenceFactory
    {
        /// <summary>
        /// 根据ID创建指引序列
        /// Create guidance sequence by ID
        /// </summary>
        public static GuidanceSequence CreateSequence(string sequenceID)
        {
            switch (sequenceID)
            {
                case "GUIDE_BUILD_FIRST_SHELF":
                    return CreateBuildFirstShelfGuidance();

                case "GUIDE_OPEN_STORE":
                    return CreateOpenStoreGuidance();

                case "GUIDE_UPGRADE_SHELF":
                    return CreateUpgradeShelfGuidance();

                case "GUIDE_PLACE_CASHIER":
                    return CreatePlaceCashierGuidance();

                default:
                    Debug.LogWarning($"[GuidanceFactory] Unknown sequence ID: {sequenceID}");
                    return null;
            }
        }

        /// <summary>
        /// 创建建造第一个货架的指引
        /// Create guidance for building first shelf
        /// </summary>
        private static GuidanceSequence CreateBuildFirstShelfGuidance()
        {
            var sequence = new GuidanceSequence("GUIDE_BUILD_FIRST_SHELF", "Build Your First Shelf");

            // Step 1: Click construction button
            var step1 = new GuidanceStep("STEP_1", "Click the Construction button to enter build mode",
                ActionType.ClickTarget);
            // Note: The actual target UI element needs to be set at runtime
            // step1.SetTargetUI(constructionButton);
            sequence.AddStep(step1);

            // Step 2: Select a shelf from the menu
            var step2 = new GuidanceStep("STEP_2", "Select a shelf from the building menu",
                ActionType.ClickTarget);
            sequence.AddStep(step2);

            // Step 3: Place the shelf on the floor
            var step3 = new GuidanceStep("STEP_3", "Click on an empty floor space to place the shelf",
                ActionType.ClickAnywhere);
            sequence.AddStep(step3);

            // Step 4: Exit build mode
            var step4 = new GuidanceStep("STEP_4", "Press ESC or click the X button to exit build mode",
                ActionType.KeyPress);
            sequence.AddStep(step4);

            return sequence;
        }

        /// <summary>
        /// 创建开店指引
        /// Create guidance for opening store
        /// </summary>
        private static GuidanceSequence CreateOpenStoreGuidance()
        {
            var sequence = new GuidanceSequence("GUIDE_OPEN_STORE", "Open Your Store");

            var step1 = new GuidanceStep("STEP_1", "Click the 'Open Store' button to start business!",
                ActionType.ClickTarget);
            sequence.AddStep(step1);

            var step2 = new GuidanceStep("STEP_2", "Watch as customers enter your store and make purchases",
                ActionType.Wait);
            sequence.AddStep(step2);

            return sequence;
        }

        /// <summary>
        /// 创建升级货架指引
        /// Create guidance for upgrading shelf
        /// </summary>
        private static GuidanceSequence CreateUpgradeShelfGuidance()
        {
            var sequence = new GuidanceSequence("GUIDE_UPGRADE_SHELF", "Upgrade Your Shelf");

            var step1 = new GuidanceStep("STEP_1", "Double-click on a shelf to see its details",
                ActionType.ClickTarget);
            sequence.AddStep(step1);

            var step2 = new GuidanceStep("STEP_2", "Click the 'Upgrade' button to improve the shelf",
                ActionType.ClickTarget);
            sequence.AddStep(step2);

            var step3 = new GuidanceStep("STEP_3", "Confirm the upgrade. It will cost Fame points!",
                ActionType.ClickTarget);
            sequence.AddStep(step3);

            return sequence;
        }

        /// <summary>
        /// 创建放置收银台指引
        /// Create guidance for placing cashier
        /// </summary>
        private static GuidanceSequence CreatePlaceCashierGuidance()
        {
            var sequence = new GuidanceSequence("GUIDE_PLACE_CASHIER", "Place a Cashier Counter");

            var step1 = new GuidanceStep("STEP_1", "Enter construction mode",
                ActionType.ClickTarget);
            sequence.AddStep(step1);

            var step2 = new GuidanceStep("STEP_2", "Select 'Facilities' tab",
                ActionType.ClickTarget);
            sequence.AddStep(step2);

            var step3 = new GuidanceStep("STEP_3", "Choose the Cashier Counter",
                ActionType.ClickTarget);
            sequence.AddStep(step3);

            var step4 = new GuidanceStep("STEP_4", "Place it near a wall (cashiers must be against walls)",
                ActionType.ClickAnywhere);
            sequence.AddStep(step4);

            return sequence;
        }

        /// <summary>
        /// 设置指引序列的运行时目标
        /// Set runtime targets for guidance sequence
        /// 注意：这个方法需要在运行时调用，传入实际的UI元素
        /// Note: This method needs to be called at runtime with actual UI elements
        /// </summary>
        public static void SetRuntimeTargets(GuidanceSequence sequence, params object[] targets)
        {
            // This is a placeholder for setting runtime targets
            // Implementation would depend on your specific UI setup
            Debug.Log($"[GuidanceFactory] Setting runtime targets for sequence: {sequence.SequenceID}");
        }
    }
}