﻿﻿﻿﻿﻿﻿﻿﻿﻿﻿﻿using System.Windows;
using TonerWatch.Desktop.Services;
using WinForms = System.Windows.Forms;

namespace TonerWatch.Desktop
{
    public partial class App : System.Windows.Application
    {
        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);
            
            try
            {
                // Initialize the main window
                var mainWindow = new MainWindow();
                mainWindow.Show();
                this.MainWindow = mainWindow;
            }
            catch (Exception ex)
            {
                System.Windows.MessageBox.Show(
                    $"Не удалось запустить главное окно приложения:\n\n{ex.Message}\n\nДетали:\n{ex.StackTrace}",
                    "TonerWatch - Ошибка запуска",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
                
                // Exit the application if we can't show the main window
                this.Shutdown(1);
            }
        }
        
        protected override void OnExit(ExitEventArgs e)
        {
            try
            {
                // Принудительно останавливаем все сервисы
                ServiceManager.Instance.Dispose();
            }
            catch (Exception ex)
            {
                // Логируем ошибку но не блокируем завершение
                System.Diagnostics.Debug.WriteLine($"Error during application exit: {ex.Message}");
            }
            finally
            {
                base.OnExit(e);
                
                // Принудительно завершаем все потоки и процесс
                System.Threading.Tasks.Task.Run(() =>
                {
                    System.Threading.Thread.Sleep(1000); // Даём время на корректное завершение
                    Environment.Exit(e.ApplicationExitCode);
                });
            }
        }
    }
}