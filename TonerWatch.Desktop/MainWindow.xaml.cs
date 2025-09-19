﻿﻿﻿﻿﻿﻿﻿﻿﻿﻿﻿﻿﻿﻿﻿﻿﻿﻿﻿﻿﻿﻿﻿﻿﻿﻿﻿﻿﻿﻿﻿﻿﻿﻿﻿﻿﻿﻿﻿﻿﻿﻿﻿﻿﻿﻿﻿﻿﻿﻿﻿using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Microsoft.Extensions.Logging;
using TonerWatch.Core.Interfaces;
using TonerWatch.Core.Models;
using TonerWatch.Desktop.Services;
using TonerWatch.Infrastructure.Services;

namespace TonerWatch.Desktop
{
    public partial class MainWindow : Window
    {
        private readonly ILogger<MainWindow> _logger;
        private readonly SettingsManager _settingsManager;
        private readonly MonitoringService _monitoringService;
        private readonly IDeviceDiscoveryService _discoveryService;
        private readonly IEmailNotificationSender _emailSender;
        private readonly ITelegramNotificationSender _telegramSender;
        private readonly IWebhookNotificationSender _webhookSender;
        private readonly SupplyNormalizationService _normalizationService;
        private readonly ForecastService _forecastService;
        private readonly AlertService _alertService;
        private readonly NotificationService _notificationService;
        
        // For bulk operations
        private readonly HashSet<DeviceViewModel> _selectedDevices = new HashSet<DeviceViewModel>();
        
        // For periodic updates
        private readonly CancellationTokenSource _cancellationTokenSource = new CancellationTokenSource();
        private readonly System.Threading.Timer _refreshTimer;
        
        public MainWindow()
        {
            InitializeComponent();
            
            // Initialize services
            _logger = ServiceManager.Instance.GetLogger<MainWindow>();
            _settingsManager = ServiceManager.Instance.GetService<SettingsManager>();
            _monitoringService = ServiceManager.Instance.GetService<MonitoringService>();
            _discoveryService = ServiceManager.Instance.GetService<IDeviceDiscoveryService>();
            _emailSender = ServiceManager.Instance.GetService<IEmailNotificationSender>();
            _telegramSender = ServiceManager.Instance.GetService<ITelegramNotificationSender>();
            _webhookSender = ServiceManager.Instance.GetService<IWebhookNotificationSender>();
            _normalizationService = ServiceManager.Instance.GetService<SupplyNormalizationService>();
            _forecastService = ServiceManager.Instance.GetService<ForecastService>();
            _alertService = ServiceManager.Instance.GetService<AlertService>();
            _notificationService = ServiceManager.Instance.GetService<NotificationService>();
            
            // Set up periodic refresh
            _refreshTimer = new System.Threading.Timer(RefreshDashboard, null, 
                TimeSpan.FromSeconds(_settingsManager.Settings.RefreshIntervalSeconds),
                TimeSpan.FromSeconds(_settingsManager.Settings.RefreshIntervalSeconds));
            
            // Load initial data
            LoadDashboardData();
            
            _logger.LogInformation("MainWindow initialized");
        }
        
        private async void LoadDashboardData()
        {
            try
            {
                // Update device list
                await UpdateDeviceList();
                
                // Update KPIs
                UpdateKpiCards();
                
                _logger.LogDebug("Dashboard data loaded");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading dashboard data");
                System.Windows.MessageBox.Show($"Ошибка загрузки данных: {ex.Message}", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
        
        private async Task UpdateDeviceList()
        {
            try
            {
                var devices = _settingsManager.Settings.Devices;
                var deviceViewModels = new List<DeviceViewModel>();
                
                foreach (var device in devices)
                {
                    var viewModel = new DeviceViewModel
                    {
                        HostnameOrIp = device.HostnameOrIp,
                        Name = device.Name,
                        Location = device.Location,
                        Status = DeviceStatus.Online, // In a real app, this would come from actual monitoring
                        StatusColor = new SolidColorBrush(Colors.Green),
                        StatusText = "ONLINE",
                        LocationAndIp = string.IsNullOrEmpty(device.Location) ? device.HostnameOrIp : $"{device.Location} • {device.HostnameOrIp}",
                        HasSnmpInfo = true,
                        SnmpInfo = "SNMPv2c • HP LaserJet"
                    };
                    
                    deviceViewModels.Add(viewModel);
                }
                
                DevicesListControl.ItemsSource = deviceViewModels;
                
                // Update device count
                DeviceCountText.Text = devices.Count.ToString();
                
                // Show/hide appropriate UI elements
                if (devices.Count == 0)
                {
                    NoDevicesMessage.Visibility = Visibility.Visible;
                    AddDeviceButtonMain.Visibility = Visibility.Collapsed;
                }
                else
                {
                    NoDevicesMessage.Visibility = Visibility.Collapsed;
                    AddDeviceButtonMain.Visibility = Visibility.Visible;
                }
                
                _logger.LogDebug("Device list updated with {DeviceCount} devices", devices.Count);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating device list");
            }
        }
        
        private void UpdateKpiCards()
        {
            try
            {
                // In a real implementation, these values would come from actual data
                CriticalSuppliesCountText.Text = "2";
                CriticalSuppliesProgressBar.Value = 15;
                CriticalSuppliesPercentText.Text = "15% Критические";
                
                AlertCountText.Text = "3";
                AlertsProgressBar.Value = 25;
                AlertsPercentText.Text = "25% Активных";
                
                ForecastDaysText.Text = "45";
                ForecastProgressBar.Value = 85;
                ForecastPercentText.Text = "85% Здоровых";
                
                OnlineCountText.Text = "8 онлайн";
                
                _logger.LogDebug("KPI cards updated");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating KPI cards");
            }
        }
        
        private void RefreshDashboard(object state)
        {
            Dispatcher.Invoke(() =>
            {
                LoadDashboardData();
                _logger.LogDebug("Dashboard refreshed");
            });
        }
        
        private async void RefreshButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                RefreshButton.Content = "🔄 Обновление...";
                RefreshButton.IsEnabled = false;
                
                LoadDashboardData();
                
                _logger.LogInformation("Manual dashboard refresh triggered");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during manual refresh");
                System.Windows.MessageBox.Show($"Ошибка обновления: {ex.Message}", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                RefreshButton.Content = "🔄 Обновить";
                RefreshButton.IsEnabled = true;
            }
        }
        
        private void SettingsButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var settingsWindow = new SettingsWindow();
                settingsWindow.Owner = this;
                
                if (settingsWindow.ShowDialog() == true)
                {
                    // Restart the refresh timer with new interval
                    _refreshTimer.Change(
                        TimeSpan.FromSeconds(_settingsManager.Settings.RefreshIntervalSeconds),
                        TimeSpan.FromSeconds(_settingsManager.Settings.RefreshIntervalSeconds));
                    
                    // Reload data
                    LoadDashboardData();
                    
                    _logger.LogInformation("Settings updated and applied");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error opening settings window");
                System.Windows.MessageBox.Show($"Ошибка открытия настроек: {ex.Message}", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
        
        private void LanguageButton_Click(object sender, RoutedEventArgs e)
        {
            // In a real implementation, this would switch languages
            System.Windows.MessageBox.Show("Переключение языка будет реализовано в будущих версиях.", "Информация", MessageBoxButton.OK, MessageBoxImage.Information);
        }
        
        private void AboutButton_Click(object sender, RoutedEventArgs e)
        {
            // In a real implementation, this would show an about dialog
            System.Windows.MessageBox.Show("TonerWatch - Мониторинг расходных материалов принтеров\nВерсия 1.0.0", "О программе", MessageBoxButton.OK, MessageBoxImage.Information);
        }
        
        private async void SearchDevicesButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                RefreshButton.Content = "🔍 Поиск...";
                RefreshButton.IsEnabled = false;
                
                // In a real implementation, this would trigger device discovery
                System.Windows.MessageBox.Show("Поиск устройств будет реализован в будущих версиях.", "Информация", MessageBoxButton.OK, MessageBoxImage.Information);
                
                _logger.LogInformation("Device search initiated");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during device search");
                System.Windows.MessageBox.Show($"Ошибка поиска устройств: {ex.Message}", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                RefreshButton.Content = "🔍 Поиск устройств";
                RefreshButton.IsEnabled = true;
            }
        }
        
        private void AddDeviceButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var addDeviceWindow = new AddDeviceWindow();
                addDeviceWindow.Owner = this;
                
                if (addDeviceWindow.ShowDialog() == true)
                {
                    // Refresh the device list
                    LoadDashboardData();
                    _logger.LogInformation("Device added successfully");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error adding device");
                System.Windows.MessageBox.Show($"Ошибка добавления устройства: {ex.Message}", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
        
        private void ViewDeviceButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (sender is System.Windows.Controls.Button button && button.Tag is DeviceViewModel device)
                {
                    // Create a DeviceSettings object from the DeviceViewModel
                    var deviceSettings = new DeviceSettings
                    {
                        HostnameOrIp = device.HostnameOrIp,
                        Name = device.Name,
                        Location = device.Location,
                        IsActive = true,
                        SnmpCommunity = "public", // Default values
                        SnmpVersion = "SNMPv2c"
                    };
                    
                    var deviceDetailWindow = new DeviceDetailWindow(deviceSettings);
                    deviceDetailWindow.Owner = this;
                    deviceDetailWindow.ShowDialog();
                    
                    _logger.LogInformation("Device detail view opened for {DeviceName}", device.Name);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error opening device detail view");
                System.Windows.MessageBox.Show($"Ошибка открытия деталей устройства: {ex.Message}", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
        
        private void RemoveDeviceButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (sender is System.Windows.Controls.Button button && button.Tag is DeviceViewModel device)
                {
                    var result = System.Windows.MessageBox.Show(
                        $"Вы уверены, что хотите удалить устройство '{device.Name}' из мониторинга?",
                        "Подтверждение удаления",
                        MessageBoxButton.YesNo,
                        MessageBoxImage.Question);
                    
                    if (result == MessageBoxResult.Yes)
                    {
                        _settingsManager.RemoveDevice(device.HostnameOrIp);
                        LoadDashboardData();
                        _logger.LogInformation("Device removed: {DeviceName}", device.Name);
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error removing device");
                System.Windows.MessageBox.Show($"Ошибка удаления устройства: {ex.Message}", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
        
        private void DeviceCheckBox_Checked(object sender, RoutedEventArgs e)
        {
            if (sender is System.Windows.Controls.CheckBox checkBox && checkBox.Tag is DeviceViewModel device)
            {
                _selectedDevices.Add(device);
                UpdateBulkOperationsPanel();
            }
        }
        
        private void DeviceCheckBox_Unchecked(object sender, RoutedEventArgs e)
        {
            if (sender is System.Windows.Controls.CheckBox checkBox && checkBox.Tag is DeviceViewModel device)
            {
                _selectedDevices.Remove(device);
                UpdateBulkOperationsPanel();
            }
        }
        
        private void UpdateBulkOperationsPanel()
        {
            if (_selectedDevices.Count > 0)
            {
                BulkOperationsPanel.Visibility = Visibility.Visible;
                SelectedDevicesCountText.Text = $"{_selectedDevices.Count} устройств";
            }
            else
            {
                BulkOperationsPanel.Visibility = Visibility.Collapsed;
            }
        }
        
        private async void BulkRefreshButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                // In a real implementation, this would refresh selected devices
                System.Windows.MessageBox.Show($"Обновление {_selectedDevices.Count} устройств будет реализовано в будущих версиях.", 
                              "Информация", MessageBoxButton.OK, MessageBoxImage.Information);
                
                _logger.LogInformation("Bulk refresh initiated for {DeviceCount} devices", _selectedDevices.Count);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during bulk refresh");
                System.Windows.MessageBox.Show($"Ошибка массового обновления: {ex.Message}", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
        
        private void BulkDeleteButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (_selectedDevices.Count == 0) return;
                
                var result = System.Windows.MessageBox.Show(
                    $"Вы уверены, что хотите удалить {_selectedDevices.Count} устройств из мониторинга?",
                    "Подтверждение удаления",
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Question);
                
                if (result == MessageBoxResult.Yes)
                {
                    foreach (var device in _selectedDevices.ToList())
                    {
                        _settingsManager.RemoveDevice(device.HostnameOrIp);
                    }
                    
                    _selectedDevices.Clear();
                    UpdateBulkOperationsPanel();
                    LoadDashboardData();
                    
                    _logger.LogInformation("{DeviceCount} devices removed", _selectedDevices.Count);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during bulk delete");
                System.Windows.MessageBox.Show($"Ошибка массового удаления: {ex.Message}", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
        
        private void GroupingComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            // In a real implementation, this would group devices based on selection
            // For now, we'll just log the selection
            if (GroupingComboBox.SelectedItem is ComboBoxItem item)
            {
                _logger.LogInformation("Device grouping changed to: {Grouping}", item.Content);
            }
        }
        
        protected override void OnClosed(EventArgs e)
        {
            _cancellationTokenSource.Cancel();
            _refreshTimer?.Dispose();
            _cancellationTokenSource?.Dispose();
            base.OnClosed(e);
        }
    }
    
    // Device view model for UI binding
    public class DeviceViewModel
    {
        public string HostnameOrIp { get; set; }
        public string Name { get; set; }
        public string Location { get; set; }
        public DeviceStatus Status { get; set; }
        public SolidColorBrush StatusColor { get; set; }
        public string StatusText { get; set; }
        public string LocationAndIp { get; set; }
        public bool HasSnmpInfo { get; set; }
        public string SnmpInfo { get; set; }
    }
}