using Prism.Services.Dialogs;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using System.Xml.Linq;
using FilterWheelShared.Common;
using FilterWheelShared.Properties;

namespace FilterWheelShared.DeviceDataService
{
    public interface IUpdate
    {
        void StartUpdate(object param = null);
        void StopUpdate(object param = null);
    }

    public interface IJson
    {
        void LoadJsonSettings(List<JsonObject> jsonDatas);
        void SaveJsonSettings(List<JsonObject> jsonDatas);
    }

    public interface IIndependentJson : IJson
    {

    }
    public class JsonObject
    {
        public string Name { get; set; }
        public object Setting { get; set; }
    }

    public class JsonData
    {
        public string CameraType { get; set; }
        public List<JsonObject> Settings { get; set; }
    }

    public class ConfigService
    {
        private static readonly ConfigService _instance;
        public static ConfigService Instance => _instance;

        private ConcurrentDictionary<string, object> _jasonDataDic;
        private List<JsonObject> _jsonDatas;
        static ConfigService()
        {
            _instance = new ConfigService();
        }

        public event EventHandler GetConfigEvent;
        public event EventHandler SaveConfigEvent;
        public void LoadSettings()
        {
            var settingFolder = ThorlabsProduct.ApplitaionSettingDir;
            if (!System.IO.Directory.Exists(settingFolder))
            {
                System.IO.Directory.CreateDirectory(settingFolder);
            }
            if (!System.IO.File.Exists(ThorlabsProduct.ThorImageCamSttingsPath))
                return;

            var json = System.IO.File.ReadAllText(ThorlabsProduct.ThorImageCamSttingsPath);
            try
            {
                var settings = JsonSerializer.Deserialize<List<JsonObject>>(json);
                //MVMManager.Instance.LoadJsonSettings<IIndependentJson>(settings);
            }
            catch (Exception e)
            {

            }
        }


        public bool LoadRecentFiles(string fileName, ObservableCollection<RecentItem> RecentFiles)
        {
            var settingFolder = ThorlabsProduct.ApplitaionSettingDir;
            if (!System.IO.Directory.Exists(settingFolder))
            {
                System.IO.Directory.CreateDirectory(settingFolder);
            }
            if (!System.IO.File.Exists(fileName))
                return false;

            try
            {
                var json = System.IO.File.ReadAllText(fileName);
                var items = JsonSerializer.Deserialize<List<RecentItem>>(json);
                foreach (var item in items)
                {
                    if (System.IO.File.Exists(item.FilePath))
                    {
                        RecentFiles.Add(item);
                    }
                }
            }
            catch (Exception e)
            {
                ;
            }
            return true;
        }

        public bool SaveRecentFiles(string fileName, ObservableCollection<RecentItem> RecentFiles)
        {
            try
            {
                string json = JsonSerializer.Serialize(RecentFiles);
                System.IO.File.WriteAllText(ThorlabsProduct.ThorImageCamHistoriesPath, json);
            }
            catch (Exception e)
            {
                ;
            }
            return true;
        }

        public T GetCorrespondingConfig<T>(string configModuleName)
        {
            if (_jsonDatas == null || _jsonDatas.Count == 0) return default(T);
            var target = _jsonDatas.FirstOrDefault(item => item.Name == configModuleName);
            if (target != null)
            {
                var obj = JsonSerializer.Deserialize<T>(target.Setting.ToString());
                _jsonDatas.Remove(target);
                return obj;
            }
            return default(T);
        }

        public void UpdateCorrespondingConfig<T>(string configModuleName, T viewmodel)
        {
            _jsonDatas.Add(new JsonObject() { Name = configModuleName, Setting = viewmodel });
        }

        public bool LoadConfig(string fileName)
        {
            var file = fileName;
            var json = System.IO.File.ReadAllText(file);

            var isSucceed = false;

            var data = JsonSerializer.Deserialize<JsonData>(json);
            if (data.CameraType != ThorlabsCamera.Instance.CurrentCamera.ModelName)
            {

                return isSucceed;
            }
            //MVMManager.Instance.LoadJsonSettings<IJson>(data.Settings);
            isSucceed = true;

            return isSucceed;
        }

        public bool SaveConfig(string filename, bool isSaveAsCameraType = false)
        {
            var file = filename;
            var isSucceed = false;
            if (isSaveAsCameraType)
            {
                JsonData jsonData = new JsonData()
                {
                    CameraType = ThorlabsCamera.Instance.CurrentCamera.ModelName,
                    Settings = new List<JsonObject>(),
                };

                //MVMManager.Instance.SaveJsonSettings<IJson>(jsonData.Settings);

                var options = new JsonSerializerOptions
                {
                    IgnoreReadOnlyFields = true,
                    IgnoreReadOnlyProperties = true,
                    WriteIndented = true,
                };

                string json = JsonSerializer.Serialize(jsonData, options);
                System.IO.File.WriteAllText(file, json);
                isSucceed = true;
            }
            else
            {
                var settings = new List<JsonObject>();

                //MVMManager.Instance.SaveJsonSettings<IIndependentJson>(settings);

                var options = new JsonSerializerOptions
                {
                    IgnoreReadOnlyFields = true,
                    IgnoreReadOnlyProperties = true,
                    WriteIndented = true,
                };
                string json = JsonSerializer.Serialize(settings, options);
                System.IO.File.WriteAllText(file, json);
            }
            return isSucceed;
        }

        public void SaveJsonFile<T>(string path, T data)
        {
            try
            {
                var options = new JsonSerializerOptions 
                { 
                    WriteIndented = true, 
                    IgnoreReadOnlyFields = true, 
                    IgnoreReadOnlyProperties = true 
                };
                string json = JsonSerializer.Serialize(data, options);

                System.IO.File.WriteAllText(path, json);
            }
            catch (Exception e)
            {
                ;
            }
        }

        public T LoadJsonFile<T>(string path)
        {
            T data = default;

            try
            {
                var json = System.IO.File.ReadAllText(path);
                data = JsonSerializer.Deserialize<T>(json);
            }
            catch (Exception e)
            {
            }
            return data;
        }
    }
}
