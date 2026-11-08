using System.Collections.Generic;
using UnityEngine;

namespace Hedgewitch.Vacuum
{
    /// <summary>Maps colliders back to the IVacuumAffectable that owns them.</summary>
    public static class VacuumRegistry
    {
        private static readonly Dictionary<Collider, IVacuumAffectable> ByCollider =
            new Dictionary<Collider, IVacuumAffectable>();

        private static readonly Dictionary<IVacuumAffectable, List<Collider>> OwnedColliders =
            new Dictionary<IVacuumAffectable, List<Collider>>();

        private static readonly List<IVacuumAffectable> Registered = new List<IVacuumAffectable>();
        private static readonly List<Collider> ScratchKeys = new List<Collider>();

        public static IReadOnlyList<IVacuumAffectable> All => Registered;

        public static void Register(IVacuumAffectable affectable, IReadOnlyList<Collider> colliders)
        {
            if (affectable == null)
            {
                return;
            }

            if (!OwnedColliders.TryGetValue(affectable, out List<Collider> owned))
            {
                owned = new List<Collider>();
                OwnedColliders.Add(affectable, owned);
                Registered.Add(affectable);
            }

            if (colliders == null)
            {
                return;
            }

            for (int i = 0; i < colliders.Count; i++)
            {
                Collider collider = colliders[i];
                if (collider == null)
                {
                    continue;
                }

                ByCollider[collider] = affectable;
                if (!owned.Contains(collider))
                {
                    owned.Add(collider);
                }
            }
        }

        public static void Unregister(IVacuumAffectable affectable)
        {
            if (affectable == null)
            {
                return;
            }

            if (OwnedColliders.TryGetValue(affectable, out List<Collider> owned))
            {
                for (int i = 0; i < owned.Count; i++)
                {
                    if (owned[i] != null)
                    {
                        ByCollider.Remove(owned[i]);
                    }
                }

                owned.Clear();
                OwnedColliders.Remove(affectable);
            }

            Registered.Remove(affectable);
        }

        public static bool TryResolve(Collider collider, out IVacuumAffectable affectable)
        {
            affectable = null;
            if (collider == null)
            {
                return false;
            }

            if (ByCollider.TryGetValue(collider, out IVacuumAffectable cached))
            {
                affectable = cached;
                return cached != null && cached.Transform != null;
            }

            IVacuumAffectable found = collider.GetComponentInParent<IVacuumAffectable>();
            ByCollider[collider] = found;
            affectable = found;
            return found != null;
        }

        public static void Prune()
        {
            ScratchKeys.Clear();
            foreach (KeyValuePair<Collider, IVacuumAffectable> pair in ByCollider)
            {
                if (pair.Key == null)
                {
                    ScratchKeys.Add(pair.Key);
                }
            }

            for (int i = 0; i < ScratchKeys.Count; i++)
            {
                ByCollider.Remove(ScratchKeys[i]);
            }

            ScratchKeys.Clear();

            for (int i = Registered.Count - 1; i >= 0; i--)
            {
                IVacuumAffectable affectable = Registered[i];
                if (affectable == null || affectable.Transform == null)
                {
                    Registered.RemoveAt(i);
                    if (affectable != null)
                    {
                        OwnedColliders.Remove(affectable);
                    }
                }
            }
        }

        public static void Clear()
        {
            ByCollider.Clear();
            OwnedColliders.Clear();
            Registered.Clear();
        }
    }
}
