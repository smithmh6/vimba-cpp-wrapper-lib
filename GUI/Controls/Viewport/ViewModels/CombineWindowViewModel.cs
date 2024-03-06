using Prism.Mvvm;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Windows.Media;
using FilterWheelShared.DeviceDataService;
using FilterWheelShared.ImageProcess;
using System.Drawing;

namespace Viewport.ViewModels
{
    public class CombineWindowViewModel : BindableBase
    {
        //public DelegateCommand BrowseCommand { get; private set; }

        private ObservableCollection<CombineImageViewModel> _images = new ObservableCollection<CombineImageViewModel>();
        public ObservableCollection<CombineImageViewModel> Images => _images;

        private bool _isConfirmAllowed;
        public bool IsConfirmAllowed
        {
            get { return _isConfirmAllowed; }
            set { SetProperty(ref _isConfirmAllowed, value); }
        }

        public CombineWindowViewModel()
        {
            //BrowseCommand = new DelegateCommand(BrowseExecute);
            //Add(filePaths);
            _images.Clear();
            for (int i = 0; i < DisplayService.Instance.Slots.Count; i++)
            {
                var img = new CombineImageViewModel(i);
                img.IsSelectedChanged += Img_IsSelectedChanged;
                _images.Add(img);
            }
        }

        public void UpdateSource()
        {
            for (int i = 0; i < DisplayService.Instance.Slots.Count; i++)
            {
                var slot = DisplayService.Instance.Slots[i];
                var img = _images[i];
                img.Name = slot.SlotName;
                img.Image = slot.SlotThumbnail;
                img.Data = slot.SlotImage;
                img.IsEnabled = img.Image != null;
            }
        }

        //private void BrowseExecute()
        //{
        //    var path = CaptureService.Instance.SaveFilePath;
        //    if (!Directory.Exists(path))
        //        path = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);

        //    var dialog = new OpenFileDialog()
        //    {
        //        Title = PluginLocalizationService.GetInstance().GetLocalizationString(PluginLocalziationKey.LoadImage),
        //        Multiselect = true,
        //        InitialDirectory = path,
        //        Filter = "TIF(*.tiff; *.tif)| *.tiff; *.tif|JPEG(*.jpg)|*.jpg"
        //    };
        //    if (dialog.ShowDialog() == true)
        //    {
        //        Add(dialog.FileNames);
        //    }
        //}

        //private void Add(IList<string> filePaths)
        //{
        //    //var openedPaths = Images.Select(i => { return i.FileName; });
        //    //var validAdd = filePaths.Where(f => !openedPaths.Contains(f)).ToList();
        //    Images.Clear();
        //    if (filePaths.Count == 1)
        //    {
        //        var fileName = filePaths[0];
        //        int fileHandle = -1;
        //        uint imageCount = 0;
        //        int result = ImageData.LoadTiffFile(fileName, ref fileHandle, ref imageCount);
        //        if (0 == result && fileHandle >= 0)
        //        {
        //            for (uint i = 0; i < imageCount; i++)
        //            {
        //                var img = new SavedImage(fileName);
        //                img.Load(fileHandle, i);
        //                if (img.ImageInfo == null) continue;
        //                var combineImg = new CombineImageViewModel(img);
        //                combineImg.IsSelectedChanged += Img_IsSelectedChanged;
        //                Images.Add(combineImg);
        //            }
        //            ImageData.CloseTiffFile(fileHandle);
        //        }
        //    }
        //    else
        //    {
        //        var savedImages = filePaths.Where(f => File.Exists(f)).Select(f =>
        //        {
        //            var img = new SavedImage(f);
        //            img.Load();
        //            return img;
        //        }).Where(i => i.ImageInfo != null).ToList();
        //        var combineImages = savedImages.Select(i =>
        //        {
        //            var combineImg = new CombineImageViewModel(i);
        //            combineImg.IsSelectedChanged += Img_IsSelectedChanged; ;
        //            return combineImg;
        //        }).ToList();
        //        Images.AddRange(combineImages);
        //    }
        //}

        private int _selectedCount = 0;

        private void Img_IsSelectedChanged(object sender, bool e)
        {
            var img = sender as CombineImageViewModel;
            if (e)
            {
                ++_selectedCount;
            }
            else
            {
                --_selectedCount;
            }
            if (e && _selectedCount == 1)
            {
                foreach (var i in Images)
                {
                    if (i.Image == null ||
                        // Resolution not equal
                        i.Data.DataInfo.x_size != img.Data.DataInfo.x_size
                        || i.Data.DataInfo.y_size != img.Data.DataInfo.y_size
                        // Pixel type not equal
                        || i.Data.DataInfo.pix_type != img.Data.DataInfo.pix_type
                        // Channel not equal
                        || i.Data.DataInfo.channels != img.Data.DataInfo.channels
                        )
                    {
                        i.IsEnabled = false;
                    }
                }
            }
            else if (!e && _selectedCount == 0)
            {
                foreach (var item in Images)
                {
                    item.IsEnabled = item.Image != null;
                }
            }
            IsConfirmAllowed = _selectedCount > 1;
        }
    }

    public class CombineImageViewModel : BindableBase
    {
        public event EventHandler<bool> IsSelectedChanged;

        private bool _isSelected;
        public bool IsSelected
        {
            get => _isSelected;
            set
            {
                if (SetProperty(ref _isSelected, value))
                {
                    IsSelectedChanged(this, _isSelected);
                }
            }
        }

        private ImageSource _smallImageSource;
        public ImageSource Image
        {
            get => _smallImageSource;
            set => SetProperty(ref _smallImageSource, value);
        }

        private ImageData _data = null;
        public ImageData Data
        {
            get => _data;
            set => SetProperty(ref _data, value);
        }

        private bool _isEnabled = true;
        public bool IsEnabled
        {
            get => _isEnabled;
            set => SetProperty(ref _isEnabled, value);
        }

        public int SlotIndex { get; }

        private string _name;
        public string Name
        {
            get => _name;
            set => SetProperty(ref _name, value);
        }

        public CombineImageViewModel(int slotIndex)
        {
            SlotIndex = slotIndex;
        }
    }
}
