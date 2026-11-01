# Map Editor

Standalone Unity 6 package. No game project, third-party packages, art assets, or render pipeline is required. Supply your own prefabs/models and materials.

## Install

In Unity: Window > Package Manager > + > Install package from git URL:

```
https://github.com/emadgh/Unity-MapEditor.git#v1.0.0
```

Use the URL without the version fragment to follow the default branch. Requires Git and Unity 6000.0 or newer.

## Use

Open **Tools > Map Editor**. Add prefabs/models by dragging them into the asset list or using Add Selection. Enable the tool and click a collider surface in Scene view.

- **Add to Scene:** single placement or brush strokes; manual, random, or sequential prefab selection; surface alignment, pivot selection, rotation and scale variation.
- **Physics Simulation:** place and settle batches, or simulate selected objects. Confirm Batch keeps results; Cancel Simulation restores the captured transforms. Uses Unity 3D physics and colliders.
- **Clotheslines:** click at least two scene points, choose optional pole/hanging prefabs and generate. Edit the resulting ClotheslineGenerator with its scene handles. Assign a wire material appropriate for your render pipeline, especially for player builds.
- **Collections:** create reusable assets with Assets > Create > Map Editor > Prefab Collection, or save from the window. Save collections under Assets in the consuming project.
- **Assets > Add to Scene:** adds selected prefab/model assets in front of the Scene camera.

Shortcuts: Shift + wheel rotates; Ctrl + wheel changes height; [ / ] cycles prefabs; Esc disables the active tool.

## Package structure

Editor contains the Editor-only assembly and UI Toolkit layout/styles. Runtime contains the clothesline component needed by generated scenes and player builds. UI assets load from the package path, including Git/PackageCache installations. No project-specific assets are included.

## Migration from the original Level Editor

Back up the project first. Remove the old LevelEditor scripts and original ClotheslineGenerator before installing to avoid duplicate menu commands and inspectors. Script meta GUIDs are preserved. The generator also declares its former namespace/assembly for API migration. Verify existing collections and generated clotheslines after migration; old namespace-based code references must be updated to EmadGH.MapEditor (runtime) or EmadGH.MapEditor.Editor.

## Validation

Compiled and smoke-tested in an empty Unity 6000.7.0b6 project with built-in modules only: package UI loaded from Packages, prefab collection creation succeeded, and ClotheslineGenerator generation completed. Interactive brush/physics workflows and player builds have not been exhaustively tested.

