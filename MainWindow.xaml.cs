using System.Diagnostics;
using System.IO;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Microsoft.Win32;

namespace SolSis.Library;

public partial class MainWindow : Window
{
    private readonly string dataFile = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "SolSisLibrary", "library.json");
    private List<GameItem> games = new();

    public MainWindow()
    {
        InitializeComponent();
        LoadGames();
        Render();
    }

    private void LoadGames()
    {
        try
        {
            if (File.Exists(dataFile))
                games = JsonSerializer.Deserialize<List<GameItem>>(File.ReadAllText(dataFile)) ?? new();
        }
        catch { games = new(); }
    }

    private void SaveGames()
    {
        Directory.CreateDirectory(Path.GetDirectoryName(dataFile)!);
        File.WriteAllText(dataFile, JsonSerializer.Serialize(games, new JsonSerializerOptions { WriteIndented = true }));
    }

    private void Render()
    {
        GamesPanel.Children.Clear();
        var q = SearchBox?.Text?.Trim().ToLowerInvariant() ?? "";
        var visible = games.Where(g => g.Name.ToLowerInvariant().Contains(q)).ToList();

        TotalGamesText.Text = $"{games.Count} games";
        TotalTimeText.Text = $"Play time: {FormatTime(games.Sum(g => g.PlayTimeSeconds))}";

        if (!visible.Any())
        {
            GamesPanel.Children.Add(new TextBlock {
                Text = games.Count == 0 ? "Your library is empty.\nClick ADD GAME to start." : "No games found.",
                Foreground = (Brush)FindResource("Muted"), FontSize = 18, Margin = new Thickness(5, 40, 0, 0)
            });
            return;
        }

        foreach (var game in visible) GamesPanel.Children.Add(CreateCard(game));
    }

    private Border CreateCard(GameItem game)
    {
        var card = new Border {
            Width = 270, Height = 300, Margin = new Thickness(0,0,18,18),
            Background = (Brush)FindResource("Card"), CornerRadius = new CornerRadius(14),
            Padding = new Thickness(12)
        };
        var stack = new StackPanel();

        var image = new Image { Height = 145, Stretch = Stretch.UniformToFill };
        if (File.Exists(game.CoverPath))
        {
            try {
                image.Source = new BitmapImage(new Uri(game.CoverPath, UriKind.Absolute));
            } catch { }
        }
        if (image.Source == null)
        {
            image.Source = MakePlaceholder();
        }

        stack.Children.Add(new Border {
            Height = 145, Background = (Brush)FindResource("Panel"), CornerRadius = new CornerRadius(10),
            Child = image
        });

        stack.Children.Add(new TextBlock {
            Text = game.Name, FontSize = 18, FontWeight = FontWeights.SemiBold,
            Margin = new Thickness(2,12,2,2), TextTrimming = TextTrimming.CharacterEllipsis
        });
        stack.Children.Add(new TextBlock {
            Text = "PLAY TIME  •  " + FormatTime(game.PlayTimeSeconds),
            Foreground = (Brush)FindResource("Muted"), FontSize = 11
        });

        var buttons = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0,12,0,0) };
        var play = new Button { Content = "▶  PLAY", Background = (Brush)FindResource("Accent"), FontWeight = FontWeights.Bold };
        play.Click += (_,__) => Launch(game);
        buttons.Children.Add(play);

        var remove = new Button { Content = "Remove", Margin = new Thickness(8,0,0,0) };
        remove.Click += (_,__) => { games.Remove(game); SaveGames(); Render(); };
        buttons.Children.Add(remove);
        stack.Children.Add(buttons);

        card.Child = stack;
        return card;
    }

    private ImageSource MakePlaceholder()
    {
        var bmp = new BitmapImage();
        bmp.BeginInit();
        bmp.UriSource = new Uri("https://dummyimage.com/540x290/171c27/8d97a8.png&text=NO+COVER");
        bmp.EndInit();
        return bmp;
    }

    private async void Launch(GameItem game)
    {
        if (!File.Exists(game.ExePath))
        {
            MessageBox.Show("The selected EXE was not found.", "SolSis Library");
            return;
        }
        try
        {
            var start = Process.Start(new ProcessStartInfo(game.ExePath) {
                WorkingDirectory = Path.GetDirectoryName(game.ExePath) ?? "",
                UseShellExecute = true
            });
            if (start == null) return;
            var timer = Stopwatch.StartNew();
            await start.WaitForExitAsync();
            timer.Stop();
            game.PlayTimeSeconds += timer.Elapsed.TotalSeconds;
            SaveGames();
            Render();
        }
        catch (Exception ex)
        {
            MessageBox.Show(ex.Message, "Could not launch game");
        }
    }

    private void AddGame_Click(object sender, RoutedEventArgs e)
    {
        var name = Microsoft.VisualBasic.Interaction.InputBox("Game name:", "Add Game", "");
        if (string.IsNullOrWhiteSpace(name)) return;

        var exeDialog = new OpenFileDialog { Filter = "Executable (*.exe)|*.exe" };
        if (exeDialog.ShowDialog() != true) return;

        var coverDialog = new OpenFileDialog {
            Filter = "Image files|*.png;*.jpg;*.jpeg;*.bmp|All files|*.*"
        };
        string cover = "";
        if (coverDialog.ShowDialog() == true) cover = coverDialog.FileName;

        games.Add(new GameItem { Name = name.Trim(), ExePath = exeDialog.FileName, CoverPath = cover });
        SaveGames();
        Render();
    }

    private void SearchBox_TextChanged(object sender, TextChangedEventArgs e) => Render();
    private void Home_Click(object sender, RoutedEventArgs e) { PageTitle.Text = "Your Library"; Render(); }
    private void AllGames_Click(object sender, RoutedEventArgs e) { PageTitle.Text = "All Games"; Render(); }

    private static string FormatTime(double seconds)
    {
        var ts = TimeSpan.FromSeconds(seconds);
        return ts.TotalHours >= 1 ? $"{(int)ts.TotalHours}h {ts.Minutes}m" : $"{ts.Minutes}m";
    }

    public class GameItem
    {
        public string Name { get; set; } = "";
        public string ExePath { get; set; } = "";
        public string CoverPath { get; set; } = "";
        public double PlayTimeSeconds { get; set; }
    }
}
