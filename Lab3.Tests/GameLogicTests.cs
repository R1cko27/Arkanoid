using System.IO;
using System.Text.Json;
using Arkanoid;

namespace Arkanoid.Tests;

[TestClass]
public sealed class GameTests
{
    // Базовое состояние: мяч закреплён на ракетке, кирпичей и предметов нет.
    private static GameState CreateBaseState() => new()
    {
        FieldWidth = Game.DefaultFieldWidth,
        FieldHeight = Game.DefaultFieldHeight,
        Score = 0,
        Lives = Game.InitialLives,
        Paddle = new PaddleState { X = 20, Width = Game.DefaultPaddleWidth },
        Ball = new BallState
        {
            X = 24.5,
            Y = 17,
            VelocityX = Game.DefaultBallVelocityX,
            VelocityY = Game.DefaultBallVelocityY,
            Attached = true,
        },
        Bricks = new List<BrickState>(),
        PowerUps = new List<PowerUpState>(),
    };

    // Создаёт игру из состояния с заданной вероятностью выпадения предметов.
    private static Game CreateGame(GameState state, double dropChance = 0.0, Random? random = null) =>
        Game.FromState(state, dropChance, random ?? new Random(1));


    // Новая партия: счёт 0, жизни исходные, все кирпичи на месте, мяч закреплён.
    [TestMethod]
    public void StartNew_InitializesField()
    {
        Game game = new();
        game.StartNew();

        Assert.AreEqual(0, game.Score);
        Assert.AreEqual(Game.InitialLives, game.Lives);
        Assert.AreEqual(Game.BrickRows * Game.BricksPerRow, game.BricksRemaining);
        Assert.IsTrue(game.Ball.Attached);
        Assert.IsFalse(game.IsGameOver);
        Assert.IsFalse(game.IsWon);
        Assert.IsEmpty(game.PowerUps);
    }

    // Закреплённый мяч не двигается при тике.
    [TestMethod]
    public void Tick_WhileBallAttached_DoesNotMoveBall()
    {
        Game game = CreateGame(CreateBaseState());

        game.Tick();

        Assert.IsTrue(game.Ball.Attached);
        Assert.AreEqual(25.0, game.Ball.X, 1e-9);
        Assert.AreEqual(17.0, game.Ball.Y, 1e-9);
    }

    // Свободный мяч смещается на свою скорость за тик.
    [TestMethod]
    public void Tick_MovesDetachedBall()
    {
        GameState state = CreateBaseState();
        state.Ball = new BallState { X = 24.5, Y = 17, VelocityX = Game.DefaultBallVelocityX, VelocityY = Game.DefaultBallVelocityY, Attached = false };
        Game game = CreateGame(state);

        game.Tick();

        Assert.AreEqual(24.76, game.Ball.X, 1e-9);
        Assert.AreEqual(16.33, game.Ball.Y, 1e-9);
    }

    // Отскок от левой стены: X прижимается к 0, Vx становится положительной.
    [TestMethod]
    public void Ball_BouncesOffLeftWall()
    {
        GameState state = CreateBaseState();
        state.Ball = new BallState { X = 0, Y = 5, VelocityX = -1, VelocityY = 0.5, Attached = false };
        Game game = CreateGame(state);

        game.Tick();

        Assert.AreEqual(0.0, game.Ball.X, 1e-9);
        Assert.AreEqual(1.0, game.Ball.VelocityX, 1e-9);
    }

    // Отскок от правой стены: X прижимается к последней клетке, Vx становится отрицательной.
    [TestMethod]
    public void Ball_BouncesOffRightWall()
    {
        GameState state = CreateBaseState();
        state.Ball = new BallState { X = 48.5, Y = 5, VelocityX = 1, VelocityY = 0.5, Attached = false };
        Game game = CreateGame(state);

        game.Tick();

        Assert.AreEqual(49.0, game.Ball.X, 1e-9);
        Assert.AreEqual(-1.0, game.Ball.VelocityX, 1e-9);
    }

    // Отскок от верхней стены: Y прижимается к 0, Vy становится положительной.
    [TestMethod]
    public void Ball_BouncesOffTopWall()
    {
        GameState state = CreateBaseState();
        state.Ball = new BallState { X = 25, Y = 0, VelocityX = 0, VelocityY = -1, Attached = false };
        Game game = CreateGame(state);

        game.Tick();

        Assert.AreEqual(0.0, game.Ball.Y, 1e-9);
        Assert.AreEqual(1.0, game.Ball.VelocityY, 1e-9);
    }

    // Ракетка не выходит за границы поля, а закреплённый мяч следует за ней.
    [TestMethod]
    public void MovePaddle_ClampsToFieldBounds()
    {
        Game game = CreateGame(CreateBaseState());

        for (int i = 0; i < 30; i++)
            game.MovePaddle(1);

        Assert.AreEqual(Game.DefaultFieldWidth - Game.DefaultPaddleWidth, game.Paddle.X, 1e-9);

        for (int i = 0; i < 30; i++)
            game.MovePaddle(-1);

        Assert.AreEqual(0.0, game.Paddle.X, 1e-9);
        Assert.AreEqual(5.0, game.Ball.X, 1e-9);
    }

    // Закреплённый мяч движется вместе с ракеткой при перемещении.
    [TestMethod]
    public void MovePaddle_MovesAttachedBall()
    {
        Game game = CreateGame(CreateBaseState());

        game.MovePaddle(1);

        Assert.AreEqual(22.0, game.Paddle.X, 1e-9);
        Assert.AreEqual(27.0, game.Ball.X, 1e-9);
    }

    // Запуск мяча снимает крепление и задаёт начальную скорость.
    [TestMethod]
    public void Launch_ReleasesAttachedBall()
    {
        Game game = CreateGame(CreateBaseState());

        game.Launch();

        Assert.IsFalse(game.Ball.Attached);
        Assert.AreEqual(Game.DefaultBallVelocityX, game.Ball.VelocityX, 1e-9);
        Assert.AreEqual(Game.DefaultBallVelocityY, game.Ball.VelocityY, 1e-9);
    }

    // Разрушение кирпича начисляет очки, мяч отражается, партия выигрывается.
    [TestMethod]
    public void BallHitsBrick_DestroysBrickAndIncreasesScore()
    {
        GameState state = CreateBaseState();
        state.Bricks.Add(new BrickState { X = 0, Y = 3, Points = 40 });
        state.Ball = new BallState { X = 2, Y = 4, VelocityX = 0, VelocityY = -1, Attached = false };
        Game game = CreateGame(state);

        game.Tick();

        Assert.AreEqual(40, game.Score);
        Assert.AreEqual(0, game.BricksRemaining);
        Assert.IsTrue(game.IsWon);
        Assert.AreEqual(1.0, game.Ball.VelocityY, 1e-9);
    }

    // Отскок от ракетки: Vy вверх, Vx зависит от точки попадания (край — вправо).
    [TestMethod]
    public void Ball_BouncesOffPaddleWithDirectionByHitPosition()
    {
        GameState state = CreateBaseState();
        state.Ball = new BallState { X = 29, Y = 17.5, VelocityX = 0, VelocityY = 1, Attached = false };
        Game game = CreateGame(state);

        game.Tick();

        Assert.AreEqual(17.0, game.Ball.Y, 1e-9);
        Assert.AreEqual(-1.0, game.Ball.VelocityY, 1e-9);
        Assert.AreEqual(0.8, game.Ball.VelocityX, 1e-9);
    }

    // Мяч мимо ракетки продолжает падать без изменения скорости.
    [TestMethod]
    public void Ball_MissesPaddle_ContinuesFalling()
    {
        GameState state = CreateBaseState();
        state.Ball = new BallState { X = 30.5, Y = 17.5, VelocityX = 0, VelocityY = 1, Attached = false };
        Game game = CreateGame(state);

        game.Tick();

        Assert.AreEqual(18.5, game.Ball.Y, 1e-9);
        Assert.AreEqual(1.0, game.Ball.VelocityY, 1e-9);
    }

    // Падение мяча за нижнюю границу отнимает жизнь и возвращает мяч на ракетку.
    [TestMethod]
    public void BallFallsBelowField_LosesLifeAndReturnsToPaddle()
    {
        GameState state = CreateBaseState();
        state.Lives = 2;
        state.Ball = new BallState { X = 25, Y = 19, VelocityX = 0, VelocityY = 1, Attached = false };
        Game game = CreateGame(state);

        game.Tick();

        Assert.AreEqual(1, game.Lives);
        Assert.IsTrue(game.Ball.Attached);
        Assert.AreEqual(17.0, game.Ball.Y, 1e-9);
    }

    // Потеря последней жизни завершает игру.
    [TestMethod]
    public void GameOver_WhenLastLifeLost()
    {
        GameState state = CreateBaseState();
        state.Lives = 1;
        state.Ball = new BallState { X = 25, Y = 19, VelocityX = 0, VelocityY = 1, Attached = false };
        Game game = CreateGame(state);

        game.Tick();

        Assert.AreEqual(0, game.Lives);
        Assert.IsTrue(game.IsGameOver);
    }


    // При вероятности 1 предмет выпадает из центра разрушенного кирпича.
    [TestMethod]
    public void BrickDestroyed_DropsPowerUp_WhenDropChanceIsOne()
    {
        GameState state = CreateBaseState();
        state.Bricks.Add(new BrickState { X = 0, Y = 3, Points = 40 });
        state.Ball = new BallState { X = 2, Y = 4, VelocityX = 0, VelocityY = -1, Attached = false };
        Game game = Game.FromState(state, dropChance: 1.0, random: new Random(42));

        game.Tick();

        Assert.HasCount(1, game.PowerUps);
        PowerUpState powerUp = game.PowerUps[0];
        Assert.IsTrue(Enum.IsDefined(powerUp.Type));
        Assert.AreEqual(2.0, powerUp.X, 1e-9);
        Assert.AreEqual(3.83, powerUp.Y, 1e-9); // 3.5 + скорость падения за тик.
    }

    // При вероятности 0 предмет не выпадает.
    [TestMethod]
    public void BrickDestroyed_DoesNotDropPowerUp_WhenDropChanceIsZero()
    {
        GameState state = CreateBaseState();
        state.Bricks.Add(new BrickState { X = 0, Y = 3, Points = 40 });
        state.Ball = new BallState { X = 2, Y = 4, VelocityX = 0, VelocityY = -1, Attached = false };
        Game game = Game.FromState(state, dropChance: 0.0, random: new Random(42));

        game.Tick();

        Assert.IsEmpty(game.PowerUps);
    }

    // Предмет каждый тик опускается вниз на скорость падения.
    [TestMethod]
    public void PowerUp_FallsEveryTick()
    {
        GameState state = CreateBaseState();
        state.PowerUps.Add(new PowerUpState { Type = PowerUpType.Expand, X = 10, Y = 10 });
        Game game = Game.FromState(state);

        game.Tick();

        Assert.HasCount(1, game.PowerUps);
        Assert.AreEqual(10.33, game.PowerUps[0].Y, 1e-9);
    }

    // Подбор Expand расширяет ракетку и убирает предмет.
    [TestMethod]
    public void PowerUp_CaughtByPaddle_Expand_WidensPaddle()
    {
        GameState state = CreateBaseState();
        state.PowerUps.Add(new PowerUpState { Type = PowerUpType.Expand, X = 24.5, Y = 17.7 });
        Game game = Game.FromState(state);

        game.Tick();

        Assert.IsEmpty(game.PowerUps);
        Assert.AreEqual(Game.DefaultPaddleWidth + Game.ExpandAmount, game.Paddle.Width);
    }

    // Расширение у правого края сдвигает ракетку внутрь поля.
    [TestMethod]
    public void PowerUp_CaughtByPaddle_Expand_ClampsPaddleToField()
    {
        GameState state = CreateBaseState();
        state.Paddle = new PaddleState { X = Game.DefaultFieldWidth - Game.DefaultPaddleWidth, Width = Game.DefaultPaddleWidth };
        state.PowerUps.Add(new PowerUpState { Type = PowerUpType.Expand, X = 45.5, Y = 17.7 });
        Game game = Game.FromState(state);

        game.Tick();

        Assert.AreEqual(Game.DefaultPaddleWidth + Game.ExpandAmount, game.Paddle.Width);
        Assert.AreEqual(Game.DefaultFieldWidth - game.Paddle.Width, game.Paddle.X, 1e-9);
    }

    // Ширина ракетки не превышает MaxPaddleWidth.
    [TestMethod]
    public void PowerUp_CaughtByPaddle_Expand_DoesNotExceedMaxWidth()
    {
        GameState state = CreateBaseState();
        state.Paddle = new PaddleState { X = 10, Width = Game.MaxPaddleWidth };
        state.PowerUps.Add(new PowerUpState { Type = PowerUpType.Expand, X = 20, Y = 17.7 });
        Game game = Game.FromState(state);

        game.Tick();

        Assert.AreEqual(Game.MaxPaddleWidth, game.Paddle.Width);
    }

    // Подбор ExtraLife увеличивает число жизней на 1.
    [TestMethod]
    public void PowerUp_CaughtByPaddle_ExtraLife_AddsLife()
    {
        GameState state = CreateBaseState();
        state.PowerUps.Add(new PowerUpState { Type = PowerUpType.ExtraLife, X = 24.5, Y = 17.7 });
        Game game = Game.FromState(state);

        game.Tick();

        Assert.IsEmpty(game.PowerUps);
        Assert.AreEqual(Game.InitialLives + 1, game.Lives);
    }

    // Подбор Slow уменьшает обе компоненты скорости с коэффициентом SlowFactor.
    [TestMethod]
    public void PowerUp_CaughtByPaddle_Slow_ReducesBallSpeed()
    {
        GameState state = CreateBaseState();
        state.Ball = new BallState { X = 25, Y = 10, VelocityX = 0.4, VelocityY = -1, Attached = false };
        state.PowerUps.Add(new PowerUpState { Type = PowerUpType.Slow, X = 24.5, Y = 17.7 });
        Game game = Game.FromState(state);

        game.Tick();

        Assert.IsEmpty(game.PowerUps);
        Assert.AreEqual(0.28, game.Ball.VelocityX, 1e-9);
        Assert.AreEqual(-0.7, game.Ball.VelocityY, 1e-9);
    }

    // Slow не опускает скорость мяча ниже MinBallSpeed.
    [TestMethod]
    public void PowerUp_Slow_DoesNotReduceSpeedBelowMinimum()
    {
        GameState state = CreateBaseState();
        state.Ball = new BallState { X = 25, Y = 10, VelocityX = 0.2, VelocityY = -0.2, Attached = false };
        state.PowerUps.Add(new PowerUpState { Type = PowerUpType.Slow, X = 24.5, Y = 17.7 });
        Game game = Game.FromState(state);

        game.Tick();

        double speed = Math.Sqrt(game.Ball.VelocityX * game.Ball.VelocityX
                                 + game.Ball.VelocityY * game.Ball.VelocityY);
        Assert.AreEqual(Game.MinBallSpeed, speed, 1e-9);
    }

    // Предмет, ушедший ниже поля, удаляется из списка.
    [TestMethod]
    public void PowerUp_FallsBelowField_IsRemoved()
    {
        GameState state = CreateBaseState();
        state.PowerUps.Add(new PowerUpState { Type = PowerUpType.Expand, X = 10, Y = 19.7 });
        Game game = Game.FromState(state);

        game.Tick();

        Assert.IsEmpty(game.PowerUps);
    }

    // Предмет мимо ракетки продолжает падать и остаётся в списке.
    [TestMethod]
    public void PowerUp_MissesPaddle_KeepsFalling()
    {
        GameState state = CreateBaseState();
        state.PowerUps.Add(new PowerUpState { Type = PowerUpType.Expand, X = 10, Y = 17.7 });
        Game game = Game.FromState(state);

        game.Tick();

        Assert.HasCount(1, game.PowerUps);
        Assert.AreEqual(18.03, game.PowerUps[0].Y, 1e-9);
    }

    // Сериализация и десериализация сохраняют состояние (JSON совпадает, счёт/жизни/списки совпадают).
    [TestMethod]
    public void RoundTrip_SerializeDeserialize_PreservesState()
    {
        Game game = new(random: new Random(42));
        game.StartNew();

        for (int i = 0; i < 80; i++)
        {
            if (i == 5)
                game.Launch();
            if (i % 7 == 0)
                game.MovePaddle(1);
            game.Tick();
        }

        GameState state = game.ToState();
        string json = GameStateSerializer.Serialize(state);
        GameState restored = GameStateSerializer.Deserialize(json);

        Assert.AreEqual(json, GameStateSerializer.Serialize(restored));
        Assert.AreEqual(game.Score, restored.Score);
        Assert.AreEqual(game.Lives, restored.Lives);
        Assert.HasCount(game.BricksRemaining, restored.Bricks);
        Assert.HasCount(game.PowerUps.Count, restored.PowerUps);
    }

    // Типы предметов пишутся в JSON строками и корректно читаются обратно.
    [TestMethod]
    public void Serialize_WritesPowerUpTypesAsStrings()
    {
        GameState state = new()
        {
            FieldWidth = Game.DefaultFieldWidth,
            FieldHeight = Game.DefaultFieldHeight,
            Lives = 3,
            Paddle = new PaddleState { X = 20, Width = Game.DefaultPaddleWidth },
            Ball = new BallState { X = 24.5, Y = 17, VelocityX = 0.4, VelocityY = -1, Attached = true },
        };
        state.PowerUps.Add(new PowerUpState { Type = PowerUpType.Expand, X = 1, Y = 2 });
        state.PowerUps.Add(new PowerUpState { Type = PowerUpType.Slow, X = 3, Y = 4 });
        state.PowerUps.Add(new PowerUpState { Type = PowerUpType.ExtraLife, X = 5, Y = 6 });

        string json = GameStateSerializer.Serialize(state);
        GameState restored = GameStateSerializer.Deserialize(json);

        StringAssert.Contains(json, "\"Expand\"");
        Assert.AreEqual(PowerUpType.Expand, restored.PowerUps[0].Type);
        Assert.AreEqual(PowerUpType.Slow, restored.PowerUps[1].Type);
        Assert.AreEqual(PowerUpType.ExtraLife, restored.PowerUps[2].Type);
    }

    // FromState восстанавливает все поля состояния без искажений.
    [TestMethod]
    public void FromState_RestoresExactState()
    {
        GameState state = new()
        {
            FieldWidth = Game.DefaultFieldWidth,
            FieldHeight = Game.DefaultFieldHeight,
            Score = 123,
            Lives = 2,
            Paddle = new PaddleState { X = 30, Width = 11 },
            Ball = new BallState { X = 33.2, Y = 12.1, VelocityX = -0.5, VelocityY = 0.7, Attached = false },
        };
        state.Bricks.Add(new BrickState { X = 0, Y = 1, Points = 40 });
        state.Bricks.Add(new BrickState { X = 45, Y = 4, Points = 10 });
        state.PowerUps.Add(new PowerUpState { Type = PowerUpType.ExtraLife, X = 10.5, Y = 8.25 });

        Game game = Game.FromState(state);

        Assert.AreEqual(123, game.Score);
        Assert.AreEqual(2, game.Lives);
        Assert.AreEqual(30.0, game.Paddle.X, 1e-9);
        Assert.AreEqual(11, game.Paddle.Width);
        Assert.AreEqual(33.2, game.Ball.X, 1e-9);
        Assert.AreEqual(12.1, game.Ball.Y, 1e-9);
        Assert.AreEqual(-0.5, game.Ball.VelocityX, 1e-9);
        Assert.AreEqual(0.7, game.Ball.VelocityY, 1e-9);
        Assert.IsFalse(game.Ball.Attached);
        Assert.AreEqual(2, game.BricksRemaining);
        Assert.HasCount(1, game.PowerUps);
        Assert.AreEqual(PowerUpType.ExtraLife, game.PowerUps[0].Type);
        Assert.AreEqual(10.5, game.PowerUps[0].X, 1e-9);
    }

    // При загрузке закреплённый мяч центрируется над ракеткой.
    [TestMethod]
    public void FromState_AttachedBall_IsCenteredOnPaddle()
    {
        GameState state = new()
        {
            FieldWidth = Game.DefaultFieldWidth,
            FieldHeight = Game.DefaultFieldHeight,
            Lives = 3,
            Paddle = new PaddleState { X = 10, Width = Game.DefaultPaddleWidth },
            Ball = new BallState { X = 0, Y = 0, VelocityX = 0.4, VelocityY = -1, Attached = true },
        };

        Game game = Game.FromState(state);

        Assert.IsTrue(game.Ball.Attached);
        Assert.AreEqual(15.0, game.Ball.X, 1e-9); // Центр ракетки: 10 + 10 / 2.
        Assert.AreEqual(17.0, game.Ball.Y, 1e-9);
    }

    // Недопустимая ширина поля в сохранении приводит к InvalidDataException.
    [TestMethod]
    public void FromState_InvalidFieldSize_Throws()
    {
        GameState state = new()
        {
            FieldWidth = 5,
            FieldHeight = Game.DefaultFieldHeight,
            Lives = 3,
            Paddle = new PaddleState { X = 0, Width = Game.DefaultPaddleWidth },
            Ball = new BallState { X = 0, Y = 0, VelocityX = 0.4, VelocityY = -1, Attached = true },
        };

        Assert.ThrowsExactly<InvalidDataException>(() => Game.FromState(state));
    }

    // Отсутствие ракетки в сохранении приводит к InvalidDataException.
    [TestMethod]
    public void FromState_MissingPaddle_Throws()
    {
        GameState state = new()
        {
            FieldWidth = Game.DefaultFieldWidth,
            FieldHeight = Game.DefaultFieldHeight,
            Lives = 3,
            Ball = new BallState { X = 0, Y = 0, VelocityX = 0.4, VelocityY = -1, Attached = true },
        };

        Assert.ThrowsExactly<InvalidDataException>(() => Game.FromState(state));
    }

    // Отрицательный счёт в сохранении приводит к InvalidDataException.
    [TestMethod]
    public void FromState_NegativeScore_Throws()
    {
        GameState state = new()
        {
            FieldWidth = Game.DefaultFieldWidth,
            FieldHeight = Game.DefaultFieldHeight,
            Score = -5,
            Lives = 3,
            Paddle = new PaddleState { X = 0, Width = Game.DefaultPaddleWidth },
            Ball = new BallState { X = 0, Y = 0, VelocityX = 0.4, VelocityY = -1, Attached = true },
        };

        Assert.ThrowsExactly<InvalidDataException>(() => Game.FromState(state));
    }

    // Повреждённый JSON вызывает JsonException.
    [TestMethod]
    public void Deserialize_CorruptedJson_Throws()
    {
        Assert.ThrowsExactly<JsonException>(() => GameStateSerializer.Deserialize("{ не json"));
    }

    // Строка "null" не является корректным сохранением и вызывает JsonException.
    [TestMethod]
    public void Deserialize_NullJson_Throws()
    {
        Assert.ThrowsExactly<JsonException>(() => GameStateSerializer.Deserialize("null"));
    }

    // Передача null в Serialize вызывает ArgumentNullException.
    [TestMethod]
    public void Serialize_NullState_Throws()
    {
        Assert.ThrowsExactly<ArgumentNullException>(() => GameStateSerializer.Serialize(null!));
    }

    // Передача null в Deserialize вызывает ArgumentNullException.
    [TestMethod]
    public void Deserialize_NullJsonArgument_Throws()
    {
        Assert.ThrowsExactly<ArgumentNullException>(() => GameStateSerializer.Deserialize(null!));
    }
}
