using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Management;
using System.Security.Principal;
using System.ServiceProcess;
using System.Threading;
using System.Windows.Forms;
using Microsoft.Win32;

namespace InfoWatchUninstaller
{
    public class MainForm : Form
    {
        // Элементы управления
        private TextBox txtProductName;
        private TextBox txtServiceName;
        private TextBox txtCustomCommand;
        private NumericUpDown nudMaxWait;
        private NumericUpDown nudCheckInterval;
        private CheckBox chkDisableNetwork;
        private Button btnUninstall;
        private Button btnBrowseCommand;
        private Button btnRefreshList;
        private RichTextBox rtbLog;
        private Label lblStatus;
        private ListBox lstInstalledApps;
        private TextBox txtSearchFilter;
        private bool isRunning = false;
        private ToolTip toolTip;

        // Переключатель: true – использовать службу, false – прямой запуск
        private bool UseServiceMethod = true;

        // Список отключенных адаптеров для последующего включения
        private readonly List<string> _disabledAdapters = new List<string>();

        // Кэш установленных продуктов (для списка)
        private List<ProductInfo> _allProducts = new List<ProductInfo>();

        // Типы установщиков
        private enum InstallerType
        {
            Unknown,
            Msi,
            InnoSetup,
            NSIS,
            Wise,
            InstallAnywhere,
            AdvancedInstaller,
            WixBurn,
            Adobe,
            Cisco
        }

        public MainForm()
        {
            InitializeComponent();
            this.Text = "Деинсталлятор через подмену службы";
            this.StartPosition = FormStartPosition.CenterScreen;
            this.Size = new Size(1050, 650);
        }

        private void InitializeComponent()
        {
            toolTip = new ToolTip();
            toolTip.AutoPopDelay = 10000;
            toolTip.InitialDelay = 500;
            toolTip.ReshowDelay = 200;
            toolTip.ShowAlways = true;

            // === ЛЕВАЯ ЧАСТЬ (параметры) ===
            Label lblProduct = new Label { Text = "Имя продукта (для поиска):", Location = new Point(20, 20), AutoSize = true };
            Label lblService = new Label { Text = "Имя службы-посредника:", Location = new Point(20, 60), AutoSize = true };
            Label lblCommand = new Label { Text = "Команда для выполнения (опционально):", Location = new Point(20, 100), AutoSize = true };
            Label lblMaxWait = new Label { Text = "Макс. время ожидания (сек):", Location = new Point(20, 140), AutoSize = true };
            Label lblInterval = new Label { Text = "Интервал проверки (сек):", Location = new Point(20, 180), AutoSize = true };

            txtProductName = new TextBox { Location = new Point(220, 17), Width = 250, Text = "InfoWatch" };
            txtServiceName = new TextBox { Location = new Point(220, 57), Width = 250, Text = "Spooler" };
            txtCustomCommand = new TextBox { Location = new Point(220, 97), Width = 350, Text = "" };

            btnBrowseCommand = new Button { Text = "Обзор…", Location = new Point(580, 95), Width = 80, Height = 25 };
            btnBrowseCommand.Click += BtnBrowseCommand_Click;

            nudMaxWait = new NumericUpDown { Location = new Point(220, 137), Width = 80, Minimum = 10, Maximum = 600, Value = 90, Increment = 5 };
            nudCheckInterval = new NumericUpDown { Location = new Point(220, 177), Width = 80, Minimum = 1, Maximum = 30, Value = 5, Increment = 1 };

            btnUninstall = new Button { Text = "Запустить деинсталляцию", Location = new Point(220, 220), Width = 180, Height = 35 };
            btnUninstall.Click += BtnUninstall_Click;

            chkDisableNetwork = new CheckBox
            {
                Text = "Отключить сеть на время удаления",
                Location = new Point(220, 265),
                AutoSize = true,
                Checked = true
            };

            lblStatus = new Label { Text = "Готов к работе", Location = new Point(20, 305), AutoSize = true, ForeColor = Color.DarkGreen };

            rtbLog = new RichTextBox
            {
                Location = new Point(20, 335),
                Width = 660,
                Height = 265,
                ReadOnly = true,
                Font = new Font("Consolas", 9),
                BackColor = Color.Black,
                ForeColor = Color.LightGray,
                WordWrap = true
            };

            // === ПРАВАЯ ЧАСТЬ (список приложений) ===
            Label lblInstalledApps = new Label
            {
                Text = "Установленные приложения:",
                Location = new Point(700, 20),
                AutoSize = true,
                Font = new Font("Microsoft Sans Serif", 9, FontStyle.Bold)
            };

            txtSearchFilter = new TextBox
            {
                Location = new Point(700, 45),
                Width = 280,
                Text = ""
            };
            txtSearchFilter.TextChanged += TxtSearchFilter_TextChanged;

            btnRefreshList = new Button
            {
                Text = "🔄 Обновить список",
                Location = new Point(700, 75),
                Width = 130,
                Height = 28
            };
            btnRefreshList.Click += BtnRefreshList_Click;

            lstInstalledApps = new ListBox
            {
                Location = new Point(700, 110),
                Width = 310,
                Height = 490,
                Font = new Font("Microsoft Sans Serif", 8.5f),
                IntegralHeight = false
            };
            lstInstalledApps.SelectedIndexChanged += LstInstalledApps_SelectedIndexChanged;

            // --- Подсказки ---
            string productTooltip = "Введите часть названия продукта для поиска в реестре.";
            string serviceTooltip = "Имя службы Windows для выполнения команды удаления.";
            string commandTooltip = "Пользовательская команда удаления (опционально).";
            string networkTooltip = "Временно отключает сетевые адаптеры для блокировки связи агента с сервером.";

            txtProductName.MouseEnter += (s, e) => toolTip.Show(productTooltip, txtProductName);
            txtProductName.MouseLeave += (s, e) => toolTip.Hide(txtProductName);

            txtServiceName.MouseEnter += (s, e) => toolTip.Show(serviceTooltip, txtServiceName);
            txtServiceName.MouseLeave += (s, e) => toolTip.Hide(txtServiceName);

            txtCustomCommand.MouseEnter += (s, e) => toolTip.Show(commandTooltip, txtCustomCommand);
            txtCustomCommand.MouseLeave += (s, e) => toolTip.Hide(txtCustomCommand);

            toolTip.SetToolTip(chkDisableNetwork, networkTooltip);
            toolTip.SetToolTip(btnBrowseCommand, "Открыть диалог выбора файла (EXE, BAT, CMD) и вставить его путь.");
            toolTip.SetToolTip(nudMaxWait, "Максимальное время ожидания завершения деинсталляции.");
            toolTip.SetToolTip(nudCheckInterval, "Интервал между проверками наличия продукта в реестре.");
            toolTip.SetToolTip(btnUninstall, "Запустить процесс удаления продукта.");
            toolTip.SetToolTip(rtbLog, "Журнал выполнения операции с цветовой маркировкой.");
            toolTip.SetToolTip(lblStatus, "Текущее состояние операции.");
            toolTip.SetToolTip(btnRefreshList, "Загрузить список всех установленных приложений из реестра.");
            toolTip.SetToolTip(txtSearchFilter, "Быстрый фильтр списка по имени приложения.");
            toolTip.SetToolTip(lstInstalledApps, "Выберите приложение из списка - имя будет автоматически подставлено в поле поиска.");

            // Добавление элементов управления
            Controls.Add(lblProduct);
            Controls.Add(txtProductName);
            Controls.Add(lblService);
            Controls.Add(txtServiceName);
            Controls.Add(lblCommand);
            Controls.Add(txtCustomCommand);
            Controls.Add(btnBrowseCommand);
            Controls.Add(lblMaxWait);
            Controls.Add(nudMaxWait);
            Controls.Add(lblInterval);
            Controls.Add(nudCheckInterval);
            Controls.Add(btnUninstall);
            Controls.Add(chkDisableNetwork);
            Controls.Add(lblStatus);
            Controls.Add(rtbLog);

            Controls.Add(lblInstalledApps);
            Controls.Add(txtSearchFilter);
            Controls.Add(btnRefreshList);
            Controls.Add(lstInstalledApps);
        }

        // ========== ОБРАБОТЧИКИ ==========

        private void BtnRefreshList_Click(object sender, EventArgs e)
        {
            LoadInstalledProducts();
        }

        private void TxtSearchFilter_TextChanged(object sender, EventArgs e)
        {
            FilterInstalledApps();
        }

        private void LstInstalledApps_SelectedIndexChanged(object sender, EventArgs e)
        {
            if (lstInstalledApps.SelectedItem is ProductInfo product)
            {
                txtProductName.Text = product.DisplayName;
                AppendLog($"[*] Выбран продукт: {product.DisplayName}", Color.Cyan);
            }
        }

        private void BtnBrowseCommand_Click(object sender, EventArgs e)
        {
            using (OpenFileDialog openFileDialog = new OpenFileDialog())
            {
                openFileDialog.Filter = "Исполняемые файлы (*.exe;*.bat;*.cmd)|*.exe;*.bat;*.cmd|Все файлы (*.*)|*.*";
                openFileDialog.Title = "Выберите файл для выполнения";
                if (openFileDialog.ShowDialog() == DialogResult.OK)
                {
                    string path = openFileDialog.FileName;
                    if (path.Contains(" "))
                        path = $"\"{path}\"";
                    txtCustomCommand.Text = path;
                }
            }
        }

        private void BtnUninstall_Click(object sender, EventArgs e)
        {
            if (isRunning)
            {
                MessageBox.Show("Операция уже выполняется.", "Занято", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            if (!IsAdministrator())
            {
                var result = MessageBox.Show("Требуются права администратора. Перезапустить приложение?",
                    "Недостаточно прав", MessageBoxButtons.YesNo, MessageBoxIcon.Warning);
                if (result == DialogResult.Yes)
                    RestartAsAdmin();
                return;
            }

            string productName = txtProductName.Text.Trim();
            string serviceName = txtServiceName.Text.Trim();
            string customCommand = txtCustomCommand.Text.Trim();
            int maxWait = (int)nudMaxWait.Value;
            int checkInterval = (int)nudCheckInterval.Value;

            if (string.IsNullOrEmpty(productName))
            {
                MessageBox.Show("Введите имя продукта.", "Ошибка", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }

            if (UseServiceMethod && string.IsNullOrEmpty(serviceName))
            {
                MessageBox.Show("При использовании метода через службу укажите имя службы.", "Ошибка", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }

            // Подтверждение удаления
            string networkWarning = chkDisableNetwork.Checked
                ? "Сетевые адаптеры будут временно отключены для блокировки связи."
                : "⚠️ ВНИМАНИЕ: Сеть останется включенной! Агент может отправить алерт о попытке удаления.";

            string warningMessage = $"Вы действительно хотите удалить продукт, содержащий \"{productName}\"?\n\n" +
                                    $"• Будет выполнена попытка принудительного удаления.\n" +
                                    $"• {networkWarning}\n" +
                                    $"• Может потребоваться перезагрузка компьютера.\n\n" +
                                    $"Продолжить?";

            if (MessageBox.Show(warningMessage, "Подтверждение удаления",
                MessageBoxButtons.YesNo, MessageBoxIcon.Warning, MessageBoxDefaultButton.Button2) == DialogResult.No)
            {
                return;
            }

            Thread worker;
            if (UseServiceMethod)
                worker = new Thread(() => RunUninstallViaService(productName, serviceName, customCommand, maxWait, checkInterval));
            else
                worker = new Thread(() => RunUninstallDirect(productName, customCommand, maxWait, checkInterval));

            worker.IsBackground = true;
            worker.Start();
        }

        // ========== ЗАГРУЗКА СПИСКА ПРИЛОЖЕНИЙ ==========

        private void LoadInstalledProducts()
        {
            lstInstalledApps.Items.Clear();
            _allProducts.Clear();
            AppendLog("[*] Загрузка списка установленных приложений...", Color.Yellow);

            try
            {
                // Используем HashSet для предотвращения дублирования по KeyName
                HashSet<string> addedKeys = new HashSet<string>();

                // 64-битные приложения
                using (RegistryKey baseKey64 = RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, RegistryView.Registry64))
                {
                    string path64 = @"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall";
                    using (RegistryKey root = baseKey64.OpenSubKey(path64))
                    {
                        if (root != null)
                        {
                            foreach (string subKeyName in root.GetSubKeyNames())
                            {
                                using (RegistryKey subKey = root.OpenSubKey(subKeyName))
                                {
                                    if (subKey == null) continue;
                                    string displayName = subKey.GetValue("DisplayName") as string;
                                    if (!string.IsNullOrEmpty(displayName) && !addedKeys.Contains(subKeyName))
                                    {
                                        string uninstallString = subKey.GetValue("UninstallString") as string;
                                        string quietUninstallString = subKey.GetValue("QuietUninstallString") as string;
                                        string exePath = ExtractExePath(uninstallString);

                                        _allProducts.Add(new ProductInfo
                                        {
                                            KeyName = subKeyName,
                                            UninstallString = uninstallString,
                                            QuietUninstallString = quietUninstallString,
                                            DisplayName = displayName,
                                            InstallerType = DetectInstallerType(uninstallString, subKey, exePath)
                                        });
                                        addedKeys.Add(subKeyName);
                                    }
                                }
                            }
                        }
                    }
                }

                // 32-битные приложения
                using (RegistryKey baseKey32 = RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, RegistryView.Registry32))
                {
                    string path32 = @"SOFTWARE\WOW6432Node\Microsoft\Windows\CurrentVersion\Uninstall";
                    using (RegistryKey root = baseKey32.OpenSubKey(path32))
                    {
                        if (root != null)
                        {
                            foreach (string subKeyName in root.GetSubKeyNames())
                            {
                                using (RegistryKey subKey = root.OpenSubKey(subKeyName))
                                {
                                    if (subKey == null) continue;
                                    string displayName = subKey.GetValue("DisplayName") as string;
                                    if (!string.IsNullOrEmpty(displayName) && !addedKeys.Contains(subKeyName))
                                    {
                                        string uninstallString = subKey.GetValue("UninstallString") as string;
                                        string quietUninstallString = subKey.GetValue("QuietUninstallString") as string;
                                        string exePath = ExtractExePath(uninstallString);

                                        _allProducts.Add(new ProductInfo
                                        {
                                            KeyName = subKeyName,
                                            UninstallString = uninstallString,
                                            QuietUninstallString = quietUninstallString,
                                            DisplayName = displayName,
                                            InstallerType = DetectInstallerType(uninstallString, subKey, exePath)
                                        });
                                        addedKeys.Add(subKeyName);
                                    }
                                }
                            }
                        }
                    }
                }

                _allProducts = _allProducts.OrderBy(p => p.DisplayName).ToList();
                FilterInstalledApps();
                AppendLog($"[+] Загружено {_allProducts.Count} приложений.", Color.Green);
            }
            catch (Exception ex)
            {
                AppendLog($"[ERROR] Ошибка загрузки списка: {ex.Message}", Color.Red);
            }
        }
        private void FilterInstalledApps()
        {
            lstInstalledApps.Items.Clear();
            string filter = txtSearchFilter.Text.Trim().ToLower();

            var filtered = string.IsNullOrEmpty(filter)
                ? _allProducts
                : _allProducts.Where(p => p.DisplayName.ToLower().Contains(filter)).ToList();

            foreach (var product in filtered)
            {
                lstInstalledApps.Items.Add(product);
            }
        }

        // ========== ВСПОМОГАТЕЛЬНЫЕ МЕТОДЫ ==========

        private bool IsAdministrator()
        {
            WindowsIdentity identity = WindowsIdentity.GetCurrent();
            WindowsPrincipal principal = new WindowsPrincipal(identity);
            return principal.IsInRole(WindowsBuiltInRole.Administrator);
        }

        private void RestartAsAdmin()
        {
            ProcessStartInfo psi = new ProcessStartInfo
            {
                FileName = Application.ExecutablePath,
                Verb = "runas",
                UseShellExecute = true
            };
            try
            {
                Process.Start(psi);
                Application.Exit();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Не удалось запустить с правами администратора: {ex.Message}", "Ошибка", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        // ---------- Извлечение пути к EXE из UninstallString ----------
        private string ExtractExePath(string uninstallString)
        {
            if (string.IsNullOrEmpty(uninstallString)) return null;
            string trimmed = uninstallString.Trim();
            if (trimmed.StartsWith("\""))
            {
                int endQuote = trimmed.IndexOf("\"", 1);
                if (endQuote > 0)
                    return trimmed.Substring(1, endQuote - 1);
            }
            else
            {
                int spaceIndex = trimmed.IndexOf(' ');
                if (spaceIndex > 0)
                    return trimmed.Substring(0, spaceIndex);
                else
                    return trimmed;
            }
            return null;
        }

        // ---------- Определение типа установщика ----------
        private InstallerType DetectInstallerType(string uninstallString, RegistryKey subKey, string exePath)
        {
            if (string.IsNullOrEmpty(uninstallString))
                return InstallerType.Unknown;

            if (uninstallString.IndexOf("msiexec", StringComparison.OrdinalIgnoreCase) >= 0)
                return InstallerType.Msi;

            try
            {
                if (subKey?.GetValue("InnoSetupCodeFile") != null)
                    return InstallerType.InnoSetup;

                object publisher = subKey?.GetValue("Publisher");
                if (publisher != null && publisher.ToString().StartsWith("NSIS:"))
                    return InstallerType.NSIS;
            }
            catch { }

            if (!string.IsNullOrEmpty(exePath))
            {
                string exeName = Path.GetFileName(exePath);
                if (exeName.Equals("unins000.exe", StringComparison.OrdinalIgnoreCase))
                    return InstallerType.InnoSetup;

                if (exeName.Equals("uninstall.exe", StringComparison.OrdinalIgnoreCase))
                {
                    if (uninstallString.IndexOf("-remove", StringComparison.OrdinalIgnoreCase) >= 0)
                        return InstallerType.AdvancedInstaller;
                    if (uninstallString.IndexOf("-silent", StringComparison.OrdinalIgnoreCase) >= 0)
                        return InstallerType.Cisco;
                    if (uninstallString.IndexOf("/S", StringComparison.OrdinalIgnoreCase) >= 0)
                        return InstallerType.NSIS;
                }

                if (uninstallString.IndexOf(" /x", StringComparison.OrdinalIgnoreCase) >= 0 ||
                    uninstallString.IndexOf(" -x", StringComparison.OrdinalIgnoreCase) >= 0)
                    return InstallerType.Wise;
            }

            if (!string.IsNullOrEmpty(exePath) && File.Exists(exePath))
            {
                try
                {
                    var versionInfo = FileVersionInfo.GetVersionInfo(exePath);
                    if (versionInfo.Comments?.IndexOf("Inno Setup", StringComparison.OrdinalIgnoreCase) >= 0 ||
                        versionInfo.FileDescription?.IndexOf("Inno", StringComparison.OrdinalIgnoreCase) >= 0)
                        return InstallerType.InnoSetup;

                    if (versionInfo.FileDescription?.IndexOf("NSIS", StringComparison.OrdinalIgnoreCase) >= 0)
                        return InstallerType.NSIS;

                    if (versionInfo.CompanyName?.IndexOf("Advanced Installer", StringComparison.OrdinalIgnoreCase) >= 0)
                        return InstallerType.AdvancedInstaller;

                    if (versionInfo.CompanyName?.IndexOf("Adobe", StringComparison.OrdinalIgnoreCase) >= 0)
                        return InstallerType.Adobe;
                }
                catch { }
            }

            if (uninstallString.IndexOf("Adobe", StringComparison.OrdinalIgnoreCase) >= 0 &&
                (uninstallString.IndexOf("Uninstaller.exe", StringComparison.OrdinalIgnoreCase) >= 0 ||
                 uninstallString.IndexOf("HDBox", StringComparison.OrdinalIgnoreCase) >= 0))
                return InstallerType.Adobe;

            return InstallerType.Unknown;
        }

        // ---------- Класс информации о продукте ----------
        private class ProductInfo
        {
            public string KeyName { get; set; }
            public string UninstallString { get; set; }
            public string QuietUninstallString { get; set; }
            public string DisplayName { get; set; }
            public InstallerType InstallerType { get; set; }

            public override string ToString()
            {
                return DisplayName;
            }
        }

        // ---------- Поиск продукта в реестре (читает реестр в реальном времени) ----------
        private ProductInfo FindProductInfo(string productName)
        {
            using (RegistryKey baseKey64 = RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, RegistryView.Registry64))
            using (RegistryKey baseKey32 = RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, RegistryView.Registry32))
            {
                string[] uninstallPaths = {
                    @"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall",
                    @"SOFTWARE\WOW6432Node\Microsoft\Windows\CurrentVersion\Uninstall"
                };

                foreach (var baseKey in new[] { baseKey64, baseKey32 })
                {
                    foreach (string path in uninstallPaths)
                    {
                        using (RegistryKey root = baseKey.OpenSubKey(path))
                        {
                            if (root == null) continue;
                            foreach (string subKeyName in root.GetSubKeyNames())
                            {
                                using (RegistryKey subKey = root.OpenSubKey(subKeyName))
                                {
                                    if (subKey == null) continue;
                                    string displayName = subKey.GetValue("DisplayName") as string;
                                    if (!string.IsNullOrEmpty(displayName) &&
                                        displayName.IndexOf(productName, StringComparison.OrdinalIgnoreCase) >= 0)
                                    {
                                        string uninstallString = subKey.GetValue("UninstallString") as string;
                                        string quietUninstallString = subKey.GetValue("QuietUninstallString") as string;
                                        string exePath = ExtractExePath(uninstallString);
                                        InstallerType type = DetectInstallerType(uninstallString, subKey, exePath);

                                        return new ProductInfo
                                        {
                                            KeyName = subKeyName,
                                            UninstallString = uninstallString,
                                            QuietUninstallString = quietUninstallString,
                                            DisplayName = displayName,
                                            InstallerType = type
                                        };
                                    }
                                }
                            }
                        }
                    }
                }
            }
            return null;
        }

        // ---------- Проверка установки продукта ----------
        private bool IsProductInstalled(string productName)
        {
            return FindProductInfo(productName) != null;
        }

        // ---------- Построение команды ----------
        private string BuildCommand(string productName, string customCommand)
        {
            if (!string.IsNullOrEmpty(customCommand))
            {
                AppendLog($"[*] Пользовательская команда: {customCommand}", Color.Cyan);
                return customCommand;
            }

            var productInfo = FindProductInfo(productName);
            if (productInfo == null)
            {
                AppendLog($"[WARN] Продукт '{productName}' не найден в реестре.", Color.Yellow);
                return null;
            }
            AppendLog($"[*] Найден ключ: {productInfo.KeyName}", Color.Green);
            AppendLog($"[*] DisplayName: {productInfo.DisplayName}", Color.Cyan);
            AppendLog($"[*] Тип установщика: {productInfo.InstallerType}", Color.Cyan);

            if (!string.IsNullOrEmpty(productInfo.QuietUninstallString))
            {
                AppendLog("[*] Используем QuietUninstallString", Color.Cyan);
                return productInfo.QuietUninstallString;
            }

            string cmd = productInfo.UninstallString;
            if (string.IsNullOrEmpty(cmd))
            {
                AppendLog("[ERROR] UninstallString пустая.", Color.Red);
                return null;
            }

            switch (productInfo.InstallerType)
            {
                case InstallerType.Msi:
                    if (cmd.IndexOf(" /I", StringComparison.OrdinalIgnoreCase) >= 0)
                        cmd = cmd.Replace("/I", "/X");
                    if (cmd.IndexOf("/quiet", StringComparison.OrdinalIgnoreCase) < 0 &&
                        cmd.IndexOf("/qb", StringComparison.OrdinalIgnoreCase) < 0)
                        cmd += " /quiet /norestart";
                    else if (cmd.IndexOf("/norestart", StringComparison.OrdinalIgnoreCase) < 0)
                        cmd += " /norestart";
                    break;

                case InstallerType.InnoSetup:
                    if (!cmd.Contains("/VERYSILENT"))
                        cmd += " /VERYSILENT /NORESTART";
                    break;

                case InstallerType.NSIS:
                    if (!cmd.Contains("/S"))
                        cmd += " /S";
                    break;

                case InstallerType.Wise:
                    if (!cmd.Contains(" /s"))
                        cmd += " /s";
                    break;

                case InstallerType.InstallAnywhere:
                    if (!cmd.Contains("-i silent"))
                        cmd += " -i silent";
                    break;

                case InstallerType.AdvancedInstaller:
                    if (cmd.IndexOf("-remove", StringComparison.OrdinalIgnoreCase) >= 0)
                        cmd = cmd.Replace("-remove", "/ex /quiet /norestart");
                    else if (!cmd.Contains("/ex"))
                        cmd += " /ex /quiet /norestart";
                    break;

                case InstallerType.WixBurn:
                    if (!cmd.Contains("-uninstall"))
                        cmd += " -uninstall -s -norestart";
                    break;

                case InstallerType.Adobe:
                    if (!cmd.Contains("--silent") && !cmd.Contains("--mode="))
                        cmd += " --silent";
                    break;

                case InstallerType.Cisco:
                    if (!cmd.Contains("-silent"))
                    {
                        if (cmd.Contains("/S"))
                            cmd = cmd.Replace("/S", "").Trim();
                        cmd += " -silent";
                    }
                    break;

                default:
                    if (!cmd.Contains("/S") && !cmd.Contains("/quiet") && !cmd.Contains("--silent"))
                        cmd += " /S";
                    break;
            }

            AppendLog($"[*] Итоговая команда: {cmd}", Color.Cyan);
            return cmd;
        }

        // ========== УПРАВЛЕНИЕ СЕТЬЮ ==========

        private void DisableNetwork()
        {
            _disabledAdapters.Clear();
            try
            {
                AppendLog("[*] Отключение сетевых адаптеров...", Color.Yellow);
                using (var searcher = new ManagementObjectSearcher("SELECT * FROM Win32_NetworkAdapter WHERE NetEnabled=true AND NetConnectionID != null"))
                {
                    foreach (ManagementObject adapter in searcher.Get())
                    {
                        string name = adapter["NetConnectionID"].ToString();
                        try
                        {
                            adapter.InvokeMethod("Disable", null);
                            _disabledAdapters.Add(name);
                            AppendLog($"  [-] Отключен: {name}", Color.Gray);
                        }
                        catch { }
                    }
                }
                Thread.Sleep(2500);
                AppendLog("[+] Сеть успешно отключена.", Color.Green);
            }
            catch (Exception ex)
            {
                AppendLog($"[ERROR] Ошибка отключения сети: {ex.Message}", Color.Red);
            }
        }

        private void EnableNetwork()
        {
            if (_disabledAdapters.Count == 0) return;
            try
            {
                AppendLog("[*] Включение сетевых адаптеров...", Color.Yellow);
                using (var searcher = new ManagementObjectSearcher("SELECT * FROM Win32_NetworkAdapter"))
                {
                    foreach (ManagementObject adapter in searcher.Get())
                    {
                        string name = adapter["NetConnectionID"]?.ToString();
                        if (!string.IsNullOrEmpty(name) && _disabledAdapters.Contains(name))
                        {
                            try
                            {
                                adapter.InvokeMethod("Enable", null);
                                AppendLog($"  [+] Включен: {name}", Color.Gray);
                            }
                            catch { }
                        }
                    }
                }
                _disabledAdapters.Clear();
                Thread.Sleep(2500);
                AppendLog("[+] Сеть успешно восстановлена.", Color.Green);
            }
            catch (Exception ex)
            {
                AppendLog($"[ERROR] Ошибка включения сети: {ex.Message}", Color.Red);
            }
        }

        // ========== МЕТОДЫ УДАЛЕНИЯ ==========

        // ---------- Прямой запуск (фоновый) ----------
        private void RunUninstallDirect(string productName, string customCommand, int maxWait, int checkInterval)
        {
            isRunning = true;
            btnUninstall.Enabled = false;
            bool disableNetwork = false;

            try
            {
                disableNetwork = (bool)chkDisableNetwork.Invoke((Func<bool>)(() => chkDisableNetwork.Checked));
                if (disableNetwork) DisableNetwork();

                AppendLog("[*] Начало операции (прямой запуск).", Color.Cyan);

                string innerCmd = BuildCommand(productName, customCommand);
                if (string.IsNullOrEmpty(innerCmd))
                    return;

                ProcessStartInfo psi = new ProcessStartInfo
                {
                    FileName = "cmd.exe",
                    Arguments = $"/c {innerCmd}",
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    WindowStyle = ProcessWindowStyle.Hidden,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true
                };

                AppendLog($"[*] Запускаем: {psi.FileName} {psi.Arguments}", Color.Yellow);
                Process process = Process.Start(psi);

                if (process == null || process.HasExited)
                {
                    AppendLog("[ERROR] Не удалось запустить процесс.", Color.Red);
                    return;
                }

                AppendLog($"[*] Процесс запущен (PID: {process.Id}).", Color.Green);

                WaitForUninstall(productName, customCommand, maxWait, checkInterval);

                if (!process.HasExited)
                {
                    process.WaitForExit(5000);
                    if (!process.HasExited)
                        process.Kill();
                }

                AppendLog("[+] Операция завершена.", Color.Green);
            }
            catch (Exception ex)
            {
                AppendLog($"[ERROR] Исключение: {ex.Message}", Color.Red);
                AppendLog(ex.StackTrace, Color.Red);
            }
            finally
            {
                if (disableNetwork) EnableNetwork();

                isRunning = false;
                btnUninstall.Invoke((Action)(() => btnUninstall.Enabled = true));
                lblStatus.Invoke((Action)(() => { lblStatus.Text = "Готов к работе"; lblStatus.ForeColor = Color.DarkGreen; }));
            }
        }

        // ---------- Бэкап оригинального ImagePath ----------
        private void SaveOriginalServicePath(string serviceName, string originalPath)
        {
            string keyPath = @"SOFTWARE\InfoWatchUninstaller\ServiceBackup";
            using (RegistryKey key = Registry.LocalMachine.CreateSubKey(keyPath))
            {
                key.SetValue(serviceName, originalPath);
            }
        }

        private string GetSavedOriginalServicePath(string serviceName)
        {
            string keyPath = @"SOFTWARE\InfoWatchUninstaller\ServiceBackup";
            using (RegistryKey key = Registry.LocalMachine.OpenSubKey(keyPath))
            {
                if (key == null) return null;
                return key.GetValue(serviceName) as string;
            }
        }

        // ---------- Метод через подмену службы ----------
        private void RunUninstallViaService(string productName, string serviceName, string customCommand, int maxWait, int checkInterval)
        {
            isRunning = true;
            btnUninstall.Enabled = false;
            bool serviceStopped = false;
            bool disableNetwork = false;

            try
            {
                disableNetwork = (bool)chkDisableNetwork.Invoke((Func<bool>)(() => chkDisableNetwork.Checked));
                if (disableNetwork) DisableNetwork();

                AppendLog("[*] Начало операции (через службу).", Color.Cyan);

                string currentPath = GetServiceImagePath(serviceName);
                if (string.IsNullOrEmpty(currentPath))
                {
                    AppendLog($"[ERROR] Служба '{serviceName}' не найдена.", Color.Red);
                    return;
                }

                if (currentPath.IndexOf("cmd.exe /c", StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    AppendLog("[WARN] Служба уже изменена. Попытка восстановить из бэкапа.", Color.Yellow);
                    string backup = GetSavedOriginalServicePath(serviceName);
                    if (!string.IsNullOrEmpty(backup))
                    {
                        SetServiceImagePath(serviceName, backup);
                        AppendLog($"[*] Восстановлен оригинальный ImagePath из бэкапа: {backup}", Color.Cyan);
                        currentPath = backup;
                    }
                    else
                    {
                        AppendLog("[ERROR] Бэкап не найден. Невозможно восстановить оригинальный путь. Используйте прямой запуск.", Color.Red);
                        return;
                    }
                }

                string savedPath = GetSavedOriginalServicePath(serviceName);
                if (string.IsNullOrEmpty(savedPath))
                {
                    SaveOriginalServicePath(serviceName, currentPath);
                    savedPath = currentPath;
                }
                string originalPath = savedPath;

                AppendLog($"[*] Оригинальный ImagePath: {originalPath}", Color.Cyan);

                string innerCmd = BuildCommand(productName, customCommand);
                if (string.IsNullOrEmpty(innerCmd))
                    return;

                string newImagePath = $"cmd.exe /c start /b \"\" {innerCmd}";
                AppendLog($"[*] Устанавливаем ImagePath = {newImagePath}", Color.Yellow);
                SetServiceImagePath(serviceName, newImagePath);

                AppendLog($"[*] Останавливаем службу '{serviceName}'...", Color.Yellow);
                StopService(serviceName);
                serviceStopped = true;
                Thread.Sleep(3000);

                AppendLog($"[*] Запускаем службу '{serviceName}' — выполнение команды начато.", Color.Green);
                bool serviceStarted = StartServiceWithCheck(serviceName);
                if (!serviceStarted)
                    AppendLog($"[WARN] Служба не запустилась (ошибка 1053?), но команда могла быть отправлена.", Color.Yellow);

                CheckProcess(innerCmd);

                WaitForUninstall(productName, customCommand, maxWait, checkInterval);

                string restorePath = GetSavedOriginalServicePath(serviceName);
                if (string.IsNullOrEmpty(restorePath))
                    restorePath = originalPath;

                AppendLog($"[*] Восстанавливаем оригинальный ImagePath: {restorePath}", Color.Yellow);
                SetServiceImagePath(serviceName, restorePath);

                AppendLog($"[*] Перезапускаем службу '{serviceName}'...", Color.Yellow);
                StopService(serviceName);
                Thread.Sleep(2000);
                bool restartOk = StartServiceWithCheck(serviceName);
                if (!restartOk)
                    AppendLog($"[WARN] Не удалось перезапустить службу '{serviceName}'.", Color.Yellow);
                else
                    AppendLog("[+] Операция завершена. Служба восстановлена.", Color.Green);
            }
            catch (Exception ex)
            {
                AppendLog($"[ERROR] Исключение: {ex.Message}", Color.Red);
                AppendLog(ex.StackTrace, Color.Red);
            }
            finally
            {
                try
                {
                    string restorePath = GetSavedOriginalServicePath(serviceName);
                    if (!string.IsNullOrEmpty(restorePath))
                    {
                        string current = GetServiceImagePath(serviceName);
                        if (current != restorePath)
                        {
                            AppendLog($"[*] Принудительное восстановление из бэкапа в finally.", Color.Yellow);
                            SetServiceImagePath(serviceName, restorePath);
                            if (serviceStopped)
                            {
                                StopService(serviceName);
                                Thread.Sleep(2000);
                                StartServiceWithCheck(serviceName);
                            }
                        }
                    }
                }
                catch { }

                if (disableNetwork) EnableNetwork();

                isRunning = false;
                btnUninstall.Invoke((Action)(() => btnUninstall.Enabled = true));
                lblStatus.Invoke((Action)(() => { lblStatus.Text = "Готов к работе"; lblStatus.ForeColor = Color.DarkGreen; }));
            }
        }

        // ========== РАБОТА СО СЛУЖБОЙ ==========

        private string GetServiceImagePath(string serviceName)
        {
            string keyPath = $@"SYSTEM\CurrentControlSet\Services\{serviceName}";
            using (RegistryKey key = Registry.LocalMachine.OpenSubKey(keyPath, false))
            {
                if (key == null) return null;
                return key.GetValue("ImagePath") as string;
            }
        }

        private void SetServiceImagePath(string serviceName, string newPath)
        {
            string keyPath = $@"SYSTEM\CurrentControlSet\Services\{serviceName}";
            using (RegistryKey key = Registry.LocalMachine.OpenSubKey(keyPath, true))
            {
                if (key == null) throw new Exception($"Не удалось открыть раздел реестра для {serviceName}");
                key.SetValue("ImagePath", newPath, RegistryValueKind.ExpandString);
            }
        }

        private void StopService(string serviceName)
        {
            using (ServiceController sc = new ServiceController(serviceName))
            {
                if (sc.Status != ServiceControllerStatus.Stopped && sc.Status != ServiceControllerStatus.StopPending)
                {
                    sc.Stop();
                    sc.WaitForStatus(ServiceControllerStatus.Stopped, TimeSpan.FromSeconds(30));
                }
            }
        }

        private bool StartServiceWithCheck(string serviceName)
        {
            try
            {
                using (ServiceController sc = new ServiceController(serviceName))
                {
                    if (sc.Status == ServiceControllerStatus.Running || sc.Status == ServiceControllerStatus.StartPending)
                        return true;

                    sc.Start();
                    int timeout = 10000;
                    int elapsed = 0;
                    int interval = 500;
                    while (elapsed < timeout)
                    {
                        sc.Refresh();
                        if (sc.Status == ServiceControllerStatus.Running)
                            return true;
                        if (sc.Status == ServiceControllerStatus.Stopped || sc.Status == ServiceControllerStatus.StopPending)
                            break;
                        Thread.Sleep(interval);
                        elapsed += interval;
                    }
                    return false;
                }
            }
            catch (Exception ex)
            {
                AppendLog($"[ERROR] Ошибка при запуске службы '{serviceName}': {ex.Message}", Color.Red);
                return false;
            }
        }

        // ---------- Проверка процесса ----------
        private void CheckProcess(string commandLine)
        {
            string processName = null;
            string exePath = commandLine.Trim();
            if (exePath.StartsWith("\""))
            {
                int endQuote = exePath.IndexOf("\"", 1);
                if (endQuote > 0)
                    exePath = exePath.Substring(1, endQuote - 1);
            }
            else
            {
                exePath = exePath.Split(' ')[0];
            }

            if (!string.IsNullOrEmpty(exePath))
                processName = Path.GetFileNameWithoutExtension(exePath);

            if (string.IsNullOrEmpty(processName))
                return;

            bool found = false;
            for (int i = 0; i < 10; i++)
            {
                if (Process.GetProcessesByName(processName).Length > 0)
                {
                    found = true;
                    break;
                }
                Thread.Sleep(500);
            }
            if (found)
                AppendLog($"[*] Процесс {processName} запущен.", Color.Green);
            else
                AppendLog($"[WARN] Процесс {processName} не обнаружен. Возможно, команда не выполнилась.", Color.Yellow);
        }

        // ---------- Ожидание завершения ----------
        private void WaitForUninstall(string productName, string customCommand, int maxWait, int checkInterval)
        {
            int elapsed = 0;
            if (string.IsNullOrEmpty(customCommand))
            {
                AppendLog($"[⏳] Ожидание завершения деинсталляции (макс. {maxWait} сек)...", Color.Yellow);
                bool uninstalled = false;
                while (elapsed < maxWait)
                {
                    Thread.Sleep(checkInterval * 1000);
                    elapsed += checkInterval;
                    if (!IsProductInstalled(productName))
                    {
                        AppendLog($"[✅] Продукт '{productName}' успешно удалён!", Color.Green);
                        uninstalled = true;

                        // Обновляем список приложений в UI-потоке
                        this.Invoke((Action)(() =>
                        {
                            LoadInstalledProducts();
                        }));

                        break;
                    }
                    AppendLog($"    [{elapsed}/{maxWait}] Продукт ещё присутствует...", Color.Gray);
                }
                if (!uninstalled)
                    AppendLog($"[WARN] Достигнут таймаут. Деинсталляция могла не завершиться.", Color.Yellow);
            }
            else
            {
                AppendLog($"[*] Пользовательская команда запущена. Ожидание {maxWait} секунд...", Color.Yellow);
                Thread.Sleep(maxWait * 1000);
                AppendLog($"[*] Ожидание завершено.", Color.Gray);
            }
        }

        // ---------- Логирование ----------
        private void AppendLog(string message, Color color)
        {
            if (rtbLog.InvokeRequired)
            {
                rtbLog.Invoke((Action)(() => AppendLog(message, color)));
                return;
            }
            string timestamp = DateTime.Now.ToString("HH:mm:ss");
            string line = $"[{timestamp}] {message}\n";
            rtbLog.SelectionStart = rtbLog.TextLength;
            rtbLog.SelectionLength = 0;
            rtbLog.SelectionColor = color;
            rtbLog.AppendText(line);
            rtbLog.ScrollToCaret();

            if (color == Color.Green || color == Color.Red)
                lblStatus.Invoke((Action)(() => { lblStatus.Text = message; lblStatus.ForeColor = color; }));
        }
    }

    // ---------- ТОЧКА ВХОДА ----------
    internal static class Program
    {
        [STAThread]
        static void Main()
        {
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);

            if (!IsAdministrator())
            {
                RestartAsAdmin();
                return;
            }

            Application.Run(new MainForm());
        }

        private static bool IsAdministrator()
        {
            WindowsIdentity identity = WindowsIdentity.GetCurrent();
            WindowsPrincipal principal = new WindowsPrincipal(identity);
            return principal.IsInRole(WindowsBuiltInRole.Administrator);
        }

        private static void RestartAsAdmin()
        {
            ProcessStartInfo psi = new ProcessStartInfo
            {
                FileName = Application.ExecutablePath,
                Verb = "runas",
                UseShellExecute = true
            };
            try
            {
                Process.Start(psi);
                Application.Exit();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Не удалось запустить с правами администратора: {ex.Message}", "Ошибка", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }
    }
}
