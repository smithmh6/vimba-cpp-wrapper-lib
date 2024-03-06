namespace FilterWheelShared.Common
{
    public class GenericPoint<T1, T2> : System.IEquatable<GenericPoint<T1, T2>>
    {
        public T1 XValue { get; private set; }
        public T2 YValue { get; private set; }

        public GenericPoint(T1 x, T2 y)
        {
            XValue = x;
            YValue = y;
        }

        public GenericPoint(GenericPoint<T1, T2> p)
        {
            XValue = p.XValue;
            YValue = p.YValue;
        }

        public bool Equals(GenericPoint<T1, T2> other)
        {
            return this.XValue.Equals(other.XValue) && this.YValue.Equals(other.YValue);
        }

        public override bool Equals(object obj)
        {
            return Equals(obj as GenericPoint<T1, T2>);
        }

        public override int GetHashCode()
        {
            return base.GetHashCode();
        }
    }

    public class IntPoint : GenericPoint<int, int>
    {
        public IntPoint(int x, int y) : base(x, y) { }
        public IntPoint(IntPoint p) : base(p) { }
    }

    public class DoublePoint : GenericPoint<double, double>
    {
        public DoublePoint(double x, double y) : base(x, y) { }
        public DoublePoint(DoublePoint p) : base(p) { }
    }

    public class ChartPoint : GenericPoint<int, double>
    {
        public ChartPoint(int x, double y) : base(x, y) { }
        public ChartPoint(ChartPoint p) : base(p) { }
    }

    public class PointEx
    {
        public IntPoint PixelPoint { get; private set; }
        public DoublePoint PhysicalPoint { get; private set; }
        public PointEx(IntPoint pixel, DoublePoint Physical)
        {
            PixelPoint = pixel;
            PhysicalPoint = Physical;
        }
    }
}
