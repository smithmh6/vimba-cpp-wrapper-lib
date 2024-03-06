using System;

namespace FilterWheelShared.Common
{
    public class CameraInfo : ICloneable, IEquatable<CameraInfo>
    {
        public string SerialNumber { get; private set; }
        public string ModelName { get; private set; }
        public CameraInfo(string sn, string model)
        {
            SerialNumber = sn;
            ModelName = model;
        }
        public override string ToString()
        {
            return $"{ModelName} {SerialNumber}";
        }

        public CameraInfo Clone()
        {
            return (CameraInfo)MemberwiseClone();
        }

        object ICloneable.Clone()
        {
            return MemberwiseClone();
        }

        public bool Equals(CameraInfo other)
        {
            return SerialNumber.Equals(other.SerialNumber) && ModelName.Equals(other.ModelName);
        }

        public override bool Equals(object obj)
        {
            return Equals(obj as CameraInfo);
        }

        public override int GetHashCode()
        {
            return base.GetHashCode();
        }
    }
}
