using System;
using System.Collections.Generic;
using UnityEngine;

namespace Monochrome.Core.Transition
{
    /// <summary>
    /// エリア移動（シーン遷移または同一シーン内ワープ）後のプレイヤー出現位置と向きを定義するマーカー。
    /// Scene ビューで視覚的に配置・確認できる Gizmo を備えています。
    /// </summary>
    [AddComponentMenu("Monochrome/Transition/Spawn Point")]
    public class SpawnPoint : MonoBehaviour
    {
        private static readonly Dictionary<string, SpawnPoint> registry = new Dictionary<string, SpawnPoint>(StringComparer.OrdinalIgnoreCase);

        [Header("Spawn Settings")]
        [Tooltip("出現地点の一意な識別ID（例: 'From_Town', 'Shop_Entrance', 'Default'）")]
        [SerializeField] private string spawnId = "Default";

        [Tooltip("出現時にプレイヤーが向く方向")]
        [SerializeField] private Vector2 facingDirection = Vector2.down;

        [Header("Gizmo Settings")]
        [SerializeField] private Color gizmoColor = new Color(0.2f, 0.8f, 1.0f, 0.8f);
        [SerializeField] private float gizmoRadius = 0.4f;

        public string SpawnId => spawnId;
        public Vector2 FacingDirection => facingDirection;
        public Vector2 Position => transform.position;

        private void OnEnable()
        {
            if (!string.IsNullOrEmpty(spawnId))
            {
                registry[spawnId] = this;
            }
        }

        private void OnDisable()
        {
            if (!string.IsNullOrEmpty(spawnId) && registry.TryGetValue(spawnId, out var sp) && sp == this)
            {
                registry.Remove(spawnId);
            }
        }

        /// <summary>
        /// 現在アクティブなシーンから指定した SpawnId を持つスポーン地点を検索します
        /// </summary>
        public static SpawnPoint Find(string id)
        {
            if (string.IsNullOrEmpty(id)) return null;

            if (registry.TryGetValue(id, out var sp) && sp != null)
            {
                return sp;
            }

            // 見つからない場合はシーン内全走査（フォールバック）
            var all = FindObjectsByType<SpawnPoint>(FindObjectsSortMode.None);
            foreach (var point in all)
            {
                if (point.SpawnId.Equals(id, StringComparison.OrdinalIgnoreCase))
                {
                    registry[id] = point;
                    return point;
                }
            }

            // 何も該当しない場合は最初のポイントを返す
            return all.Length > 0 ? all[0] : null;
        }

        private void OnDrawGizmos()
        {
            Gizmos.color = gizmoColor;
            Vector3 pos = transform.position;

            // 足元マーカー
            Gizmos.DrawWireSphere(pos, gizmoRadius);
            Gizmos.DrawSphere(pos, gizmoRadius * 0.3f);

            // 向きを示す矢印
            Vector3 dir = new Vector3(facingDirection.x, facingDirection.y, 0).normalized * (gizmoRadius * 1.5f);
            if (dir != Vector3.zero)
            {
                Gizmos.color = Color.yellow;
                Gizmos.DrawRay(pos, dir);

                // 矢印の先端
                Vector3 right = Quaternion.Euler(0, 0, 150) * dir * 0.3f;
                Vector3 left = Quaternion.Euler(0, 0, -150) * dir * 0.3f;
                Gizmos.DrawRay(pos + dir, right);
                Gizmos.DrawRay(pos + dir, left);
            }

#if UNITY_EDITOR
            UnityEditor.Handles.Label(pos + Vector3.up * (gizmoRadius + 0.2f), $"Spawn: {spawnId}");
#endif
        }
    }
}
