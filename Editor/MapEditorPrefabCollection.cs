using System.Collections.Generic;
using UnityEngine;

namespace EmadGH.MapEditor.Editor
{
    [UnityEngine.Scripting.APIUpdating.MovedFrom(true, "HasanKachal.Editor", "HasanKachal.Editor", "LevelEditorPrefabCollection")]
    [CreateAssetMenu(
        fileName = "New Prefab Collection",
        menuName = "Map Editor/Prefab Collection")]
    public sealed class MapEditorPrefabCollection : ScriptableObject
    {
        [SerializeField] private string category = "General";
        [SerializeField] private List<GameObject> prefabs = new List<GameObject>();

        public string DisplayName => string.IsNullOrWhiteSpace(category)
            ? name
            : $"{category}/{name}";

        public IReadOnlyList<GameObject> Prefabs => prefabs;

        public void SetPrefabs(IReadOnlyList<GameObject> source)
        {
            prefabs.Clear();
            if (source == null)
            {
                return;
            }

            for (int index = 0; index < source.Count; index++)
            {
                GameObject prefab = source[index];
                if (prefab != null && !prefabs.Contains(prefab))
                {
                    prefabs.Add(prefab);
                }
            }
        }
    }
}
