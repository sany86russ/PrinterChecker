using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data; // For IValueConverter
using Microsoft.Extensions.Logging;
using TonerWatch.Core.Models;
using TonerWatch.Desktop.Services;
using System.Windows.Forms; // Added to resolve Button ambiguity


namespace TonerWatch.Desktop
{
    // Add converters at the top of the file, before the SettingsWindow class
    public class BooleanToStatusConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            return (bool)value ? "Активен" : "Отключен";
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }

    public class BooleanToColorConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            return (bool)value ? "#10B981" : "#EF4444"; // Green for enabled, red for disabled
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }

    public partial class SettingsWindow : Window
    {
        private readonly SettingsManager _settingsManager;
        private readonly ILogger<SettingsWindow> _logger;
        private DesktopSettings _workingSettings;
        private NetworkSegment? _editingSegment; // Track the segment being edited
        
        public SettingsWindow()
        {
            InitializeComponent();
            
            try
            {
                _settingsManager = ServiceManager.Instance.GetService<SettingsManager>();
                _logger = ServiceManager.Instance.GetLogger<SettingsWindow>();
                
                // Create a copy of current settings for editing
                _workingSettings = CloneSettings(_settingsManager.Settings);
                
                // Initialize combo boxes with items
                InitializeComboBoxes();
                
                LoadSettingsToUI();
                
                // Subscribe to combo box selection change for custom interval
                RefreshIntervalComboBox.SelectionChanged += RefreshIntervalComboBox_SelectionChanged;
                
                // Subscribe to subnet range text changes for validation
                SubnetRangeTextBox.TextChanged += SubnetRangeTextBox_TextChanged;
                
                _logger.LogInformation("Settings window initialized");
            }
            catch (Exception ex)
            {
                System.Windows.MessageBox.Show($"Error initializing settings: {ex.Message}", "Error", 
                               MessageBoxButton.OK, MessageBoxImage.Error);
                DialogResult = false;
                Close();
            }
        }
        
        private void InitializeComboBoxes()
        {
            // Initialize Refresh Interval ComboBox
            RefreshIntervalComboBox.Items.Clear();
            RefreshIntervalComboBox.Items.Add("15 секунд");
            RefreshIntervalComboBox.Items.Add("30 секунд");
            RefreshIntervalComboBox.Items.Add("1 минута");
            RefreshIntervalComboBox.Items.Add("5 минут");
            RefreshIntervalComboBox.Items.Add("10 минут");
            RefreshIntervalComboBox.Items.Add("Пользовательский");
            
            // Initialize Discovery Interval ComboBox
            DiscoveryIntervalComboBox.Items.Clear();
            DiscoveryIntervalComboBox.Items.Add("5 минут");
            DiscoveryIntervalComboBox.Items.Add("15 минут");
            DiscoveryIntervalComboBox.Items.Add("30 минут");
            DiscoveryIntervalComboBox.Items.Add("1 час");
            DiscoveryIntervalComboBox.Items.Add("2 часа");
            DiscoveryIntervalComboBox.Items.Add("6 часов");
            DiscoveryIntervalComboBox.Items.Add("12 часов");
            DiscoveryIntervalComboBox.Items.Add("1 день");
            DiscoveryIntervalComboBox.Items.Add("Отключено");
            
            // Add converters to resources
            Resources.Add("BooleanToStatusConverter", new BooleanToStatusConverter());
            Resources.Add("BooleanToColorConverter", new BooleanToColorConverter());
        }
        
        private void LoadSettingsToUI()
        {
            try
            {
                // General settings
                RefreshIntervalComboBox.SelectedIndex = _workingSettings.RefreshIntervalSeconds switch
                {
                    15 => 0,
                    30 => 1,
                    60 => 2,
                    300 => 3,
                    600 => 4,
                    _ => 5 // Custom
                };
                
                // Show/hide custom interval input
                if (RefreshIntervalComboBox.SelectedIndex == 5)
                {
                    CustomIntervalGrid.Visibility = Visibility.Visible;
                    CustomIntervalTextBox.Text = _workingSettings.RefreshIntervalSeconds.ToString();
                }
                else
                {
                    CustomIntervalGrid.Visibility = Visibility.Collapsed;
                }
                
                // Discovery interval
                var discoveryInterval = _workingSettings.DiscoveryIntervalMinutes;
                DiscoveryIntervalComboBox.SelectedIndex = discoveryInterval switch
                {
                    5 => 0,
                    15 => 1,
                    30 => 2,
                    60 => 3,
                    120 => 4,
                    360 => 5,
                    720 => 6,
                    1440 => 7,
                    -1 => 8, // Disabled
                    _ => 2
                };
                
                StartWithWindowsCheckBox.IsChecked = _workingSettings.StartWithWindows;
                MinimizeToTrayCheckBox.IsChecked = _workingSettings.MinimizeToTray;
                ShowDesktopNotificationsCheckBox.IsChecked = _workingSettings.ShowDesktopNotifications;
                
                // Alert thresholds
                CriticalSlider.Value = _workingSettings.CriticalThreshold;
                WarningSlider.Value = _workingSettings.WarningThreshold;
                CriticalValueText.Text = $"{_workingSettings.CriticalThreshold:F0}%";
                WarningValueText.Text = $"{_workingSettings.WarningThreshold:F0}%";
                
                WindowsNotificationsCheckBox.IsChecked = _workingSettings.WindowsNotificationsEnabled;
                EmailNotificationsCheckBox.IsChecked = _workingSettings.EmailNotificationsEnabled;
                
                // Network settings - support multiple IP ranges
                SubnetRangeTextBox.Text = _workingSettings.SubnetRange;
                AutoDiscoveryCheckBox.IsChecked = _workingSettings.AutoDiscoveryEnabled;
                SnmpCheckBox.IsChecked = _workingSettings.SnmpEnabled;
                
                // Validate IP ranges on load
                ValidateAndVisualizeIpRanges();
                
                // SNMP settings
                SnmpVersionComboBox.SelectedIndex = _workingSettings.SnmpVersion switch
                {
                    "SNMPv1" => 0,
                    "SNMPv2c" => 1,
                    "SNMPv3" => 2,
                    _ => 1 // Default to SNMPv2c
                };
                SnmpCommunityTextBox.Text = _workingSettings.SnmpCommunity;
                SnmpTimeoutTextBox.Text = _workingSettings.SnmpTimeoutMs.ToString();
                
                // Performance settings
                MaxConcurrentScansTextBox.Text = _workingSettings.MaxConcurrentScans.ToString();
                ScanTimeoutTextBox.Text = _workingSettings.ScanTimeoutSeconds.ToString();
                ScanRetriesTextBox.Text = _workingSettings.ScanRetries.ToString();
                ScanRetryDelayTextBox.Text = _workingSettings.ScanRetryDelayMs.ToString();
                
                // Network segments
                UpdateNetworkSegmentsList();
                
                _logger.LogDebug("Settings loaded to UI");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading settings to UI");
                throw;
            }
        }
        
        private void SaveUIToSettings()
        {
            try
            {
                // General settings - refresh interval
                if (RefreshIntervalComboBox.SelectedIndex == 5) // Custom
                {
                    if (int.TryParse(CustomIntervalTextBox.Text, out int customInterval) && customInterval > 0)
                    {
                        _workingSettings.RefreshIntervalSeconds = customInterval;
                    }
                    else
                    {
                        throw new ArgumentException("Custom interval must be a positive integer.");
                    }
                }
                else
                {
                    _workingSettings.RefreshIntervalSeconds = RefreshIntervalComboBox.SelectedIndex switch
                    {
                        0 => 15,
                        1 => 30,
                        2 => 60,
                        3 => 300,
                        4 => 600,
                        _ => 60
                    };
                }
                
                // Discovery interval
                _workingSettings.DiscoveryIntervalMinutes = DiscoveryIntervalComboBox.SelectedIndex switch
                {
                    0 => 5,
                    1 => 15,
                    2 => 30,
                    3 => 60,
                    4 => 120,
                    5 => 360,
                    6 => 720,
                    7 => 1440,
                    8 => -1, // Disabled
                    _ => 30
                };
                
                _workingSettings.StartWithWindows = StartWithWindowsCheckBox.IsChecked ?? false;
                _workingSettings.MinimizeToTray = MinimizeToTrayCheckBox.IsChecked ?? true;
                _workingSettings.ShowDesktopNotifications = ShowDesktopNotificationsCheckBox.IsChecked ?? true;
                
                // Alert thresholds
                _workingSettings.CriticalThreshold = CriticalSlider.Value;
                _workingSettings.WarningThreshold = WarningSlider.Value;
                _workingSettings.WindowsNotificationsEnabled = WindowsNotificationsCheckBox.IsChecked ?? true;
                _workingSettings.EmailNotificationsEnabled = EmailNotificationsCheckBox.IsChecked ?? false;
                
                // Network settings - support multiple IP ranges
                _workingSettings.SubnetRange = SubnetRangeTextBox.Text.Trim();
                _workingSettings.AutoDiscoveryEnabled = AutoDiscoveryCheckBox.IsChecked ?? true;
                _workingSettings.SnmpEnabled = SnmpCheckBox.IsChecked ?? true;
                
                // Network segments are handled separately through the UI
                
                // SNMP settings
                _workingSettings.SnmpVersion = SnmpVersionComboBox.SelectedIndex switch
                {
                    0 => "SNMPv1",
                    1 => "SNMPv2c",
                    2 => "SNMPv3",
                    _ => "SNMPv2c"
                };
                _workingSettings.SnmpCommunity = SnmpCommunityTextBox.Text.Trim();
                if (int.TryParse(SnmpTimeoutTextBox.Text, out int snmpTimeout) && snmpTimeout > 0)
                {
                    _workingSettings.SnmpTimeoutMs = snmpTimeout;
                }
                else
                {
                    _workingSettings.SnmpTimeoutMs = 5000; // Default value
                }
                
                // Performance settings
                if (int.TryParse(MaxConcurrentScansTextBox.Text, out int maxConcurrentScans) && maxConcurrentScans > 0)
                {
                    _workingSettings.MaxConcurrentScans = maxConcurrentScans;
                }
                else
                {
                    _workingSettings.MaxConcurrentScans = 50; // Default value
                }
                
                if (int.TryParse(ScanTimeoutTextBox.Text, out int scanTimeout) && scanTimeout > 0)
                {
                    _workingSettings.ScanTimeoutSeconds = scanTimeout;
                }
                else
                {
                    _workingSettings.ScanTimeoutSeconds = 3; // Default value
                }
                
                if (int.TryParse(ScanRetriesTextBox.Text, out int scanRetries) && scanRetries >= 0)
                {
                    _workingSettings.ScanRetries = scanRetries;
                }
                else
                {
                    _workingSettings.ScanRetries = 1; // Default value
                }
                
                if (int.TryParse(ScanRetryDelayTextBox.Text, out int scanRetryDelay) && scanRetryDelay >= 0)
                {
                    _workingSettings.ScanRetryDelayMs = scanRetryDelay;
                }
                else
                {
                    _workingSettings.ScanRetryDelayMs = 1000; // Default value
                }
                
                _logger.LogDebug("Settings saved from UI");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error saving settings from UI");
                throw;
            }
        }
        
        private void RefreshIntervalComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (CustomIntervalGrid != null)
            {
                CustomIntervalGrid.Visibility = RefreshIntervalComboBox.SelectedIndex == 5 
                    ? Visibility.Visible 
                    : Visibility.Collapsed;
            }
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
                Devices = original.Devices.Select(d => new DeviceSettings
                {
                    HostnameOrIp = d.HostnameOrIp,
                    Name = d.Name,
                    Location = d.Location,
                    IsActive = d.IsActive,
                    PollingIntervalSeconds = d.PollingIntervalSeconds,
                    SnmpCommunity = d.SnmpCommunity,
                    SnmpVersion = d.SnmpVersion,
                    CriticalThreshold = d.CriticalThreshold,
                    WarningThreshold = d.WarningThreshold,
                    AddedAt = d.AddedAt
                }).ToList()
            };
        }

        private void SaveButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                // Validate IP ranges before saving
                if (!ValidateIpRanges())
                {
                    System.Windows.MessageBox.Show("Пожалуйста, исправьте ошибки в конфигурации IP-диапазонов.", 
                                   "Ошибка конфигурации", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }
                
                // Сохраняем данные из UI в рабочие настройки
                SaveUIToSettings();
                
                // Применяем настройки через менеджер
                _settingsManager.UpdateSettings(_workingSettings);
                
                _logger.LogInformation("Настройки успешно сохранены пользователем");
                
                System.Windows.MessageBox.Show("Настройки успешно сохранены!\n\n" +
                               "✅ Общие настройки\n" +
                               "✅ Пороги уведомлений\n" +
                               "✅ Сетевые параметры\n\n" +
                               "Новые настройки вступят в силу немедленно.", 
                               "TonerWatch - Настройки сохранены", 
                               MessageBoxButton.OK, MessageBoxImage.Information);
                
                DialogResult = true;
                Close();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Ошибка сохранения настроек");
                System.Windows.MessageBox.Show($"Ошибка сохранения настроек:\n\n{ex.Message}", 
                               "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            var result = System.Windows.MessageBox.Show(
                "Вы уверены, что хотите закрыть окно настроек?\n\n" +
                "Несохранённые изменения будут потеряны.",
                "Подтверждение закрытия",
                MessageBoxButton.YesNo,
                MessageBoxImage.Question);

            if (result == MessageBoxResult.Yes)
            {
                DialogResult = false;
                Close();
            }
        }

        private void ResetButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var result = System.Windows.MessageBox.Show(
                    "Вы уверены, что хотите сбросить все настройки к значениям по умолчанию?\n\n" +
                    "Это действие нельзя отменить.",
                    "Сброс настроек",
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Warning);

                if (result == MessageBoxResult.Yes)
                {
                    _workingSettings = new DesktopSettings();
                    LoadSettingsToUI();
                    
                    _logger.LogInformation("Настройки сброшены к значениям по умолчанию");
                    System.Windows.MessageBox.Show("Настройки сброшены к значениям по умолчанию.", 
                                   "Сброс выполнен", MessageBoxButton.OK, MessageBoxImage.Information);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Ошибка сброса настроек");
                System.Windows.MessageBox.Show($"Ошибка сброса настроек:\n\n{ex.Message}", 
                               "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void CriticalSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (CriticalValueText != null)
                CriticalValueText.Text = $"{e.NewValue:F0}%";
        }

        private void WarningSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (WarningValueText != null)
                WarningValueText.Text = $"{e.NewValue:F0}%";
        }
        
        // IP Range Validation and Visualization
        private void SubnetRangeTextBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            ValidateAndVisualizeIpRanges();
        }
        
        private bool ValidateIpRanges()
        {
            var ipRanges = SubnetRangeTextBox.Text.Split(new[] { '\n', '\r', ',' }, StringSplitOptions.RemoveEmptyEntries)
                .Select(s => s.Trim())
                .Where(s => !string.IsNullOrEmpty(s))
                .ToList();
            
            foreach (var range in ipRanges)
            {
                if (!IsValidIpRange(range))
                {
                    return false;
                }
            }
            
            return true;
        }
        
        private void ValidateAndVisualizeIpRanges()
        {
            var ipRanges = SubnetRangeTextBox.Text.Split(new[] { '\n', '\r', ',' }, StringSplitOptions.RemoveEmptyEntries)
                .Select(s => s.Trim())
                .Where(s => !string.IsNullOrEmpty(s))
                .ToList();
            
            var validRanges = new List<string>();
            var hasErrors = false;
            
            foreach (var range in ipRanges)
            {
                if (IsValidIpRange(range))
                {
                    validRanges.Add(range);
                }
                else
                {
                    hasErrors = true;
                }
            }
            
            // Update validation message
            if (hasErrors)
            {
                IpRangeValidationText.Text = "⚠️ Некоторые диапазоны IP-адресов имеют неверный формат";
                IpRangeValidationText.Foreground = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Colors.Orange);
                IpRangeValidationText.Visibility = Visibility.Visible;
            }
            else if (ipRanges.Count == 0)
            {
                IpRangeValidationText.Text = "ℹ️ Не указаны диапазоны IP-адресов для сканирования";
                IpRangeValidationText.Foreground = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Colors.Gray);
                IpRangeValidationText.Visibility = Visibility.Visible;
            }
            else
            {
                IpRangeValidationText.Text = "✅ Все диапазоны IP-адресов имеют правильный формат";
                IpRangeValidationText.Foreground = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Colors.Green);
                IpRangeValidationText.Visibility = Visibility.Visible;
            }
            
            // Update visualization
            NetworkSegmentsVisualization.ItemsSource = validRanges;
        }
        
        private bool IsValidIpRange(string range)
        {
            // Check for CIDR notation (e.g., 192.168.1.0/24)
            if (range.Contains("/"))
            {
                var parts = range.Split('/');
                if (parts.Length != 2)
                    return false;
                
                if (!int.TryParse(parts[1], out int prefixLength) || prefixLength < 0 || prefixLength > 32)
                    return false;
                
                return IsValidIpAddress(parts[0]);
            }
            
            // Check for range notation (e.g., 192.168.1.1-254)
            if (range.Contains("-"))
            {
                var parts = range.Split('-');
                if (parts.Length != 2)
                    return false;
                
                // First part should be a valid IP
                if (!IsValidIpAddress(parts[0]))
                    return false;
                
                // Second part should be a valid number or IP
                if (int.TryParse(parts[1], out int endNumber))
                {
                    return endNumber >= 0 && endNumber <= 255;
                }
                else
                {
                    return IsValidIpAddress(range);
                }
            }
            
            // Single IP address
            return IsValidIpAddress(range);
        }
        
        private bool IsValidIpAddress(string ip)
        {
            var ipPattern = @"^(\d{1,3})\.(\d{1,3})\.(\d{1,3})\.(\d{1,3})$";
            var match = Regex.Match(ip, ipPattern);
            
            if (!match.Success)
                return false;
            
            // Check that each octet is between 0 and 255
            for (int i = 1; i <= 4; i++)
            {
                if (!int.TryParse(match.Groups[i].Value, out int octet) || octet < 0 || octet > 255)
                    return false;
            }
            
            return true;
        }
        
        // IP Range Preset Handlers
        private void PresetLocalNetwork_Click(object sender, RoutedEventArgs e)
        {
            SubnetRangeTextBox.Text = "192.168.1.0/24";
        }
        
        private void PresetClassC_Click(object sender, RoutedEventArgs e)
        {
            SubnetRangeTextBox.Text = "192.168.0.0/16";
        }
        
        private void ClearIpRanges_Click(object sender, RoutedEventArgs e)
        {
            SubnetRangeTextBox.Text = "";
        }
        
        // Network Segments UI Methods
        private void UpdateNetworkSegmentsList()
        {
            if (NetworkSegmentsList != null)
            {
                NetworkSegmentsList.ItemsSource = _workingSettings.NetworkSegments;
                NoSegmentsText.Visibility = _workingSettings.NetworkSegments.Any() ? Visibility.Collapsed : Visibility.Visible;
            }
        }
        
        private void AddSegmentButton_Click(object sender, RoutedEventArgs e)
        {
            _editingSegment = null;
            ShowSegmentConfigPanel(true);
        }
        
        private void EditSegmentButton_Click(object sender, RoutedEventArgs e)
        {
            if (sender is System.Windows.Controls.Button button && button.Tag is NetworkSegment segment)
            {
                _editingSegment = segment;
                PopulateSegmentForm(segment);
                ShowSegmentConfigPanel(false);
            }
        }
        
        private void DeleteSegmentButton_Click(object sender, RoutedEventArgs e)
        {
            if (sender is System.Windows.Controls.Button button && button.Tag is NetworkSegment segment)
            {
                var result = System.Windows.MessageBox.Show(
                    $"Вы уверены, что хотите удалить сетевой сегмент '{segment.Name}'?",
                    "Подтверждение удаления",
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Question);
                
                if (result == MessageBoxResult.Yes)
                {
                    _workingSettings.NetworkSegments.Remove(segment);
                    UpdateNetworkSegmentsList();
                }
            }
        }
        
        private void SaveSegmentButton_Click(object sender, RoutedEventArgs e)
        {
            if (!ValidateSegmentForm())
                return;
                
            NetworkSegment segment;
            
            if (_editingSegment == null)
            {
                // Creating new segment
                segment = new NetworkSegment();
                _workingSettings.NetworkSegments.Add(segment);
            }
            else
            {
                // Editing existing segment
                segment = _editingSegment;
            }
            
            // Populate segment from form
            segment.Name = SegmentNameTextBox.Text.Trim();
            segment.Description = SegmentDescriptionTextBox.Text.Trim();
            segment.IpRange = SegmentIpRangeTextBox.Text.Trim();
            segment.IsEnabled = SegmentEnabledCheckBox.IsChecked ?? true;
            segment.ScanTimeout = TimeSpan.FromSeconds(int.TryParse(SegmentTimeoutTextBox.Text, out var timeout) ? timeout : 3);
            segment.MaxConcurrentScans = int.TryParse(SegmentMaxScansTextBox.Text, out var maxScans) ? maxScans : 50;
            segment.ScanRetries = int.TryParse(SegmentRetriesTextBox.Text, out var retries) ? retries : 1;
            segment.EnableSNMPFingerprinting = SegmentSnmpCheckBox.IsChecked ?? true;
            segment.HasCustomSchedule = SegmentScheduleCheckBox.IsChecked ?? false;
            segment.CustomSchedule = SegmentScheduleTextBox.Text.Trim();
            segment.AvoidPeakTimes = SegmentAvoidPeakCheckBox.IsChecked ?? false;
            
            UpdateNetworkSegmentsList();
            HideSegmentConfigPanel();
        }
        
        private void CancelSegmentButton_Click(object sender, RoutedEventArgs e)
        {
            HideSegmentConfigPanel();
        }
        
        private void SegmentScheduleCheckBox_Checked(object sender, RoutedEventArgs e)
        {
            if (SegmentScheduleGrid != null)
                SegmentScheduleGrid.Visibility = Visibility.Visible;
        }
        
        private void SegmentScheduleCheckBox_Unchecked(object sender, RoutedEventArgs e)
        {
            if (SegmentScheduleGrid != null)
                SegmentScheduleGrid.Visibility = Visibility.Collapsed;
        }
        
        private void ShowSegmentConfigPanel(bool isNew)
        {
            if (SegmentConfigPanel != null)
            {
                SegmentConfigPanel.Visibility = Visibility.Visible;
                SegmentConfigTitle.Text = isNew ? "Добавить новый сегмент" : "Редактировать сегмент";
                ClearSegmentForm();
            }
        }
        
        private void HideSegmentConfigPanel()
        {
            if (SegmentConfigPanel != null)
            {
                SegmentConfigPanel.Visibility = Visibility.Collapsed;
                _editingSegment = null;
            }
        }
        
        private void ClearSegmentForm()
        {
            SegmentNameTextBox.Text = "";
            SegmentDescriptionTextBox.Text = "";
            SegmentIpRangeTextBox.Text = "";
            SegmentEnabledCheckBox.IsChecked = true;
            SegmentTimeoutTextBox.Text = "3";
            SegmentMaxScansTextBox.Text = "50";
            SegmentRetriesTextBox.Text = "1";
            SegmentSnmpCheckBox.IsChecked = true;
            SegmentScheduleCheckBox.IsChecked = false;
            SegmentScheduleTextBox.Text = "every 60 minutes";
            SegmentAvoidPeakCheckBox.IsChecked = false;
            
            if (SegmentScheduleGrid != null)
                SegmentScheduleGrid.Visibility = Visibility.Collapsed;
        }
        
        private void PopulateSegmentForm(NetworkSegment segment)
        {
            SegmentNameTextBox.Text = segment.Name;
            SegmentDescriptionTextBox.Text = segment.Description;
            SegmentIpRangeTextBox.Text = segment.IpRange;
            SegmentEnabledCheckBox.IsChecked = segment.IsEnabled;
            SegmentTimeoutTextBox.Text = segment.ScanTimeout.TotalSeconds.ToString();
            SegmentMaxScansTextBox.Text = segment.MaxConcurrentScans.ToString();
            SegmentRetriesTextBox.Text = segment.ScanRetries.ToString();
            SegmentSnmpCheckBox.IsChecked = segment.EnableSNMPFingerprinting;
            SegmentScheduleCheckBox.IsChecked = segment.HasCustomSchedule;
            SegmentScheduleTextBox.Text = segment.CustomSchedule ?? "every 60 minutes";
            SegmentAvoidPeakCheckBox.IsChecked = segment.AvoidPeakTimes;
            
            if (SegmentScheduleGrid != null)
                SegmentScheduleGrid.Visibility = segment.HasCustomSchedule ? Visibility.Visible : Visibility.Collapsed;
        }
        
        private bool ValidateSegmentForm()
        {
            if (string.IsNullOrWhiteSpace(SegmentNameTextBox.Text))
            {
                System.Windows.MessageBox.Show("Пожалуйста, введите название сегмента.", "Ошибка валидации", MessageBoxButton.OK, MessageBoxImage.Warning);
                SegmentNameTextBox.Focus();
                return false;
            }
            
            if (string.IsNullOrWhiteSpace(SegmentIpRangeTextBox.Text))
            {
                System.Windows.MessageBox.Show("Пожалуйста, введите IP диапазон.", "Ошибка валидации", MessageBoxButton.OK, MessageBoxImage.Warning);
                SegmentIpRangeTextBox.Focus();
                return false;
            }
            
            // Validate IP range format
            if (!IsValidIpRange(SegmentIpRangeTextBox.Text))
            {
                System.Windows.MessageBox.Show("Пожалуйста, введите корректный IP диапазон.\nПоддерживаются форматы: CIDR (192.168.1.0/24), диапазоны (192.168.1.1-254), несколько через запятую", 
                              "Ошибка валидации", MessageBoxButton.OK, MessageBoxImage.Warning);
                SegmentIpRangeTextBox.Focus();
                return false;
            }
            
            return true;
        }
    }
}