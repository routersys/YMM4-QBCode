using System.Windows;
using System.Windows.Data;
using YukkuriMovieMaker.Commons;

namespace YMM4QRBarcodePlugin
{
    internal class ValidationPanelEditorAttribute : PropertyEditorAttribute2
    {
        public override FrameworkElement Create()
        {
            return new ValidationPanel();
        }

        public override void SetBindings(FrameworkElement control, ItemProperty[] itemProperties)
        {
            if (control is not ValidationPanel panel || itemProperties.Length == 0) return;

            var binding = new Binding
            {
                Source = itemProperties[0].PropertyOwner,
                Mode = BindingMode.OneWay
            };
            panel.SetBinding(ValidationPanel.ParameterSetProperty, binding);
        }

        public override void ClearBindings(FrameworkElement control)
        {
            BindingOperations.ClearBinding(control, ValidationPanel.ParameterSetProperty);
        }
    }
}