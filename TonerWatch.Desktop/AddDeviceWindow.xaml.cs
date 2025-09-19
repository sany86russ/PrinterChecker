using System;
using System.Net;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using Microsoft.Extensions.Logging;
using TonerWatch.Desktop.Services;
using TonerWatch.Core.Models;

namespace TonerWatch.Desktop
{
    public partial class AddDeviceWindow : Window
    {
        private readonly ILogger<AddDeviceWindow> _logger;
        private readonly SettingsManager _settingsManager;
        
        public AddDeviceWindow()
        {
            InitializeComponent();
            _logger = ServiceManager.Instance.GetLogger<AddDeviceWindow>();
            _settingsManager = ServiceManager.Instance.GetService<SettingsManager>();
        }
        
        private void AddButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var ipAddressOrHost = IpAddressTextBox.Text.Trim();
                var deviceName = DeviceNameTextBox.Text.Trim();
                var location = LocationTextBox.Text.Trim();
                
                // Validate IP address or hostname
                if (string.IsNullOrEmpty(ipAddressOrHost))
                {
                    System.Windows.MessageBox.Show("Пожалуйста, введите IP адрес или имя хоста устройства.", 
                                  "Ошибка ввода", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }
                
                // Validate IP format
                if (!IsValidIpAddressOrHost(ipAddressOrHost))
                {
                    System.Windows.MessageBox.Show("Пожалуйста, введите корректный IP адрес или имя хоста.", 
                                  "Ошибка ввода", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }
                
                // Create new device settings
                var deviceSettings = new DeviceSettings
                {
                    HostnameOrIp = ipAddressOrHost,
                    Name = string.IsNullOrEmpty(deviceName) ? ipAddressOrHost : deviceName,
                    Location = location,
                    IsActive = true,
                    PollingIntervalSeconds = 300, // Default 5 minutes
                    SnmpCommunity = _settingsManager.Settings.SnmpCommunity,
                    SnmpVersion = _settingsManager.Settings.SnmpVersion,
                    CriticalThreshold = _settingsManager.Settings.CriticalThreshold,
                    WarningThreshold = _settingsManager.Settings.WarningThreshold,
                    AddedAt = DateTime.UtcNow
                };
                
                // Add to settings
                _settingsManager.AddDevice(deviceSettings);
                
                _logger.LogInformation("Device added: {IpAddressOrHost}", ipAddressOrHost);
                
                DialogResult = true;
                Close();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error adding device");
                System.Windows.MessageBox.Show($"Ошибка при добавлении устройства: {ex.Message}", 
                              "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
        
        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }
        
        private bool IsValidIpAddressOrHost(string input)
        {
            // Check if it's a valid IP address
            if (IPAddress.TryParse(input, out _))
                return true;
            
            // Check if it's a valid hostname
            var hostnamePattern = @"^([a-zA-Z0-9]|[a-zA-Z0-9][a-zA-Z0-9\-]{0,61}[a-zA-Z0-9])(\.([a-zA-Z0-9]|[a-zA-Z0-9][a-zA-Z0-9\-]{0,61}[a-zA-Z0-9]))*$";
            return Regex.IsMatch(input, hostnamePattern);
        }
        
        private void IpAddressTextBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            var input = IpAddressTextBox.Text.Trim();
            
            if (string.IsNullOrEmpty(input))
            {
                IpValidationText.Visibility = Visibility.Collapsed;
                return;
            }
            
            if (IsValidIpAddressOrHost(input))
            {
                IpValidationText.Text = "✅ Формат адреса корректный";
                IpValidationText.Foreground = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Colors.Green);
                IpValidationText.Visibility = Visibility.Visible;
            }
            else
            {
                IpValidationText.Text = "⚠️ Неверный формат IP адреса или имени хоста";
                IpValidationText.Foreground = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Colors.Orange);
                IpValidationText.Visibility = Visibility.Visible;
            }
        }
    }
}