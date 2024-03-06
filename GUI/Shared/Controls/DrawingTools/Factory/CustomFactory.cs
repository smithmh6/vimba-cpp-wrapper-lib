using DrawingTool.Factory;
using DrawingTool.Factory.Material;
using DrawingTool.Factory.Products;

namespace FilterWheelShared.Controls.DrawingTools.Factory
{
    public class CustomFactory : GraphicFactory
    {
        public override GraphicBase GetGraphic(GraphicViewModelBase vm)
        {
            if (vm is Materials.CustomScalerViewModel csvm)
            {
                return new Products.GraphicCustomScaler() { DataContext = csvm };
            }
            if (vm is Materials.CustomProfileViewModel pvm)
            {
                return new Products.GraphicProfile { DataContext = pvm };
            }
            if (vm is Materials.CustomRulerViewModel rvm)
            {
                return new Products.GraphicCustomRuler() { DataContext = rvm, StringFormat = "{0:0.00}" };
            }
            return base.GetGraphic(vm);
        }
    }

}
