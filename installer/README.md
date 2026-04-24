# Installer Build (WiX)

Dieses Verzeichnis enthält die WiX-5-Installerprojekte:

- `VortexModLists.Installer.Msi`: MSI mit den App-Dateien
- `VortexModLists.Installer.Bundle`: Bootstrapper-EXE mit .NET Desktop Runtime Prerequisite

## Erwartete Build-Reihenfolge

1. App framework-dependent veröffentlichen nach `publish/win-x64`
2. MSI bauen
3. Bundle bauen

Das Bundle lädt die .NET Desktop Runtime 10 (x64) bei Bedarf dynamisch herunter und installiert danach die MSI.
