using Prism.Mvvm;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace Viewport.ViewModels
{
    public class ProfileViewModel : BindableBase
    {
        private Dictionary<string, PropertyInfo> _properties = new Dictionary<string, PropertyInfo>();

        #region Properties

        private int _lineWidth = 1;
        public int LineWidth
        {
            get => _lineWidth;
            set => SetProperty(ref _lineWidth, value);
        }

        #endregion      

        public PropertyInfo GetPropertyInfo(string propertyName)
        {
            PropertyInfo myPropInfo = null;
            if (!_properties.TryGetValue(propertyName, out myPropInfo))
            {
                myPropInfo = typeof(ProfileViewModel).GetProperty(propertyName);
                if (null != myPropInfo)
                {
                    _properties.Add(propertyName, myPropInfo);
                }
            }
            return myPropInfo;
        }
    }
}
