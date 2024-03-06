using Microsoft.Xaml.Behaviors;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows;
using Telerik.Windows.Controls;

namespace Viewport.Infrastructure
{
    public class GridViewBehavior<T> : Behavior<RadGridView>
    {
        private bool modelHandle;
        private bool viewHandle;

        public static readonly DependencyProperty SelectedItemsProperty =
            DependencyProperty.Register(nameof(SelectedItems), typeof(ObservableCollection<T>), typeof(GridViewBehavior<T>),
                new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault, OnSelectedItemsChanged));

        public ObservableCollection<T> SelectedItems
        {
            get => (ObservableCollection<T>)GetValue(SelectedItemsProperty);
            set => SetValue(SelectedItemsProperty, value);
        }

        private static void OnSelectedItemsChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            var behavior = d as GridViewBehavior<T>;
            if (behavior == null)
            {
                throw new System.Exception("Could not transform the dependencyobject to Behavior");
            }

            if (e.OldValue != null
                && e.OldValue is ObservableCollection<T> collection)
            {
                collection.Clear();
                collection.CollectionChanged -= behavior.SelectedItems_CollectionChanged;
            }

            if (e.NewValue is ObservableCollection<T> newcollection)
            {
                newcollection.CollectionChanged += behavior.SelectedItems_CollectionChanged;
                behavior.SelectItems();
            }
        }

        protected virtual void SelectedItems_CollectionChanged(object sender, System.Collections.Specialized.NotifyCollectionChangedEventArgs e)
        {
            SelectItems();
        }

        private void SelectItems()
        {
            if (viewHandle)
                return;
            if (AssociatedObject == null)
                return;
            modelHandle = true;
            AssociatedObject.SelectedItems.Clear();//Notice: must execute on UIThread.
            if (SelectedItems != null)
            {
                foreach (var item in SelectedItems)
                {
                    AssociatedObject.SelectedItems.Add(item);
                }
            }
            modelHandle = false;
        }

        protected override void OnAttached()
        {
            base.OnAttached();
            AssociatedObject.SelectionChanged += AssociatedObject_SelectionChanged;
            SelectItems();
        }

        protected virtual void AssociatedObject_SelectionChanged(object sender, SelectionChangeEventArgs e)
        {
            if (modelHandle)//if model-items is handling
                return;
            if (AssociatedObject.Items.SourceCollection == null)
                return;
            if (SelectedItems == null)
                return;

            viewHandle = true;
            SelectedItems.Clear();
            var c = AssociatedObject.SelectedItems.Cast<T>().ToArray();
            SelectedItems.AddRange(c);
            viewHandle = false;
        }

        protected override void OnDetaching()
        {
            base.OnDetaching();
            if (AssociatedObject != null)
            {
                AssociatedObject.SelectionChanged -= AssociatedObject_SelectionChanged;
            }
        }
    }

}
