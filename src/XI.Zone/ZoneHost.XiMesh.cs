using System;
using System.IO;
using System.Numerics;

namespace XI.Zone
{
    // CZone::LoadXiMesh / xiMesh(): a zone's collision mesh, ximeshes/<name>.ximesh with the C++'s name fix-ups, loaded
    // the first time something asks for it; a zone without one (or XiMeshDir null) behaves as NullXiMesh: no floor, no
    // terrain, nothing blocks a line of sight. CBaseEntity::CanSeeTarget (eyes 2 yalms up, through the entity's
    // LineOfSightCache) and EntityPathOwner::inWater.
    public sealed partial class ZoneHost
    {
        private const float EntityHeight = 2.0f; // ENTITY_HEIGHT

        public string XiMeshDir;

        public XiMesh XiMeshOf(ushort zone)
        {
            if (XiMeshDir == null) return null;
            var info = ZoneInfoOf(zone);
            if (info.XiMeshLoaded) return info.XiMesh;
            info.XiMeshLoaded = true;
            string file = Path.Combine(XiMeshDir, XiMeshName(info.Name) + ".ximesh");
            if (!File.Exists(file)) return null;
            try
            {
                info.XiMesh = XiMesh.Load(file);
            }
            catch (Exception e)
            {
                log_($"CZone::LoadXiMesh: Failed to load '{file}': {e.Message}");
            }
            return info.XiMesh;
        }

        // the zone_settings name to the ximesh file's
        public static string XiMeshName(string zoneName)
        {
            string meshName = zoneName;
            // Rala_Waterways_U -> Rala_Waterways_[U]
            if (meshName.Length >= 2 && meshName.EndsWith("_U", StringComparison.Ordinal))
                meshName = meshName.Substring(0, meshName.Length - 2) + "_[U]";
            // Escha_ZiTah -> Escha-ZiTah
            if (meshName.StartsWith("Escha_", StringComparison.Ordinal)) meshName = "Escha-" + meshName.Substring(6);
            // Desuetia_Empyreal_Paradox -> Desuetia-Empyreal_Paradox
            if (meshName.StartsWith("Desuetia_", StringComparison.Ordinal)) meshName = "Desuetia-" + meshName.Substring(9);
            if (meshName == "Ship_bound_for_Selbina_Pirates") meshName = "Ship_bound_for_Selbina_ID-227";
            else if (meshName == "Ship_bound_for_Mhaura_Pirates") meshName = "Ship_bound_for_Mhaura_ID-228";
            // Maquette_Abdhaljs-Legion_A -> Maquette_Abdhaljs-LegionA
            if (meshName.StartsWith("Maquette_Abdhaljs-Legion_", StringComparison.Ordinal)) meshName = meshName.Remove(meshName.Length - 2, 1);
            return meshName;
        }

        private XiMesh XiMeshOfEntity(object e) => XiMeshOf(ZoneOf(e));

        // xiMesh()->rayIntersect in the zone (no mesh: nothing blocks)
        private bool XiMeshRayIntersect(ushort zone, Vector3 start, Vector3 end, bool ignoreTransparentBarriers = true) =>
            XiMeshOf(zone)?.RayIntersect(start, end, ignoreTransparentBarriers) ?? false;

        // CBaseEntity::CanSeeTarget: a ray from the entity's eyes to the point's, 2 yalms up each (Y negative-up)
        private bool CanSeeTarget(BattleEntity e, float x, float y, float z)
        {
            var src = new Vector3(e.X, e.Y - EntityHeight, e.Z);
            var dst = new Vector3(x, y - EntityHeight, z);
            ushort zone = ZoneOf(e);
            long now = NowMs();
            e.LosCache ??= new LineOfSightCache();
            if (e.LosCache.Get(src, dst, zone, now) is bool cached) return cached;
            bool canSee = !XiMeshRayIntersect(zone, src, dst);
            e.LosCache.Put(src, dst, zone, canSee, now);
            return canSee;
        }

        private bool CanSeeTarget(BattleEntity e, BattleEntity target) => CanSeeTarget(e, target.X, target.Y, target.Z);

        // EntityPathOwner::inWater: shallow or deep water under the entity
        private bool InWater(BattleEntity e)
        {
            var terrain = XiMeshOfEntity(e)?.GetTerrainAt(e.X, e.Y, e.Z) ?? TerrainType.None;
            return terrain is TerrainType.ShallowWater or TerrainType.DeepWater;
        }
    }
}
