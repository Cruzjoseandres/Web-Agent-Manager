using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace WebAgentManagerApp
{
    public static class WpfControlHelper
    {
        public static Button CreateStyledButton(string text, string hexColor, bool isPrimary)
        {
            Button btn = new Button
            {
                Content = text,
                Cursor = Cursors.Hand,
                FontSize = 13,
                FontWeight = FontWeights.SemiBold,
                Padding = new Thickness(12, 6, 12, 6),
                BorderThickness = new Thickness(1),
                BorderBrush = new SolidColorBrush((Color)ColorConverter.ConvertFromString(hexColor))
            };

            if (isPrimary)
            {
                btn.Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString(hexColor));
                btn.Foreground = Brushes.White;
            }
            else
            {
                btn.Background = Brushes.White;
                btn.Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString(hexColor));
            }

            var style = new Style(typeof(Button));
            var template = new ControlTemplate(typeof(Button));
            var elemFactory = new FrameworkElementFactory(typeof(Border));
            elemFactory.Name = "border";
            elemFactory.SetValue(Border.CornerRadiusProperty, new CornerRadius(6));
            elemFactory.SetValue(Border.BackgroundProperty, new TemplateBindingExtension(Button.BackgroundProperty));
            elemFactory.SetValue(Border.BorderBrushProperty, new TemplateBindingExtension(Button.BorderBrushProperty));
            elemFactory.SetValue(Border.BorderThicknessProperty, new TemplateBindingExtension(Button.BorderThicknessProperty));

            var contentFactory = new FrameworkElementFactory(typeof(ContentPresenter));
            contentFactory.SetValue(ContentPresenter.HorizontalAlignmentProperty, HorizontalAlignment.Center);
            contentFactory.SetValue(ContentPresenter.VerticalAlignmentProperty, VerticalAlignment.Center);
            contentFactory.SetValue(ContentPresenter.MarginProperty, new TemplateBindingExtension(Button.PaddingProperty));
            elemFactory.AppendChild(contentFactory);

            template.VisualTree = elemFactory;

            var triggerOver = new Trigger { Property = Button.IsMouseOverProperty, Value = true };
            triggerOver.Setters.Add(new Setter { TargetName = "border", Property = Border.OpacityProperty, Value = 0.85 });
            template.Triggers.Add(triggerOver);

            style.Setters.Add(new Setter(Button.TemplateProperty, template));
            btn.Style = style;

            return btn;
        }
    }
}
