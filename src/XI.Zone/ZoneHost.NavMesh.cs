using System.IO;

namespace XI.Zone
{
    // CZone::LoadNavMesh / navMesh(): a zone's navmesh, navmeshes/<zone_settings name>.nav, loaded the first time
    // something asks for it, with LoadRoamRegions' warning for a region that does not sit on it. A zone without a
    // file has none (the C++ would bake one from the collision mesh, which is not ported) and its paths are straight
    // lines (PathFind.cs). NavMeshDir null turns navmeshes off altogether.
    public sealed partial class ZoneHost
    {
        public string NavMeshDir;

        public DetourNavMesh NavMeshOf(ushort zone)
        {
            if (NavMeshDir == null) return null;
            var info = ZoneInfoOf(zone);
            if (info.NavMeshLoaded) return info.NavMesh;
            info.NavMeshLoaded = true;
            info.NavMesh = DetourNavMesh.Load(Path.Combine(NavMeshDir, info.Name + ".nav"), zone, log_);
            if (info.NavMesh != null)
                foreach (var region in info.Regions.Values)
                    if (!region.HasWalkableSurface(info.NavMesh, new System.Random(zone)))
                        log_($"LoadRoamRegions: {region.Name} does not sit on the navmesh of zone {zone}");
            return info.NavMesh;
        }
    }
}
