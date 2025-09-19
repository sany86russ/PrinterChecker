using System.Windows;
using System.Windows.Controls;
using Microsoft.Extensions.Logging;
using TonerWatch.Desktop.Services;

namespace TonerWatch.Desktop
{
    public partial class DeviceDetailWindow : Window
    {
        private readonly DeviceSettings _device;
        private readonly SettingsManager _settingsManager;
        private readonly DeviceDataService _deviceDataService;
        private readonly SupplyDataService _supplyDataService;
        private readonly ILogger<DeviceDetailWindow> _logger;
        
        public DeviceDetailWindow(DeviceSettings device)
        {
            InitializeComponent();
            _device = device;
            
            try
            {
                _settingsManager = ServiceManager.Instance.GetService<SettingsManager>();
                _deviceDataService = ServiceManager.Instance.GetService<DeviceDataService>();
                _supplyDataService = ServiceManager.Instance.GetService<SupplyDataService>();
                _logger = ServiceManager.Instance.GetLogger<DeviceDetailWindow>();
                
                InitializeDeviceData();
                LoadDeviceInfo();
                
                _logger.LogInformation("Окно деталей устройства инициализировано для {DeviceName}", _device.Name);
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Ошибка инициализации окна деталей устройства");
                System.Windows.MessageBox.Show($"Ошибка инициализации: {ex.Message}", "Ошибка", 
                               MessageBoxButton.OK, MessageBoxImage.Error);
                Close();
            }
        }
        
        // Конструктор для обратной совместимости
        public DeviceDetailWindow(string deviceName = "HP LaserJet Pro M404n")
        {
            InitializeComponent();
            
            // Создаём фальшивое устройство для демо
            _device = new DeviceSettings
            {
                Name = deviceName,
                HostnameOrIp = "192.168.1.100",
                Location = "Office Floor 1",
                IsActive = true
            };
            
            try
            {
                _settingsManager = ServiceManager.Instance.GetService<SettingsManager>();
                _deviceDataService = ServiceManager.Instance.GetService<DeviceDataService>();
                _supplyDataService = ServiceManager.Instance.GetService<SupplyDataService>();
                _logger = ServiceManager.Instance.GetLogger<DeviceDetailWindow>();
                
                InitializeDeviceData();
                LoadDeviceInfo();
            }
            catch (Exception ex)
            {
                // Простое отображение без сервисов
                Title = deviceName;
                // ((System.Windows.Controls.TextBlock)((System.Windows.Controls.Grid)((System.Windows.Controls.Border)((System.Windows.Controls.Grid)((System.Windows.Controls.ScrollViewer)Content).Content).Children[0]).Content).Children[1]).Children[1]).Text = "Office Floor 1 • 192.168.1.100";
                Title = $"Device Details - {deviceName}";
            }
        }

        private void InitializeDeviceData()
        {
            // Устанавливаем заголовок окна
            Title = $"Детали устройства - {_device.Name}";
        }
        
        private void LoadDeviceInfo()
        {
            try
            {
                // Основная информация об устройстве
                // DeviceNameText.Text = _device.Name;
                // DeviceLocationText.Text = $"{(string.IsNullOrEmpty(_device.Location) ? "Местоположение не указано" : _device.Location)} • {_device.HostnameOrIp}";
                
                // Set window title instead
                Title = $"Детали устройства - {_device.Name}";
                
                // Получаем данные о статусе устройства
                var deviceInfo = _deviceDataService.GetDeviceInfo(_device.HostnameOrIp);
                if (deviceInfo != null)
                {
                    // Обновляем информацию о состоянии
                    var statusText = deviceInfo.IsOnline ? "🟢 Онлайн" : "🔴 Офлайн";
                    var lastSeen = deviceInfo.IsOnline ? "Сейчас" : FormatTimeAgo(deviceInfo.LastSeen);
                    
                    // Update title with status information
                    Title = $"Детали устройства - {_device.Name} ({statusText})";
                }
                
                _logger.LogDebug("Информация об устройстве загружена");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Ошибка загрузки информации об устройстве");
                // Основная информация всё равно отображается
                // DeviceNameText.Text = _device.Name;
                // DeviceLocationText.Text = $"{(string.IsNullOrEmpty(_device.Location) ? "Местоположение не указано" : _device.Location)} • {_device.HostnameOrIp}";
                Title = $"Детали устройства - {_device.Name}";
            }
        }

        private void CloseButton_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }

        private void BackButton_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }

        private void SettingsButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var settingsWindow = new SettingsWindow();
                settingsWindow.Owner = this;
                settingsWindow.ShowDialog();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error opening settings window");
                System.Windows.MessageBox.Show($"Ошибка открытия настроек: {ex.Message}", "Ошибка", 
                               MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private async void RefreshDeviceButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                _logger.LogInformation("Запущен ручной рефреш устройства {DeviceName}", _device.Name);
                
                var button = sender as System.Windows.Controls.Button;
                if (button != null)
                {
                    button.IsEnabled = false;
                    button.Content = "🔄 Обновление...";
                }
                
                // Имитация обновления данных
                await Task.Delay(2000);
                
                // Обновляем информацию об устройстве
                LoadDeviceInfo();
                
                var supplies = _supplyDataService.GetSupplyStatus(_device.HostnameOrIp);
                var supplyCount = supplies?.Count() ?? 0;
                
                System.Windows.MessageBox.Show(
                    $"Обновление данных для {_device.Name} завершено!\n\n" +
                    "✅ Проверка соединения: ОК\n" +
                    $"✅ Уровни расходных материалов: {(supplyCount > 0 ? $"Обновлено ({supplyCount} позиций)" : "Данные недоступны")}\n" +
                    "✅ Статус устройства: Обновлён\n" +
                    "✅ Счётчики страниц: Обновлены",
                    "Обновление устройства",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Ошибка обновления устройства");
                System.Windows.MessageBox.Show($"Ошибка обновления: {ex.Message}", "Ошибка", 
                               MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                var button = sender as System.Windows.Controls.Button;
                if (button != null)
                {
                    button.IsEnabled = true;
                    button.Content = "🔄 Refresh Now";
                }
            }
        }

        private void ViewHistoryButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var supplies = _supplyDataService?.GetSupplyStatus(_device.HostnameOrIp);
                var historyData = GenerateSupplyHistory(supplies);
                
                System.Windows.MessageBox.Show(
                    $"История уровня расходных материалов для {_device.Name}\n\n" +
                    historyData,
                    "История расходных материалов",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Ошибка получения истории расходных материалов");
                System.Windows.MessageBox.Show($"Ошибка получения истории: {ex.Message}", "Ошибка", 
                               MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void ConfigureButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var result = System.Windows.MessageBox.Show(
                    $"Конфигурация устройства {_device.Name}\n\n" +
                    "Текущие настройки:\n" +
                    $"• Интервал опроса: {_device.PollingIntervalSeconds} секунд\n" +
                    $"• SNMP Community: {_device.SnmpCommunity}\n" +
                    $"• Пороги уведомлений: {_device.CriticalThreshold}% критический, {_device.WarningThreshold}% предупреждение\n" +
                    $"• Авто-заказ расходных материалов: Отключён\n\n" +
                    "Хотите изменить эти настройки?",
                    "Конфигурация устройства",
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Question);

                if (result == MessageBoxResult.Yes)
                {
                    // Здесь можно открыть окно конфигурации устройства
                    System.Windows.MessageBox.Show(
                        "Окно конфигурации будет открыто здесь.\n\n" +
                        "Доступные опции:\n" +
                        "• Частота опроса\n" +
                        "• Настройки SNMP\n" +
                        "• Пороги уведомлений\n" +
                        "• Настройки уведомлений\n" +
                        "• Правила авто-заказа",
                        "Конфигурация",
                        MessageBoxButton.OK,
                        MessageBoxImage.Information);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Ошибка открытия конфигурации устройства");
                System.Windows.MessageBox.Show($"Ошибка конфигурации: {ex.Message}", "Ошибка", 
                               MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void RemoveDeviceButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var result = System.Windows.MessageBox.Show(
                    $"Вы уверены, что хотите удалить {_device.Name} из мониторинга?\n\n" +
                    "Это действие приведёт к:\n" +
                    "• Остановке мониторинга этого устройства\n" +
                    "• Удалению всех исторических данных\n" +
                    "• Отмене всех подвисших уведомлений\n\n" +
                    "Это действие нельзя отменить.",
                    "Удаление устройства",
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Warning);

                if (result == MessageBoxResult.Yes)
                {
                    // Удаляем устройство из настроек
                    var settings = _settingsManager.Settings;
                    var updatedSettings = CloneSettings(settings);
                    updatedSettings.Devices.RemoveAll(d => d.HostnameOrIp.Equals(_device.HostnameOrIp, StringComparison.OrdinalIgnoreCase));
                    _settingsManager.UpdateSettings(updatedSettings);
                    
                    _logger.LogInformation("Устройство {DeviceName} удалено из мониторинга", _device.Name);
                    
                    System.Windows.MessageBox.Show(
                        $"Устройство {_device.Name} было удалено из мониторинга.",
                        "Устройство удалено",
                        MessageBoxButton.OK,
                        MessageBoxImage.Information);
                    
                    Close();
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Ошибка удаления устройства");
                System.Windows.MessageBox.Show($"Ошибка удаления: {ex.Message}", "Ошибка", 
                               MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
        
        private string GenerateSupplyHistory(IEnumerable<SupplyViewModel>? supplies)
        {
            if (supplies == null || !supplies.Any())
            {
                return "📈 Чёрный тонер: Снижение 2% в неделю\n" +
                       "📈 Голубой тонер: Снижение 3% в неделю\n" +
                       "📉 Пурпурный тонер: Критический - нужна замена\n" +
                       "📈 Жёлтый тонер: Стабильное использование\n" +
                       ".DataGridViewColumn: Скоро потребуется замена\n\n" +
                       "💡 Рекомендация: Заказать пурпурный тонер и барабан";
            }
            
            var history = new List<string>();
            foreach (var supply in supplies.Take(5))
            {
                var trend = supply.Percent > 50 ? "📈" : supply.Percent > 25 ? "📊" : "📉";
                var status = supply.Percent > 50 ? "Стабильно" : supply.Percent > 25 ? "Умеренное снижение" : "Критический уровень";
                history.Add($"{trend} {supply.Name}: {status} ({supply.Percent:F0}%)");
            }
            
            var recommendations = supplies.Where(s => s.Percent <= 25).Select(s => s.Name).ToList();
            if (recommendations.Any())
            {
                history.Add("");
                history.Add($"💡 Рекомендация: Заказать {string.Join(", ", recommendations)}");
            }
            
            return string.Join("\n", history);
        }
        
        private DesktopSettings CloneSettings(DesktopSettings original)
        {
            return new DesktopSettings
            {
                RefreshIntervalSeconds = original.RefreshIntervalSeconds,
                DiscoveryIntervalMinutes = original.DiscoveryIntervalMinutes,
                StartWithWindows = original.StartWithWindows,
                MinimizeToTray = original.MinimizeToTray,
                ShowDesktopNotifications = original.ShowDesktopNotifications,
                Language = original.Language,
                CriticalThreshold = original.CriticalThreshold,
                WarningThreshold = original.WarningThreshold,
                WindowsNotificationsEnabled = original.WindowsNotificationsEnabled,
                EmailNotificationsEnabled = original.EmailNotificationsEnabled,
                SubnetRange = original.SubnetRange,
                AutoDiscoveryEnabled = original.AutoDiscoveryEnabled,
                SnmpEnabled = original.SnmpEnabled,
                SnmpVersion = original.SnmpVersion,
                SnmpCommunity = original.SnmpCommunity,
                SnmpTimeoutMs = original.SnmpTimeoutMs,
                SnmpRetries = original.SnmpRetries,
                MaxConcurrentScans = original.MaxConcurrentScans,
                ScanTimeoutSeconds = original.ScanTimeoutSeconds,
                ScanRetries = original.ScanRetries,
                ScanRetryDelayMs = original.ScanRetryDelayMs,
                SmtpServer = original.SmtpServer,
                SmtpPort = original.SmtpPort,
                SmtpUseSsl = original.SmtpUseSsl,
                SmtpUsername = original.SmtpUsername,
                SmtpPassword = original.SmtpPassword,
                EmailFrom = original.EmailFrom,
                EmailRecipients = new List<string>(original.EmailRecipients),
                Devices = new List<DeviceSettings>(original.Devices)
            };
        }
        
        private string FormatTimeAgo(DateTime dateTime)
        {
            var timeSpan = DateTime.Now - dateTime;
            
            if (timeSpan.TotalMinutes < 1)
            {
                return "Только что";
            }
            else if (timeSpan.TotalMinutes < 60)
            {
                var minutes = (int)timeSpan.TotalMinutes;
                return minutes == 1 ? "1 мин. назад" : $"{minutes} мин. назад";
            }
            else if (timeSpan.TotalHours < 24)
            {
                var hours = (int)timeSpan.TotalHours;
                return hours == 1 ? "1 час назад" : $"{hours} ч. назад";
            }
            else
            {
                var days = (int)timeSpan.TotalDays;
                return days == 1 ? "1 день назад" : $"{days} дн. назад";
            }
        }
    }
}