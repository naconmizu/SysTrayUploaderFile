using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Forms;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;

namespace ever
{
    /// <summary>
    /// Interação lógica para MainWindow.xam
    /// </summary>
    public partial class MainWindow : Window
    {
        public MainWindow()
        {
            InitializeComponent();
        }

        private void Window_DragEnter(object sender, System.Windows.DragEventArgs e)
        {
            if (e.Data.GetDataPresent(System.Windows.DataFormats.FileDrop))
            {
                e.Effects = System.Windows.DragDropEffects.Copy;
            }
            else
            {
                e.Effects = System.Windows.DragDropEffects.None;
            }

        }

        private async void Window_Drop(object sender, System.Windows.DragEventArgs e)
        {
            if (!e.Data.GetDataPresent(System.Windows.DataFormats.FileDrop))
                return;

            try
            {
                string[] droppedFiles = (string[])e.Data.GetData(System.Windows.DataFormats.FileDrop);

                if (droppedFiles == null || droppedFiles.Length == 0)
                {
                    ShowNotification("No files detected", ToolTipIcon.Warning);
                    return;
                }

                // Clear previous results
                DropText.Text = "Processing files...";

                // Process files
                await ProcessDroppedFilesAsync(droppedFiles);
            }
            catch (Exception ex)
            {
                DropText.Text = $"Error processing dropped files: {ex.Message}";
                ShowNotification("Error processing files", ToolTipIcon.Error);
            }
        }

        private async Task ProcessDroppedFilesAsync(string[] filePaths)
        {
            var results = new System.Text.StringBuilder();
            int successCount = 0;
            int totalCount = filePaths.Length;

            foreach (var filePath in filePaths)
            {
                string filename = System.IO.Path.GetFileName(filePath);

                try
                {
                    // Validate file before upload
                    if (!ValidateFile(filePath))
                    {
                        results.AppendLine($"❌ Skipped: {filename} (invalid file)");
                        continue;
                    }

                    // Show progress
                    results.AppendLine($"📤 Uploading: {filename}...");
                    DropText.Text = results.ToString();

                    // Upload file asynchronously (don't block UI)
                    await Uploader.UploadFileAsync(filePath);

                    // Update result
                    results.Remove(results.Length - $"📤 Uploading: {filename}...{Environment.NewLine}".Length,
                                  $"📤 Uploading: {filename}...{Environment.NewLine}".Length);
                    results.AppendLine($"✅ Uploaded: {filename}");
                    successCount++;
                }
                catch (Exception ex)
                {
                    // Remove progress line and add error
                    var progressLine = $"📤 Uploading: {filename}...{Environment.NewLine}";
                    if (results.ToString().EndsWith(progressLine))
                    {
                        results.Remove(results.Length - progressLine.Length, progressLine.Length);
                    }

                    results.AppendLine($"❌ Failed: {filename} - {ex.Message}");
                }

                // Update UI
                DropText.Text = results.ToString();
            }

            // Show final notification
            ShowFinalNotification(successCount, totalCount);
        }

        private bool ValidateFile(string filePath)
        {
            try
            {
                // Check if file exists
                if (!File.Exists(filePath))
                    return false;

                // Check file size (example: max 100MB)
                var fileInfo = new FileInfo(filePath);
                const long maxFileSize = 100 * 1024 * 1024; // 100MB
                if (fileInfo.Length > maxFileSize)
                {
                    ShowNotification($"File too large: {fileInfo.Name} (max 100MB)", ToolTipIcon.Warning);
                    return false;
                }

                // Check if file is accessible
                using (var fs = File.OpenRead(filePath))
                {
                    // File is readable
                }

                return true;
            }
            catch (Exception ex)
            {
                ShowNotification($"File validation error: {ex.Message}", ToolTipIcon.Warning);
                return false;
            }
        }

        private void ShowFinalNotification(int successCount, int totalCount)
        {
            if (successCount == totalCount)
            {
                ShowNotification($"All {totalCount} files uploaded successfully! ✅", ToolTipIcon.Info);
            }
            else if (successCount > 0)
            {
                ShowNotification($"{successCount}/{totalCount} files uploaded successfully", ToolTipIcon.Warning);
            }
            else
            {
                ShowNotification("No files were uploaded", ToolTipIcon.Error);
            }
        }

        private void ShowNotification(string message, ToolTipIcon icon)
        {
            // Use the existing tray icon from App instead of creating a new one
            var app = (App)System.Windows.Application.Current;
            app?.ShowTrayMessage(message, icon);
        }
 

       
    }
}
