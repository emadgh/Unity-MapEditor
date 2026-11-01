using System;
using UnityEditor;
using UnityEngine;

namespace EmadGH.MapEditor.Editor
{
    internal sealed class MapEditorSceneInteraction
    {
        public bool TryGetPlacementPosition(
            Vector2 guiPosition,
            Predicate<Transform> shouldIgnore,
            out Vector3 position,
            out Vector3 normal)
        {
            Ray ray = HandleUtility.GUIPointToWorldRay(guiPosition);
            Physics.SyncTransforms();
            RaycastHit[] hits = Physics.RaycastAll(
                ray,
                Mathf.Infinity,
                Physics.DefaultRaycastLayers,
                QueryTriggerInteraction.Ignore);
            Array.Sort(hits, (left, right) => left.distance.CompareTo(right.distance));

            for (int index = 0; index < hits.Length; index++)
            {
                RaycastHit hit = hits[index];
                if (shouldIgnore != null && shouldIgnore(hit.collider.transform))
                {
                    continue;
                }

                position = hit.point;
                normal = hit.normal.sqrMagnitude > 0.0001f
                    ? hit.normal.normalized
                    : Vector3.up;
                return true;
            }

            Plane groundPlane = new Plane(Vector3.up, Vector3.zero);
            if (groundPlane.Raycast(ray, out float distance) && distance >= 0f)
            {
                position = ray.GetPoint(distance);
                normal = Vector3.up;
                return true;
            }

            position = default;
            normal = Vector3.up;
            return false;
        }

        public static bool CaptureSceneInput(Event currentEvent)
        {
            if (currentEvent.type != EventType.Layout)
            {
                return false;
            }

            HandleUtility.AddDefaultControl(GUIUtility.GetControlID(FocusType.Passive));
            return true;
        }

        public static bool IsPrimaryClick(Event currentEvent)
        {
            return currentEvent.type == EventType.MouseDown &&
                   currentEvent.button == 0 &&
                   !currentEvent.alt;
        }

        public static bool IsPointerEvent(Event currentEvent)
        {
            return currentEvent.type == EventType.MouseMove ||
                   currentEvent.type == EventType.MouseDown ||
                   currentEvent.type == EventType.MouseDrag ||
                   currentEvent.type == EventType.MouseUp;
        }
    }
}
