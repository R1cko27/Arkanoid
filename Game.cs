using System.IO;

namespace Arkanoid;

// Логика консольного аналога игры Arkanoid: ракетка, мяч, кирпичи и предметы,
// выпадающие из разрушенных кирпичей.

// Мяч, ракетка и предметы хранятся в непрерывных координатах; каждый вызов
// Tick() продвигает игровое время на один шаг. Мяч считается точкой,
public sealed class Game
{
    public const int DefaultFieldWidth = 50; // Ширина игрового поля
    public const int DefaultFieldHeight = 20; // Высота игрового поля
    public const double DefaultDropChance = 0.4; // Вероятность выпадения предмета из разрушенного кирпича по умолчанию.
    public const int BrickRows = 4; // Количество рядов кирпичей.
    public const int BricksPerRow = 10; // Количество кирпичей в ряду.
    public const int BrickWidth = 4; // Ширина кирпича в клетках.
    public const int BrickGap = 1; // Зазор между соседними кирпичами в клетках.
    public const int BrickTopOffset = 1; // Отступ первого ряда кирпичей от верхней стены.
    public const int DefaultPaddleWidth = 10; // Начальная ширина ракетки.
    public const int PaddleStep = 2;  // Шаг перемещения ракетки за одно нажатие клавиши.
    public const int MaxPaddleWidth = 20; // Максимальная ширина ракетки после расширения.
    public const int ExpandAmount = 2; // На сколько клеток предмет Expand расширяет ракетку.
    public const double SlowFactor = 0.7; // Множитель скорости мяча для предмета Slow.
    public const double MinBallSpeed = 0.33; // Минимальная скорость мяча после замедления.
    public const double PowerUpFallSpeed = 0.33; // Скорость падения предметов (клеток за шаг).
    public const int PaddleRowOffset = 2; // Расстояние ряда ракетки от нижней стены поля.
    public const double DefaultBallVelocityX = 0.26; // Начальная горизонтальная скорость мяча.
    public const double DefaultBallVelocityY = -0.67; // Начальная вертикальная скорость мяча.
    public const int InitialLives = 3; // Начальное количество жизней.

    private readonly int _fieldWidth;
    private readonly int _fieldHeight;
    private readonly double _dropChance;
    private readonly Random _random;
    private readonly List<Brick> _bricks = new();
    private readonly List<PowerUp> _powerUps = new();

    private double _paddleX;
    private int _paddleWidth;
    private double _ballX;
    private double _ballY;
    private double _ballVx;
    private double _ballVy;
    private bool _ballAttached;
    private int _score;
    private int _lives;

    // fieldWidth  — ширина поля, не менее 20 клеток;
    // fieldHeight — высота поля, не менее 10 клеток;
    // dropChance  — вероятность выпадения предмета из кирпича (от 0 до 1);
    // random      — генератор случайных чисел; по умолчанию создаётся новый.
    // Кидает ArgumentOutOfRangeException, если размеры поля или вероятность вне допустимых границ.

    public Game(
        int fieldWidth = DefaultFieldWidth,
        int fieldHeight = DefaultFieldHeight,
        double dropChance = DefaultDropChance,
        Random? random = null)
    {
        if (fieldWidth < 20)
            throw new ArgumentOutOfRangeException(nameof(fieldWidth), "Ширина поля должна быть не менее 20 клеток.");
        if (fieldHeight < 10)
            throw new ArgumentOutOfRangeException(nameof(fieldHeight), "Высота поля должна быть не менее 10 клеток.");
        if (dropChance < 0.0 || dropChance > 1.0)
            throw new ArgumentOutOfRangeException(nameof(dropChance), "Вероятность должна лежать в диапазоне от 0 до 1.");

        _fieldWidth = fieldWidth;
        _fieldHeight = fieldHeight;
        _dropChance = dropChance;
        _random = random ?? new Random();
    }
    public int FieldWidth => _fieldWidth; // Ширина игрового поля.
    public int FieldHeight => _fieldHeight; // Высота игрового поля.
    public int Score => _score; // Текущий счёт.
    public int Lives => _lives; // Количество оставшихся жизней.
    public bool IsGameOver => _lives <= 0; // Игра окончена (жизни закончились).
    public bool IsWon => _bricks.Count > 0 && _bricks.All(b => b.Destroyed); // Все кирпичи разрушены — победа..
    public int BricksRemaining => _bricks.Count(b => !b.Destroyed); // Количество неразрушенных кирпичей
    public PaddleState Paddle => new() { X = _paddleX, Width = _paddleWidth }; // Текущее состояние ракетки.
    // Текущее состояние мяча.
    public BallState Ball => new()
    {
        X = _ballX,
        Y = _ballY,
        VelocityX = _ballVx,
        VelocityY = _ballVy,
        Attached = _ballAttached,
    };

    // Неразрушенные кирпичи.
    public IReadOnlyList<BrickState> Bricks =>
        _bricks.Where(b => !b.Destroyed)
               .Select(b => new BrickState { X = b.X, Y = b.Y, Points = b.Points })
               .ToList();

    // Падающие в данный момент предметы.
    public IReadOnlyList<PowerUpState> PowerUps =>
        _powerUps.Select(p => new PowerUpState { Type = p.Type, X = p.X, Y = p.Y }).ToList();

    // Номер ряда, в котором находится ракетка.
    private int PaddleRow => _fieldHeight - PaddleRowOffset;

    // Начинает новую партию: заполняет поле кирпичами, сбрасывает счёт, жизни,
    // ракетку и мяч.
    public void StartNew()
    {
        _bricks.Clear();
        _powerUps.Clear();

        for (int row = 0; row < BrickRows; row++)
        {
            for (int col = 0; col < BricksPerRow; col++)
            {
                _bricks.Add(new Brick
                {
                    X = col * (BrickWidth + BrickGap),
                    Y = BrickTopOffset + row,
                    Points = (BrickRows - row) * 10,
                });
            }
        }

        _score = 0;
        _lives = InitialLives;
        _paddleWidth = DefaultPaddleWidth;
        _paddleX = (_fieldWidth - _paddleWidth) / 2.0;
        AttachBall();
    }

    // Перемещает ракетку влево (направление -1) или вправо (направление 1).
    // Закреплённый мяч движется вместе с ракеткой.
    public void MovePaddle(int direction)
    {
        if (direction == 0)
            return;

        double step = direction > 0 ? PaddleStep : -PaddleStep;
        _paddleX = Math.Clamp(_paddleX + step, 0, _fieldWidth - _paddleWidth);

        if (_ballAttached)
            CenterBallOnPaddle();
    }

    // Запускает закреплённый на ракетке мяч.
    public void Launch()
    {
        if (_ballAttached)
        {
            _ballAttached = false;
            _ballVx = DefaultBallVelocityX;
            _ballVy = DefaultBallVelocityY;
        }
    }

    // Продвигает игру на один тик: движение мяча, отражения от стен, ракетки и кирпичей,
    // разрушение кирпичей с выпадением предметов, падение предметов и их подбор.
    public void Tick()
    {
        if (!_ballAttached)
        {
            // Движение мяча.
            _ballX += _ballVx;
            _ballY += _ballVy;

            // Отражение от левой стены
            if (_ballX < 0)
            {
                _ballX = 0;
                _ballVx = Math.Abs(_ballVx);
            }
            // Отражение от правой стены
            else if (_ballX >= _fieldWidth - 1)
            {
                _ballX = _fieldWidth - 1;
                _ballVx = -Math.Abs(_ballVx);
            }
            // Отражение от потолка
            if (_ballY < 0)
            {
                _ballY = 0;
                _ballVy = Math.Abs(_ballVy);
            }
            // Мяч упал за нижнюю границу — потеря жизни.
            if (_ballY >= _fieldHeight)
                LoseLife();

            if (!_ballAttached)
            {
                // Столкновение с кирпичами.
                foreach (Brick brick in _bricks)
                {
                    if (brick.Destroyed)
                        continue;

                    if (BallHits(brick))
                    {
                        brick.Destroyed = true;
                        _score += brick.Points;
                        _ballVy = -_ballVy;

                        if (_random.NextDouble() < _dropChance)
                        {
                            _powerUps.Add(new PowerUp
                            {
                                Type = RollPowerUpType(),
                                X = brick.X + BrickWidth / 2.0,
                                Y = brick.Y + 0.5,
                            });
                        }
                    }
                }

                // Отражение от ракетки.
                double paddleRow = PaddleRow;
                if (_ballVy > 0
                    && _ballY >= paddleRow
                    && _ballY < paddleRow + 1
                    && _ballX >= _paddleX
                    && _ballX <= _paddleX + _paddleWidth)
                {
                    _ballY = paddleRow - 1;
                    _ballVy = -Math.Abs(_ballVy);

                    double half = _paddleWidth / 2.0;
                    double relative = Math.Clamp((_ballX - (_paddleX + half)) / half, -1.0, 1.0);
                    _ballVx = relative;
                }
            }
        }

        // Падение предметов.
        foreach (PowerUp powerUp in _powerUps)
            powerUp.Y += PowerUpFallSpeed;

        // Подбор предметов ракеткой.
        for (int i = _powerUps.Count - 1; i >= 0; i--)
        {
            PowerUp powerUp = _powerUps[i];

            if (powerUp.Y >= _fieldHeight)
            {
                _powerUps.RemoveAt(i);
                continue;
            }

            double paddleRow = PaddleRow;
            if (powerUp.Y >= paddleRow
                && powerUp.Y < paddleRow + 1
                && powerUp.X >= _paddleX
                && powerUp.X <= _paddleX + _paddleWidth)
            {
                ApplyPowerUp(powerUp.Type);
                _powerUps.RemoveAt(i);
            }
        }
    }

    // Возвращает текущее состояние игры для сериализации.
    public GameState ToState() => new()
    {
        FieldWidth = _fieldWidth,
        FieldHeight = _fieldHeight,
        Score = _score,
        Lives = _lives,
        Paddle = Paddle,
        Ball = Ball,
        Bricks = Bricks.ToList(),
        PowerUps = PowerUps.ToList(),
    };

    // Восстанавливает игру из сохранённого состояния.
    // state      — сохранённое состояние;
    // dropChance — вероятность выпадения предметов для дальнейшей игры;
    // random     — генератор случайных чисел для дальнейшей игры.
    // Кидает InvalidDataException, если состояние повреждено или содержит недопустимые данные.
    public static Game FromState(GameState state, double dropChance = DefaultDropChance, Random? random = null)
    {
        ArgumentNullException.ThrowIfNull(state);

        if (state.FieldWidth < 20 || state.FieldHeight < 10)
            throw new InvalidDataException("Недопустимые размеры поля в сохранении.");
        if (state.Score < 0)
            throw new InvalidDataException("Отрицательный счёт в сохранении.");
        if (state.Lives < 0)
            throw new InvalidDataException("Отрицательное число жизней в сохранении.");
        if (state.Paddle is null || state.Ball is null || state.Bricks is null || state.PowerUps is null)
            throw new InvalidDataException("В сохранении отсутствуют обязательные данные.");

        Game game = new(state.FieldWidth, state.FieldHeight, dropChance, random);

        game._score = state.Score;
        game._lives = state.Lives;
        game._paddleWidth = Math.Clamp(state.Paddle.Width, 1, MaxPaddleWidth);
        game._paddleX = Math.Clamp(state.Paddle.X, 0, game._fieldWidth - game._paddleWidth);

        game._ballX = state.Ball.X;
        game._ballY = state.Ball.Y;
        game._ballVx = state.Ball.VelocityX;
        game._ballVy = state.Ball.VelocityY;
        game._ballAttached = state.Ball.Attached;
        if (game._ballAttached)
            game.CenterBallOnPaddle();

        foreach (BrickState brick in state.Bricks)
        {
            game._bricks.Add(new Brick { X = brick.X, Y = brick.Y, Points = brick.Points, Destroyed = false });
        }

        foreach (PowerUpState powerUp in state.PowerUps)
        {
            game._powerUps.Add(new PowerUp { Type = powerUp.Type, X = powerUp.X, Y = powerUp.Y });
        }

        return game;
    }

    // Закрепляет мяч на ракетке и задаёт ему начальную скорость.
    private void AttachBall()
    {
        _ballAttached = true;
        _ballVx = DefaultBallVelocityX;
        _ballVy = DefaultBallVelocityY;
        CenterBallOnPaddle();
    }

    // Помещает мяч над центром ракетки.
    private void CenterBallOnPaddle()
    {
        _ballX = _paddleX + _paddleWidth / 2.0;
        _ballY = PaddleRow - 1;
    }

    // Проверяет, попал ли мяч (точка) в прямоугольник кирпича.
    private bool BallHits(Brick brick) =>
        _ballX >= brick.X
        && _ballX < brick.X + BrickWidth
        && _ballY >= brick.Y
        && _ballY < brick.Y + 1;

    // Отнимает жизнь и возвращает мяч на ракетку.
    private void LoseLife()
    {
        _lives--;
        AttachBall();
    }

    // Случайно выбирает тип выпадающего предмета.
    private PowerUpType RollPowerUpType() => (PowerUpType)_random.Next(3);

    // Применяет эффект подобранного предмета.
    private void ApplyPowerUp(PowerUpType type)
    {
        switch (type)
        {
            case PowerUpType.Expand:
                _paddleWidth = Math.Min(_paddleWidth + ExpandAmount, MaxPaddleWidth);
                _paddleX = Math.Clamp(_paddleX, 0, _fieldWidth - _paddleWidth);
                if (_ballAttached)
                    CenterBallOnPaddle();
                break;

            case PowerUpType.Slow:
            {
                double speed = Math.Sqrt(_ballVx * _ballVx + _ballVy * _ballVy);
                if (speed > 0)
                {
                    double newSpeed = Math.Max(MinBallSpeed, speed * SlowFactor);
                    double factor = newSpeed / speed;
                    _ballVx *= factor;
                    _ballVy *= factor;
                }

                break;
            }

            case PowerUpType.ExtraLife:
                _lives++;
                break;
        }
    }

    // Кирпич игрового поля.
    private sealed class Brick
    {
        public int X;
        public int Y;
        public int Points;
        public bool Destroyed;
    }

    // Падающий предмет.
    private sealed class PowerUp
    {
        public PowerUpType Type;
        public double X;
        public double Y;
    }
}
