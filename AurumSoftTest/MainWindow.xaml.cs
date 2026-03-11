using AurumSoftTest.Models;
using AurumSoftTest.Services;
using Microsoft.Win32;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Runtime.CompilerServices;
using System.Windows;

namespace AurumSoftTest
{
    public partial class MainWindow : Window, INotifyPropertyChanged
    {
        private readonly WellDataService _service = new();
        private string? _statusText;

        public ObservableCollection<WellAgregate> Summaries { get; } = new();
        public ObservableCollection<ValidationError> Errors { get; } = new();

        public string? StatusText
        {
            get => _statusText;
            set { _statusText = value; OnPropertyChanged(); }
        }

        public MainWindow()
        {
            InitializeComponent();
            DataContext = this;
        }

        private async void OnLoadClick(object sender, RoutedEventArgs e)
        {
            var dlg = new OpenFileDialog
            {
                Filter = "CSV файлы (*.csv)|*.csv|Все файлы (*.*)|*.*",
                Title = "Выберите CSV файл со скважинами"
            };

            if (dlg.ShowDialog() != true)
                return;

            try
            {
                var (summaries, errors) = await _service.LoadAsync(dlg.FileName);

                Summaries.Clear();
                foreach (var s in summaries)
                    Summaries.Add(s);

                Errors.Clear();
                foreach (var err in errors)
                    Errors.Add(err);

                StatusText = $"Файл: {Path.GetFileName(dlg.FileName)} — скважин: {summaries.Count}, ошибок: {errors.Count}";
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, $"Ошибка загрузки файла: {ex.Message}", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private async void OnExportClick(object sender, RoutedEventArgs e)
        {
            if (!Summaries.Any())
            {
                MessageBox.Show(this, "Нет данных для экспорта. Сначала загрузите CSV.", "Экспорт невозможен", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            var dlg = new SaveFileDialog
            {
                Filter = "JSON файлы (*.json)|*.json|Все файлы (*.*)|*.*",
                FileName = "well-summary.json",
                Title = "Сохранить сводку в JSON"
            };

            if (dlg.ShowDialog() != true)
                return;

            try
            {
                await _service.ExportSummaryToJsonAsync(dlg.FileName, Summaries);
                StatusText = $"Сводка сохранена в {Path.GetFileName(dlg.FileName)}";
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, $"Ошибка экспорта: {ex.Message}", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        public event PropertyChangedEventHandler? PropertyChanged;
        private void OnPropertyChanged([CallerMemberName] string? propertyName = null) =>
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}