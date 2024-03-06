using System;
using System.Configuration;
using System.IO;
using System.Security.AccessControl;

namespace FilterWheelShared.Common
{
    public static class ThorlabsProduct
    {
        public static Configuration AppConfig
        {
            get { return ConfigurationManager.OpenExeConfiguration(ConfigurationUserLevel.None); }
        }
        public static string ProductName
        {
            get { return ConfigurationManager.AppSettings["ProductName"]; }
        }
        // The keyword of hardware description to filter certain ones from devices listed out
        public static string HardwareFilterName
        {
            get { return ConfigurationManager.AppSettings["HardwareFilterName"]; }
        }
        public static string ProductShortDisplayName
        {
            get { return ConfigurationManager.AppSettings["ProductShortDisplayName"]; }
        }
        public static string ProductLongDisplayName
        {
            get { return ConfigurationManager.AppSettings["ProductLongDisplayName"]; }
        }
        public static string Version
        {
            get { return ConfigurationManager.AppSettings["Version"]; }
        }
        public static string CheckUpdateUri
        {
            get { return ConfigurationManager.AppSettings["CheckUpdateUri"]; }
        }
        public static string UpdatePageUri
        {
            get { return ConfigurationManager.AppSettings["UpdatePageUri"]; }
        }
        public static string ProductInfoUrl
        {
            get { return ConfigurationManager.AppSettings["ProductInfoUrl"]; }
        }
        public static bool IsSupportMultiLanguage
        {
            get { return ConfigurationManager.AppSettings["MultiLanguage"] == "true"; }
        }
        public static string LanguageFileDirectory
        {
            get { return ConfigurationManager.AppSettings["LanguageFileDirectory"]; }
        }

        public const string CopyRight = "©2023-2024 Thorlabs. All rights reserved.";

        public const string DarkThemeName = "Dark";
        public const string LightThemeName = "Light";

        public static string SerialNo { get; set; } = string.Empty;
        public static string WorkDirectory { get; set; } = string.Empty;

        public static string InstallDirectory { get; set; } = string.Empty;

        private static string AppFilePath
        {
            get { return @"\Thorlabs\" + ProductName + @"\"; }
        }

        public static string CommonApplicationDataDir
        {
            get => Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData) + AppFilePath;
        }

        public static string LocalApplicationDataDir
        {
            get => Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData) + AppFilePath;
        }

        public static string DocumentDir
        {
            get => Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
        }

        public static string ApplitaionSettingDir => LocalApplicationDataDir + @"Settings\";
        public static string ThorImageCamSttingsPath => ApplitaionSettingDir + "camera settings.json";
        public static string ThorImageCamPopupsLocationPath => ApplitaionSettingDir + "location.json";
        public static string ThorImageCamHistoriesPath => LocalApplicationDataDir + "Histories.json";

        public const string ThorImageCamSettingsRootNodeName = "ThorImageCAMSettings";
        public const string ThorImageCamHistoriesRootNodeName = "Histories";

        public const string PersonalizationFileName = "user.config";

        public static Configuration PersonalizationConfig
        {
            get
            {
                var fileMap = new ExeConfigurationFileMap();
                fileMap.ExeConfigFilename = ApplitaionSettingDir + PersonalizationFileName;
                Configuration configFile;

                try
                {
                    configFile = ConfigurationManager.OpenMappedExeConfiguration(fileMap, ConfigurationUserLevel.None);
                }
                catch
                {
                    File.Delete(fileMap.ExeConfigFilename);
                    configFile = ConfigurationManager.OpenMappedExeConfiguration(fileMap, ConfigurationUserLevel.None);
                }
                if (!configFile.HasFile)
                {
                    configFile.AppSettings.Settings.Add("currentDirectory", Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments));
                    configFile.Save(ConfigurationSaveMode.Modified);
                }

                return configFile;
            }
        }

        public static void GrantFullAccessPrivilegeToEveryone(string filename)
        {
            var fi = new FileInfo(filename);
            var fileSecurity = fi.GetAccessControl();
            fileSecurity.AddAccessRule(new FileSystemAccessRule("Everyone", FileSystemRights.FullControl, AccessControlType.Allow));
            fileSecurity.AddAccessRule(new FileSystemAccessRule("Users", FileSystemRights.FullControl, AccessControlType.Allow));
            fi.SetAccessControl(fileSecurity);
        }

    }
}
