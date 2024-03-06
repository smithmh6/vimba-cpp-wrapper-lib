using Prism.Commands;
using Prism.Events;
using Prism.Ioc;
using Prism.Mvvm;
using System;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Input;
using FilterWheelShared.DeviceDataService;
using FilterWheelShared.Event;
using FilterWheelShared.ImageProcess;

namespace FilterWheelShared.Common
{
    public class RecentItem
    {
        [JsonIgnore]
        public string PathForMenuItem { get; }
        public string FilePath { get; }
        [JsonIgnore]
        public ICommand LoadCommand { get; }

        public RecentItem(string filePath)
        {
            FilePath = filePath;
            PathForMenuItem = $"_{filePath}";
            LoadCommand = new DelegateCommand(OnLoadExecute);
        }

        private void OnLoadExecute()
        {
            ImageLoadService.Instance.LoadImage(FilePath);
        }
    }

    public class ImageLoadService : BindableBase
    {
        private static readonly IEventAggregator _eventAggregator = ContainerLocator.Current.Resolve<IEventAggregator>();

        private static readonly Lazy<ImageLoadService> _instance = new Lazy<ImageLoadService>(() => new ImageLoadService());
        private ImageLoadService()
        {
            RunCommand = new DelegateCommand(OnRunExecute, CanRunExecute);
            StopCommand = new DelegateCommand(OnStopExecute);
            _eventAggregator.GetEvent<CloseApplicationEvent>().Subscribe((e) => Close());
            _manual_wait_event = new ManualResetEvent(false);
            _manual_move_event = new ManualResetEvent(true);
        }
        public static ImageLoadService Instance => _instance.Value;

        public bool Enabled => ImageCount > 1;

        private uint _imageCount = 0;
        public uint ImageCount
        {
            get => _imageCount;
            private set
            {
                SetProperty(ref _imageCount, value);
                RaisePropertyChanged(nameof(Enabled));
            }
        }

        private int _interval = 1000;
        private double _frameRate = 1;
        public double FrameRate
        {
            get => _frameRate;
            set
            {
                var target = Math.Clamp(value, 0.1, 30);
                SetProperty(ref _frameRate, target);
                _interval = (int)(1000 / _frameRate);
            }
        }

        private uint _currentIndex = 1;
        public uint CurrentIndex
        {
            get => _currentIndex;
            set
            {
                if (IsRunnig)
                {
                    _currentIndex = value;
                    return;
                }
                //if (SetProperty(ref _currentIndex, value))
                MoveToSpecifiedIndex(value - 1);
                //{
                //    (SetProperty(ref _currentIndex, value);
                //}
            }
        }

        private bool _isrunning = false;
        public bool IsRunnig
        {
            get => _isrunning;
            private set => SetProperty(ref _isrunning, value);
        }

        public ICommand RunCommand { get; private set; }
        public ICommand StopCommand { get; private set; }

        private bool MoveToSpecifiedIndex(uint imageIndex)
        {
            if (imageIndex < 0 || imageIndex >= ImageCount || _previousHdl < 0)
                return false;

            int p2dImgHdl = -1;
            TiffImageSimpleInfo info = new TiffImageSimpleInfo();
            var result = CameraLIbCommand.GetImageData(_previousHdl, imageIndex, ref info);
            if (result != 0)
            {
                //load error, log and alert
                return false;
            }
            ThorlabsCamera.Instance.OnLoadImage(p2dImgHdl);
            _currentIndex = imageIndex + 1;
            RaisePropertyChanged(nameof(CurrentIndex));
            return true;
        }

        private readonly ManualResetEvent _manual_move_event;
        private readonly ManualResetEvent _manual_wait_event;

        [MethodImpl(MethodImplOptions.Synchronized)]
        private void OnRunExecute()
        {
            if (_currentIndex >= ImageCount)
                _currentIndex = 0;
            Task task = new Task(() =>
            {
                IsRunnig = true;
                _manual_wait_event.Reset();
                Stopwatch sw = new Stopwatch();
                while (IsRunnig)
                {
                    _manual_move_event.WaitOne();
                    sw.Restart();
                    var is_successful = MoveToSpecifiedIndex(_currentIndex);
                    if (!is_successful)
                    {
                        IsRunnig = false;
                        break;
                    }
                    sw.Stop();
                    var remain = (int)(_interval - sw.ElapsedMilliseconds);
                    if (remain < 0)
                        continue;
                    var isSignal = _manual_wait_event.WaitOne(remain);
                    if (isSignal || !IsRunnig)
                    {
                        break;
                    }
                }
            });
            task.Start();
        }

        private bool CanRunExecute()
        {
            return !IsRunnig;
        }

        private void OnStopExecute()
        {
            if (IsRunnig)
            {
                IsRunnig = false;
                _manual_move_event.Set();
                _manual_wait_event.Set();
            }
        }

        public void StartManulMove()
        {
            _manual_move_event.Reset();
        }

        public void CompleteManulMove()
        {
            _manual_move_event.Set();
        }

        private int _previousHdl = -1;
        public void LoadImage(string fileFullPath)
        {
            Close();
            CaptureService.Instance.StopLive();
            //_eventAggregator.GetEvent<ResetMainPanelEvent>().Publish();

            uint imageCount = 0;
            uint validBits = 0;
            int fileHandle = -1;
            int p2dImageHandle = ImageData.LoadImage(fileFullPath, ref fileHandle, ref imageCount, out _);
            if (p2dImageHandle != -1)
            {
                ImageData temp = new ImageData(p2dImageHandle);
                ThorlabsCamera.Instance.ResetMinMax(temp.DataInfo.channels == P2dChannels.P2D_CHANNELS_3, (int)validBits, ThorCamStatus.Loaded);
                ThorlabsCamera.Instance.OnLoadImage(p2dImageHandle);

                ImageCount = imageCount;
                if (imageCount > 1)
                    _previousHdl = fileHandle;

                //the exception only used to hold file full path
                _eventAggregator?.GetEvent<ThorCamStatusEvent>().Publish(new ThorCamStatusEventArgs(ThorCamStatus.Loaded, new Exception(fileFullPath)));
                return;
            }
            _eventAggregator.GetEvent<ThorCamStatusEvent>().Publish(new ThorCamStatusEventArgs(fileFullPath, ErrorType.LoadImageFailed));
        }

        public void Close()
        {
            IsRunnig = false;
            _manual_wait_event.Set();

            ImageCount = 0;
            RaisePropertyChanged(nameof(Enabled));

            if (_previousHdl > -1)
            {
                CameraLIbCommand.CloseImage(_previousHdl);
                _previousHdl = -1;
            }

            _currentIndex = 1;
            RaisePropertyChanged(nameof(CurrentIndex));
        }
    }
}
