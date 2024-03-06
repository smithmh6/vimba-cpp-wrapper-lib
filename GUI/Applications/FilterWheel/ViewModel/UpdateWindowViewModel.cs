using Prism.Commands;
using Prism.Mvvm;
using System;
using FilterWheel.Localization;
using FilterWheelShared.Common;
using FilterWheelShared.SoftwareUpdate;

namespace FilterWheel.ViewModel
{
    public class UpdateWindowViewModel : BindableBase
    {
        public DelegateCommand RefreshCommand { get; private set; }
        public DelegateCommand DownloadCommand { get; private set; }
        public DelegateCommand LoadedCommand { get; private set; }
        public DelegateCommand ClosedCommand { get; private set; }
        private SoftwareUpdateService updateService;

        private bool refreshCanExecute = true;
        private bool downloadCanExecute = false;

        public string CurrentVersion
        {
            get { return ThorlabsProduct.Version; }
        }

        private string lastestVersion;
        public string LastestVersion
        {
            get => lastestVersion;
            set => SetProperty(ref lastestVersion, value);
        }

        public string UpdatePageUri
        {
            get { return ThorlabsProduct.UpdatePageUri; }
        }

        private String progress;
        public String Progress
        {
            get => progress;
            set
            {
                if (Equals(progress, value)) return;
                SetProperty(ref progress, value);
            }
        }

        public UpdateWindowViewModel()
        {
            LoadedCommand = new DelegateCommand(RefreshCommandExecute);
            ClosedCommand = new DelegateCommand(CancelDownload);
            RefreshCommand = new DelegateCommand(RefreshCommandExecute, RefreshCommandCanExecute);
            DownloadCommand = new DelegateCommand(DownloadCommandExecute, DownloadCommandCanExecute);
            updateService = new SoftwareUpdateService(CurrentVersion, ThorlabsProduct.CheckUpdateUri);
        }

        private ShellLocalizationKey softwareStatus;
        public ShellLocalizationKey SoftwareStatus
        {
            get { return softwareStatus; }
            set { SetProperty(ref softwareStatus, value); }
        }

        public async void RefreshCommandExecute()
        {
            refreshCanExecute = false;
            RefreshCommand.RaiseCanExecuteChanged();

            int checkResult;
            try
            {
                checkResult = await updateService.CheckInfoFromServer().ConfigureAwait(false);
                if (checkResult > 0)
                {
                    SoftwareStatus = checkResult == 1 ? ShellLocalizationKey.ServerUnavailable : ShellLocalizationKey.NoSoftwareAvailable;
                    refreshCanExecute = true;
                    RefreshCommand.RaiseCanExecuteChanged();
                    return;
                }
            }
            catch (Exception e)
            {
                SoftwareStatus = ShellLocalizationKey.ParsingError;
                refreshCanExecute = true;
                RefreshCommand.RaiseCanExecuteChanged();
                System.Diagnostics.Debug.WriteLine(e.Message);
                return;
            }
            if (updateService.IsNewVersionAvailable)
            {
                SoftwareStatus = ShellLocalizationKey.CanUpdate;
                downloadCanExecute = true;
            }
            else
            {
                SoftwareStatus = ShellLocalizationKey.NoNeedToUpdate;
                downloadCanExecute = false;
            }
            LastestVersion = updateService.LatestVersion;
            DownloadCommand.RaiseCanExecuteChanged();
            refreshCanExecute = true;
            RefreshCommand.RaiseCanExecuteChanged();
        }

        private bool RefreshCommandCanExecute()
        {
            return refreshCanExecute;
        }

        private void DownloadCommandExecute()
        {
            updateService.DownloadUpdate();
        }

        private bool DownloadCommandCanExecute()
        {
            return downloadCanExecute;
        }

        public void CancelDownload()
        {
            updateService.CancelDownloadUpdate();
        }
    }
}
