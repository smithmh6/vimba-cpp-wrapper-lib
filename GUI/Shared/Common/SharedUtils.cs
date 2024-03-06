using System;

namespace FilterWheelShared.Common
{
    public static class SharedUtils
    {
        public static string AppStartupTimeStamp { get; private set; }

        public static void UpdateAppStartupTimeStamp()
        {
            TimeSpan ts = DateTime.Now - new DateTime(1970, 1, 1, 0, 0, 0, 0);
            AppStartupTimeStamp = Convert.ToInt64(ts.TotalSeconds).ToString();
        }
    }
}
