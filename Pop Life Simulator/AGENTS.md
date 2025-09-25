# Repository Guidelines

## Project Structure & Module Organization
Pop Life Simulator targets Unity 6000.0.46f1. Gameplay code sits in Assets/Scripts with Runtime (core simulation like FloorGrid, ConstructionManager), Manager (bootstrap/services), Data (ScriptableObjects), and UI (canvas controllers). Scenes live under Assets/Scenes, art/audio/third-party libs under Assets/ThirdParty, and configuration assets in Assets/Settings. Place new prefabs inside module-specific folders and update scene references when wiring them in.

## Build, Test, and Development Commands
Open the project with Unity Hub or Unity.exe -projectPath "Pop Life Simulator" -editorVersion 6000.0.46f1. Build Windows players through Unity.exe -projectPath "Pop Life Simulator" -quit -batchmode -buildWindows64Player "Builds/Windows/PopLife.exe". Run automated checks via Unity.exe -projectPath "Pop Life Simulator" -runTests -testResults Logs/TestResults.xml. Clean the Library/ cache before archiving or sharing builds to avoid oversized diffs.

## Coding Style & Naming Conventions
Use four-space indentation and UTF-8 source files. Name classes and ScriptableObjects with PascalCase, methods with verbs, and serialized privates as [SerializeField] private Foo fooBar;. Aggregate runtime state through properties instead of new public fields. Prefix managers with their subsystem (e.g., UIManager, AudioManager) and wrap code in namespaces that mirror folder structure such as PopLife.Runtime or PopLife.UI.

## Testing Guidelines
Add play mode tests for systems that touch floors, inventories, or currency loops. Store edit mode specs in Assets/Tests/EditMode and play mode specs in Assets/Tests/PlayMode. Name fixtures <Feature>Tests and cases Method_Scenario_ExpectedResult. Maintain coverage on ConstructionManager, FloorGrid, and resource flow managers; capture regression scenes when visual bugs surface.

## Commit & Pull Request Guidelines
Commits stay short and imperative (separate archetypes, small update) and cover one change set. Pull requests must include a summary, validation notes (editor version, commands executed), linked issue or task ID, and media for UI or placement tweaks. Request review before merging to main and wait for automated tests to succeed.