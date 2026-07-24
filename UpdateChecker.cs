using System;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Net;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace InfoWatchUninstaller
{
    internal static class UpdateChecker
    {
        private const string GitHubApiUrl = "https://api.github.com/repos/kizrrum/SysUninstaller/releases/latest";
        private const string GitHubReleasesPage = "https://github.com/kizrrum/SysUninstaller/releases/latest";

        public static async void CheckForUpdates(Action<string, Color> logAction)
        {
            try
            {
                logAction?.Invoke("[Обновление] Проверка наличия новой версии...", Color.Gray);

                // Включаем поддержку TLS 1.2 (обязательно для GitHub API в .NET 4.5.2)
                ServicePointManager.SecurityProtocol = SecurityProtocolType.Tls12;

                using (var client = new WebClient())
                {
                    client.Headers.Add("User-Agent", "SysUninstaller-UpdateCheck/1.0");

                    string json = await client.DownloadStringTaskAsync(GitHubApiUrl);
                    logAction?.Invoke("[Обновление] Ответ от GitHub получен.", Color.Gray);

                    string latestVersionString = ExtractJsonValue(json, "tag_name")?.TrimStart('v', 'V');
                    if (string.IsNullOrEmpty(latestVersionString))
                    {
                        logAction?.Invoke("[Обновление] Не удалось извлечь tag_name из ответа.", Color.Red);
                        return;
                    }

                    if (!Version.TryParse(latestVersionString, out Version latestVersion))
                    {
                        logAction?.Invoke($"[Обновление] Не удалось распознать версию: '{latestVersionString}'", Color.Red);
                        return;
                    }

                    Version currentVersion = GetCurrentVersion();
                    if (currentVersion == null)
                    {
                        logAction?.Invoke("[Обновление] Не удалось получить текущую версию приложения.", Color.Red);
                        return;
                    }

                    logAction?.Invoke($"[Обновление] Текущая версия: {currentVersion}, доступная: {latestVersion}", Color.Gray);

                    if (latestVersion <= currentVersion)
                    {
                        logAction?.Invoke("[Обновление] Нет новой версии.", Color.Gray);
                        return;
                    }

                    logAction?.Invoke("[Обновление] Обнаружена новая версия!", Color.Green);

                    string downloadUrl = ExtractDownloadUrl(json);
                    if (string.IsNullOrEmpty(downloadUrl))
                    {
                        logAction?.Invoke("[Обновление] Не удалось найти ссылку для загрузки.", Color.Yellow);
                        ShowManualUpdatePrompt(latestVersion, currentVersion);
                        return;
                    }

                    DialogResult result = MessageBox.Show(
                        $"Доступна новая версия: v{latestVersion}\n" +
                        $"Текущая версия: v{currentVersion}\n\n" +
                        "Загрузить и установить обновление автоматически?",
                        "Обновление доступно",
                        MessageBoxButtons.YesNo,
                        MessageBoxIcon.Information);

                    if (result == DialogResult.Yes)
                    {
                        await DownloadAndInstallUpdate(client, downloadUrl, logAction);
                    }
                }
            }
            catch (Exception ex)
            {
                logAction?.Invoke($"[Обновление] Ошибка: {ex.Message}", Color.Red);
            }
        }

        private static async Task DownloadAndInstallUpdate(WebClient client, string downloadUrl, Action<string, Color> logAction)
        {
            string tempFolder = Path.GetTempPath();
            string newExePath = Path.Combine(tempFolder, "SysUninstaller_Update.exe");
            string batchPath = Path.Combine(tempFolder, "update_sysuninstaller.bat");

            try
            {
                logAction?.Invoke("[Обновление] Загрузка новой версии...", Color.Gray);
                await client.DownloadFileTaskAsync(downloadUrl, newExePath);
                logAction?.Invoke("[Обновление] Загрузка завершена. Установка обновления...", Color.Green);

                string currentExePath = Application.ExecutablePath;

                string batchContent =
                    "@echo off\r\n" +
                    "timeout /t 2 /nobreak > nul\r\n" +
                    $"copy /y \"{newExePath}\" \"{currentExePath}\"\r\n" +
                    $"start \"\" \"{currentExePath}\"\r\n" +
                    $"del /f /q \"{batchPath}\"\r\n" +
                    $"del /f /q \"{newExePath}\"\r\n";

                File.WriteAllText(batchPath, batchContent);

                ProcessStartInfo psi = new ProcessStartInfo
                {
                    FileName = batchPath,
                    WindowStyle = ProcessWindowStyle.Hidden,
                    CreateNoWindow = true,
                    UseShellExecute = false
                };
                Process.Start(psi);

                Application.Exit();
            }
            catch (Exception ex)
            {
                logAction?.Invoke($"[Обновление] Ошибка при загрузке/установке: {ex.Message}", Color.Red);
                MessageBox.Show($"Не удалось выполнить автоматическое обновление: {ex.Message}\r\n" +
                                "Вы можете обновиться вручную, скачав новую версию с GitHub.",
                                "Ошибка обновления",
                                MessageBoxButtons.OK,
                                MessageBoxIcon.Error);
            }
        }

        private static void ShowManualUpdatePrompt(Version latest, Version current)
        {
            DialogResult result = MessageBox.Show(
                $"Доступна новая версия: v{latest}\nТекущая версия: v{current}\n\n" +
                "Не удалось найти ссылку для автоматической загрузки. Открыть страницу релиза вручную?",
                "Обновление доступно",
                MessageBoxButtons.YesNo,
                MessageBoxIcon.Information);

            if (result == DialogResult.Yes)
                Process.Start(GitHubReleasesPage);
        }

        private static string ExtractDownloadUrl(string json)
        {
            string key = "\"browser_download_url\":\"";
            int start = json.IndexOf(key, StringComparison.OrdinalIgnoreCase);
            if (start < 0) return null;
            start += key.Length;
            int end = json.IndexOf('"', start);
            if (end < 0) return null;
            return json.Substring(start, end - start);
        }

        private static string ExtractJsonValue(string json, string key)
        {
            string search = $"\"{key}\":\"";
            int start = json.IndexOf(search, StringComparison.OrdinalIgnoreCase);
            if (start < 0) return null;
            start += search.Length;
            int end = json.IndexOf('"', start);
            if (end < 0) return null;
            return json.Substring(start, end - start);
        }

        private static Version GetCurrentVersion()
        {
            try
            {
                return new Version(Application.ProductVersion);
            }
            catch
            {
                return null;
            }
        }
    }
}
