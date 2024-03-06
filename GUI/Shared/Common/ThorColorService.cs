using Prism.Mvvm;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.WebSockets;
using System.Windows.Media;
using Thorlabs.CustomControls.TelerikAndSciChart.Controls.ColorMapEditor;

namespace FilterWheelShared.Common
{
    public static class ThorColorExtension
    {
        private static readonly Dictionary<CustomPixelFormat, Tuple<int[], int>> _dataOrder = new Dictionary<CustomPixelFormat, Tuple<int[], int>>()
        {
            //byte[] with index of ARGB
            { CustomPixelFormat.ARGB, new Tuple<int[], int>(new int[] { 0, 1, 2, 3 }, 4)},
            { CustomPixelFormat.RGBA, new Tuple<int[], int>(new int[] { 3, 0, 1, 2 }, 4)},
            { CustomPixelFormat.RGB, new Tuple<int[], int>(new int[] { -1, 0, 1, 2 }, 3)},
            { CustomPixelFormat.BGR, new Tuple<int[], int>(new int[] { -1, 2, 1, 0 }, 3)},
        };

        public static IList<byte> GetData(this ThorColor color, CustomPixelFormat format)
        {
            if (color.PixelFormat == format)
                return color.Data;

            int srcSize = _dataOrder[color.PixelFormat].Item2;
            int dstSize = _dataOrder[format].Item2;
            var srcOrder = _dataOrder[color.PixelFormat].Item1;
            var dstOrder = _dataOrder[format].Item1;

            var pixelCount = color.Data.Count / srcSize;

            var dstArray = new byte[pixelCount * dstSize];

            for (int i = 0; i < pixelCount; i++)
            {
                for (int j = 0; j < 4; j++)
                {
                    var srcIndex = srcOrder[j];
                    var dstIndex = dstOrder[j];
                    if (dstIndex < 0 && dstIndex < 0)
                        continue;
                    if (srcIndex < 0)
                        dstArray[i * dstSize + dstIndex] = 255;
                    else
                        dstArray[i * dstSize + dstIndex] = color.Data[i * srcSize + srcIndex];

                }
            }

            return dstArray;

            //byte[] aList = null;
            //byte[] rList = null;
            //byte[] gList = null;
            //byte[] bList = null;
            //int pixelCount = 0;
            //switch (color.PixelFormat)
            //{
            //    case CustomPixelFormat.ARGB:
            //        pixelCount = color.Data.Count / 4;
            //        aList = color.Data.Where((d, i) => i % 4 == 0).ToArray();
            //        rList = color.Data.Skip(1).Where((d, i) => i % 4 == 0).ToArray();
            //        gList = color.Data.Skip(2).Where((d, i) => i % 4 == 0).ToArray();
            //        bList = color.Data.Skip(3).Where((d, i) => i % 4 == 0).ToArray();
            //        break;
            //    case CustomPixelFormat.RGBA:
            //        pixelCount = color.Data.Count / 4;
            //        rList = color.Data.Where((d, i) => i % 4 == 0).ToArray();
            //        gList = color.Data.Skip(1).Where((d, i) => i % 4 == 0).ToArray();
            //        bList = color.Data.Skip(2).Where((d, i) => i % 4 == 0).ToArray();
            //        aList = color.Data.Skip(3).Where((d, i) => i % 4 == 0).ToArray();
            //        break;
            //    case CustomPixelFormat.RGB:
            //        pixelCount = color.Data.Count / 3;
            //        rList = color.Data.Where((d, i) => i % 3 == 0).ToArray();
            //        gList = color.Data.Skip(1).Where((d, i) => i % 3 == 0).ToArray();
            //        bList = color.Data.Skip(2).Where((d, i) => i % 3 == 0).ToArray();
            //        break;
            //    case CustomPixelFormat.BGR:
            //        pixelCount = color.Data.Count / 3;
            //        bList = color.Data.Where((d, i) => i % 3 == 0).ToArray();
            //        gList = color.Data.Skip(1).Where((d, i) => i % 3 == 0).ToArray();
            //        rList = color.Data.Skip(2).Where((d, i) => i % 3 == 0).ToArray();
            //        break;
            //}

            //aList ??= Enumerable.Repeat((byte)255, pixelCount).ToArray();
            //var zippedAR = aList.Zip(rList, (a, r) => (a, r));
            //var zippedGB = gList.Zip(bList, (g, b) => (g, b));
            //var zippedARGB = zippedAR.Zip(zippedGB, (ar, gb) => (ar.a, ar.r, gb.g, gb.b));

            //switch (format)
            //{
            //    case CustomPixelFormat.ARGB:
            //        return zippedARGB.SelectMany((z, i) => new[] { z.a, z.r, z.g, z.b }).ToList();
            //    case CustomPixelFormat.RGBA:
            //        return zippedARGB.SelectMany((z, i) => new[] { z.r, z.g, z.b, z.a }).ToList();
            //    case CustomPixelFormat.RGB:
            //        return zippedARGB.SelectMany((z, i) => new[] { z.r, z.g, z.b }).ToList();
            //    case CustomPixelFormat.BGR:
            //        return zippedARGB.SelectMany((z, i) => new[] { z.b, z.g, z.r }).ToList();
            //}
            //return new List<byte>();
        }

        //color pixel format with saved file is ARGB, ARGB is default format
        public static void Save(this ThorColor color, string folderPath)
        {
            var folderInfo = new DirectoryInfo(folderPath);
            if (!folderInfo.Exists)
                folderInfo.Create();
            var filePath = folderInfo.FullName + "\\" + color.Name;
            using (var fs = File.Open(filePath, FileMode.OpenOrCreate))
            {
                using (var sw = new StreamWriter(fs))
                {
                    var data = color.GetData(CustomPixelFormat.ARGB);
                    for (int i = 0; i < data.Count; i += 4)
                    {
                        sw.WriteLine($"{data[i]},{data[i + 1]},{data[i + 2]},{data[i + 3]}");
                    }
                }
            }
        }

        public static List<ThorColor> LoadColor(string path, bool isCustom = false)
        {
            var ComputerColorDirecotoryPath = path;
            var colorList = new List<ThorColor>();
            if (!Directory.Exists(ComputerColorDirecotoryPath))
            {
                return colorList;
            }

            foreach (var filepath in Directory.GetFiles(ComputerColorDirecotoryPath))
            {
                using (var fs = new StreamReader(filepath))
                {
                    var name = Path.GetFileNameWithoutExtension(filepath);
                    var content = fs.ReadToEnd();
                    var bytelist =
                        content.Trim().Split(new char[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries)
                            .Take(256)
                            .Select(x =>
                            {
                                var sarry = x.Split(',');
                                return sarry.Select(x1 => byte.Parse(x1));
                            })
                            .SelectMany(x => x);
                    colorList.Add(new ThorColor(bytelist.ToList(), isCustom ? CustomPixelFormat.ARGB : CustomPixelFormat.RGB)
                    {
                        Name = name,
                        IsDeletable = isCustom,
                    });
                }
            }
            return colorList;
        }

    }

    public class ThorColorService : BindableBase
    {
        #region Singleton
        private ThorColorService()
        {

        }

        private static readonly ThorColorService Instance = new ThorColorService();
        public static ThorColorService GetInstance()
        {
            if (_initialized == false)
                throw new Exception("Must call Init function first!");
            return Instance;
        }
        #endregion

        public ThorColor RedColor { get; private set; }
        public ThorColor GreenColor { get; private set; }
        public ThorColor BlueColor { get; private set; }
        public ThorColor GrayColor { get; private set; }

        public List<ThorColor> SystemColors { get; private set; }
        public List<ThorColor> CustomColors { get; private set; }

        private static string _systemColorFolder;

        private static string _customColorFolder;

        private static bool _initialized = false;
        public static void Init(string systemColorFolder, string customColorFolder)
        {
            _systemColorFolder = systemColorFolder;
            _customColorFolder = customColorFolder;
            Instance.LoadSystemColors(_systemColorFolder);
            Instance.LoadCustomColors(_customColorFolder);
            _initialized = true;
        }

        #region Load ColorList
        public void LoadSystemColors(string path)
        {
            SystemColors = ThorColorExtension.LoadColor(path);
            RedColor = GetStandardColor("red", Colors.Red);
            GreenColor = GetStandardColor("green", Colors.Lime);
            BlueColor = GetStandardColor("blue", Colors.Blue);
            GrayColor = GetStandardColor("gray", Colors.White);
        }

        public void LoadCustomColors(string path)
        {
            CustomColors = ThorColorExtension.LoadColor(path, true);
        }

        private ThorColor GetStandardColor(string name, Color color)
        {
            var tempThorColor = SystemColors.FirstOrDefault(c => string.Compare(name, c.Name, StringComparison.OrdinalIgnoreCase) == 0);
            if (tempThorColor == null)
            {
                tempThorColor = new ThorColor(color)
                {
                    IsDeletable = false,
                    Name = name
                };
                SystemColors.Add(tempThorColor);
            }
            return tempThorColor;
        }

        #endregion

        #region Add & Remove Custom Color

        public void AddCustomColor(ThorColor color)
        {
            if (CustomColors.Any(c => c.Name == color.Name))
                return;
            CustomColors.Add(color);
            color.Save(_customColorFolder);
        }

        public void RemoveCustomColor(ThorColor color)
        {
            var filePath = _customColorFolder + "\\" + color.Name;
            if (File.Exists(filePath))
                File.Delete(filePath);
            CustomColors.Remove(color);
        }

        public ThorColor GetCorrespondingColor(string colorName)
        {
            ThorColor color = GrayColor;
            var sysColors = SystemColors.Where(c => c.Name == colorName).ToList();
            if(sysColors!= null&& sysColors.Count>0)
            {
                color = sysColors.FirstOrDefault();
                return color;
            }
            var cusColors= CustomColors.Where(c => c.Name == colorName).ToList();
            if (cusColors != null && cusColors.Count > 0)
            {
                color = cusColors.FirstOrDefault();
                return color;
            }
            return color;
        }

        #endregion
    }

}
