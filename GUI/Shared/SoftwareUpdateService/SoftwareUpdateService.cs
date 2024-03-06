using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using System.Xml.Linq;

namespace FilterWheelShared.SoftwareUpdate
{
    public class SoftwareUpdateService
    {
        private string checkUpdateUrl = string.Empty;
        private string downloadLink = string.Empty;
        public string CurrentVersion { get; } = string.Empty;
        public string LatestVersion { get; private set; } = string.Empty;

        public bool IsNewVersionAvailable
        {
            get { return CompareVersion(LatestVersion, CurrentVersion); }
        }

        private CancellationTokenSource tokenSource;

        public SoftwareUpdateService(string version, string url)
        {
            CurrentVersion = version;
            checkUpdateUrl = url;
            tokenSource = new CancellationTokenSource();
        }


        public async Task<int> CheckInfoFromServer()
        {
            string updateInfo = await GetServerUpdateInfo().ConfigureAwait(false);
            if (string.IsNullOrEmpty(updateInfo))
                return 1;
            else if (updateInfo.Contains("No Software Available"))
                return 2;
            else
            {
                ParseUpdateInfo(updateInfo);
                return 0;
            }
        }

        private Task<string> GetServerUpdateInfo()
        {
            return Task.Run(() =>
            {
                try
                {
                    var web = new HttpClient();
                    web.Timeout = new TimeSpan(hours: 0, minutes: 0, seconds: 3);
                    var res = web.GetAsync(checkUpdateUrl);
                    var result = res.Result.Content.ReadAsStringAsync().Result;
                    res.ContinueWith((x) => web.Dispose());
                    return result;
                }
                catch (Exception)
                {
                    return null;
                }
            });
        }

        private void ParseUpdateInfo(string strHtml)
        {
            StringReader sr = new StringReader(strHtml.Trim());
            XDocument doc = XDocument.Load(sr, LoadOptions.None);
            var versionEle = doc.Descendants("VersionNumber").FirstOrDefault();
            if (versionEle != null)
                LatestVersion = versionEle.Value;

            versionEle = doc.Descendants("DownloadLink").FirstOrDefault();
            if (versionEle != null)
                downloadLink = versionEle.Value;
        }

        private bool CompareVersion(string version1, string version2)
        {
            if (String.IsNullOrEmpty(version1))
                return false;

            var v1Nums = version1.Split('.');
            var v2Nums = version2.Split('.');
            int shorter = v1Nums.Length < v2Nums.Length ? v1Nums.Length : v2Nums.Length;

            for (int i = 0; i < shorter; i++)
            {
                var v1 = Convert.ToInt32(v1Nums[i]);
                var v2 = Convert.ToInt32(v2Nums[i]);
                if (v1 > v2)
                    return true;
                else if (v1 < v2)
                    return false;
            }
            if (v1Nums.Length > v2Nums.Length)
                return true;
            return false;
        }

        public void CancelDownloadUpdate()
        {
            tokenSource.Cancel();
        }

        public void DownloadUpdate()
        {
            if (string.IsNullOrEmpty(downloadLink))
                return;

            ProcessStartInfo ps = new ProcessStartInfo()
            {
                WindowStyle = ProcessWindowStyle.Normal,
                UseShellExecute = true,
                FileName = downloadLink,
                ErrorDialog = true
            };
            Process.Start(ps);
        }
    }
}
