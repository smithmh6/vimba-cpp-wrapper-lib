using System;
using System.Threading;
using System.Threading.Tasks;
using FilterWheelShared.Common;

namespace FilterWheelShared.DeviceDataService
{
    public delegate void PreReconnectDel(PreReconnectEventParam eventParam);
    public delegate void ReconnectingDel(ReconnectingEventParam eventParam);
    public delegate void PostReconnectDel(PostReconnectEventParam eventParam);

    public interface IConnectWindow
    {
        int Times { get; set; }
        void ShowDialog();
        bool ReconnectSuccessful { get; set; }
        void Close();
    }
    public class PreReconnectEventParam
    {

    }
    public class ReconnectingEventParam
    {
        public int Times { get; private set; }
        public ReconnectingEventParam(int times)
        {
            Times = times;
        }
    }
    public class PostReconnectEventParam
    {
        public int TotalTimes { get; private set; }
        public bool IsManual { get; private set; }
        public bool IsSuccessful { get; private set; }
        public PostReconnectEventParam(int totalTimes, bool isManual, bool isSuccessful)
        {
            TotalTimes = totalTimes;
            IsManual = isManual;
            IsSuccessful = isSuccessful;
        }
    }

    public static class ReconnectService
    {
        private static IConnectWindow connect_window;
        private static bool is_successful = false;
        private static int total_time = 0;
        private static ManualResetEvent manul_reset_event;
        private static AutoResetEvent reconnect_mutex;

        public static event PreReconnectDel PreReconnect;
        public static event ReconnectingDel Reconnecting;
        public static event PostReconnectDel PostReconnect;

        public static int Interval { get; set; }

        static ReconnectService()
        {
            Interval = 1000;
            //if (ConfigurationManager.AppSettings.AllKeys.Contains("ReconnectInterval"))
            //{
            //    var str = ConfigurationManager.AppSettings["ReconnectInterval"];
            //    if (int.TryParse(str, out int res))
            //    {
            //        Interval = res;
            //    }
            //}
            manul_reset_event = new ManualResetEvent(false);
            is_successful = false;
            reconnect_mutex = new AutoResetEvent(true);
        }

        public static async Task<bool> Reconnect(Func<bool> execute_connect, Func<bool> judge_connect = null)
        {
            Task<bool> task = new Task<bool>(() =>
            {
                reconnect_mutex.WaitOne();
                if (judge_connect != null && judge_connect())
                {
                    return true;
                }
                if (connect_window == null)
                {
                    throw new InvalidOperationException();
                }
                total_time = 0;
                is_successful = false;
                bool isManual = false;
                connect_window.Times = 0;
                manul_reset_event.Reset();
                connect_window.ReconnectSuccessful = false;
                PreReconnect?.Invoke(new PreReconnectEventParam());
                Task.Run(() =>
                {
                    connect_window.ShowDialog();
                });
                //Judge the window state before start reconnect.
                //To avoid this case: once reconnect,disconnect immediately, the process continue forever, the user could not stop
                do
                {
                    isManual = manul_reset_event.WaitOne(Interval);
                    if (isManual)
                    {
                        break;
                    }
                    total_time++;
                    is_successful = execute_connect();
                    connect_window.Times = total_time;
                    Reconnecting?.Invoke(new ReconnectingEventParam(total_time));
                    if (is_successful)
                    {
                        connect_window.ReconnectSuccessful = is_successful;
                        break;
                    }
                } while (true);
                PostReconnect?.Invoke(new PostReconnectEventParam(total_time, isManual, is_successful));
                reconnect_mutex.Set();
                return is_successful;
            });
            task.Start();
            return await task;
        }

        public static void StopReconnect()
        {
            manul_reset_event.Set();
        }

        public static void InjectConnectWindow(IConnectWindow connectWindow)
        {
            connect_window = connectWindow;
        }
    }
}
