using Prism.Events;
using System;

namespace FilterWheelShared.Event
{
    public enum ThorCamStatus
    {
        None,
        Error,
        Living,
        Capturing,
        Snapshoting,
        Loaded,
        Saved,
        Export,
        Jogging,
    }

    public enum ErrorType
    {
        None,
        LoadImageFailed,
        SaveDirectoryError,
        SaveDirectoryAccess,
        IntervalError,
        InternalTriggerError,
        OpenCameraFailed,
        SetAutoExposureFailed,
        StartPreviewFailed,
        StopPreviewFailed,
        ExportFileFailed,
        FileOccupied,
    }

    public class JoggingToLivingException : Exception
    {
    }

    public class ThorCamStatusEventArgs
    {
        public ThorCamStatus Status { get; }
        public Exception Except { get; }

        public ErrorType ErrorType { get; } = ErrorType.None;

        public ThorCamStatusEventArgs(ThorCamStatus status, Exception except)
        {
            Status = status;
            Except = except;
        }

        //Just for error
        public ThorCamStatusEventArgs(string exceptMsg, ErrorType error)
        {
            Status = ThorCamStatus.Error;
            Except = new Exception(exceptMsg);
            ErrorType = error;
        }
    }

    public class ThorCamStatusEvent : PubSubEvent<ThorCamStatusEventArgs>
    {
    }
}
