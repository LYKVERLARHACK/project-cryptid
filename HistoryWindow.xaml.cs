using System.Diagnostics;
using System.ComponentModel;
using System.IO;
using System.Net.Http;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using Microsoft.Data.Sqlite;
using RealityCheckDesktop.Models;
using RealityCheckDesktop.Services;

namespace RealityCheckDesktop;

public partial class HistoryWindow : Window
{
    private readonly IncidentStore _store;
    private readonly IncidentChatService _chat;
    private bool _isSending;

    public HistoryWindow(IncidentStore store, IncidentChatService chat)
    {
        InitializeComponent();
        _store = store;
        _chat = chat;
        Loaded += (_, _) => RefreshIncidents();
    }

    private void RefreshButton_Click(object sender, RoutedEventArgs e) => RefreshIncidents();

    private void RefreshIncidents()
    {
        var selectedId = (IncidentsList.SelectedItem as IncidentRecord)?.Id;
        try
        {
            var incidents = _store.GetRecentIncidents();
            IncidentsList.ItemsSource = incidents;
            IncidentsList.SelectedItem = incidents.FirstOrDefault(item => item.Id == selectedId)
                ?? incidents.FirstOrDefault();
            IncidentContent.Visibility = incidents.Count == 0 ? Visibility.Collapsed : Visibility.Visible;
            EmptyHistoryPanel.Visibility = incidents.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
            if (incidents.Count == 0)
            {
                ClearIncident();
            }
            StatusText.Text = $"Saved locally in {_store.DatabasePath}";
        }
        catch (Exception ex) when (ex is SqliteException or IOException or UnauthorizedAccessException or InvalidOperationException or DllNotFoundException)
        {
            StatusText.Text = $"Could not load incident history: {ex.Message}";
        }
    }

    private void IncidentsList_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (IncidentsList.SelectedItem is not IncidentRecord incident)
        {
            ClearIncident();
            return;
        }

        IncidentTitle.Text = incident.DisplayTitle;
        IncidentMeta.Text = incident.DisplaySubtitle;
        QuestionInput.IsEnabled = true;
        SendButton.IsEnabled = !_isSending;
        IncidentSummary.Text = incident.Summary;
        IncidentExplanation.Text = incident.Explanation;
        IncidentDetails.Text = FormatDetails(incident.DetailsJson);
        OpenVideoButton.Visibility = IsYouTubeUrl(incident.SourceUrl) ? Visibility.Visible : Visibility.Collapsed;
        try
        {
            RenderMessages(_store.GetMessages(incident.Id));
        }
        catch (Exception ex) when (ex is SqliteException or IOException or UnauthorizedAccessException or InvalidOperationException or DllNotFoundException)
        {
            StatusText.Text = $"Could not load questions for this incident: {ex.Message}";
        }
    }

    private async void SendQuestion_Click(object sender, RoutedEventArgs e)
    {
        var incident = IncidentsList.SelectedItem as IncidentRecord;
        var question = QuestionInput.Text.Trim();
        if (_isSending || incident is null || question.Length == 0) return;

        _isSending = true;
        SendButton.IsEnabled = false;
        StatusText.Text = "Asking the local model about this incident…";
        try
        {
            var history = _store.GetMessages(incident.Id);
            var answer = await _chat.AskAsync(incident, history, question);
            _store.AddExchange(incident.Id, question, answer);
            QuestionInput.Clear();
            if ((IncidentsList.SelectedItem as IncidentRecord)?.Id == incident.Id)
            {
                RenderMessages(_store.GetMessages(incident.Id));
            }
            StatusText.Text = "Answer saved locally.";
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or InvalidOperationException
                                   or JsonException or SqliteException or IOException or UnauthorizedAccessException
                                   or DllNotFoundException)
        {
            StatusText.Text = $"Could not answer or save this question: {ex.Message}";
        }
        finally
        {
            _isSending = false;
            SendButton.IsEnabled = IncidentsList.SelectedItem is IncidentRecord;
        }
    }

    private void OpenVideoButton_Click(object sender, RoutedEventArgs e)
    {
        if (IncidentsList.SelectedItem is not IncidentRecord incident || !IsYouTubeUrl(incident.SourceUrl)) return;
        try
        {
            Process.Start(new ProcessStartInfo(incident.SourceUrl) { UseShellExecute = true });
        }
        catch (Win32Exception ex)
        {
            StatusText.Text = $"Could not open the video: {ex.Message}";
        }
    }

    private void RenderMessages(IReadOnlyList<IncidentMessage> messages)
    {
        ChatMessages.Children.Clear();
        foreach (var message in messages)
        {
            var isUser = message.Role == "user";
            var bubble = new Border
            {
                Background = BrushColor(isUser ? "#292D30" : "#1D2023"),
                BorderBrush = BrushColor("#34383C"),
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(3),
                Padding = new Thickness(10, 7, 10, 7),
                Margin = new Thickness(isUser ? 48 : 0, 0, isUser ? 0 : 48, 9),
                HorizontalAlignment = isUser ? System.Windows.HorizontalAlignment.Right : System.Windows.HorizontalAlignment.Left,
                MaxWidth = 680,
                Child = new StackPanel
                {
                    Children =
                    {
                        new TextBlock
                        {
                            Text = isUser ? "YOU" : "QWEN",
                            Foreground = BrushColor("#9AA0A5"),
                            FontSize = 9,
                            FontFamily = new System.Windows.Media.FontFamily("Consolas"),
                            FontWeight = FontWeights.SemiBold,
                            Margin = new Thickness(0, 0, 0, 3)
                        },
                        new TextBlock
                        {
                            Text = message.Content,
                            Foreground = BrushColor("#E0E2E4"),
                            FontSize = 12,
                            TextWrapping = TextWrapping.Wrap
                        }
                    }
                }
            };
            ChatMessages.Children.Add(bubble);
        }
        ChatScrollViewer.ScrollToEnd();
    }

    private void ClearIncident()
    {
        IncidentTitle.Text = "Select an incident";
        IncidentMeta.Text = "";
        IncidentSummary.Text = "";
        IncidentExplanation.Text = "";
        IncidentDetails.Text = "";
        OpenVideoButton.Visibility = Visibility.Collapsed;
        ChatMessages.Children.Clear();
        QuestionInput.IsEnabled = false;
        SendButton.IsEnabled = false;
    }

    private static string FormatDetails(string detailsJson)
    {
        try
        {
            using var document = JsonDocument.Parse(detailsJson);
            return JsonSerializer.Serialize(document.RootElement, new JsonSerializerOptions { WriteIndented = true });
        }
        catch (JsonException)
        {
            return detailsJson;
        }
    }

    private static bool IsYouTubeUrl(string value) =>
        Uri.TryCreate(value, UriKind.Absolute, out var uri)
        && (uri.Host.Equals("youtube.com", StringComparison.OrdinalIgnoreCase)
            || uri.Host.EndsWith(".youtube.com", StringComparison.OrdinalIgnoreCase)
            || uri.Host.Equals("youtu.be", StringComparison.OrdinalIgnoreCase));

    private static System.Windows.Media.SolidColorBrush BrushColor(string hex) =>
        new((System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString(hex));
}
