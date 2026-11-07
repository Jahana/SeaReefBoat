using System;

namespace XI.Zone
{
    // trigger_area.cpp: ITriggerArea, the zone's trigger areas a zone script registers in onInitialize (CLuaZone::
    // register*TriggerArea); CZone::CheckTriggerAreas tests each char's position against them. A char in the zone is
    // (x, y, z) with y the height, as position_t is.
    // The ID-zero warning of the ITriggerArea constructor is logged by the registering binding (this class has no log).
    public abstract class TriggerArea
    {
        public readonly uint Id;    // m_triggerAreaID
        private short count_;       // m_count, the Lua scripts' counter (getCount / addCount / delCount)

        protected TriggerArea(uint id) => Id = id;

        // ITriggerArea::getCount / addCount / delCount
        public short Count => count_;
        public short AddCount(short n) => count_ += n;
        public short DelCount(short n) => count_ -= n;

        public abstract bool IsPointInside(float x, float y, float z);
    }

    // CCuboidTriggerArea: an axis box, optionally turned about its centre in the x/z plane by rotation (radians)
    public sealed class CuboidTriggerArea : TriggerArea
    {
        private readonly float xMin_, yMin_, zMin_, xMax_, yMax_, zMax_, rotation_, sin_, cos_;

        public CuboidTriggerArea(uint id, float xMin, float yMin, float zMin, float xMax, float yMax, float zMax, float rotation) : base(id)
        {
            xMin_ = xMin;
            yMin_ = yMin;
            zMin_ = zMin;
            xMax_ = xMax;
            yMax_ = yMax;
            zMax_ = zMax;
            rotation_ = rotation;
            sin_ = MathF.Sin(rotation);
            cos_ = MathF.Cos(rotation);
        }

        // CCuboidTriggerArea::isPointInside
        public override bool IsPointInside(float x, float y, float z)
        {
            if (rotation_ != 0.0f)
            {
                float xCentre = Midpoint(xMin_, xMax_);
                float zCentre = Midpoint(zMin_, zMax_);
                float dX = x - xCentre;
                float dZ = z - zCentre;

                x = xCentre + dX * cos_ + dZ * sin_;
                z = zCentre + dZ * cos_ - dX * sin_;
            }

            return xMin_ <= x && yMin_ <= y && zMin_ <= z && xMax_ >= x && yMax_ >= y && zMax_ >= z;
        }

        // std::midpoint for floats: (a + b) / 2 for zone-sized coordinates (its overflow branches never apply)
        private static float Midpoint(float a, float b) => (a + b) / 2;
    }

    // CCylindricalTriggerArea: a circle in the x/z plane, any height
    public sealed class CylindricalTriggerArea : TriggerArea
    {
        private readonly float x_, z_, radius_;

        public CylindricalTriggerArea(uint id, float x, float z, float radius) : base(id)
        {
            x_ = x;
            z_ = z;
            radius_ = radius;
        }

        // CCylindricalTriggerArea::isPointInside: the y component is "infinite"
        public override bool IsPointInside(float x, float y, float z)
        {
            float dX = x - x_;
            float dZ = z - z_;
            float distanceSquared = dX * dX + dZ * dZ;
            return distanceSquared <= radius_ * radius_;
        }
    }

    // CSphericalTriggerArea
    public sealed class SphericalTriggerArea : TriggerArea
    {
        private readonly float x_, y_, z_, radius_;

        public SphericalTriggerArea(uint id, float x, float y, float z, float radius) : base(id)
        {
            x_ = x;
            y_ = y;
            z_ = z;
            radius_ = radius;
        }

        // CSphericalTriggerArea::isPointInside
        public override bool IsPointInside(float x, float y, float z)
        {
            float dX = x - x_;
            float dY = y - y_;
            float dZ = z - z_;
            float distanceSquared = dX * dX + dY * dY + dZ * dZ;
            return distanceSquared <= radius_ * radius_;
        }
    }
}
