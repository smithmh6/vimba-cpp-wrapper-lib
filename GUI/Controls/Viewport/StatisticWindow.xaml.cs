using Prism.Events;
using Prism.Ioc;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Linq;
using System.Windows;
using Telerik.Windows.Controls;
using FilterWheelShared.Common;
using FilterWheelShared.DeviceDataService;

namespace Viewport
{
    /// <summary>
    /// Interaction logic for StatisticWindow.xaml
    /// </summary>
    public partial class StatisticWindow : RadWindow
    {
        public static readonly DependencyProperty SelectedROIsProperty = DependencyProperty.Register(nameof(SelectedROIs), typeof(ObservableCollection<StatisticItem>), typeof(StatisticWindow));

        public ObservableCollection<StatisticItem> SelectedROIs
        {
            get => (ObservableCollection<StatisticItem>)GetValue(SelectedROIsProperty);
            set => SetValue(SelectedROIsProperty, value);
        }

        private readonly IEventAggregator _eventAggregator;

        public StatisticWindow()
        {
            Init(true);

            InitializeComponent();

            Name = nameof(StatisticWindow);

            _eventAggregator = ContainerLocator.Container.Resolve<IEventAggregator>();
            _eventAggregator.GetEvent<FilterWheelShared.Event.ROISelectedEvent>().Subscribe(() =>
                {
                    _updateComing = true;
                    _roiSelectedChanged = true;
                }
            );
            //_eventAggregator.GetEvent<FilterWheelShared.Event.ClearROIEvent>().Subscribe((index) =>
            //{
            //    SelectedROIs.CollectionChanged -= SelectedROIs_CollectionChanged;
            //    SelectedROIs.Clear();
            //    SelectedROIs.CollectionChanged += SelectedROIs_CollectionChanged;
            //});

            SelectedROIs = new ObservableCollection<StatisticItem>();
            SelectedROIs.CollectionChanged += SelectedROIs_CollectionChanged;

            //this.DataContext = MVMManager.Instance["StatisticWindowViewModel"];
        }

        private bool _updateComing = false;
        private bool _roiSelectedChanged = false;

        public static void Init(bool isStandAlone)
        {
            if (!isStandAlone)
            {
                CultureInfo.CurrentCulture =
                CultureInfo.CurrentUICulture =
                CultureInfo.DefaultThreadCurrentCulture =
                CultureInfo.DefaultThreadCurrentUICulture = CultureInfo.InvariantCulture;
            }

            PluginCommon.PluginTheme.LoadPluginTheme(isStandAlone);

            FilterWheelShared.Localization.LocalizationManager.GetInstance().AddListener(PluginCommon.Localization.PluginLocalizationService.GetInstance());
        }

        private void OnROISelected()
        {
            if (_roiSelectedChanged)
            {
                var source = StatisticGrid.Items;
                var count = source.ItemCount;
                var idList = new List<int>();
                for (int i = 0; i < count; i++)
                {
                    var item = (StatisticItem)source[i];
                    if (item.IsSelected && !idList.Contains(item.Index))
                        idList.Add(item.Index);
                }

                SelectedROIs.CollectionChanged -= SelectedROIs_CollectionChanged;
                SelectedROIs.Clear();

                for (int i = 0; i < count; i++)
                {
                    var item = (StatisticItem)source[i];
                    if (idList.Contains(item.Index))
                        SelectedROIs.Add(item);
                }

                if (SelectedROIs.Count > 0)
                {
                    var index = this.StatisticGrid.Items.IndexOf(SelectedROIs[0]);
                    this.StatisticGrid.ScrollIndexIntoView(index);
                    //this.StatisticGrid.ScrollIntoViewAsync(this.StatisticGrid.Items[index], //the row 
                    //          new Action<FrameworkElement>((f) =>
                    //          {
                    //              (f as GridViewRow).IsSelected = true; // the callback method; if it is not necessary, you may set that parameter to null; 
                    //          }));
                }
                SelectedROIs.CollectionChanged += SelectedROIs_CollectionChanged;
                if (_updateComing)
                {
                    _updateComing = false;
                    return;
                }
                _roiSelectedChanged = false;
            }
        }

        private void SelectedROIs_CollectionChanged(object sender, System.Collections.Specialized.NotifyCollectionChangedEventArgs e)
        {
            _eventAggregator.GetEvent<FilterWheelShared.Event.StatisticItemSelectedEvent>().Publish(e);
        }

        protected override bool OnClosing()
        {
            _eventAggregator.GetEvent<FilterWheelShared.Event.UpdatePopupStatisticEvent>().Unsubscribe(UpdateStatistic);

            if (this.DataContext is IUpdate @interface)
            {
                @interface.StopUpdate();
            }

            return base.OnClosing();
        }

        protected override void OnActivated(EventArgs e)
        {
            if (this.DataContext is IUpdate @interface)
            {
                @interface.StartUpdate();
            }

            base.OnActivated(e);
        }

        public void Reset()
        {
            _isUpdating = false;
            _updateComing = false;
            _roiSelectedChanged = true;
            _eventAggregator.GetEvent<FilterWheelShared.Event.UpdatePopupStatisticEvent>().Subscribe(UpdateStatistic, ThreadOption.BackgroundThread);
        }

        private bool _isUpdating = false;
        private void UpdateStatistic(int arg)
        {
            if (_isUpdating)
                return;

            this.Dispatcher.InvokeAsync(() =>
            {
                do
                {
                    _isUpdating = true;
                    var isColor = DisplayService.Instance.IsColor;
                    List<StatisticItem> validItems = null;
                    if (isColor)
                    {
                        validItems = DisplayService.Instance.StatisticRGB.Where(i => i.IsChannelEnable).ToList();
                    }
                    else
                    {
                        validItems = DisplayService.Instance.StatisticMono.Where(i => i.IsChannelEnable).ToList();
                    }

                    if (validItems == null || validItems.Count == 0)
                    {
                        //this.Dispatcher.Invoke(() =>
                        //{
                        StatisticGrid.Items.Clear();
                        this.Close();
                        //}, System.Windows.Threading.DispatcherPriority.Input);
                        _isUpdating = false;
                        return;
                    }

                    //this.Dispatcher.Invoke(() =>
                    //{
                    var source = StatisticGrid.Items;
                    var count = source.Count;
                    var isSourceChanged = false;
                    //if (count == 0)
                    //{
                    //    foreach (var item in validItems)
                    //    {
                    //        source.Add(item);
                    //        isSourceChanged = true;
                    //    }
                    //}
                    //else
                    {
                        var removeItems = new List<object>();
                        for (int i = 0; i < count; i++)
                        {
                            var itemi = source[i];
                            if (!validItems.Contains(itemi))
                            {
                                removeItems.Add(itemi);
                            }
                        }

                        foreach (var item in removeItems)
                        {
                            source.Remove(item);
                            isSourceChanged = true;
                        }

                        int id = -1;
                        for (int i = 0; i < validItems.Count; i++)
                        {
                            var item = validItems[i];
                            if (item.Index != id)
                            {
                                id = item.Index;
                                item.ShowIndexAndColor = true;
                            }
                            else
                            {
                                item.ShowIndexAndColor = false;
                            }

                            if (i >= source.Count)
                            {
                                source.Add(item);
                                isSourceChanged = true;
                                continue;
                            }
                            var thisItem = (StatisticItem)source[i];
                            if (thisItem.Index == item.Index && thisItem.ChannelType == item.ChannelType)
                            {
                                //thisItem.Update(item);
                            }
                            else
                            {
                                source.Insert(i, item);
                                isSourceChanged = true;
                            }
                        }
                    }
                    if (isSourceChanged)
                        source.Refresh();

                    OnROISelected();
                }
                while (false);
                _isUpdating = false;
            }, System.Windows.Threading.DispatcherPriority.Input);
            //_isUpdating = false;
        }
    }

    public class GridViewBehavior : Infrastructure.GridViewBehavior<StatisticItem> { }
}
