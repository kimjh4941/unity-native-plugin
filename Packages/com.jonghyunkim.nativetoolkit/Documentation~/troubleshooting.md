# Troubleshooting

## Localization shows empty

- Ensure SelectedLocale is set and `InitializationOperation.IsDone`.
- Verify keys exist in tables and the correct table name is used.

## Editor scripts not compiling

- Confirm Editor scripts are under Editor/ and in `NativeToolkit.Editor` asmdef.
- Ensure dependencies in asmdef: Unity.Localization, Unity.Localization.Editor, Unity.Addressables, Unity.ResourceManager.

## Android/iOS build errors

- Check native Plugins import settings (platform filters).
- Clean Addressables build if resource GUID/layout changed.
