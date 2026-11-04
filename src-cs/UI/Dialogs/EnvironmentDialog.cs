using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace WebAgentManagerApp
{
    public static class EnvironmentDialog
    {
        public static void Open(Window owner, ClientItem client, EnvironmentItem existingEnv, List<WebAgentVersion> detectedVersions, Action onSaved, Action<string> showToast)
        {
            bool isNew = (existingEnv == null);
            EnvironmentItem item = isNew ? new EnvironmentItem() : existingEnv;

            // Si es un ambiente nuevo y el cliente ya tiene otros ambientes, heredar IP, esquema y versión
            EnvironmentItem refEnv = null;
            if (isNew && client.Environments != null && client.Environments.Count > 0)
            {
                refEnv = client.Environments.LastOrDefault(e => !string.IsNullOrWhiteSpace(e.ServerIp)) ?? client.Environments[0];
            }

            UiPresentationHelper.ServerAddressParts initialParts;
            if (!isNew)
            {
                initialParts = UiPresentationHelper.ParseServerAddress(item.ServerIp);
            }
            else if (refEnv != null)
            {
                initialParts = UiPresentationHelper.ParseServerAddress(refEnv.ServerIp);
                initialParts.WebPort = ""; // usuario solo ingresa el nuevo puerto
            }
            else
            {
                initialParts = new UiPresentationHelper.ServerAddressParts { IsHttps = false, Host = "", WebPort = "" };
            }

            Window dlg = new Window
            {
                Title = isNew ? "Agregar Ambiente a " + client.Name : "Editar Ambiente de " + client.Name,
                Width = 510,
                Height = 650,
                WindowStartupLocation = WindowStartupLocation.CenterOwner,
                Owner = owner,
                ResizeMode = ResizeMode.NoResize,
                Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#F8F9FA")),
                FontFamily = new FontFamily("Segoe UI")
            };

            StackPanel form = new StackPanel { Margin = new Thickness(24) };

            form.Children.Add(new TextBlock
            {
                Text = isNew ? "Nuevo Ambiente para " + client.Name : "Editar Ambiente de " + client.Name,
                FontSize = 18,
                FontWeight = FontWeights.Bold,
                Margin = new Thickness(0, 0, 0, 14)
            });

            form.Children.Add(new TextBlock
            {
                Text = "Nombre del Ambiente (ej. Producción, Desarrollo, QA):",
                FontWeight = FontWeights.SemiBold,
                FontSize = 12,
                Margin = new Thickness(0, 0, 0, 4)
            });
            TextBox txtName = new TextBox
            {
                Text = item.Name ?? "Producción",
                Padding = new Thickness(8, 6, 8, 6),
                FontSize = 13,
                Margin = new Thickness(0, 0, 0, 10)
            };
            form.Children.Add(txtName);

            // Campos separados: IP/Host del Servidor y Puerto Web (Servidor)
            Grid srvGrid = new Grid { Margin = new Thickness(0, 0, 0, 6) };
            srvGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(2.2, GridUnitType.Star) });
            srvGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(10, GridUnitType.Pixel) });
            srvGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1.1, GridUnitType.Star) });

            StackPanel hostCol = new StackPanel();
            hostCol.Children.Add(new TextBlock { Text = "IP / Host del Servidor:", FontWeight = FontWeights.SemiBold, FontSize = 12, Margin = new Thickness(0, 0, 0, 4) });
            TextBox txtHost = new TextBox { Text = initialParts.Host ?? "", Padding = new Thickness(8, 6, 8, 6), FontSize = 13, ToolTip = "Dirección IP o host del servidor Protheus (ej. 192.168.0.10 o erp.empresa.com)" };
            hostCol.Children.Add(txtHost);
            Grid.SetColumn(hostCol, 0);
            srvGrid.Children.Add(hostCol);

            StackPanel portCol = new StackPanel();
            portCol.Children.Add(new TextBlock { Text = "Puerto Web:", FontWeight = FontWeights.SemiBold, FontSize = 12, Margin = new Thickness(0, 0, 0, 4) });
            TextBox txtWebPort = new TextBox { Text = initialParts.WebPort ?? "", Padding = new Thickness(8, 6, 8, 6), FontSize = 13, ToolTip = "Puerto del servicio WebApp / Broker (ej. 50200, 50101, 30008)" };
            portCol.Children.Add(txtWebPort);
            Grid.SetColumn(portCol, 2);
            srvGrid.Children.Add(portCol);

            form.Children.Add(srvGrid);

            CheckBox chkHttps = new CheckBox
            {
                Content = "Usar conexión segura HTTPS (SSL)",
                IsChecked = initialParts.IsHttps,
                FontSize = 12,
                FontWeight = FontWeights.SemiBold,
                Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#343A40")),
                Margin = new Thickness(0, 0, 0, 6)
            };
            form.Children.Add(chkHttps);

            Border previewBox = new Border
            {
                Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#E9ECEF")),
                CornerRadius = new CornerRadius(6),
                Padding = new Thickness(10, 6, 10, 6),
                Margin = new Thickness(0, 0, 0, 10)
            };
            TextBlock lblUrlPreview = new TextBlock
            {
                FontSize = 11,
                Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#0D6EFD")),
                FontFamily = new FontFamily("Consolas, Segoe UI"),
                TextTrimming = TextTrimming.CharacterEllipsis
            };
            previewBox.Child = lblUrlPreview;
            form.Children.Add(previewBox);

            Action updatePreview = () => {
                string built = UiPresentationHelper.BuildServerAddress(chkHttps.IsChecked == true, txtHost.Text, txtWebPort.Text);
                lblUrlPreview.Text = "Enlace resultante: " + (string.IsNullOrEmpty(built) ? "(Ingresa IP y Puerto)" : built);
            };
            updatePreview();

            form.Children.Add(new TextBlock { Text = "Puerto(s) de WebAgent (se auto-calcula, o pon el del appserver.ini):", FontWeight = FontWeights.SemiBold, FontSize = 12, Margin = new Thickness(0, 0, 0, 4) });
            TextBox txtPorts = new TextBox { Text = item.AgentPort ?? "", Padding = new Thickness(8, 6, 8, 6), FontSize = 13, Margin = new Thickness(0, 0, 0, 6) };
            form.Children.Add(txtPorts);

            TextBlock lblPortTip = new TextBlock
            {
                Text = "\u2139\uFE0F Protheus calcula el agente sumando 41 a los primeros 3 dígitos (ej: 50200 ➔ 50241, 50101 ➔ 50141). Si en appserver.ini definieron [WebAgent] Port=, puedes escribirlo manualmente aquí.",
                FontSize = 11,
                Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#6C757D")),
                TextWrapping = TextWrapping.Wrap,
                Margin = new Thickness(0, 0, 0, 10)
            };
            form.Children.Add(lblPortTip);

            bool autoCalculated = string.IsNullOrWhiteSpace(txtPorts.Text);
            txtWebPort.TextChanged += (s, e) => {
                updatePreview();
                if (autoCalculated || string.IsNullOrWhiteSpace(txtPorts.Text))
                {
                    string p = txtWebPort.Text.Trim();
                    string pred = !string.IsNullOrEmpty(p) ? SmartRouter.PredictWebAgentPort(":" + p) : "";
                    if (!string.IsNullOrEmpty(pred))
                    {
                        txtPorts.Text = pred;
                        autoCalculated = true;
                    }
                }
            };
            txtHost.TextChanged += (s, e) => updatePreview();
            chkHttps.Click += (s, e) => updatePreview();
            txtPorts.TextChanged += (s, e) => {
                string p = txtWebPort.Text.Trim();
                string pred = !string.IsNullOrEmpty(p) ? SmartRouter.PredictWebAgentPort(":" + p) : "";
                if (!string.IsNullOrEmpty(pred) && txtPorts.Text != pred)
                {
                    autoCalculated = false;
                }
            };

            form.Children.Add(new TextBlock { Text = "Versión de WebAgent requerida por este ambiente:", FontWeight = FontWeights.SemiBold, FontSize = 12, Margin = new Thickness(0, 0, 0, 4) });
            ComboBox cboVersions = new ComboBox { Padding = new Thickness(8, 6, 8, 6), FontSize = 13, Margin = new Thickness(0, 0, 0, 10), IsEditable = true };

            var standardVersions = new string[] {
                "web-agent-1.1.1-x64",
                "web-agent-1.1.0-x64",
                "web-agent-1.0.25-x64",
                "web-agent-1.0.24-x64",
                "web-agent-1.0.23-x64",
                "web-agent-1.1.1-x86"
            };
            var versionSet = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var sv in standardVersions) versionSet.Add(sv);
            foreach (var v in (detectedVersions ?? new List<WebAgentVersion>())) versionSet.Add(v.FolderName);
            if (!string.IsNullOrEmpty(item.VersionFolder)) versionSet.Add(item.VersionFolder);

            foreach (var verName in versionSet)
            {
                cboVersions.Items.Add(verName);
            }
            if (!string.IsNullOrEmpty(item.VersionFolder))
            {
                cboVersions.Text = item.VersionFolder;
            }
            else if (refEnv != null && !string.IsNullOrEmpty(refEnv.VersionFolder))
            {
                cboVersions.Text = refEnv.VersionFolder;
            }
            else if (cboVersions.Items.Count > 0)
            {
                cboVersions.SelectedIndex = 0;
            }
            form.Children.Add(cboVersions);

            form.Children.Add(new TextBlock { Text = "Descripción / Notas adicionales (opcional):", FontWeight = FontWeights.SemiBold, FontSize = 12, Margin = new Thickness(0, 0, 0, 4) });
            TextBox txtDesc = new TextBox { Text = item.Description ?? "", Padding = new Thickness(8, 6, 8, 6), FontSize = 13, Margin = new Thickness(0, 0, 0, 16) };
            form.Children.Add(txtDesc);

            StackPanel btnRow = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right };

            Button btnCancel = WpfControlHelper.CreateStyledButton("Cancelar", "#6C757D", false);
            btnCancel.Click += (s, e) => dlg.Close();
            btnRow.Children.Add(btnCancel);

            Button btnSave = WpfControlHelper.CreateStyledButton(isNew ? "Agregar Ambiente" : "Guardar Cambios", "#0D6EFD", true);
            btnSave.Margin = new Thickness(10, 0, 0, 0);
            btnSave.Click += (s, e) => {
                if (string.IsNullOrWhiteSpace(txtName.Text))
                {
                    MessageBox.Show("Debes ingresar un nombre para el ambiente (ej. Producción o Desarrollo).", "Campo requerido", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }
                string finalServerUrl = UiPresentationHelper.BuildServerAddress(
                    chkHttps.IsChecked == true,
                    txtHost.Text,
                    txtWebPort.Text
                );
                if (string.IsNullOrWhiteSpace(finalServerUrl))
                {
                    MessageBox.Show("Debes ingresar al menos la IP o Host del servidor y el Puerto Web.", "Campos requeridos", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }
                string chosenVer = cboVersions.Text.Trim();
                if (string.IsNullOrWhiteSpace(chosenVer))
                {
                    MessageBox.Show("Debes seleccionar o escribir la versión de WebAgent requerida.", "Campo requerido", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                item.Name = txtName.Text.Trim();
                item.ServerIp = finalServerUrl;
                item.AgentPort = txtPorts.Text.Trim();
                item.VersionFolder = chosenVer;
                item.Description = txtDesc.Text.Trim();

                if (client.Environments == null) client.Environments = new List<EnvironmentItem>();
                if (isNew) client.Environments.Add(item);

                if (onSaved != null) onSaved();
                if (showToast != null) showToast(isNew ? "Ambiente '" + item.Name + "' agregado a " + client.Name + "." : "Cambios guardados en '" + item.Name + "'.");
                dlg.Close();
            };
            btnRow.Children.Add(btnSave);

            form.Children.Add(btnRow);
            dlg.Content = form;
            dlg.ShowDialog();
        }
    }
}

