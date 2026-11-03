using System.Text;
using Arkanoid;

Console.OutputEncoding = Encoding.UTF8;

const string SavePath = "save.json";
Random random = new();

Game game = CreateOrLoadGame(random, SavePath);

Console.Clear();
Console.WriteLine("ARKANOID (вариант 9): аналог Arkanoid с предметами из кирпичей.");
Console.WriteLine("Предметы: E — расширить ракетку; S — замедлить мяч; L — дополнительная жизнь.");
Console.WriteLine("Нажмите любую клавишу, чтобы начать...");
Console.ReadKey(true);

bool exit = false;
while (!exit)
{
    // Один сеанс игры: продолжается, пока не закончится партия или игрок не выйдет.
    while (!game.IsGameOver && !game.IsWon && !exit)
    {
        if (Console.KeyAvailable)
        {
            ConsoleKey key = Console.ReadKey(true).Key;
            switch (key)
            {
                case ConsoleKey.LeftArrow:
                    game.MovePaddle(-1);
                    break;

                case ConsoleKey.RightArrow:
                    game.MovePaddle(1);
                    break;

                case ConsoleKey.Spacebar:
                case ConsoleKey.Enter:
                    game.Launch();
                    break;

                case ConsoleKey.Escape:
                    GameStateSerializer.Save(game.ToState(), SavePath);
                    Console.Clear();
                    Console.WriteLine($"Состояние сохранено в файл \"{SavePath}\". До встречи!");
                    exit = true;
                    break;
            }
        }

        if (!exit)
        {
            game.Tick();
            Render(game);
            Thread.Sleep(30); // ~33 тика в секунду - скорость обновления экрана
        }
    }

    if (exit)
        break;

    Console.Clear();
    if (game.IsWon)
    {
        Console.WriteLine($"ПОБЕДА! Все кирпичи разрушены. Итоговый счёт: {game.Score}.");
    }
    else
    {
        Console.WriteLine($"Игра окончена. Итоговый счёт: {game.Score}.");
    }

    // Партия закончена - сохранение больше не нужно.
    File.Delete(SavePath);

    Console.Write("Сыграть ещё раз? [Д/н]: ");
    string? answer = Console.ReadLine();
    bool again = string.IsNullOrWhiteSpace(answer)
        || answer.Trim().ToLowerInvariant() is "д" or "да" or "y" or "yes";
    if (again)
    {
        game = NewGame(random);
    }
    else
    {
        exit = true;
    }
}

// При первом запуске предлагает продолжить сохранённую партию, если сохранение есть.
static Game CreateOrLoadGame(Random random, string savePath)
{
    if (!File.Exists(savePath))
        return NewGame(random);

    Console.Write("Найдено сохранение. Продолжить игру? [Д/н]: ");
    string? answer = Console.ReadLine();
    bool resume = string.IsNullOrWhiteSpace(answer)
        || answer.Trim().ToLowerInvariant() is "д" or "да" or "y" or "yes";
    if (!resume)
        return NewGame(random);

    try
    {
        return Game.FromState(GameStateSerializer.Load(savePath), Game.DefaultDropChance, random);
    }
    catch (Exception ex)
    {
        Console.WriteLine($"Не удалось загрузить сохранение: {ex.Message}. Начинается новая игра.");
        return NewGame(random);
    }
}

// Создаёт игру и начинает новую партию.
static Game NewGame(Random random)
{
    Game game = new(Game.DefaultFieldWidth, Game.DefaultFieldHeight, Game.DefaultDropChance, random);
    game.StartNew();
    return game;
}

// Выводит текущее состояние игры в консоль в текстовом виде.
static void Render(Game game)
{
    StringBuilder sb = new();

    sb.Append("Счёт: ").Append(game.Score)
      .Append("   Жизни: ").Append(game.Lives)
      .Append("   Кирпичей: ").Append(game.BricksRemaining).AppendLine();
    sb.AppendLine("<- -> — движение   Space — запуск мяча   Esc — сохранить и выйти");

    char[,] field = new char[game.FieldHeight, game.FieldWidth];
    for (int y = 0; y < game.FieldHeight; y++)
    {
        for (int x = 0; x < game.FieldWidth; x++)
            field[y, x] = ' ';
    }

    foreach (BrickState brick in game.Bricks)
    {
        for (int dx = 0; dx < Game.BrickWidth; dx++)
        {
            int x = brick.X + dx;
            if (x >= 0 && x < game.FieldWidth && brick.Y >= 0 && brick.Y < game.FieldHeight)
                field[brick.Y, x] = '#';
        }
    }

    PaddleState paddle = game.Paddle;
    int paddleRow = game.FieldHeight - Game.PaddleRowOffset;
    int paddleX = (int)Math.Round(paddle.X);
    for (int dx = 0; dx < paddle.Width; dx++)
    {
        int x = paddleX + dx;
        if (x >= 0 && x < game.FieldWidth)
            field[paddleRow, x] = '=';
    }

    BallState ball = game.Ball;
    int ballX = (int)Math.Round(ball.X);
    int ballY = (int)Math.Round(ball.Y);
    if (ballX >= 0 && ballX < game.FieldWidth && ballY >= 0 && ballY < game.FieldHeight)
        field[ballY, ballX] = 'o';

    foreach (PowerUpState powerUp in game.PowerUps)
    {
        int x = (int)Math.Round(powerUp.X);
        int y = (int)Math.Round(powerUp.Y);
        if (x < 0 || x >= game.FieldWidth || y < 0 || y >= game.FieldHeight)
            continue;

        field[y, x] = powerUp.Type switch
        {
            PowerUpType.Expand => 'E',
            PowerUpType.Slow => 'S',
            PowerUpType.ExtraLife => 'L',
            _ => '?',
        };
    }

    sb.Append('+').Append('-', game.FieldWidth).Append('+').AppendLine();
    for (int y = 0; y < game.FieldHeight; y++)
    {
        sb.Append('|');
        for (int x = 0; x < game.FieldWidth; x++)
            sb.Append(field[y, x]);
        sb.Append('|').AppendLine();
    }

    sb.Append('+').Append('-', game.FieldWidth).Append('+');

    Console.SetCursorPosition(0, 0);
    Console.Write(sb.ToString());
}
