using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace WebAgentManagerApp
{
    public static class ClientDialog
    {
        public static void Open(Window owner, ClientItem existingClient, List<ClientItem> clients, Action onSaved, Action<string> showToast)
        {
            bool isNew = (existingClient == null);
            ClientItem item = isNew ? new ClientItem() : existingClient;

            Window dlg = new Window
            {
                Title = isNew ? "Crear Nuevo Cliente" : "Editar Cliente",
                Width = 460,
                Height = 280,
                WindowStartupLocation = WindowStartupLocation.CenterOwner,
                Owner = owner,
                ResizeMode = ResizeMode.NoResize,
                Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#F8F9FA")),
                FontFamily = new FontFamily("Segoe UI")
            };

            StackPanel form = new StackPanel { Margin = new Thickness(24) };

            form.Children.Add(new TextBlock
            {
                Text = isNew ? "Registrar Nuevo Cliente / Empresa" : "Actualizar Cliente",
                FontSize = 18,
                FontWeight = FontWeights.Bold,
                Margin = new Thickness(0, 0, 0, 14)
            });

            form.Children.Add(new TextBlock
            {
                Text = "Nombre del Cliente / Empresa (ej. Maprial, Totalpec):",
                FontWeight = FontWeights.SemiBold,
                FontSize = 12,
                Margin = new Thickness(0, 0, 0, 4)
            });
            TextBox txtName = new TextBox
            {
                Text = item.Name ?? "",
                Padding = new Thickness(8, 6, 8, 6),
                FontSize = 13,
                Margin = new Thickness(0, 0, 0, 10)
            };
            form.Children.Add(txtName);

            form.Children.Add(new TextBlock
            {
                Text = "Descripción / Notas generales (opcional):",
                FontWeight = FontWeights.SemiBold,
                FontSize = 12,
                Margin = new Thickness(0, 0, 0, 4)
            });
            TextBox txtDesc = new TextBox
            {
                Text = item.Description ?? "",
                Padding = new Thickness(8, 6, 8, 6),
                FontSize = 13,
                Margin = new Thickness(0, 0, 0, 16)
            };
            form.Children.Add(txtDesc);

            StackPanel btnRow = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right };

            Button btnCancel = WpfControlHelper.CreateStyledButton("Cancelar", "#6C757D", false);
            btnCancel.Click += (s, e) => dlg.Close();
            btnRow.Children.Add(btnCancel);

            Button btnSave = WpfControlHelper.CreateStyledButton(isNew ? "Crear Cliente" : "Guardar Cambios", "#0D6EFD", true);
            btnSave.Margin = new Thickness(10, 0, 0, 0);
            btnSave.Click += (s, e) =>
            {
                if (string.IsNullOrWhiteSpace(txtName.Text))
                {
                    MessageBox.Show("Debes ingresar un nombre para el cliente.", "Campo requerido", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                item.Name = txtName.Text.Trim();
                item.Description = txtDesc.Text.Trim();

                if (isNew) clients.Add(item);
                if (onSaved != null) onSaved();
                if (showToast != null) showToast(isNew ? "Cliente '" + item.Name + "' creado exitosamente." : "Cambios guardados en '" + item.Name + "'.");
                dlg.Close();
            };
            btnRow.Children.Add(btnSave);

            form.Children.Add(btnRow);
            dlg.Content = form;
            dlg.ShowDialog();
        }
    }
}

