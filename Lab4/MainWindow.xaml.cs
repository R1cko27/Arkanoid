using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;
using System.Windows.Threading;
using Arkanoid;

namespace Lab4;

public partial class MainWindow : Window
{
    private const string SavePath = "save.json"; // Путь к файлу сохранения
    private const double CellSize = 20; // Размер клетки поля в пикселях

    // Кисти для отрисовки объектов поля
    private static readonly Brush BrickBrush = new SolidColorBrush(Color.FromRgb(0x3F, 0x7F, 0xD0)); // Кирпич
    private static readonly Brush PaddleBrush = new SolidColorBrush(Color.FromRgb(0x2E, 0xC0, 0x6E)); // Ракетка
    private static readonly Brush BallBrush = new SolidColorBrush(Color.FromRgb(0xFF, 0xD2, 0x4D)); // Мяч
    private static readonly Brush ExpandBrush = new SolidColorBrush(Color.FromRgb(0xFF, 0x9F, 0x40)); // Предмет Expand
    private static readonly Brush SlowBrush = new SolidColorBrush(Color.FromRgb(0x40, 0xC0, 0xE0)); // Предмет Slow
    private static readonly Brush ExtraLifeBrush = new SolidColorBrush(Color.FromRgb(0x7F, 0xD0, 0x50)); // Предмет ExtraLife

    private readonly Random _random = new(); // Генератор случайных чисел
    private Game _game = null!; // Текущая партия
    private DispatcherTimer _timer = null!; // Таймер игрового цикла

    // Создаёт окно и подписывается на событие загрузки
    public MainWindow()
    {
        InitializeComponent();
        Loaded += OnLoaded;
    }

    // Загружает партию, создаёт таймер и выполняет первую отрисовку
    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        _game = CreateOrLoadGame();

        // 33 мс на тик — примерно 30 кадров в секунду
        _timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(33) };
        _timer.Tick += (_, _) => OnTick();
        _timer.Start();

        Focus();
        Render();
        UpdateStatus();
    }

    // Предлагает продолжить сохранённую партию, а если сохранения нет — начинает новую
    private Game CreateOrLoadGame()
    {
        if (File.Exists(SavePath))
        {
            MessageBoxResult answer = MessageBox.Show(
                "Найдено сохранение. Продолжить игру?",
                "Arkanoid", MessageBoxButton.YesNo, MessageBoxImage.Question);

            if (answer == MessageBoxResult.Yes)
            {
                try
                {
                    return Game.FromState(GameStateSerializer.Load(SavePath), Game.DefaultDropChance, _random);
                }
                catch (Exception ex)
                {
                    MessageBox.Show(
                        $"Не удалось загрузить сохранение: {ex.Message}. Начинается новая игра.",
                        "Arkanoid", MessageBoxButton.OK, MessageBoxImage.Warning);
                }
            }
        }

        return NewGame();
    }

    // Создаёт новую партию с параметрами по умолчанию
    private Game NewGame()
    {
        Game game = new(Game.DefaultFieldWidth, Game.DefaultFieldHeight, Game.DefaultDropChance, _random);
        game.StartNew();
        return game;
    }

    // Обрабатывает один тик игры
    private void OnTick()
    {
        _game.Tick();
        Render();
        UpdateStatus();

        if (!_game.IsGameOver && !_game.IsWon)
            return;

        // Партия закончена
        _timer.Stop();
        File.Delete(SavePath);

        string message = _game.IsWon
            ? $"Победа! Все кирпичи разрушены. Итоговый счёт: {_game.Score}."
            : $"Игра окончена. Итоговый счёт: {_game.Score}.";

        MessageBoxResult again = MessageBox.Show(
            message + "\nСыграть ещё раз?",
            "Arkanoid", MessageBoxButton.YesNo, MessageBoxImage.Information);

        if (again == MessageBoxResult.Yes)
        {
            _game = NewGame();
            _timer.Start();
            Render();
            UpdateStatus();
        }
        else
        {
            Close();
        }
    }

    // Обновляет строку состояния
    private void UpdateStatus()
    {
        ScoreText.Text = _game.Score.ToString();
        LivesText.Text = _game.Lives.ToString();
        BricksText.Text = _game.BricksRemaining.ToString();
    }

    // Обрабатывает нажатия клавиш
    private void Window_KeyDown(object sender, KeyEventArgs e)
    {
        switch (e.Key)
        {
            // Движение ракетки
            case Key.Left:
                _game.MovePaddle(-1);
                break;

            case Key.Right:
                _game.MovePaddle(1);
                break;

            // Запуск мяча
            case Key.Space:
            case Key.Enter:
                _game.Launch();
                break;

            // Сохранение состояния и выход
            case Key.Escape:
                GameStateSerializer.Save(_game.ToState(), SavePath);
                MessageBox.Show("Состояние сохранено в файл save.json. До встречи!",
                    "Arkanoid", MessageBoxButton.OK, MessageBoxImage.Information);
                Close();
                break;
        }
    }

    // Перерисовывает игровое поле
    private void Render()
    {
        // Каждый кадр поле очищается и рисуется заново
        Field.Children.Clear();

        // Кирпичи
        foreach (BrickState brick in _game.Bricks)
        {
            Rectangle brickRect = new()
            {
                Width = Game.BrickWidth * CellSize - 6,
                Height = CellSize - 8,
                Fill = BrickBrush,
            };
            Canvas.SetLeft(brickRect, brick.X * CellSize + 3);
            Canvas.SetTop(brickRect, brick.Y * CellSize + 4);
            Field.Children.Add(brickRect);
        }

        // Ракетка
        PaddleState paddle = _game.Paddle;
        Rectangle paddleRect = new()
        {
            Width = paddle.Width * CellSize,
            Height = CellSize,
            Fill = PaddleBrush,
        };
        Canvas.SetLeft(paddleRect, paddle.X * CellSize);
        Canvas.SetTop(paddleRect, (_game.FieldHeight - Game.PaddleRowOffset) * CellSize);
        Field.Children.Add(paddleRect);

        // Мяч
        BallState ball = _game.Ball;
        Ellipse ballEllipse = new()
        {
            Width = CellSize,
            Height = CellSize,
            Fill = BallBrush,
        };
        Canvas.SetLeft(ballEllipse, (ball.X - 0.5) * CellSize);
        Canvas.SetTop(ballEllipse, (ball.Y - 0.5) * CellSize);
        Field.Children.Add(ballEllipse);

        // Падающие предметы
        foreach (PowerUpState powerUp in _game.PowerUps)
        {
            Rectangle square = new()
            {
                Width = CellSize,
                Height = CellSize,
                Fill = PowerUpBrush(powerUp.Type),
                Stroke = Brushes.Black,
                StrokeThickness = 1,
            };
            Canvas.SetLeft(square, (powerUp.X - 0.5) * CellSize);
            Canvas.SetTop(square, (powerUp.Y - 0.5) * CellSize);
            Field.Children.Add(square);

            TextBlock letter = new()
            {
                Text = powerUp.Type switch
                {
                    PowerUpType.Expand => "E",
                    PowerUpType.Slow => "S",
                    PowerUpType.ExtraLife => "L",
                    _ => "?",
                },
                Foreground = Brushes.Black,
                FontSize = 11,
                FontWeight = FontWeights.Bold,
            };
            Canvas.SetLeft(letter, (powerUp.X - 0.35) * CellSize);
            Canvas.SetTop(letter, (powerUp.Y - 0.45) * CellSize);
            Field.Children.Add(letter);
        }
    }

    // Возвращает кисть для типа предмета
    private static Brush PowerUpBrush(PowerUpType type) => type switch
    {
        PowerUpType.Expand => ExpandBrush,
        PowerUpType.Slow => SlowBrush,
        PowerUpType.ExtraLife => ExtraLifeBrush,
        _ => Brushes.White,
    };
}
