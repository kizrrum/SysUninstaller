using System;
using System.Diagnostics;
using System.Drawing;
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
        private Button btnUninstall;
        private Button btnBrowseCommand;
        private RichTextBox rtbLog;
        private Label lblStatus;
        private bool isRunning = false;

        private ToolTip toolTip;

        public MainForm()
        {
            InitializeComponent();
            this.Text = "Деинсталлятор через подмену службы";
            this.StartPosition = FormStartPosition.CenterScreen;
            this.Size = new Size(720, 620);
        }

        private void InitializeComponent()
        {
            toolTip = new ToolTip();
            toolTip.AutoPopDelay = 10000;
            toolTip.InitialDelay = 500;
            toolTip.ReshowDelay = 200;
            toolTip.ShowAlways = true;

            // Метки
            Label lblProduct = new Label { Text = "Имя продукта (для поиска):", Location = new Point(20, 20), AutoSize = true };
            Label lblService = new Label { Text = "Имя службы-посредника:", Location = new Point(20, 60), AutoSize = true };
            Label lblCommand = new Label { Text = "Команда для выполнения (опционально):", Location = new Point(20, 100), AutoSize = true };
            Label lblMaxWait = new Label { Text = "Макс. время ожидания (сек):", Location = new Point(20, 140), AutoSize = true };
            Label lblInterval = new Label { Text = "Интервал проверки (сек):", Location = new Point(20, 180), AutoSize = true };

            // Поля ввода
            txtProductName = new TextBox { Location = new Point(220, 17), Width = 250, Text = "InfoWatch" };
            txtServiceName = new TextBox { Location = new Point(220, 57), Width = 250, Text = "Spooler" };
            txtCustomCommand = new TextBox { Location = new Point(220, 97), Width = 350, Text = "" };

            // Кнопка "Обзор"
            btnBrowseCommand = new Button { Text = "Обзор…", Location = new Point(580, 95), Width = 80, Height = 25 };
            btnBrowseCommand.Click += BtnBrowseCommand_Click;

            // Счётчики
            nudMaxWait = new NumericUpDown { Location = new Point(220, 137), Width = 80, Minimum = 10, Maximum = 600, Value = 90, Increment = 5 };
            nudCheckInterval = new NumericUpDown { Location = new Point(220, 177), Width = 80, Minimum = 1, Maximum = 30, Value = 5, Increment = 1 };

            // Кнопка запуска
            btnUninstall = new Button { Text = "Запустить деинсталляцию", Location = new Point(220, 220), Width = 180, Height = 35 };
            btnUninstall.Click += BtnUninstall_Click;

            // Статус
            lblStatus = new Label { Text = "Готов к работе", Location = new Point(20, 270), AutoSize = true, ForeColor = Color.DarkGreen };

            // Журнал
            rtbLog = new RichTextBox
            {
                Location = new Point(20, 300),
                Width = 660,
                Height = 270,
                ReadOnly = true,
                Font = new Font("Consolas", 9),
                BackColor = Color.Black,
                ForeColor = Color.LightGray,
                WordWrap = true
            };

            // --- Настройка подсказок ---
            string productTooltip = "Введите часть названия продукта (или полное имя), по которому будет выполняться поиск в разделе Uninstall реестра.\nНапример: 'InfoWatch' или 'Kaspersky'.";
            string serviceTooltip = "Имя службы Windows, которая будет временно использована для выполнения команды удаления.\nРекомендуется выбирать не критические службы (например, Spooler, upnphost, wuauserv).";
            string commandTooltip = "Если поле не пустое, то вместо автоматической команды msiexec /x {GUID} будет выполнена эта команда.\nМожно указать полный путь к EXE, BAT, CMD с параметрами.\nПример: C:\\temp\\uninstall.bat /silent";

            txtProductName.MouseEnter += (s, e) => toolTip.Show(productTooltip, txtProductName);
            txtProductName.MouseLeave += (s, e) => toolTip.Hide(txtProductName);

            txtServiceName.MouseEnter += (s, e) => toolTip.Show(serviceTooltip, txtServiceName);
            txtServiceName.MouseLeave += (s, e) => toolTip.Hide(txtServiceName);

            txtCustomCommand.MouseEnter += (s, e) => toolTip.Show(commandTooltip, txtCustomCommand);
            txtCustomCommand.MouseLeave += (s, e) => toolTip.Hide(txtCustomCommand);

            toolTip.SetToolTip(btnBrowseCommand, "Открыть диалог выбора файла (EXE, BAT, CMD) и вставить его путь в поле команды.");
            toolTip.SetToolTip(nudMaxWait, "Максимальное время (в секундах), которое программа будет ожидать завершения деинсталляции.\nЕсли по истечении этого времени продукт всё ещё присутствует в системе, операция считается неудачной.");
            toolTip.SetToolTip(nudCheckInterval, "Интервал (в секундах) между проверками наличия продукта в реестре во время ожидания завершения удаления.");
            toolTip.SetToolTip(btnUninstall, "Начать процесс деинсталляции. Будут выполнены:\n1) поиск GUID продукта,\n2) подмена ImagePath службы,\n3) запуск службы для выполнения команды,\n4) ожидание завершения,\n5) восстановление оригинального пути.");
            toolTip.SetToolTip(rtbLog, "Здесь отображаются все этапы выполнения операции с цветовой маркировкой.");
            toolTip.SetToolTip(lblStatus, "Текущее состояние операции.");

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
            Controls.Add(lblStatus);
            Controls.Add(rtbLog);
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
                var result = MessageBox.Show("Требуются права администратора. Перезапустить приложение с правами администратора?",
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

            if (string.IsNullOrEmpty(productName) || string.IsNullOrEmpty(serviceName))
            {
                MessageBox.Show("Заполните оба поля: имя продукта и службы.", "Ошибка", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }

            Thread worker = new Thread(() => RunUninstall(productName, serviceName, customCommand, maxWait, checkInterval));
            worker.IsBackground = true;
            worker.Start();
        }

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

        private void RunUninstall(string productName, string serviceName, string customCommand, int maxWait, int checkInterval)
        {
            isRunning = true;
            btnUninstall.Enabled = false;
            AppendLog("[*] Начало операции.", Color.Cyan);
            try
            {
                string originalPath = GetServiceImagePath(serviceName);
                if (string.IsNullOrEmpty(originalPath))
                {
                    AppendLog($"[ERROR] Служба '{serviceName}' не найдена или доступ запрещён.", Color.Red);
                    return;
                }
                AppendLog($"[*] Оригинальный ImagePath: {originalPath}", Color.Cyan);

                string guid = null;
                if (string.IsNullOrEmpty(customCommand))
                {
                    guid = FindProductGuid(productName);
                    if (string.IsNullOrEmpty(guid))
                    {
                        AppendLog($"[WARN] Продукт '{productName}' не найден в реестре. Возможно уже удалён.", Color.Yellow);
                        return;
                    }
                    AppendLog($"[*] Найден GUID продукта: {guid}", Color.Green);
                }
                else
                {
                    AppendLog($"[*] Будет выполнена пользовательская команда: {customCommand}", Color.Cyan);
                }

                string innerCmd;
                if (string.IsNullOrEmpty(customCommand))
                {
                    innerCmd = $"msiexec.exe /x {guid} /qn /norestart";
                }
                else
                {
                    innerCmd = customCommand;
                }

                string newImagePath = $"cmd.exe /c {innerCmd}";
                AppendLog($"[*] Устанавливаем ImagePath = {newImagePath}", Color.Yellow);
                SetServiceImagePath(serviceName, newImagePath);

                AppendLog($"[*] Останавливаем службу '{serviceName}'...", Color.Yellow);
                StopService(serviceName);
                Thread.Sleep(3000);

                AppendLog($"[*] Запускаем службу '{serviceName}' — выполнение команды начато.", Color.Green);
                StartService(serviceName);

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
                            break;
                        }
                        AppendLog($"    [{elapsed}/{maxWait}] Продукт ещё присутствует...", Color.Gray);
                    }
                    if (!uninstalled)
                        AppendLog($"[WARN] Достигнут таймаут. Деинсталляция могла не завершиться.", Color.Yellow);
                }
                else
                {
                    AppendLog($"[*] Пользовательская команда запущена. Ожидание {maxWait} секунд для завершения...", Color.Yellow);
                    Thread.Sleep(maxWait * 1000);
                    AppendLog($"[*] Ожидание завершено.", Color.Gray);
                }

                AppendLog($"[*] Восстанавливаем оригинальный ImagePath...", Color.Yellow);
                SetServiceImagePath(serviceName, originalPath);

                AppendLog($"[*] Перезапускаем службу '{serviceName}'...", Color.Yellow);
                StopService(serviceName);
                Thread.Sleep(2000);
                StartService(serviceName);

                AppendLog("[+] Операция завершена. Служба восстановлена.", Color.Green);
            }
            catch (Exception ex)
            {
                AppendLog($"[ERROR] Исключение: {ex.Message}", Color.Red);
                AppendLog(ex.StackTrace, Color.Red);
            }
            finally
            {
                isRunning = false;
                btnUninstall.Invoke((Action)(() => btnUninstall.Enabled = true));
                lblStatus.Invoke((Action)(() => lblStatus.Text = "Готов к работе"));
            }
        }

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

        private string FindProductGuid(string productName)
        {
            string uninstallKey = @"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall";
            using (RegistryKey root = Registry.LocalMachine.OpenSubKey(uninstallKey))
            {
                if (root == null) return null;
                foreach (string subKeyName in root.GetSubKeyNames())
                {
                    using (RegistryKey subKey = root.OpenSubKey(subKeyName))
                    {
                        if (subKey == null) continue;
                        object displayNameObj = subKey.GetValue("DisplayName");
                        if (displayNameObj != null)
                        {
                            string displayName = displayNameObj.ToString();
                            if (displayName.IndexOf(productName, StringComparison.OrdinalIgnoreCase) >= 0)
                                return subKeyName;
                        }
                    }
                }
            }
            return null;
        }

        private bool IsProductInstalled(string productName)
        {
            return FindProductGuid(productName) != null;
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

        private void StartService(string serviceName)
        {
            using (ServiceController sc = new ServiceController(serviceName))
            {
                if (sc.Status != ServiceControllerStatus.Running && sc.Status != ServiceControllerStatus.StartPending)
                {
                    sc.Start();
                    sc.WaitForStatus(ServiceControllerStatus.Running, TimeSpan.FromSeconds(30));
                }
            }
        }

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

    // ---------- Точка входа ----------
    internal static class Program
    {
        [STAThread]
        static void Main()
        {
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);

            // *** ДОБАВЛЕНА ПРОВЕРКА ПРАВ АДМИНИСТРАТОРА ПРИ ЗАПУСКЕ ***
            if (!IsAdministrator())
            {
                RestartAsAdmin();
                return; // завершаем текущий процесс, дальше не идём
            }

            Application.Run(new MainForm());
        }

        // Статические методы для проверки и перезапуска
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
                Verb = "runas",        // запрос повышения прав
                UseShellExecute = true
            };
            try
            {
                Process.Start(psi);
                Application.Exit();    // закрываем текущий экземпляр
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Не удалось запустить с правами администратора: {ex.Message}", "Ошибка", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }
    }
}