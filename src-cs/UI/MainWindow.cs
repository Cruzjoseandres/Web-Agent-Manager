using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using Microsoft.Win32;

namespace WebAgentManagerApp
{
    public class MainWindow : Window
    {
        private string exeDir;
        private string localRoot;
        private string versionsDir;
        private string configFile;
        private string toolsDir;

        private List<ClientItem> clients = new List<ClientItem>();
        private List<WebAgentVersion> detectedVersions = new List<WebAgentVersion>();
        private List<PortProcessHelper.PortOwnerInfo> lastActivePorts = new List<PortProcessHelper.PortOwnerInfo>();
        private readonly HashSet<string> collapsedClientKeys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        private DispatcherTimer statusTimer;

        private class EnvVisualBinding
        {
            public string ClientId;
            public string EnvId;
            public EnvironmentItem Env;
            public Border CardBorder;
            public Border StatusPill;
            public TextBlock StatusTxt;
            public Button ActionBtn;
            public bool LastRunning;
            public int EnvPort;
        }

        private readonly List<EnvVisualBinding> envBindings = new List<EnvVisualBinding>();
        private bool isUpdatingLiveStatus = false;

        // UI Controls
        private Border statusIconCircle;
        private TextBlock statusIconText;
        private TextBlock txtActiveBadge;
        private TextBlock txtActiveDetails;
        private StackPanel cardsPanel;
        private TextBlock txtToast;
        private TextBox txtSearch;
        private TextBlock txtSearchSummary;

        public MainWindow()
        {
            InitializePaths();
            InitializeWindowLayout();
            InstallerExtractor.CleanupRogueGlobalInstall(versionsDir);
            ScanAllVersions();
            LoadClients();
            SyncRegistryWithActiveClients();
            RefreshUI();
            StartStatusTimer();
        }

        private void StartStatusTimer()
        {
            statusTimer = new DispatcherTimer();
            statusTimer.Interval = TimeSpan.FromSeconds(1.0);
            statusTimer.Tick += async (s, e) =>
            {
                await UpdateAllLiveStatusesAsync();
            };
            statusTimer.Start();
        }

        private async System.Threading.Tasks.Task UpdateAllLiveStatusesAsync()
        {
            if (isUpdatingLiveStatus) return;
            isUpdatingLiveStatus = true;
            try
            {
                SyncRegistryWithActiveClients();
                var activePorts = await System.Threading.Tasks.Task.Run(() => PortProcessHelper.GetWebAgentPorts());
                lastActivePorts = activePorts ?? new List<PortProcessHelper.PortOwnerInfo>();
                UpdateStatusBannerWithPorts(activePorts);
                UpdateEnvironmentCardsWithPorts(activePorts);
            }
            catch { }
            finally
            {
                isUpdatingLiveStatus = false;
            }
        }

        private void InitializePaths()
        {
            localRoot = PathHelper.GetDataRoot();
            exeDir = PathHelper.GetExeDir();

            versionsDir = PathHelper.GetVersionsDir();
            configFile = PathHelper.GetConfigFile();
            toolsDir = Path.Combine(localRoot, "tools");

            if (!Directory.Exists(versionsDir)) Directory.CreateDirectory(versionsDir);
            if (!Directory.Exists(Path.Combine(localRoot, "config"))) Directory.CreateDirectory(Path.Combine(localRoot, "config"));
            if (!Directory.Exists(toolsDir)) Directory.CreateDirectory(toolsDir);

            string portableCfg = Path.Combine(exeDir, "config", "clientes.json");
            if (!File.Exists(configFile) && File.Exists(portableCfg) &&
                !string.Equals(portableCfg, configFile, StringComparison.OrdinalIgnoreCase))
            {
                try { File.Copy(portableCfg, configFile); } catch { }
            }

            PathHelper.EnsureEmbeddedTools(toolsDir);
            PathHelper.GetPermanentExePath();
        }

        private void InitializeWindowLayout()
        {
            Title = "TOTVS WebAgent Manager";
            Width = 920;
            Height = 720;
            MinWidth = 760;
            MinHeight = 560;
            WindowStartupLocation = WindowStartupLocation.CenterScreen;
            Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#F8F9FA"));
            FontFamily = new FontFamily("Segoe UI");

            try
            {
                using (Stream iconStream = Assembly.GetExecutingAssembly().GetManifestResourceStream("app.ico"))
                {
                    if (iconStream != null)
                    {
                        Icon = BitmapFrame.Create(iconStream, BitmapCreateOptions.PreservePixelFormat, BitmapCacheOption.OnLoad);
                    }
                }
            }
            catch { }

            Grid mainGrid = new Grid();
            mainGrid.Margin = new Thickness(20);
            mainGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto }); // Header
            mainGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto }); // Status Banner
            mainGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto }); // Toolbar
            mainGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) }); // Cards List
            mainGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto }); // Footer

            // 1. ENCABEZADO
            Border headerBorder = new Border
            {
                Background = Brushes.White,
                CornerRadius = new CornerRadius(10),
                Padding = new Thickness(20, 15, 20, 15),
                Margin = new Thickness(0, 0, 0, 14),
                BorderBrush = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#E9ECEF")),
                BorderThickness = new Thickness(1)
            };
            StackPanel headerStack = new StackPanel();
            TextBlock titleTxt = new TextBlock
            {
                Text = "TOTVS Protheus - Gestor de WebAgent",
                FontSize = 20,
                FontWeight = FontWeights.Bold,
                Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#212529"))
            };
            TextBlock subTxt = new TextBlock
            {
                Text = "Administra tus clientes y sus ambientes (Producción, Desarrollo, QA), con detección en tiempo real de los procesos de Windows.",
                FontSize = 13,
                Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#6C757D")),
                Margin = new Thickness(0, 3, 0, 0)
            };
            headerStack.Children.Add(titleTxt);
            headerStack.Children.Add(subTxt);
            headerBorder.Child = headerStack;
            Grid.SetRow(headerBorder, 0);
            mainGrid.Children.Add(headerBorder);

            // 2. BANNER DE ESTADO ACTUAL (TIEMPO REAL EN WINDOWS)
            Border statusBorder = new Border
            {
                Background = Brushes.White,
                CornerRadius = new CornerRadius(10),
                Padding = new Thickness(18, 14, 18, 14),
                Margin = new Thickness(0, 0, 0, 14),
                BorderBrush = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#E9ECEF")),
                BorderThickness = new Thickness(1)
            };
            Grid statusGrid = new Grid();
            statusGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            statusGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            statusGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            statusIconCircle = new Border
            {
                Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#D1E7DD")),
                CornerRadius = new CornerRadius(20),
                Width = 40,
                Height = 40,
                Margin = new Thickness(0, 0, 14, 0)
            };
            statusIconText = new TextBlock
            {
                Text = "OK",
                FontWeight = FontWeights.Bold,
                FontSize = 12,
                Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#0F5132")),
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center
            };
            statusIconCircle.Child = statusIconText;
            Grid.SetColumn(statusIconCircle, 0);
            statusGrid.Children.Add(statusIconCircle);

            StackPanel statusInfo = new StackPanel { VerticalAlignment = VerticalAlignment.Center };
            StackPanel line1 = new StackPanel { Orientation = Orientation.Horizontal };
            line1.Children.Add(new TextBlock { Text = "Agentes en Windows: ", FontSize = 13, Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#495057")) });
            txtActiveBadge = new TextBlock { Text = "Cargando...", FontSize = 14, FontWeight = FontWeights.Bold, Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#0F5132")) };
            line1.Children.Add(txtActiveBadge);
            statusInfo.Children.Add(line1);

            txtActiveDetails = new TextBlock
            {
                Text = "Verificando procesos y puertos en Windows...",
                FontSize = 12,
                Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#6C757D")),
                Margin = new Thickness(0, 2, 0, 0)
            };
            statusInfo.Children.Add(txtActiveDetails);
            Grid.SetColumn(statusInfo, 1);
            statusGrid.Children.Add(statusInfo);

            Button btnRefresh = CreateStyledButton("\u21BB Actualizar", "#6C757D", false);
            btnRefresh.Click += (s, e) => { ScanAllVersions(); RefreshUI(); ShowToast("Estado actualizado."); };
            Grid.SetColumn(btnRefresh, 2);
            statusGrid.Children.Add(btnRefresh);

            statusBorder.Child = statusGrid;
            Grid.SetRow(statusBorder, 1);
            mainGrid.Children.Add(statusBorder);

            // 3. BUSCADOR Y BARRA DE BOTONES (TOOLBAR)
            StackPanel toolbarSection = new StackPanel
            {
                Margin = new Thickness(0, 0, 0, 14)
            };

            Border searchBorder = new Border
            {
                Background = Brushes.White,
                CornerRadius = new CornerRadius(8),
                Padding = new Thickness(12, 9, 12, 9),
                Margin = new Thickness(0, 0, 0, 10),
                BorderBrush = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#DEE2E6")),
                BorderThickness = new Thickness(1)
            };
            Grid searchGrid = new Grid();
            searchGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            searchGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            searchGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            searchGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            TextBlock searchLabel = new TextBlock
            {
                Text = "Buscar:",
                FontSize = 13,
                FontWeight = FontWeights.SemiBold,
                Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#343A40")),
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(0, 0, 10, 0)
            };
            Grid.SetColumn(searchLabel, 0);
            searchGrid.Children.Add(searchLabel);

            txtSearch = new TextBox
            {
                FontSize = 13,
                Padding = new Thickness(9, 6, 9, 6),
                MaxLength = 200,
                ToolTip = "Busca por nombre de cliente, ambiente o dirección IP/URL."
            };
            Grid.SetColumn(txtSearch, 1);
            searchGrid.Children.Add(txtSearch);

            Button btnClearSearch = CreateStyledButton("Limpiar", "#6C757D", false);
            btnClearSearch.Margin = new Thickness(8, 0, 0, 0);
            btnClearSearch.IsEnabled = false;
            btnClearSearch.Opacity = 0.55;
            btnClearSearch.Click += (s, e) =>
            {
                txtSearch.Clear();
                txtSearch.Focus();
            };
            Grid.SetColumn(btnClearSearch, 2);
            searchGrid.Children.Add(btnClearSearch);

            txtSearchSummary = new TextBlock
            {
                Text = "Todos los clientes",
                FontSize = 12,
                Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#6C757D")),
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(12, 0, 0, 0)
            };
            Grid.SetColumn(txtSearchSummary, 3);
            searchGrid.Children.Add(txtSearchSummary);

            txtSearch.TextChanged += (s, e) =>
            {
                bool hasSearch = !string.IsNullOrWhiteSpace(txtSearch.Text);
                btnClearSearch.IsEnabled = hasSearch;
                btnClearSearch.Opacity = hasSearch ? 1.0 : 0.55;
                RefreshUI(false);
            };

            searchBorder.Child = searchGrid;
            toolbarSection.Children.Add(searchBorder);

            WrapPanel toolbar = new WrapPanel
            {
                Orientation = Orientation.Horizontal
            };

            Button btnNewClient = CreateStyledButton("+ Nuevo Cliente", "#0D6EFD", true);
            btnNewClient.Padding = new Thickness(16, 9, 16, 9);
            btnNewClient.Click += (s, e) => OpenClientDialog(null);
            toolbar.Children.Add(btnNewClient);

            Button btnScan = CreateStyledButton("\U0001F50D Escanear Equipo", "#495057", false);
            btnScan.Margin = new Thickness(10, 0, 0, 0);
            btnScan.Click += (s, e) => {
                int found = ScanAllVersions();
                RefreshUI();
                MessageBox.Show("Se encontraron " + detectedVersions.Count + " versiones de WebAgent listas y protegidas.", "Escaneo Completo", MessageBoxButton.OK, MessageBoxImage.Information);
            };
            toolbar.Children.Add(btnScan);

            Button btnImport = CreateStyledButton("\U0001F4E5 Importar Instalador (.exe)", "#198754", false);
            btnImport.Margin = new Thickness(10, 0, 0, 0);
            btnImport.Click += (s, e) => ImportInstallerManual();
            toolbar.Children.Add(btnImport);

            Button btnExport = CreateStyledButton("\U0001F4E4 Exportar Config (TI)", "#495057", false);
            btnExport.Margin = new Thickness(10, 0, 0, 0);
            btnExport.ToolTip = "Exporta todos los clientes y ambientes a un archivo JSON para compartir con el equipo.";
            btnExport.Click += (s, e) => ExportConfiguration();
            toolbar.Children.Add(btnExport);

            Button btnImportCfg = CreateStyledButton("\U0001F4C2 Importar Config", "#495057", false);
            btnImportCfg.Margin = new Thickness(10, 0, 0, 0);
            btnImportCfg.ToolTip = "Importa un archivo JSON de clientes y ambientes generado por TI o compañeros.";
            btnImportCfg.Click += (s, e) => ImportConfiguration();
            toolbar.Children.Add(btnImportCfg);

            Button btnKillIdle = CreateStyledButton("\U0001F9F9 Limpiar Puertos Libres", "#6C757D", false);
            btnKillIdle.Margin = new Thickness(10, 0, 0, 0);
            btnKillIdle.ToolTip = "Cierra procesos en espera sin desconectar los que tienen una pestaña activa.";
            btnKillIdle.Click += (s, e) => {
                int killed = PortProcessHelper.KillUnconnectedWebAgents();
                RefreshUI();
                ShowToast("Se limpiaron " + killed + " procesos inactivos.");
            };
            toolbar.Children.Add(btnKillIdle);

            toolbarSection.Children.Add(toolbar);
            Grid.SetRow(toolbarSection, 2);
            mainGrid.Children.Add(toolbarSection);

            // 4. LISTA DE TARJETAS DE CLIENTES Y AMBIENTES
            ScrollViewer scroll = new ScrollViewer
            {
                VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
                HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
                PanningMode = PanningMode.VerticalOnly,
                Margin = new Thickness(0, 0, 0, 14)
            };
            cardsPanel = new StackPanel { Orientation = Orientation.Vertical };
            scroll.Content = cardsPanel;
            Grid.SetRow(scroll, 3);
            mainGrid.Children.Add(scroll);

            // 5. BARRA INFERIOR (FOOTER)
            Border footerBorder = new Border
            {
                Background = Brushes.White,
                CornerRadius = new CornerRadius(10),
                Padding = new Thickness(16, 10, 16, 10),
                BorderBrush = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#E9ECEF")),
                BorderThickness = new Thickness(1)
            };
            Grid footerGrid = new Grid();
            footerGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            footerGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            txtToast = new TextBlock
            {
                Text = "Listo para operar.",
                FontSize = 12,
                Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#495057")),
                VerticalAlignment = VerticalAlignment.Center
            };
            Grid.SetColumn(txtToast, 0);
            footerGrid.Children.Add(txtToast);

            Button btnKillAll = CreateStyledButton("\u26A1 Detener Todos los Agentes", "#DC3545", false);
            btnKillAll.ToolTip = "Finaliza todos los procesos web-agent.exe que se estén ejecutando en Windows.";
            btnKillAll.Click += (s, e) => {
                var res = MessageBox.Show("¿Deseas cerrar todos los procesos de WebAgent que se estén ejecutando en Windows?", "Confirmar detención total", MessageBoxButton.YesNo, MessageBoxImage.Warning);
                if (res == MessageBoxResult.Yes)
                {
                    StopAllWebAgents();
                    RefreshUI();
                    ShowToast("Todos los agentes en Windows fueron detenidos.");
                }
            };
            Grid.SetColumn(btnKillAll, 1);
            footerGrid.Children.Add(btnKillAll);

            footerBorder.Child = footerGrid;
            Grid.SetRow(footerBorder, 4);
            mainGrid.Children.Add(footerBorder);

            Content = mainGrid;
        }

        private Button CreateStyledButton(string text, string hexColor, bool isPrimary)
        {
            return WpfControlHelper.CreateStyledButton(text, hexColor, isPrimary);
        }

        // ================= ESCANEO Y GESTIÓN DE VERSIONES =================
        public int ScanAllVersions()
        {
            detectedVersions = InstallerExtractor.ScanAllVersions(versionsDir, exeDir, toolsDir);
            return detectedVersions.Count;
        }

        private void ImportInstallerManual()
        {
            OpenFileDialog dlg = new OpenFileDialog();
            dlg.Title = "Seleccionar Instalador Oficial de WebAgent (.exe)";
            dlg.Filter = "Instaladores WebAgent (*.exe)|*.exe|Todos los archivos (*.*)|*.*";
            if (dlg.ShowDialog() == true)
            {
                string folderName = "";
                Match m = Regex.Match(Path.GetFileName(dlg.FileName), @"web-agent-([0-9.]+)-windows-(x64|x86)", RegexOptions.IgnoreCase);
                if (m.Success)
                {
                    folderName = "web-agent-" + m.Groups[1].Value + "-" + m.Groups[2].Value.ToLower();
                }
                else
                {
                    folderName = "web-agent-importado-" + DateTime.Now.ToString("yyyyMMdd");
                }

                bool ok = InstallerExtractor.ExtractInstaller(toolsDir, versionsDir, dlg.FileName, folderName);
                if (ok)
                {
                    ScanAllVersions();
                    RefreshUI();
                    MessageBox.Show("La versión " + folderName + " fue importada exitosamente y ya está disponible en tu lista.", "Importación Exitosa", MessageBoxButton.OK, MessageBoxImage.Information);
                }
                else
                {
                    MessageBox.Show("No se pudo extraer el instalador seleccionado. Asegúrate de que sea un archivo de instalación oficial de WebAgent.", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }

        // ================= GESTIÓN DE CLIENTES Y AMBIENTES =================
        private void LoadClients()
        {
            clients = ConfigService.LoadClients(configFile);
        }

        public void SaveClients()
        {
            try
            {
                ConfigService.SaveClients(configFile, clients);
            }
            catch (Exception ex)
            {
                MessageBox.Show("Error al guardar clientes: " + ex.Message, "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void ExportConfiguration()
        {
            try
            {
                SaveFileDialog sfd = new SaveFileDialog
                {
                    Title = "Exportar Configuración de Ambientes Protheus (TI)",
                    FileName = "ambientes_protheus_webagent.json",
                    Filter = "Archivo JSON (*.json)|*.json|Todos los archivos (*.*)|*.*",
                    DefaultExt = ".json"
                };
                if (sfd.ShowDialog() == true)
                {
                    ConfigService.ExportConfiguration(clients, sfd.FileName);
                    ShowToast("Configuración exportada correctamente.");
                    MessageBox.Show("Configuración de ambientes exportada exitosamente a:\n" + sfd.FileName + "\n\nEste archivo contiene todos los clientes, ambientes, versiones y puertos.\nPuedes compartirlo con el equipo para que lo importen con 1 clic.", "Exportación Exitosa", MessageBoxButton.OK, MessageBoxImage.Information);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show("Error al exportar configuración: " + ex.Message, "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void ImportConfiguration()
        {
            try
            {
                OpenFileDialog ofd = new OpenFileDialog
                {
                    Title = "Importar Configuración de Ambientes Protheus",
                    Filter = "Archivo JSON (*.json)|*.json|Todos los archivos (*.*)|*.*"
                };
                if (ofd.ShowDialog() == true)
                {
                    int addedClients, addedEnvs, updatedEnvs;
                    ConfigService.ImportConfiguration(ofd.FileName, clients, out addedClients, out addedEnvs, out updatedEnvs);

                    SaveClients();
                    SyncRegistryWithActiveClients();
                    RefreshUI();
                    ShowToast("Configuración importada exitosamente.");
                    MessageBox.Show(string.Format("Configuración importada exitosamente:\n- {0} cliente(s) nuevo(s)\n- {1} ambiente(s) nuevo(s)\n- {2} ambiente(s) actualizados\n\nTodos los puertos y versiones ya quedaron listos.", addedClients, addedEnvs, updatedEnvs), "Importación Exitosa", MessageBoxButton.OK, MessageBoxImage.Information);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show("Error al importar configuración: " + ex.Message, "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void SyncRegistryWithActiveClients()
        {
            RegistryService.SyncRegistryWithActiveClients(clients);
        }

        public async void StartEnvironment(EnvironmentItem env)
        {
            string targetFolder = env.VersionFolder;
            string targetExe = SmartRouter.ResolveVersionExecutable(targetFolder);
            if (string.IsNullOrEmpty(targetExe) || !File.Exists(targetExe))
            {
                MessageBox.Show("No se encontró el ejecutable de la versión: " + targetFolder + ".\nUsa el botón 'Importar Instalador (.exe)' para agregarla.", "Versión no encontrada", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            BinaryPatcher.DisableAutoUpdate(targetExe);

            int port = 0;
            int.TryParse(env.AgentPort, out port);
            if (port <= 0)
            {
                string pred = SmartRouter.PredictWebAgentPort(env.ServerIp);
                int.TryParse(pred, out port);
            }
            if (port <= 0) port = 21021;

            // 1. Limpiar cualquier WebAgent rogue (ej: el que inicia por defecto en el puerto 21021 o desde Programs\web-agent)
            var allRunningPorts = PortProcessHelper.GetWebAgentPorts();
            foreach (var rogue in allRunningPorts)
            {
                if (rogue.Port == 21021 || (!string.IsNullOrEmpty(rogue.ExecutablePath) && rogue.ExecutablePath.IndexOf("Programs\\web-agent\\", StringComparison.OrdinalIgnoreCase) >= 0))
                {
                    PortProcessHelper.KillProcessByIdSafe(rogue.Pid);
                }
            }

            // 2. Si ya hay un proceso en este puerto específico
            allRunningPorts = PortProcessHelper.GetWebAgentPorts();
            var existing = allRunningPorts.FirstOrDefault(p => p.Port == port);
            if (existing != null)
            {
                if (string.Equals(existing.ExecutablePath, targetExe, StringComparison.OrdinalIgnoreCase))
                {
                    ShowToast("El agente ya está en ejecución en el puerto " + port + ".");
                    await UpdateAllLiveStatusesAsync();
                    return;
                }
                PortProcessHelper.KillProcessByIdSafe(existing.Pid);
            }

            try
            {
                Process.Start(new ProcessStartInfo
                {
                    FileName = targetExe,
                    Arguments = "web-agent:?port=" + port,
                    WorkingDirectory = Path.GetDirectoryName(targetExe),
                    UseShellExecute = true
                });
                ShowToast("Iniciando WebAgent en puerto " + port + "...");
            }
            catch (Exception ex)
            {
                MessageBox.Show("Error al iniciar WebAgent: " + ex.Message, "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }

            await System.Threading.Tasks.Task.Delay(500);
            await UpdateAllLiveStatusesAsync();
        }

        public async void StopEnvironment(EnvironmentItem env)
        {
            int port = 0;
            int.TryParse(env.AgentPort, out port);
            if (port <= 0)
            {
                string pred = SmartRouter.PredictWebAgentPort(env.ServerIp);
                int.TryParse(pred, out port);
            }

            if (port > 0)
            {
                var ports = PortProcessHelper.GetWebAgentPorts();
                var existing = ports.FirstOrDefault(p => p.Port == port);
                if (existing != null)
                {
                    PortProcessHelper.KillProcessByIdSafe(existing.Pid);
                    ShowToast("WebAgent en puerto " + port + " detenido.");
                }
            }
            await System.Threading.Tasks.Task.Delay(300);
            await UpdateAllLiveStatusesAsync();
        }

        public async void StopAllWebAgents()
        {
            foreach (var proc in Process.GetProcessesByName("web-agent"))
            {
                try { proc.Kill(); proc.WaitForExit(1000); } catch { }
            }
            ShowToast("Todos los procesos de WebAgent han sido detenidos.");
            await System.Threading.Tasks.Task.Delay(300);
            await UpdateAllLiveStatusesAsync();
        }

        // ================= VISTA Y TARJETAS EN TIEMPO REAL =================
        private void UpdateStatusBannerWithPorts(List<PortProcessHelper.PortOwnerInfo> activePorts)
        {
            if (activePorts == null) activePorts = new List<PortProcessHelper.PortOwnerInfo>();
            var runningEnvsList = new List<string>();

            foreach (var po in activePorts)
            {
                bool matched = false;
                foreach (var c in clients)
                {
                    if (c.Environments == null) continue;
                    foreach (var e in c.Environments)
                    {
                        if (SmartRouter.EnvironmentMatchesPort(e, po.Port))
                        {
                            runningEnvsList.Add(string.Format("{0} > {1} (Port {2})", c.Name, e.Name, po.Port));
                            matched = true;
                            break;
                        }
                    }
                    if (matched) break;
                }
                if (!matched)
                {
                    if (po.Port == 21021)
                    {
                        runningEnvsList.Add("WebAgent Genérico TOTVS (Port 21021)");
                    }
                    else
                    {
                        runningEnvsList.Add("WebAgent activo (Port " + po.Port + ")");
                    }
                }
            }

            if (runningEnvsList.Count > 0)
            {
                bool hasRogue21021 = activePorts.Any(p => p.Port == 21021);
                statusIconCircle.Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString(hasRogue21021 ? "#FFF3CD" : "#D1E7DD"));
                statusIconText.Text = hasRogue21021 ? "!" : "OK";
                statusIconText.Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString(hasRogue21021 ? "#664D03" : "#0F5132"));

                txtActiveBadge.Text = string.Join("  \u2022  ", runningEnvsList.ToArray());
                txtActiveBadge.Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString(hasRogue21021 ? "#664D03" : "#0F5132"));

                if (hasRogue21021 && !activePorts.Any(p => p.Port != 21021))
                {
                    txtActiveDetails.Text = "Detectado instalador genérico en puerto 21021. Para entrar a Maprial BIB u otro ambiente, pulsa '\u25B6 Iniciar' en su tarjeta.";
                }
                else
                {
                    txtActiveDetails.Text = string.Format("Tienes {0} proceso(s) de WebAgent en ejecución real en este equipo.", runningEnvsList.Count);
                }
            }
            else
            {
                statusIconCircle.Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#E9ECEF"));
                statusIconText.Text = "OFF";
                statusIconText.Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#6C757D"));

                txtActiveBadge.Text = "Ninguno (WebAgent en reposo / Detenido)";
                txtActiveBadge.Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#6C757D"));
                txtActiveDetails.Text = "Los agentes se iniciarán automáticamente al navegar en Protheus o puedes pulsar 'Iniciar' en cualquiera de tus ambientes.";
            }
        }

        private void UpdateEnvironmentCardsWithPorts(List<PortProcessHelper.PortOwnerInfo> activePorts)
        {
            if (activePorts == null) activePorts = new List<PortProcessHelper.PortOwnerInfo>();

            foreach (var b in envBindings)
            {
                int envPort = b.EnvPort;
                PortProcessHelper.PortOwnerInfo po = (envPort > 0) ? activePorts.FirstOrDefault(p => p.Port == envPort) : null;
                bool isRunning = (po != null);

                if (isRunning)
                {
                    b.CardBorder.Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#F0FFF4"));
                    b.CardBorder.BorderBrush = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#86EFAC"));
                    b.CardBorder.BorderThickness = new Thickness(2);

                    b.StatusPill.Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#D1E7DD"));
                    b.StatusTxt.Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#0F5132"));
                    if (po != null && po.HasEstablishedConnection)
                    {
                        b.StatusTxt.Text = "\u25CF Conectado al Navegador (PID " + po.Pid + ")";
                    }
                    else
                    {
                        b.StatusTxt.Text = "\u25CF En ejecución (Port " + envPort + " \u2022 PID " + (po != null ? po.Pid.ToString() : "") + ")";
                    }

                    b.ActionBtn.Content = "\u25FC Detener";
                    b.ActionBtn.Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#DC3545"));
                    b.ActionBtn.Foreground = Brushes.White;
                    b.ActionBtn.BorderBrush = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#DC3545"));
                    b.ActionBtn.Tag = "STOP";
                    b.LastRunning = true;
                }
                else
                {
                    b.CardBorder.Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#F8F9FA"));
                    b.CardBorder.BorderBrush = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#E9ECEF"));
                    b.CardBorder.BorderThickness = new Thickness(1);

                    b.StatusPill.Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#E9ECEF"));
                    b.StatusTxt.Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#6C757D"));
                    b.StatusTxt.Text = "\u25CB Detenido";

                    b.ActionBtn.Content = "\u25B6 Iniciar";
                    b.ActionBtn.Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#198754"));
                    b.ActionBtn.Foreground = Brushes.White;
                    b.ActionBtn.BorderBrush = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#198754"));
                    b.ActionBtn.Tag = "START";
                    b.LastRunning = false;
                }
            }
        }

        private void RefreshUI(bool refreshActivePorts = true)
        {
            List<PortProcessHelper.PortOwnerInfo> activePorts = refreshActivePorts
                ? PortProcessHelper.GetWebAgentPorts()
                : lastActivePorts;
            if (activePorts == null) activePorts = new List<PortProcessHelper.PortOwnerInfo>();
            lastActivePorts = activePorts;
            UpdateStatusBannerWithPorts(activePorts);
            cardsPanel.Children.Clear();
            envBindings.Clear();

            string searchText = txtSearch != null ? txtSearch.Text.Trim() : "";

            if (clients.Count == 0)
            {
                if (txtSearchSummary != null) txtSearchSummary.Text = "0 clientes";
                Border emptyState = new Border
                {
                    Background = Brushes.White,
                    CornerRadius = new CornerRadius(10),
                    Padding = new Thickness(30),
                    Margin = new Thickness(0, 10, 0, 0),
                    BorderBrush = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#E9ECEF")),
                    BorderThickness = new Thickness(1)
                };
                StackPanel sp = new StackPanel { HorizontalAlignment = HorizontalAlignment.Center };
                sp.Children.Add(new TextBlock
                {
                    Text = "No tienes ningún cliente registrado todavía.",
                    FontSize = 16,
                    FontWeight = FontWeights.Bold,
                    Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#495057")),
                    HorizontalAlignment = HorizontalAlignment.Center
                });
                sp.Children.Add(new TextBlock
                {
                    Text = "Haz clic en '+ Nuevo Cliente' para agregar tu primer cliente, o usa 'Importar Config' si TI te compartió un archivo.",
                    FontSize = 13,
                    Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#6C757D")),
                    Margin = new Thickness(0, 6, 0, 0),
                    HorizontalAlignment = HorizontalAlignment.Center
                });
                emptyState.Child = sp;
                cardsPanel.Children.Add(emptyState);
                return;
            }

            int visibleClientCount = 0;
            int visibleEnvironmentCount = 0;

            foreach (var cli in clients)
            {
                if (string.IsNullOrWhiteSpace(cli.Name)) continue;
                if (!UiPresentationHelper.ClientMatchesSearch(cli, searchText)) continue;

                List<EnvironmentItem> visibleEnvironments = UiPresentationHelper.GetVisibleEnvironments(cli, searchText);
                visibleClientCount++;
                visibleEnvironmentCount += visibleEnvironments.Count;
                string clientKey = !string.IsNullOrWhiteSpace(cli.Id) ? cli.Id : "name:" + cli.Name;
                bool isCollapsed = collapsedClientKeys.Contains(clientKey);
                bool showEnvironments = UiPresentationHelper.ShouldShowEnvironments(isCollapsed, searchText);

                Border clientCard = new Border
                {
                    Background = Brushes.White,
                    CornerRadius = new CornerRadius(10),
                    Padding = new Thickness(18, 16, 18, 16),
                    Margin = new Thickness(0, 0, 0, 14),
                    BorderThickness = new Thickness(1),
                    BorderBrush = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#E9ECEF"))
                };

                StackPanel cardContent = new StackPanel();

                // Cabecera del Cliente
                Grid headerGrid = new Grid();
                headerGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
                headerGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

                StackPanel titleStack = new StackPanel();
                TextBlock nameTxt = new TextBlock
                {
                    Text = "\U0001F3E2  " + cli.Name,
                    FontSize = 17,
                    FontWeight = FontWeights.Bold,
                    Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#212529"))
                };
                titleStack.Children.Add(nameTxt);

                string descPart = !string.IsNullOrWhiteSpace(cli.Description) ? cli.Description + "  \u2022  " : "";
                int countEnv = cli.Environments != null ? cli.Environments.Count : 0;
                string environmentSummary = countEnv + " ambiente(s) registrado(s)";
                if (!string.IsNullOrWhiteSpace(searchText) && visibleEnvironments.Count != countEnv)
                {
                    environmentSummary = visibleEnvironments.Count + " coincidencia(s) de " + countEnv + " ambiente(s)";
                }
                TextBlock subTxt = new TextBlock
                {
                    Text = descPart + environmentSummary,
                    FontSize = 12,
                    Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#6C757D")),
                    Margin = new Thickness(0, 2, 0, 0)
                };
                titleStack.Children.Add(subTxt);
                Grid.SetColumn(titleStack, 0);
                headerGrid.Children.Add(titleStack);

                StackPanel clientActions = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };

                ClientItem curCli = cli;
                string currentClientKey = clientKey;
                Button btnCollapse = CreateStyledButton(
                    showEnvironments ? "\u25B2 Ocultar (" + visibleEnvironments.Count + ")" : "\u25BC Mostrar (" + countEnv + ")",
                    "#495057",
                    false);
                btnCollapse.Margin = new Thickness(0, 0, 8, 0);
                btnCollapse.ToolTip = string.IsNullOrWhiteSpace(searchText)
                    ? "Muestra u oculta los ambientes de este cliente."
                    : "Los resultados permanecen visibles mientras hay una búsqueda activa.";
                btnCollapse.IsEnabled = string.IsNullOrWhiteSpace(searchText);
                btnCollapse.Opacity = btnCollapse.IsEnabled ? 1.0 : 0.65;
                btnCollapse.Click += (s, e) =>
                {
                    if (collapsedClientKeys.Contains(currentClientKey))
                        collapsedClientKeys.Remove(currentClientKey);
                    else
                        collapsedClientKeys.Add(currentClientKey);
                    RefreshUI(false);
                };
                clientActions.Children.Add(btnCollapse);

                Button btnAddEnv = CreateStyledButton("+ Agregar Ambiente", "#0D6EFD", true);
                btnAddEnv.Margin = new Thickness(0, 0, 8, 0);
                btnAddEnv.Click += (s, e) => OpenEnvironmentDialog(curCli, null);
                clientActions.Children.Add(btnAddEnv);

                Button btnEditCli = CreateStyledButton("Editar", "#6C757D", false);
                btnEditCli.Margin = new Thickness(0, 0, 8, 0);
                btnEditCli.Click += (s, e) => OpenClientDialog(curCli);
                clientActions.Children.Add(btnEditCli);

                Button btnDelCli = CreateStyledButton("Eliminar", "#DC3545", false);
                btnDelCli.Click += (s, e) => {
                    var res = MessageBox.Show("¿Seguro que deseas eliminar el cliente '" + curCli.Name + "' y todos sus ambientes?", "Confirmar eliminación", MessageBoxButton.YesNo, MessageBoxImage.Question);
                    if (res == MessageBoxResult.Yes)
                    {
                        if (curCli.Environments != null)
                        {
                            foreach (var env in curCli.Environments) StopEnvironment(env);
                        }
                        clients.Remove(curCli);
                        SaveClients();
                        SyncRegistryWithActiveClients();
                        RefreshUI();
                        ShowToast("Cliente eliminado.");
                    }
                };
                clientActions.Children.Add(btnDelCli);

                Grid.SetColumn(clientActions, 1);
                headerGrid.Children.Add(clientActions);

                cardContent.Children.Add(headerGrid);

                // Lista de Ambientes dentro del Cliente
                StackPanel envsPanel = new StackPanel { Margin = new Thickness(0, 12, 0, 0) };

                if (showEnvironments && visibleEnvironments.Count == 0)
                {
                    Border emptyEnv = new Border
                    {
                        Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#F8F9FA")),
                        CornerRadius = new CornerRadius(6),
                        Padding = new Thickness(14),
                        Margin = new Thickness(0, 4, 0, 0)
                    };
                    emptyEnv.Child = new TextBlock
                    {
                        Text = "Este cliente aún no tiene ambientes configurados. Haz clic en '+ Agregar Ambiente' arriba para registrar Producción, Desarrollo, etc.",
                        FontSize = 12,
                        Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#6C757D")),
                        FontStyle = FontStyles.Italic
                    };
                    envsPanel.Children.Add(emptyEnv);
                }
                else if (showEnvironments)
                {
                    foreach (var env in visibleEnvironments)
                    {
                        int envPort = 0;
                        int.TryParse(env.AgentPort, out envPort);
                        if (envPort <= 0)
                        {
                            string pred = SmartRouter.PredictWebAgentPort(env.ServerIp);
                            int.TryParse(pred, out envPort);
                        }

                        PortProcessHelper.PortOwnerInfo po = (envPort > 0) ? activePorts.FirstOrDefault(p => p.Port == envPort) : null;
                        bool isRunning = (po != null);

                        Border envCard = new Border
                        {
                            Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString(isRunning ? "#F0FFF4" : "#F8F9FA")),
                            BorderBrush = new SolidColorBrush((Color)ColorConverter.ConvertFromString(isRunning ? "#86EFAC" : "#E9ECEF")),
                            BorderThickness = new Thickness(isRunning ? 2 : 1),
                            CornerRadius = new CornerRadius(6),
                            Padding = new Thickness(14, 10, 14, 10),
                            Margin = new Thickness(0, 0, 0, 8)
                        };

                        Grid envGrid = new Grid();
                        envGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
                        envGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

                        StackPanel envInfo = new StackPanel();

                        // Línea 1: Nombre del ambiente + Badges
                        StackPanel r1 = new StackPanel { Orientation = Orientation.Horizontal };
                        TextBlock envTitle = new TextBlock
                        {
                            Text = "\U0001F4CC " + env.Name,
                            FontSize = 14,
                            FontWeight = FontWeights.Bold,
                            Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#212529")),
                            Margin = new Thickness(0, 0, 10, 0),
                            VerticalAlignment = VerticalAlignment.Center
                        };
                        r1.Children.Add(envTitle);

                        Border verBadge = new Border
                        {
                            Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#0D6EFD")),
                            CornerRadius = new CornerRadius(4),
                            Padding = new Thickness(6, 2, 6, 2),
                            Margin = new Thickness(0, 0, 8, 0)
                        };
                        verBadge.Child = new TextBlock
                        {
                            Text = env.VersionFolder ?? "web-agent",
                            Foreground = Brushes.White,
                            FontSize = 11,
                            FontWeight = FontWeights.SemiBold
                        };
                        r1.Children.Add(verBadge);

                        Border portBadge = new Border
                        {
                            Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#6C757D")),
                            CornerRadius = new CornerRadius(4),
                            Padding = new Thickness(6, 2, 6, 2),
                            Margin = new Thickness(0, 0, 8, 0)
                        };
                        portBadge.Child = new TextBlock
                        {
                            Text = "Puerto: " + (envPort > 0 ? envPort.ToString() : "(auto)"),
                            Foreground = Brushes.White,
                            FontSize = 11,
                            FontWeight = FontWeights.SemiBold
                        };
                        r1.Children.Add(portBadge);

                        envInfo.Children.Add(r1);

                        // Línea 2: Servidor / IP y notas
                        if (!string.IsNullOrWhiteSpace(env.ServerIp) || !string.IsNullOrWhiteSpace(env.Description))
                        {
                            TextBlock r2 = new TextBlock
                            {
                                FontSize = 12,
                                Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#495057")),
                                Margin = new Thickness(0, 3, 0, 0)
                            };

                            if (!string.IsNullOrWhiteSpace(env.ServerIp))
                            {
                                r2.Inlines.Add(new Run("Servidor: "));
                                string browserUrl = UiPresentationHelper.NormalizeBrowserUrl(env.ServerIp);
                                if (!string.IsNullOrEmpty(browserUrl))
                                {
                                    string currentBrowserUrl = browserUrl;
                                    Hyperlink serverLink = new Hyperlink(new Run(env.ServerIp.Trim()))
                                    {
                                        Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#0D6EFD")),
                                        Cursor = Cursors.Hand,
                                        ToolTip = "Abrir en el navegador predeterminado"
                                    };
                                    serverLink.Click += (s, e) =>
                                    {
                                        try
                                        {
                                            Process.Start(new ProcessStartInfo
                                             {
                                                FileName = currentBrowserUrl,
                                                UseShellExecute = true
                                            });
                                        }
                                        catch (Exception ex)
                                        {
                                            MessageBox.Show("No se pudo abrir la dirección en el navegador:\n" + ex.Message, "No se pudo abrir el enlace", MessageBoxButton.OK, MessageBoxImage.Warning);
                                        }
                                        e.Handled = true;
                                    };
                                    r2.Inlines.Add(serverLink);
                                }
                                else
                                {
                                    r2.Inlines.Add(new Run(env.ServerIp.Trim()));
                                }
                            }

                            if (!string.IsNullOrWhiteSpace(env.Description))
                            {
                                string separator = !string.IsNullOrWhiteSpace(env.ServerIp) ? "  |  " : "";
                                r2.Inlines.Add(new Run(separator + env.Description));
                            }
                            envInfo.Children.Add(r2);
                        }

                        // Línea 3: Estado Real en Windows
                        StackPanel r3 = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 5, 0, 0) };
                        Border statusPill = new Border
                        {
                            CornerRadius = new CornerRadius(10),
                            Padding = new Thickness(8, 2, 8, 2)
                        };
                        TextBlock statusTxt = new TextBlock { FontSize = 11, FontWeight = FontWeights.SemiBold };

                        if (isRunning)
                        {
                            statusPill.Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#D1E7DD"));
                            statusTxt.Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#0F5132"));
                            if (po != null && po.HasEstablishedConnection)
                            {
                                statusTxt.Text = "\u25CF Conectado al Navegador (PID " + po.Pid + ")";
                            }
                            else
                            {
                                statusTxt.Text = "\u25CF En ejecución (Port " + envPort + " \u2022 PID " + po.Pid + ")";
                            }
                        }
                        else
                        {
                            statusPill.Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#E9ECEF"));
                            statusTxt.Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#6C757D"));
                            statusTxt.Text = "\u25CB Detenido";
                        }
                        statusPill.Child = statusTxt;
                        r3.Children.Add(statusPill);
                        envInfo.Children.Add(r3);

                        Grid.SetColumn(envInfo, 0);
                        envGrid.Children.Add(envInfo);

                        // Columna de Acciones del Ambiente
                        StackPanel envActions = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };

                        EnvironmentItem curEnv = env;
                        Button btnToggle = CreateStyledButton(isRunning ? "\u25FC Detener" : "\u25B6 Iniciar", isRunning ? "#DC3545" : "#198754", true);
                        btnToggle.Margin = new Thickness(0, 0, 6, 0);
                        btnToggle.Tag = isRunning ? "STOP" : "START";
                        btnToggle.Click += (s, e) =>
                        {
                            if ((string)btnToggle.Tag == "STOP")
                                StopEnvironment(curEnv);
                            else
                                StartEnvironment(curEnv);
                        };
                        envActions.Children.Add(btnToggle);

                        Button btnEditEnv = CreateStyledButton("Editar", "#6C757D", false);
                        btnEditEnv.Margin = new Thickness(0, 0, 6, 0);
                        btnEditEnv.Click += (s, e) => OpenEnvironmentDialog(curCli, curEnv);
                        envActions.Children.Add(btnEditEnv);

                        Button btnDelEnv = CreateStyledButton("Eliminar", "#DC3545", false);
                        btnDelEnv.Click += (s, e) => {
                            var res = MessageBox.Show("¿Seguro que deseas eliminar el ambiente '" + curEnv.Name + "'?", "Confirmar eliminación", MessageBoxButton.YesNo, MessageBoxImage.Question);
                            if (res == MessageBoxResult.Yes)
                            {
                                StopEnvironment(curEnv);
                                curCli.Environments.Remove(curEnv);
                                SaveClients();
                                SyncRegistryWithActiveClients();
                                RefreshUI();
                                ShowToast("Ambiente eliminado.");
                            }
                        };
                        envActions.Children.Add(btnDelEnv);

                        Grid.SetColumn(envActions, 1);
                        envGrid.Children.Add(envActions);

                        envCard.Child = envGrid;
                        envsPanel.Children.Add(envCard);

                        // Registrar vinculación visual para actualización en vivo
                        envBindings.Add(new EnvVisualBinding
                        {
                            ClientId = cli.Id,
                            EnvId = env.Id,
                            Env = env,
                            CardBorder = envCard,
                            StatusPill = statusPill,
                            StatusTxt = statusTxt,
                            ActionBtn = btnToggle,
                            LastRunning = isRunning,
                            EnvPort = envPort
                        });
                    }
                }

                if (showEnvironments) cardContent.Children.Add(envsPanel);
                clientCard.Child = cardContent;
                cardsPanel.Children.Add(clientCard);
            }

            if (txtSearchSummary != null)
            {
                if (string.IsNullOrWhiteSpace(searchText))
                    txtSearchSummary.Text = visibleClientCount + " cliente(s)  •  " + visibleEnvironmentCount + " ambiente(s)";
                else
                    txtSearchSummary.Text = visibleClientCount + " cliente(s)  •  " + visibleEnvironmentCount + " coincidencia(s)";
            }

            if (visibleClientCount == 0)
            {
                Border noResults = new Border
                {
                    Background = Brushes.White,
                    CornerRadius = new CornerRadius(10),
                    Padding = new Thickness(30),
                    Margin = new Thickness(0, 10, 0, 0),
                    BorderBrush = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#E9ECEF")),
                    BorderThickness = new Thickness(1)
                };
                StackPanel noResultsContent = new StackPanel { HorizontalAlignment = HorizontalAlignment.Center };
                noResultsContent.Children.Add(new TextBlock
                {
                    Text = "No se encontraron clientes ni ambientes.",
                    FontSize = 16,
                    FontWeight = FontWeights.Bold,
                    Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#495057")),
                    HorizontalAlignment = HorizontalAlignment.Center
                });
                noResultsContent.Children.Add(new TextBlock
                {
                    Text = "Prueba con otro nombre de cliente, ambiente o dirección IP.",
                    FontSize = 13,
                    Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#6C757D")),
                    Margin = new Thickness(0, 6, 0, 0),
                    HorizontalAlignment = HorizontalAlignment.Center
                });
                noResults.Child = noResultsContent;
                cardsPanel.Children.Add(noResults);
            }
        }

        private void ShowToast(string message)
        {
            txtToast.Text = message;
            txtToast.Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#198754"));
        }

        private void OpenClientDialog(ClientItem existingClient)
        {
            ClientDialog.Open(this, existingClient, clients, () =>
            {
                SaveClients();
                SyncRegistryWithActiveClients();
                RefreshUI();
            }, msg => ShowToast(msg));
        }

        private void OpenEnvironmentDialog(ClientItem client, EnvironmentItem existingEnv)
        {
            EnvironmentDialog.Open(this, client, existingEnv, detectedVersions, () =>
            {
                SaveClients();
                SyncRegistryWithActiveClients();
                RefreshUI();
            }, msg => ShowToast(msg));
        }
    }
}
