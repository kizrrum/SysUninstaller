using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Management;
using System.Runtime.InteropServices;
using System.Security.Principal;
using System.ServiceProcess;
using System.Threading;
using System.Windows.Forms;
using Microsoft.Win32;

namespace InfoWatchUninstaller
{
    // ---------- Вспомогательный класс для запуска процесса от SYSTEM ----------
    internal static class SystemProcessLauncher
    {
        private const string SE_DEBUG_NAME = "SeDebugPrivilege";

        [DllImport("advapi32.dll", SetLastError = true)]
        private static extern bool OpenProcessToken(IntPtr ProcessHandle, uint DesiredAccess, out IntPtr TokenHandle);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern IntPtr OpenProcess(uint processAccess, bool bInheritHandle, int processId);

        [DllImport("advapi32.dll", SetLastError = true)]
        private static extern bool DuplicateTokenEx(
            IntPtr hExistingToken,
            uint dwDesiredAccess,
            IntPtr lpTokenAttributes,
            int ImpersonationLevel,
            int TokenType,
            out IntPtr phNewToken);

        [DllImport("advapi32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
        private static extern bool CreateProcessWithTokenW(
            IntPtr hToken,
            int logonFlags,
            string applicationName,
            string commandLine,
            int creationFlags,
            IntPtr environment,
            string currentDirectory,
            ref STARTUPINFO startupInfo,
            out PROCESS_INFORMATION processInformation);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool CloseHandle(IntPtr hObject);

        [DllImport("advapi32.dll", SetLastError = true)]
        private static extern bool AdjustTokenPrivileges(IntPtr TokenHandle, bool DisableAllPrivileges,
            ref TOKEN_PRIVILEGES NewState, int BufferLength, IntPtr PreviousState, IntPtr ReturnLength);

        [DllImport("advapi32.dll", SetLastError = true)]
        private static extern bool LookupPrivilegeValue(string lpSystemName, string lpName, out long lpLuid);

        [StructLayout(LayoutKind.Sequential)]
        private struct STARTUPINFO
        {
            public int cb;
            public string lpReserved;
            public string lpDesktop;
            public string lpTitle;
            public int dwX, dwY, dwXSize, dwYSize;
            public int dwXCountChars, dwYCountChars;
            public int dwFillAttribute;
            public int dwFlags;
            public short wShowWindow;
            public short cbReserved2;
            public IntPtr lpReserved2;
            public IntPtr hStdInput, hStdOutput, hStdError;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct PROCESS_INFORMATION
        {
            public IntPtr hProcess;
            public IntPtr hThread;
            public int dwProcessId;
            public int dwThreadId;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct TOKEN_PRIVILEGES
        {
            public int PrivilegeCount;
            public long Luid;
            public int Attributes;
        }

        private const uint PROCESS_QUERY_INFORMATION = 0x0400;
        private const uint TOKEN_DUPLICATE = 0x0002;
        private const uint TOKEN_QUERY = 0x0008;
        private const uint TOKEN_ASSIGN_PRIMARY = 0x0001;
        private const uint TOKEN_ADJUST_DEFAULT = 0x0080;
        private const uint TOKEN_ADJUST_SESSIONID = 0x0100;
        private const uint TOKEN_ADJUST_PRIVILEGES = 0x0020;
        private const int SecurityImpersonation = 2;
        private const int TokenPrimary = 1;
        private const int CREATE_NEW_CONSOLE = 0x00000010;
        private const int SE_PRIVILEGE_ENABLED = 0x2;

        private static bool EnablePrivilege(string privilegeName)
        {
            try
            {
                using (Process currentProcess = Process.GetCurrentProcess())
                {
                    IntPtr hToken;
                    if (!OpenProcessToken(currentProcess.Handle, TOKEN_ADJUST_PRIVILEGES | TOKEN_QUERY, out hToken))
                        return false;

                    long luid;
                    if (!LookupPrivilegeValue(null, privilegeName, out luid))
                        return false;

                    TOKEN_PRIVILEGES tp = new TOKEN_PRIVILEGES
                    {
                        PrivilegeCount = 1,
                        Luid = luid,
                        Attributes = SE_PRIVILEGE_ENABLED
                    };

                    bool result = AdjustTokenPrivileges(hToken, false, ref tp, 0, IntPtr.Zero, IntPtr.Zero);
                    CloseHandle(hToken);
                    return result;
                }
            }
            catch { return false; }
        }

        public static int LaunchProcessAsSystem(string commandLine)
        {
            EnablePrivilege(SE_DEBUG_NAME);

            Process proc = Process.GetProcessesByName("winlogon").FirstOrDefault();
            if (proc == null) return -1;

            IntPtr hProcess = OpenProcess(PROCESS_QUERY_INFORMATION, false, proc.Id);
            if (hProcess == IntPtr.Zero) return -2;

            IntPtr hToken;
            if (!OpenProcessToken(hProcess, TOKEN_DUPLICATE | TOKEN_QUERY, out hToken))
            {
                CloseHandle(hProcess);
                return -3;
            }
            CloseHandle(hProcess);

            IntPtr hPrimaryToken;
            bool success = DuplicateTokenEx(
                hToken,
                TOKEN_ASSIGN_PRIMARY | TOKEN_DUPLICATE | TOKEN_QUERY | TOKEN_ADJUST_DEFAULT | TOKEN_ADJUST_SESSIONID,
                IntPtr.Zero,
                SecurityImpersonation,
                TokenPrimary,
                out hPrimaryToken);

            if (!success)
            {
                CloseHandle(hToken);
                return -4;
            }

            STARTUPINFO si = new STARTUPINFO();
            si.cb = Marshal.SizeOf(si);
            PROCESS_INFORMATION pi;

            bool created = CreateProcessWithTokenW(
                hPrimaryToken,
                0,
                null,
                commandLine,
                CREATE_NEW_CONSOLE,
                IntPtr.Zero,
                null,
                ref si,
                out pi);

            CloseHandle(hToken);
            CloseHandle(hPrimaryToken);

            if (created)
            {
                CloseHandle(pi.hProcess);
                CloseHandle(pi.hThread);
                return pi.dwProcessId;
            }
            else
            {
                return -5;
            }
        }
    }

    // ---------- Типы установщиков ----------
    internal enum InstallerType
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

    // ---------- Информация о продукте ----------
    internal class ProductInfo
    {
        public string KeyName { get; set; }
        public string UninstallString { get; set; }
        public string QuietUninstallString { get; set; }
        public string DisplayName { get; set; }
        public InstallerType InstallerType { get; set; }
        public string Publisher { get; set; }
        public string DisplayVersion { get; set; }
        public string InstallLocation { get; set; }
        public string InstallDate { get; set; }
        public string EstimatedSize { get; set; }

        public override string ToString()
        {
            return DisplayName;
        }
    }

    // ---------- Главная форма ----------
    public class MainForm : Form
    {
        private TextBox txtProductName;
        private TextBox txtServiceName;
        private TextBox txtCustomCommand;
        private NumericUpDown nudMaxWait;
        private NumericUpDown nudCheckInterval;
        private CheckBox chkDisableNetwork;
        private Button btnUninstall;
        private Button btnStop;
        private Button btnBrowseCommand;
        private Button btnRefreshList;
        private RichTextBox rtbLog;
        private Label lblStatus;
        private ListBox lstInstalledApps;
        private TextBox txtSearchFilter;
        private bool isRunning = false;
        private ToolTip toolTip;

        // Методы удаления
        private enum UninstallMethod { Service, Direct, SystemImpersonation }
        private UninstallMethod selectedMethod = UninstallMethod.Direct;

        private readonly List<string> _disabledAdapters = new List<string>();
        private readonly List<ProductInfo> _allProducts = new List<ProductInfo>();

        // Для отмены операции
        private CancellationTokenSource cts;

        public MainForm()
        {
            InitializeComponent();
            this.Text = Loc.Title;
            this.StartPosition = FormStartPosition.CenterScreen;
            this.Size = new Size(1050, 650);
            System.Threading.Tasks.Task.Delay(2000).ContinueWith(_ => UpdateChecker.CheckForUpdates((msg, color) => AppendLog(msg, color)));
        }

        private void InitializeComponent()
        {
            toolTip = new ToolTip { AutoPopDelay = 10000, InitialDelay = 500, ReshowDelay = 200, ShowAlways = true };

            // Левая часть
            Label lblProduct = new Label { Text = Loc.ProductLabel, Location = new Point(20, 20), AutoSize = true };
            Label lblService = new Label { Text = Loc.ServiceLabel, Location = new Point(20, 60), AutoSize = true };
            Label lblCommand = new Label { Text = Loc.CommandLabel, Location = new Point(20, 100), AutoSize = true };
            Label lblMaxWait = new Label { Text = Loc.MaxWaitLabel, Location = new Point(20, 140), AutoSize = true };
            Label lblInterval = new Label { Text = Loc.CheckIntervalLabel, Location = new Point(20, 180), AutoSize = true };

            txtProductName = new TextBox { Location = new Point(220, 17), Width = 250, Text = "InfoWatch" };
            txtServiceName = new TextBox { Location = new Point(220, 57), Width = 250, Text = "Spooler" };
            txtCustomCommand = new TextBox { Location = new Point(220, 97), Width = 350 };

            // Тултипы для полей ввода и их меток
            toolTip.SetToolTip(lblProduct, Loc.TooltipProductLabel);
            toolTip.SetToolTip(lblService, Loc.TooltipServiceLabel);
            toolTip.SetToolTip(lblCommand, Loc.TooltipCommandLabel);

            txtProductName.MouseHover += (s, ev) => {
                toolTip.Show(Loc.TooltipProductName, txtProductName);
            };
            txtProductName.MouseLeave += (s, ev) => toolTip.Hide(txtProductName);

            txtServiceName.MouseHover += (s, ev) => {
                toolTip.Show(Loc.TooltipServiceName, txtServiceName);
            };
            txtServiceName.MouseLeave += (s, ev) => toolTip.Hide(txtServiceName);

            txtCustomCommand.MouseHover += (s, ev) => {
                toolTip.Show(Loc.TooltipCustomCommand, txtCustomCommand);
            };
            txtCustomCommand.MouseLeave += (s, ev) => toolTip.Hide(txtCustomCommand);

            btnBrowseCommand = new Button { Text = Loc.BrowseButton, Location = new Point(580, 95), Width = 80, Height = 25 };
            btnBrowseCommand.Click += BtnBrowseCommand_Click;

            // Кнопка помощи
            Button btnHelp = new Button
            {
                Text = Loc.HelpButton,
                Location = new Point(665, 95),
                Width = 25,
                Height = 25,
                Font = new Font("Microsoft Sans Serif", 9, FontStyle.Bold)
            };
            btnHelp.Click += (s, e) =>
            {
                MessageBox.Show(Loc.HelpText, Loc.TitleAbout, MessageBoxButtons.OK, MessageBoxIcon.Information);
            };
            Controls.Add(btnHelp);

            nudMaxWait = new NumericUpDown { Location = new Point(220, 137), Width = 80, Minimum = 10, Maximum = 600, Value = 90 };
            nudCheckInterval = new NumericUpDown { Location = new Point(220, 177), Width = 80, Minimum = 1, Maximum = 30, Value = 5 };

            btnUninstall = new Button { Text = Loc.UninstallButton, Location = new Point(220, 220), Width = 180, Height = 35 };
            btnUninstall.Click += BtnUninstall_Click;

            // Кнопка "Стоп"
            // Кнопка "Стоп" – теперь левее кнопки запуска
            btnStop = new Button
            {
                Text = Loc.StopButton,
                Location = new Point(130, 220),
                Width = 80,
                Height = 35,
                Enabled = false
            };
            btnStop.Click += BtnStop_Click;

            chkDisableNetwork = new CheckBox
            {
                Text = Loc.NetworkCheckbox,
                Location = new Point(220, 265),
                AutoSize = true,
                Checked = false
            };

            GroupBox gbMethod = new GroupBox
            {
                Text = Loc.MethodGroup,
                Location = new Point(450, 220),
                Size = new Size(200, 100)
            };

            RadioButton rbService = new RadioButton { Text = Loc.ServiceRadio, Location = new Point(10, 20), Size = new Size(180, 20) };
            RadioButton rbDirect = new RadioButton { Text = Loc.DirectRadio, Location = new Point(10, 45), Size = new Size(180, 20), Checked = true };
            RadioButton rbSystem = new RadioButton { Text = Loc.SystemRadio, Location = new Point(10, 70), Size = new Size(180, 20) };

            toolTip.SetToolTip(rbService, Loc.TooltipServiceMethod);
            toolTip.SetToolTip(rbDirect, Loc.TooltipDirectMethod);
            toolTip.SetToolTip(rbSystem, Loc.TooltipSystemMethod);

            rbService.CheckedChanged += (s, e) => { if (rbService.Checked) { selectedMethod = UninstallMethod.Service; txtServiceName.Enabled = true; } };
            rbDirect.CheckedChanged += (s, e) => { if (rbDirect.Checked) { selectedMethod = UninstallMethod.Direct; txtServiceName.Enabled = false; } };
            rbSystem.CheckedChanged += (s, e) => { if (rbSystem.Checked) { selectedMethod = UninstallMethod.SystemImpersonation; txtServiceName.Enabled = false; } };

            gbMethod.Controls.Add(rbService);
            gbMethod.Controls.Add(rbDirect);
            gbMethod.Controls.Add(rbSystem);

            lblStatus = new Label { Text = Loc.StatusReady, Location = new Point(20, 305), AutoSize = true, ForeColor = Color.DarkGreen };

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

            // Правая часть (список приложений)
            Label lblInstalledApps = new Label
            {
                Text = Loc.InstalledAppsLabel,
                Location = new Point(700, 20),
                AutoSize = true,
                Font = new Font("Microsoft Sans Serif", 9, FontStyle.Bold)
            };
            txtSearchFilter = new TextBox { Location = new Point(700, 45), Width = 280 };
            txtSearchFilter.TextChanged += TxtSearchFilter_TextChanged;
            btnRefreshList = new Button { Text = Loc.RefreshButton, Location = new Point(700, 75), Width = 130, Height = 28 };
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
            lstInstalledApps.DoubleClick += LstInstalledApps_DoubleClick;

            // Тултип для списка приложений
            toolTip.SetToolTip(lstInstalledApps, Loc.TooltipInstalledAppsList);
            toolTip.SetToolTip(lblInstalledApps, Loc.TooltipInstalledAppsLabel);

            // Добавление всех элементов на форму
            Controls.AddRange(new Control[] {
            lblProduct, txtProductName,
            lblService, txtServiceName,
            lblCommand, txtCustomCommand, btnBrowseCommand,
            lblMaxWait, nudMaxWait, lblInterval, nudCheckInterval,
            btnUninstall, btnStop, chkDisableNetwork, gbMethod,
            lblStatus, rtbLog,
            lblInstalledApps, txtSearchFilter, btnRefreshList, lstInstalledApps
        });
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
                AppendLog(Loc.LogSelectedProduct(product.DisplayName), Color.Cyan);
            }
        }

        private void LstInstalledApps_DoubleClick(object sender, EventArgs e)
        {
            if (lstInstalledApps.SelectedItem is ProductInfo product)
            {
                using (var detailsForm = new ProductDetailsForm(product, this))
                {
                    if (detailsForm.ShowDialog(this) == DialogResult.OK)
                    {
                        txtProductName.Text = product.DisplayName;
                        AppendLog(Loc.LogSelectedForUninstall(product.DisplayName), Color.Cyan);
                    }
                }
            }
        }

        private void BtnBrowseCommand_Click(object sender, EventArgs e)
        {
            using (OpenFileDialog openFileDialog = new OpenFileDialog())
            {
                openFileDialog.Filter = Loc.OpenFileDialogFilter;
                openFileDialog.Title = Loc.OpenFileDialogTitle;
                if (openFileDialog.ShowDialog() == DialogResult.OK)
                {
                    string path = openFileDialog.FileName;
                    if (path.Contains(" ")) path = $"\"{path}\"";
                    txtCustomCommand.Text = path;
                }
            }
        }

        private void BtnUninstall_Click(object sender, EventArgs e)
        {
            if (isRunning)
            {
                MessageBox.Show(Loc.OperationAlreadyRunning, Loc.TitleBusy, MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            if (!IsAdministrator())
            {
                var result = MessageBox.Show(Loc.AdminRequired, Loc.TitleInsufficientRights,
                    MessageBoxButtons.YesNo, MessageBoxIcon.Warning);
                if (result == DialogResult.Yes) RestartAsAdmin();
                return;
            }

            string productName = txtProductName.Text.Trim();
            string serviceName = txtServiceName.Text.Trim();
            string customCommand = txtCustomCommand.Text.Trim();
            int maxWait = (int)nudMaxWait.Value;
            int checkInterval = (int)nudCheckInterval.Value;

            if (string.IsNullOrEmpty(productName))
            {
                MessageBox.Show(Loc.EnterProductName, Loc.TitleError, MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }

            if (selectedMethod == UninstallMethod.Service && string.IsNullOrEmpty(serviceName))
            {
                MessageBox.Show(Loc.ServiceNameRequired, Loc.TitleError, MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }

            bool disableNetwork = chkDisableNetwork.Checked;
            string networkWarning = disableNetwork ? Loc.NetworkOff : Loc.NetworkOn;

            string methodName;
            switch (selectedMethod)
            {
                case UninstallMethod.Service: methodName = Loc.MethodViaService; break;
                case UninstallMethod.Direct: methodName = Loc.MethodDirect; break;
                case UninstallMethod.SystemImpersonation: methodName = Loc.MethodSystem; break;
                default: methodName = "?"; break;
            }

            string warningMessage = Loc.ConfirmUninstall(productName, methodName, networkWarning);
            if (MessageBox.Show(warningMessage, Loc.TitleConfirmUninstall,
                MessageBoxButtons.YesNo, MessageBoxIcon.Warning, MessageBoxDefaultButton.Button2) == DialogResult.No)
                return;

            // Создаём токен отмены
            cts = new CancellationTokenSource();
            btnUninstall.Enabled = false;
            btnStop.Enabled = true;

            Thread worker = null;
            switch (selectedMethod)
            {
                case UninstallMethod.Service:
                    worker = new Thread(() => RunUninstallViaService(productName, serviceName, customCommand, maxWait, checkInterval, cts.Token, disableNetwork));
                    break;
                case UninstallMethod.Direct:
                    worker = new Thread(() => RunUninstallDirect(productName, customCommand, maxWait, checkInterval, cts.Token, disableNetwork));
                    break;
                case UninstallMethod.SystemImpersonation:
                    worker = new Thread(() => RunUninstallViaSystem(productName, customCommand, maxWait, checkInterval, cts.Token, disableNetwork));
                    break;
            }

            if (worker != null)
            {
                worker.IsBackground = true;
                worker.Start();
            }
        }

        private void BtnStop_Click(object sender, EventArgs e)
        {
            if (isRunning && cts != null && !cts.IsCancellationRequested)
            {
                cts.Cancel();
                AppendLog(Loc.LogOperationStopped, Color.Orange);
            }
        }

        // ========== ЗАГРУЗКА СПИСКА ПРИЛОЖЕНИЙ ==========

        internal void LoadInstalledProducts()
        {
            lstInstalledApps.Items.Clear();
            _allProducts.Clear();
            AppendLog(Loc.LogStart, Color.Yellow);

            try
            {
                HashSet<string> addedKeys = new HashSet<string>();
                List<ProductInfo> tempList = new List<ProductInfo>();

                void ProcessRegistryView(RegistryView view, string registryPath)
                {
                    using (RegistryKey baseKey = RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, view))
                    using (RegistryKey root = baseKey.OpenSubKey(registryPath))
                    {
                        if (root == null) return;
                        foreach (string subKeyName in root.GetSubKeyNames())
                        {
                            using (RegistryKey subKey = root.OpenSubKey(subKeyName))
                            {
                                if (subKey == null) continue;
                                string displayName = subKey.GetValue("DisplayName") as string;
                                if (!string.IsNullOrEmpty(displayName) && !addedKeys.Contains(subKeyName))
                                {
                                    var product = ExtractProductInfo(subKey, subKeyName);
                                    tempList.Add(product);
                                    addedKeys.Add(subKeyName);
                                }
                            }
                        }
                    }
                }

                ProcessRegistryView(RegistryView.Registry64, @"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall");
                ProcessRegistryView(RegistryView.Registry32, @"SOFTWARE\WOW6432Node\Microsoft\Windows\CurrentVersion\Uninstall");

                _allProducts.AddRange(tempList.OrderBy(p => p.DisplayName));
                FilterInstalledApps();
                AppendLog(string.Format(Loc.LogLoaded, _allProducts.Count), Color.Green);
            }
            catch (Exception ex)
            {
                AppendLog($"[ERROR] {Loc.LogLoadError(ex.Message)}", Color.Red);
            }
        }

        private ProductInfo ExtractProductInfo(RegistryKey subKey, string keyName)
        {
            string displayName = subKey.GetValue("DisplayName") as string;
            string uninstallString = subKey.GetValue("UninstallString") as string;
            string quietUninstallString = subKey.GetValue("QuietUninstallString") as string;
            string publisher = subKey.GetValue("Publisher") as string;
            string displayVersion = subKey.GetValue("DisplayVersion") as string;
            string installLocation = subKey.GetValue("InstallLocation") as string;
            string installDate = subKey.GetValue("InstallDate") as string;
            object estimatedSizeRaw = subKey.GetValue("EstimatedSize");
            string estimatedSize = estimatedSizeRaw != null ? Convert.ToString(estimatedSizeRaw) : null;
            string exePath = ExtractExePath(uninstallString);
            InstallerType installerType = DetectInstallerType(uninstallString, subKey, exePath);

            return new ProductInfo
            {
                KeyName = keyName,
                UninstallString = uninstallString,
                QuietUninstallString = quietUninstallString,
                DisplayName = displayName,
                Publisher = publisher,
                DisplayVersion = displayVersion,
                InstallLocation = installLocation,
                InstallDate = installDate,
                EstimatedSize = estimatedSize,
                InstallerType = installerType
            };
        }

        private void FilterInstalledApps()
        {
            lstInstalledApps.Items.Clear();
            string filter = txtSearchFilter.Text.Trim().ToLower();
            var filtered = string.IsNullOrEmpty(filter)
                ? _allProducts
                : _allProducts.Where(p => p.DisplayName.ToLower().Contains(filter)).ToList();
            foreach (var product in filtered)
                lstInstalledApps.Items.Add(product);
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
                MessageBox.Show(Loc.RestartAsAdminFailed(ex.Message), Loc.TitleError, MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }
        public bool RemoveRegistryEntry(string keyName)
        {
            if (string.IsNullOrEmpty(keyName))
                return false;

            bool removed = false;
            string[] paths = {
        @"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall",
        @"SOFTWARE\WOW6432Node\Microsoft\Windows\CurrentVersion\Uninstall"
    };

            foreach (string path in paths)
            {
                try
                {
                    using (RegistryKey parent64 = RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, RegistryView.Registry64))
                    {
                        using (RegistryKey key = parent64.OpenSubKey(path, true))
                        {
                            if (key != null && key.GetSubKeyNames().Contains(keyName))
                            {
                                key.DeleteSubKeyTree(keyName);
                                removed = true;
                            }
                        }
                    }

                    using (RegistryKey parent32 = RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, RegistryView.Registry32))
                    {
                        using (RegistryKey key = parent32.OpenSubKey(path, true))
                        {
                            if (key != null && key.GetSubKeyNames().Contains(keyName))
                            {
                                key.DeleteSubKeyTree(keyName);
                                removed = true;
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    AppendLog($"[ERROR] {Loc.LogRegistryDeleteFailed(keyName, path, ex.Message)}", Color.Red);
                }
            }

            return removed;
        }
        private string ExtractExePath(string uninstallString)
        {
            if (string.IsNullOrEmpty(uninstallString)) return null;
            string trimmed = uninstallString.Trim();
            if (trimmed.StartsWith("\""))
            {
                int endQuote = trimmed.IndexOf("\"", 1);
                if (endQuote > 0) return trimmed.Substring(1, endQuote - 1);
            }
            else
            {
                int spaceIndex = trimmed.IndexOf(' ');
                if (spaceIndex > 0) return trimmed.Substring(0, spaceIndex);
                else return trimmed;
            }
            return null;
        }

        private InstallerType DetectInstallerType(string uninstallString, RegistryKey subKey, string exePath)
        {
            if (string.IsNullOrEmpty(uninstallString)) return InstallerType.Unknown;
            if (uninstallString.IndexOf("msiexec", StringComparison.OrdinalIgnoreCase) >= 0) return InstallerType.Msi;

            try
            {
                if (subKey?.GetValue("InnoSetupCodeFile") != null) return InstallerType.InnoSetup;
                object publisher = subKey?.GetValue("Publisher");
                if (publisher != null && publisher.ToString().StartsWith("NSIS:")) return InstallerType.NSIS;
            }
            catch { }

            if (!string.IsNullOrEmpty(exePath))
            {
                string exeName = Path.GetFileName(exePath);
                if (exeName.Equals("unins000.exe", StringComparison.OrdinalIgnoreCase)) return InstallerType.InnoSetup;
                if (exeName.Equals("uninstall.exe", StringComparison.OrdinalIgnoreCase))
                {
                    if (uninstallString.IndexOf("-remove", StringComparison.OrdinalIgnoreCase) >= 0) return InstallerType.AdvancedInstaller;
                    if (uninstallString.IndexOf("-silent", StringComparison.OrdinalIgnoreCase) >= 0) return InstallerType.Cisco;
                    if (uninstallString.IndexOf("/S", StringComparison.OrdinalIgnoreCase) >= 0) return InstallerType.NSIS;
                }
                if (uninstallString.IndexOf(" /x", StringComparison.OrdinalIgnoreCase) >= 0 ||
                    uninstallString.IndexOf(" -x", StringComparison.OrdinalIgnoreCase) >= 0) return InstallerType.Wise;
            }

            if (!string.IsNullOrEmpty(exePath) && File.Exists(exePath))
            {
                try
                {
                    var versionInfo = FileVersionInfo.GetVersionInfo(exePath);
                    if (versionInfo.Comments?.IndexOf("Inno Setup", StringComparison.OrdinalIgnoreCase) >= 0 ||
                        versionInfo.FileDescription?.IndexOf("Inno", StringComparison.OrdinalIgnoreCase) >= 0) return InstallerType.InnoSetup;
                    if (versionInfo.FileDescription?.IndexOf("NSIS", StringComparison.OrdinalIgnoreCase) >= 0) return InstallerType.NSIS;
                    if (versionInfo.CompanyName?.IndexOf("Advanced Installer", StringComparison.OrdinalIgnoreCase) >= 0) return InstallerType.AdvancedInstaller;
                    if (versionInfo.CompanyName?.IndexOf("Adobe", StringComparison.OrdinalIgnoreCase) >= 0) return InstallerType.Adobe;
                }
                catch { }
            }

            if (uninstallString.IndexOf("Adobe", StringComparison.OrdinalIgnoreCase) >= 0 &&
                (uninstallString.IndexOf("Uninstaller.exe", StringComparison.OrdinalIgnoreCase) >= 0 ||
                 uninstallString.IndexOf("HDBox", StringComparison.OrdinalIgnoreCase) >= 0)) return InstallerType.Adobe;

            return InstallerType.Unknown;
        }

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
                                        return ExtractProductInfo(subKey, subKeyName);
                                }
                            }
                        }
                    }
                }
            }
            return null;
        }

        private bool IsProductInstalled(string productName)
        {
            return FindProductInfo(productName) != null;
        }

        private string BuildCommand(string productName, string customCommand, bool addSilentKeys = true)
        {
            if (!string.IsNullOrEmpty(customCommand))
            {
                AppendLog(Loc.LogCustomCommand(customCommand), Color.Cyan);
                return customCommand;
            }

            ProductInfo productInfo = FindProductInfo(productName);
            if (productInfo == null)
            {
                AppendLog(Loc.LogProductNotFound(productName), Color.Yellow);
                return null;
            }

            AppendLog(Loc.LogKeyFound(productInfo.KeyName), Color.Green);
            AppendLog(Loc.LogDisplayName(productInfo.DisplayName), Color.Cyan);
            AppendLog(Loc.LogInstallerType(productInfo.InstallerType.ToString()), Color.Cyan);

            if (!string.IsNullOrEmpty(productInfo.QuietUninstallString))
            {
                AppendLog(Loc.LogUsingQuietUninstall, Color.Cyan);
                return productInfo.QuietUninstallString;
            }

            string cmd = productInfo.UninstallString;
            if (string.IsNullOrEmpty(cmd))
            {
                AppendLog(Loc.LogUninstallStringEmpty, Color.Red);
                return null;
            }

            if (addSilentKeys)
            {
                switch (productInfo.InstallerType)
                {
                    case InstallerType.Msi:
                        if (cmd.IndexOf(" /I", StringComparison.OrdinalIgnoreCase) >= 0) cmd = cmd.Replace("/I", "/X");
                        if (cmd.IndexOf("/quiet", StringComparison.OrdinalIgnoreCase) < 0 &&
                            cmd.IndexOf("/qb", StringComparison.OrdinalIgnoreCase) < 0) cmd += " /qn /norestart REBOOT=ReallySuppress";
                        else if (cmd.IndexOf("/norestart", StringComparison.OrdinalIgnoreCase) < 0) cmd += " /norestart";
                        break;
                    case InstallerType.InnoSetup:
                        if (!cmd.Contains("/VERYSILENT")) cmd += " /VERYSILENT /NORESTART";
                        break;
                    case InstallerType.NSIS:
                        if (!cmd.Contains("/S")) cmd += " /S";
                        break;
                    case InstallerType.Wise:
                        if (!cmd.Contains(" /s")) cmd += " /s";
                        break;
                    case InstallerType.InstallAnywhere:
                        if (!cmd.Contains("-i silent")) cmd += " -i silent";
                        break;
                    case InstallerType.AdvancedInstaller:
                        if (cmd.IndexOf("-remove", StringComparison.OrdinalIgnoreCase) >= 0) cmd = cmd.Replace("-remove", "/ex /quiet /norestart");
                        else if (!cmd.Contains("/ex")) cmd += " /ex /quiet /norestart";
                        break;
                    case InstallerType.WixBurn:
                        if (!cmd.Contains("-uninstall")) cmd += " -uninstall -s -norestart";
                        break;
                    case InstallerType.Adobe:
                        if (!cmd.Contains("--silent") && !cmd.Contains("--mode=")) cmd += " --silent";
                        break;
                    case InstallerType.Cisco:
                        if (!cmd.Contains("-silent"))
                        {
                            if (cmd.Contains("/S")) cmd = cmd.Replace("/S", "").Trim();
                            cmd += " -silent";
                        }
                        break;
                    default:
                        if (!cmd.Contains("/S") && !cmd.Contains("/quiet") && !cmd.Contains("--silent")) cmd += " /S";
                        break;
                }
            }

            // Экранирование пробелов в пути к исполняемому файлу
            if (!string.IsNullOrEmpty(cmd) && !cmd.TrimStart().StartsWith("\""))
            {
                string trimmed = cmd.TrimStart();
                // Ищем конец пути к исполняемому файлу (расширение .exe, .bat, .cmd)
                int exeEnd = -1;
                foreach (string ext in new[] { ".exe", ".bat", ".cmd" })
                {
                    int idx = trimmed.IndexOf(ext, StringComparison.OrdinalIgnoreCase);
                    if (idx >= 0)
                    {
                        exeEnd = idx + ext.Length;
                        break;
                    }
                }

                if (exeEnd > 0 && (exeEnd >= trimmed.Length || trimmed[exeEnd] == ' ' || trimmed[exeEnd] == '\t'))
                {
                    // Отделяем путь к файлу от аргументов
                    string exePath = trimmed.Substring(0, exeEnd);
                    string args = trimmed.Substring(exeEnd);
                    cmd = $"\"{exePath}\"{args}";
                }
                else if (trimmed.IndexOf(' ') >= 0)
                {
                    // Расширение не найдено, но есть пробелы – считаем, что аргументов нет, оборачиваем всю строку
                    cmd = $"\"{trimmed}\"";
                }
            }

            AppendLog(Loc.LogFinalCommand(cmd), Color.Cyan);
            return cmd;
        }

        // ========== УПРАВЛЕНИЕ СЕТЬЮ ==========

        private void DisableNetwork()
        {
            _disabledAdapters.Clear();
            try
            {
                AppendLog(Loc.LogNetworkDisabling, Color.Yellow);
                using (var searcher = new ManagementObjectSearcher("SELECT * FROM Win32_NetworkAdapter WHERE NetEnabled=true AND NetConnectionID != null"))
                {
                    foreach (ManagementObject adapter in searcher.Get().Cast<ManagementObject>())
                    {
                        string name = adapter["NetConnectionID"].ToString();
                        try
                        {
                            adapter.InvokeMethod("Disable", null);
                            _disabledAdapters.Add(name);
                            AppendLog($"  [-] {Loc.LogNetworkAdapterDisabled(name)}", Color.Gray);
                        }
                        catch { }
                    }
                }
                Thread.Sleep(2500);
                AppendLog(Loc.LogNetworkDisabled, Color.Green);
            }
            catch (Exception ex)
            {
                AppendLog($"[ERROR] {Loc.LogNetworkDisableError(ex.Message)}", Color.Red);
            }
        }

        private void EnableNetwork()
        {
            if (_disabledAdapters.Count == 0) return;
            try
            {
                AppendLog(Loc.LogNetworkEnabling, Color.Yellow);
                using (var searcher = new ManagementObjectSearcher("SELECT * FROM Win32_NetworkAdapter"))
                {
                    foreach (ManagementObject adapter in searcher.Get().Cast<ManagementObject>())
                    {
                        string name = adapter["NetConnectionID"]?.ToString();
                        if (!string.IsNullOrEmpty(name) && _disabledAdapters.Contains(name))
                        {
                            try
                            {
                                adapter.InvokeMethod("Enable", null);
                                AppendLog($"  [+] {Loc.LogNetworkAdapterEnabled(name)}", Color.Gray);
                            }
                            catch { }
                        }
                    }
                }
                _disabledAdapters.Clear();
                Thread.Sleep(2500);
                AppendLog(Loc.LogNetworkEnabled, Color.Green);
            }
            catch (Exception ex)
            {
                AppendLog($"[ERROR] {Loc.LogNetworkEnableError(ex.Message)}", Color.Red);
            }
        }

        // ========== МЕТОДЫ УДАЛЕНИЯ (ИСПРАВЛЕНО – disableNetwork передаётся параметром) ==========

        private void RunUninstallDirect(string productName, string customCommand, int maxWait, int checkInterval, CancellationToken token, bool disableNetwork)
        {
            isRunning = true;
            try
            {
                if (disableNetwork) DisableNetwork();

                AppendLog(Loc.LogUninstallStartDirect, Color.Cyan);
                string innerCmd = BuildCommand(productName, customCommand, false);
                if (string.IsNullOrEmpty(innerCmd)) return;

                // Извлекаем путь к исполняемому файлу для проверки существования
                string exePath = ExtractExePath(innerCmd);
                if (!string.IsNullOrEmpty(exePath) && !File.Exists(exePath))
                {
                    AppendLog(Loc.LogUninstallerFileNotFound(exePath), Color.Yellow);
                    DialogResult removeReg = MessageBox.Show(Loc.FileNotFoundDialog(exePath),
                        Loc.TitleFileNotFound,
                        MessageBoxButtons.YesNo,
                        MessageBoxIcon.Warning);
                    if (removeReg == DialogResult.Yes)
                    {
                        ProductInfo prod = FindProductInfo(productName);
                        if (prod != null && RemoveRegistryEntry(prod.KeyName))
                        {
                            AppendLog(Loc.LogRegistryEntryRemoved, Color.Green);
                            this.Invoke((Action)(() => LoadInstalledProducts()));
                        }
                        else
                            AppendLog(Loc.RegistryRemoveFailed, Color.Red);
                    }
                    return; // Выходим, не запуская процесс
                }

                ProcessStartInfo psi = new ProcessStartInfo
                {
                    FileName = "cmd.exe",
                    Arguments = $"/c {innerCmd}",
                    UseShellExecute = false,
                    CreateNoWindow = false,        // показывать окно консоли
                    WindowStyle = ProcessWindowStyle.Normal
                };
                AppendLog(Loc.LogLaunching(psi.FileName, psi.Arguments), Color.Yellow);
                Process process = Process.Start(psi);
                if (process == null || process.HasExited)
                {
                    AppendLog(Loc.ProcessFailedToStart, Color.Red);
                    return;
                }
                AppendLog(Loc.LogProcessStarted(process.Id), Color.Green);
                WaitForUninstall(productName, customCommand, maxWait, checkInterval, token);
                // Ждём завершения процесса (без убийства)
                process.WaitForExit();
                AppendLog(Loc.LogProcessFinished, Color.Cyan);

                if (!IsProductInstalled(productName))
                    AppendLog("✅ " + Loc.ProductRemovedSuccess, Color.Green);
                else
                    AppendLog(Loc.LogRegistryEntryRemain, Color.Yellow);
            }
            catch (Exception ex) { AppendLog($"[ERROR] {ex.Message}", Color.Red); }
            finally
            {
                if (disableNetwork) EnableNetwork();
                FinishOperation();
            }
        }

        private void SaveOriginalServicePath(string serviceName, string originalPath)
        {
            string keyPath = @"SOFTWARE\InfoWatchUninstaller\ServiceBackup";
            using (RegistryKey key = Registry.LocalMachine.CreateSubKey(keyPath))
                key.SetValue(serviceName, originalPath);
        }

        private string GetSavedOriginalServicePath(string serviceName)
        {
            string keyPath = @"SOFTWARE\InfoWatchUninstaller\ServiceBackup";
            using (RegistryKey key = Registry.LocalMachine.OpenSubKey(keyPath))
                return key?.GetValue(serviceName) as string;
        }

        private void RunUninstallViaService(string productName, string serviceName, string customCommand, int maxWait, int checkInterval, CancellationToken token, bool disableNetwork)
        {
            isRunning = true;
            bool serviceStopped = false;
            try
            {
                if (disableNetwork) DisableNetwork();

                AppendLog(Loc.LogUninstallStartService, Color.Cyan);
                string currentPath = GetServiceImagePath(serviceName);
                if (string.IsNullOrEmpty(currentPath))
                {
                    AppendLog(Loc.LogServiceNotFound(serviceName), Color.Red);
                    return;
                }

                if (currentPath.IndexOf("cmd.exe /c", StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    AppendLog(Loc.LogServiceAlreadyModified, Color.Yellow);
                    string backup = GetSavedOriginalServicePath(serviceName);
                    if (!string.IsNullOrEmpty(backup))
                    {
                        SetServiceImagePath(serviceName, backup);
                        AppendLog(Loc.LogServiceRestoredFromBackup(backup), Color.Cyan);
                        currentPath = backup;
                    }
                    else
                    {
                        AppendLog(Loc.LogServiceBackupNotFound, Color.Red);
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

                AppendLog(Loc.LogOriginalImagePath(originalPath), Color.Cyan);
                string innerCmd = BuildCommand(productName, customCommand);
                if (string.IsNullOrEmpty(innerCmd)) return;

                string newImagePath = $"cmd.exe /c start /b \"\" {innerCmd}";
                AppendLog(Loc.LogSettingImagePath(newImagePath), Color.Yellow);
                SetServiceImagePath(serviceName, newImagePath);

                AppendLog(Loc.LogStoppingService(serviceName), Color.Yellow);
                StopService(serviceName);
                serviceStopped = true;
                Thread.Sleep(3000);

                AppendLog(Loc.LogStartingService(serviceName), Color.Green);
                bool serviceStarted = StartServiceWithCheck(serviceName);
                if (!serviceStarted) AppendLog(Loc.LogServiceStartWarning, Color.Yellow);

                CheckProcess(innerCmd);
                WaitForUninstall(productName, customCommand, maxWait, checkInterval, token);

                string restorePath = GetSavedOriginalServicePath(serviceName) ?? originalPath;
                AppendLog(Loc.LogRestoringImagePath(restorePath), Color.Yellow);
                SetServiceImagePath(serviceName, restorePath);

                AppendLog(Loc.LogRestartingService(serviceName), Color.Yellow);
                StopService(serviceName);
                Thread.Sleep(2000);
                bool restartOk = StartServiceWithCheck(serviceName);
                if (!restartOk) AppendLog(Loc.LogServiceRestartFailed, Color.Yellow);
                else AppendLog(Loc.LogServiceRestored, Color.Green);
            }
            catch (Exception ex) { AppendLog($"[ERROR] {ex.Message}", Color.Red); }
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
                            AppendLog(Loc.LogForceRestoreBackup, Color.Yellow);
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
                FinishOperation();
            }
        }

        private void RunUninstallViaSystem(string productName, string customCommand, int maxWait, int checkInterval, CancellationToken token, bool disableNetwork)
        {
            isRunning = true;
            try
            {
                if (disableNetwork) DisableNetwork();

                AppendLog(Loc.LogUninstallStartSystem, Color.Cyan);
                string innerCmd = BuildCommand(productName, customCommand);
                if (string.IsNullOrEmpty(innerCmd)) return;

                string fullCmd = $"cmd.exe /c {innerCmd}";
                AppendLog(Loc.LogSystemLaunch(fullCmd), Color.Yellow);

                int pid = SystemProcessLauncher.LaunchProcessAsSystem(fullCmd);
                if (pid > 0)
                {
                    AppendLog(Loc.LogProcessStarted(pid), Color.Green);
                    WaitForUninstall(productName, customCommand, maxWait, checkInterval, token);
                }
                else
                {
                    AppendLog(Loc.LogSystemLaunchFailed(pid), Color.Red);
                }
            }
            catch (Exception ex) { AppendLog($"[ERROR] {ex.Message}", Color.Red); }
            finally
            {
                if (disableNetwork) EnableNetwork();
                FinishOperation();
            }
        }

        private void FinishOperation()
        {
            isRunning = false;
            if (btnUninstall.InvokeRequired)
            {
                btnUninstall.Invoke((Action)(() =>
                {
                    btnUninstall.Enabled = true;
                    btnStop.Enabled = false;
                }));
            }
            else
            {
                btnUninstall.Enabled = true;
                btnStop.Enabled = false;
            }
            lblStatus.Invoke((Action)(() => { lblStatus.Text = Loc.StatusReady; lblStatus.ForeColor = Color.DarkGreen; }));
        }

        // ========== РАБОТА СО СЛУЖБОЙ ==========

        private string GetServiceImagePath(string serviceName)
        {
            string keyPath = $@"SYSTEM\CurrentControlSet\Services\{serviceName}";
            using (RegistryKey key = Registry.LocalMachine.OpenSubKey(keyPath, false))
                return key?.GetValue("ImagePath") as string;
        }

        private void SetServiceImagePath(string serviceName, string newPath)
        {
            string keyPath = $@"SYSTEM\CurrentControlSet\Services\{serviceName}";
            using (RegistryKey key = Registry.LocalMachine.OpenSubKey(keyPath, true))
            {
                if (key == null) throw new Exception(Loc.ServiceRegistryOpenFailed(serviceName));
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
                    int timeout = 10000, elapsed = 0, interval = 500;
                    while (elapsed < timeout)
                    {
                        sc.Refresh();
                        if (sc.Status == ServiceControllerStatus.Running) return true;
                        if (sc.Status == ServiceControllerStatus.Stopped || sc.Status == ServiceControllerStatus.StopPending) break;
                        Thread.Sleep(interval);
                        elapsed += interval;
                    }
                    return false;
                }
            }
            catch (Exception ex)
            {
                AppendLog(Loc.LogServiceError(serviceName, ex.Message), Color.Red);
                return false;
            }
        }

        private void CheckProcess(string commandLine)
        {
            string processName = null;
            string exePath = commandLine.Trim();
            if (exePath.StartsWith("\""))
            {
                int endQuote = exePath.IndexOf("\"", 1);
                if (endQuote > 0) exePath = exePath.Substring(1, endQuote - 1);
            }
            else exePath = exePath.Split(' ')[0];
            if (!string.IsNullOrEmpty(exePath)) processName = Path.GetFileNameWithoutExtension(exePath);
            if (string.IsNullOrEmpty(processName)) return;

            bool found = false;
            for (int i = 0; i < 10; i++)
            {
                if (Process.GetProcessesByName(processName).Length > 0) { found = true; break; }
                Thread.Sleep(500);
            }
            if (found) AppendLog(Loc.LogProcessStartedName(processName), Color.Green);
            else AppendLog(Loc.LogProcessNotFound(processName), Color.Yellow);
        }

        // ========== ЖУРНАЛ И ОЖИДАНИЕ ==========

        private void WaitForUninstall(string productName, string customCommand, int maxWait, int checkInterval, CancellationToken token)
        {
            int elapsed = 0;
            if (string.IsNullOrEmpty(customCommand))
            {
                AppendLog(Loc.LogWaitUninstall(maxWait), Color.Yellow);
                bool uninstalled = false;
                while (elapsed < maxWait)
                {
                    if (token.IsCancellationRequested)
                    {
                        AppendLog(Loc.LogWaitInterrupted, Color.Orange);
                        break;
                    }

                    Thread.Sleep(checkInterval * 1000);
                    elapsed += checkInterval;

                    if (!IsProductInstalled(productName))
                    {
                        AppendLog(Loc.LogProductRemoved(productName), Color.Green);
                        uninstalled = true;
                        this.Invoke((Action)(() => LoadInstalledProducts()));
                        break;
                    }
                    AppendLog($"    [{elapsed}/{maxWait}] {Loc.LogProductStillPresent}", Color.Gray);
                }
                if (!uninstalled && !token.IsCancellationRequested)
                    AppendLog(Loc.LogTimeout, Color.Yellow);
            }
            else
            {
                AppendLog(Loc.LogCustomCommandWait(maxWait), Color.Yellow);
                for (int i = 0; i < maxWait; i++)
                {
                    if (token.IsCancellationRequested)
                    {
                        AppendLog(Loc.LogWaitInterrupted, Color.Orange);
                        break;
                    }
                    Thread.Sleep(1000);
                }
                if (!token.IsCancellationRequested)
                    AppendLog(Loc.LogWaitComplete, Color.Gray);
            }
        }

        private void AppendLog(string message, Color color)
        {
            if (rtbLog.InvokeRequired) { rtbLog.Invoke((Action)(() => AppendLog(message, color))); return; }
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

    // ---------- Форма деталей продукта ----------
    internal class ProductDetailsForm : Form

    {
        private readonly ProductInfo product;
        private readonly MainForm ownerForm;  // Ссылка на главную форму для вызова удаления

        public ProductDetailsForm(ProductInfo productInfo, MainForm owner)
        {
            product = productInfo;
            ownerForm = owner;
            InitializeComponent();
        }

        private void InitializeComponent()
        {
            this.Text = Loc.ProductDetailsTitle;
            this.StartPosition = FormStartPosition.CenterParent;
            this.Size = new Size(550, 480);
            this.FormBorderStyle = FormBorderStyle.FixedDialog;
            this.MaximizeBox = false;
            this.MinimizeBox = false;

            var panel = new Panel { AutoScroll = true, Dock = DockStyle.Fill, Padding = new Padding(10) };
            int y = 10, labelWidth = 120, valueLeft = 130, fieldWidth = 340, lineHeight = 30;

            AddLabelValue(panel, Loc.DetailName, product.DisplayName, ref y, labelWidth, valueLeft, fieldWidth, lineHeight);
            AddLabelValue(panel, Loc.DetailPublisher, product.Publisher ?? Loc.Unknown, ref y, labelWidth, valueLeft, fieldWidth, lineHeight);
            AddLabelValue(panel, Loc.DetailVersion, product.DisplayVersion ?? Loc.NotSpecified, ref y, labelWidth, valueLeft, fieldWidth, lineHeight);
            string formattedDate = FormatInstallDate(product.InstallDate);
            AddLabelValue(panel, Loc.DetailInstallDate, formattedDate ?? Loc.NotSpecified, ref y, labelWidth, valueLeft, fieldWidth, lineHeight);
            string sizeStr = FormatSize(product.EstimatedSize);
            AddLabelValue(panel, Loc.DetailSize, sizeStr, ref y, labelWidth, valueLeft, fieldWidth, lineHeight);
            AddLabelValue(panel, Loc.DetailLocation, product.InstallLocation ?? Loc.NotSpecifiedM, ref y, labelWidth, valueLeft, fieldWidth, lineHeight);
            AddLabelValue(panel, Loc.DetailRegistryKey, product.KeyName, ref y, labelWidth, valueLeft, fieldWidth, lineHeight);
            AddLabelValue(panel, Loc.DetailInstallerType, product.InstallerType.ToString(), ref y, labelWidth, valueLeft, fieldWidth, lineHeight);
            AddLabelValue(panel, Loc.DetailUninstallCmd, product.UninstallString ?? Loc.Absent, ref y, labelWidth, valueLeft, fieldWidth, lineHeight);
            AddLabelValue(panel, Loc.DetailQuietCmd, product.QuietUninstallString ?? Loc.Absent, ref y, labelWidth, valueLeft, fieldWidth, lineHeight);

            var btnCopy = new Button
            {
                Text = Loc.CopyUninstallCmd,
                Location = new Point(valueLeft, y + 10),
                Width = 200,
                Height = 30
            };
            btnCopy.Click += (s, e) =>
            {
                if (!string.IsNullOrEmpty(product.UninstallString))
                {
                    Clipboard.SetText(product.UninstallString);
                    MessageBox.Show(Loc.UninstallCmdCopied);
                }
                else MessageBox.Show(Loc.UninstallCmdNotFound);
            };

            var btnUseForUninstall = new Button
            {
                Text = Loc.UseForUninstall,
                Location = new Point(valueLeft + 210, y + 10),
                Width = 180,
                Height = 30
            };
            btnUseForUninstall.Click += (s, e) => { this.DialogResult = DialogResult.OK; this.Close(); };
            var btnDeleteRegistry = new Button
            {
                Text = Loc.DeleteRegistryEntry,
                Location = new Point(valueLeft + 10, y + 50), // y – последнее значение после тихой команды
                Width = 200,
                Height = 30
            };
            btnDeleteRegistry.Click += (s, e) =>
            {
                if (MessageBox.Show(
                    Loc.ConfirmDeleteRegistry(product.DisplayName),
                    Loc.TitleConfirmRegistryDelete,
                    MessageBoxButtons.YesNo, MessageBoxIcon.Warning) == DialogResult.Yes)
                {
                    if (ownerForm.RemoveRegistryEntry(product.KeyName))
                    {
                        ownerForm.LoadInstalledProducts();
                        MessageBox.Show(Loc.RegistryDeleted, Loc.TitleDone, MessageBoxButtons.OK, MessageBoxIcon.Information);
                        this.DialogResult = DialogResult.OK;
                        this.Close();
                    }
                    else
                    {
                        MessageBox.Show(Loc.RegistryDeleteFailed, Loc.TitleError, MessageBoxButtons.OK, MessageBoxIcon.Error);
                    }
                }
            };

            
            panel.Controls.Add(btnCopy);
            panel.Controls.Add(btnUseForUninstall);
            panel.Controls.Add(btnDeleteRegistry);
            this.Controls.Add(panel);
        }

        private void AddLabelValue(Panel parent, string labelText, string value, ref int y, int labelWidth, int valueLeft, int fieldWidth, int lineHeight)
        {
            Label lbl = new Label { Text = labelText, Location = new Point(10, y), Size = new Size(labelWidth - 10, lineHeight), TextAlign = ContentAlignment.MiddleRight, Font = new Font("Microsoft Sans Serif", 8.5f, FontStyle.Bold) };
            TextBox txt = new TextBox { Text = value, Location = new Point(valueLeft, y), Size = new Size(fieldWidth, lineHeight), ReadOnly = true, Font = new Font("Consolas", 8.5f), BackColor = SystemColors.ControlLightLight };
            parent.Controls.Add(lbl);
            parent.Controls.Add(txt);
            y += lineHeight + 4;
        }

        private string FormatInstallDate(string rawDate)
        {
            if (string.IsNullOrEmpty(rawDate) || rawDate.Length != 8) return rawDate;
            if (int.TryParse(rawDate, out _))
            {
                try
                {
                    int year = int.Parse(rawDate.Substring(0, 4));
                    int month = int.Parse(rawDate.Substring(4, 2));
                    int day = int.Parse(rawDate.Substring(6, 2));
                    return $"{day:D2}.{month:D2}.{year}";
                }
                catch { }
            }
            return rawDate;
        }

        private string FormatSize(string estimatedSizeKb)
        {
            if (string.IsNullOrEmpty(estimatedSizeKb)) return Loc.NotSpecifiedSize;
            if (int.TryParse(estimatedSizeKb, out int kb))
            {
                if (kb >= 1048576) return Loc.SizeGb(kb / 1048576.0);
                if (kb >= 1024) return Loc.SizeMb(kb / 1024.0);
                return Loc.SizeKb(kb);
            }
            return estimatedSizeKb;
        }
    }

    // ---------- Точка входа ----------
    internal static class Program
    {
        [STAThread]
        static void Main()
        {
            Application.EnableVisualStyles();
            //System.Threading.Thread.CurrentThread.CurrentUICulture = new System.Globalization.CultureInfo("en-US");
            Application.SetCompatibleTextRenderingDefault(false);

            if (!IsAdministrator())
            {
                DialogResult result = MessageBox.Show(
                    Loc.AdminStartupMessage,
                    Loc.TitleAdminRequired,
                    MessageBoxButtons.YesNo,
                    MessageBoxIcon.Information);

                if (result == DialogResult.Yes)
                {
                    try
                    {
                        ProcessStartInfo psi = new ProcessStartInfo
                        {
                            FileName = Application.ExecutablePath,
                            Verb = "runas",
                            UseShellExecute = true
                        };
                        Process.Start(psi);
                        Application.Exit();
                    }
                    catch
                    {
                        MessageBox.Show(
                            Loc.AdminStartupFailed,
                            Loc.TitleError,
                            MessageBoxButtons.OK,
                            MessageBoxIcon.Error);
                        Application.Exit();
                    }
                }
                else
                {
                    Application.Exit();
                }
            }
            else
            {
                Application.Run(new MainForm());
            }
        }

        private static bool IsAdministrator()
        {
            WindowsIdentity identity = WindowsIdentity.GetCurrent();
            WindowsPrincipal principal = new WindowsPrincipal(identity);
            return principal.IsInRole(WindowsBuiltInRole.Administrator);
        }
    }
}
