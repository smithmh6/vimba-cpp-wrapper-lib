using Prism.Mvvm;

namespace FilterWheelShared.Common
{
    public enum Status
    {
        Normal,
        Warning,
        Error,
        Ready,
        Busy,
    }

    public class ProgramStatus : BindableBase
    {
        private Status _status = Status.Ready;
        public Status StatusEnum
        {
            get => _status;
            set => SetProperty(ref _status, value);
        }
        private string _message = "Ready";
        public string StatusMessage
        {
            get => _message;
            set => SetProperty(ref _message, value);
        }
    }

    public class PixelInfo : BindableBase
    {
        private bool _isMono = true;
        public bool IsMono
        {
            get => _isMono;
            set => SetProperty(ref _isMono, value);
        }

        private string _colorInfo = string.Empty;
        public string ColorInfo
        {
            get => _colorInfo;
            set => SetProperty(ref _colorInfo, value);
        }
    }

}
