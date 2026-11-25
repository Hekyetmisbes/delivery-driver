using System.Collections.Generic;
using TrafficSystem;
using UnityEngine;

namespace DeliveryDriver.Quest.UI
{
    /// <summary>Restores only renderers disabled for the minimap camera pass.</summary>
    public sealed class MinimapNpcRendererFilter
    {
        private readonly List<Renderer> cached = new List<Renderer>();
        private readonly List<Renderer> hidden = new List<Renderer>();
        private readonly List<Renderer> scratch = new List<Renderer>();
        private float nextRefreshTime = float.NegativeInfinity;

        public void Hide(float now, float interval)
        {
            if (hidden.Count != 0)
                return;

            if (now >= nextRefreshTime)
                Refresh(now, interval);

            foreach (Renderer renderer in cached)
            {
                if (renderer == null || !renderer.enabled)
                    continue;
                renderer.enabled = false;
                hidden.Add(renderer);
            }
        }

        public void Restore()
        {
            foreach (Renderer renderer in hidden)
            {
                if (renderer != null)
                    renderer.enabled = true;
            }
            hidden.Clear();
        }

        public void Reset()
        {
            Restore();
            cached.Clear();
            scratch.Clear();
            nextRefreshTime = float.NegativeInfinity;
        }

        private void Refresh(float now, float interval)
        {
            cached.Clear();
            // Preserve the existing scene discovery semantics, but avoid one array per NPC.
            foreach (NpcCarAgent npc in Object.FindObjectsByType<NpcCarAgent>(FindObjectsSortMode.None))
            {
                if (npc == null)
                    continue;

                scratch.Clear();
                npc.GetComponentsInChildren(true, scratch);
                foreach (Renderer renderer in scratch)
                {
                    if (renderer != null)
                        cached.Add(renderer);
                }
            }
            scratch.Clear();
            // Throttle even if there are currently no NPCs.
            nextRefreshTime = now + Mathf.Max(0.1f, interval);
        }
    }
}
