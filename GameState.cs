using System.Text.Json;
using System.Text.Json.Serialization;

namespace Arkanoid;

// Тип предмета, выпадающего из разрушенного кирпича.
public enum PowerUpType
{
    Expand, // Расширение ракетки на Game.ExpandAmount клеток.
    Slow, // Замедление мяча (скорость умножается на Game.SlowFactor).
    ExtraLife, // Дополнительная жизнь.
}

// Состояние ракетки: левый край и ширина в клетках.
public sealed class PaddleState
{
    // X-координата левого края ракетки.
    public double X { get; set; }
    // Ширина ракетки в клетках.
    public int Width { get; set; }
}

// Состояние мяча: положение, скорость и признак крепления к ракетке.
public sealed class BallState
{
    // X-координата мяча.
    public double X { get; set; }
    // Y-координата мяча.
    public double Y { get; set; }
    // Горизонтальная скорость (клеток за шаг).
    public double VelocityX { get; set; }
    // Вертикальная скорость (клеток за шаг).
    public double VelocityY { get; set; }
    // Мяч лежит на ракетке и ждёт запуска.
    public bool Attached { get; set; }
}

// Состояние неразрушенного кирпича: положение и количество очков за его разрушение.
public sealed class BrickState
{
    // X-координата левого края кирпича.
    public int X { get; set; }
    // Y-координата кирпича (ряд).
    public int Y { get; set; }
    // Очки, начисляемые за разрушение кирпича.
    public int Points { get; set; }
}

// Состояние падающего предмета: тип и положение.
public sealed class PowerUpState
{
    // Тип предмета.
    public PowerUpType Type { get; set; }
    // X-координата предмета.
    public double X { get; set; }
    // Y-координата предмета.
    public double Y { get; set; }
}

// Полное состояние игры, пригодное для сериализации и последующего восстановления.
public sealed class GameState
{
    // Ширина игрового поля.
    public int FieldWidth { get; set; }
    // Высота игрового поля.
    public int FieldHeight { get; set; }
    // Текущий счёт.
    public int Score { get; set; }
    // Количество оставшихся жизней.
    public int Lives { get; set; }
    // Состояние ракетки.
    public PaddleState? Paddle { get; set; }
    // Состояние мяча.
    public BallState? Ball { get; set; }
    // Неразрушенные кирпичи.
    public List<BrickState> Bricks { get; set; } = new();
    // Падающие в данный момент предметы.
    public List<PowerUpState> PowerUps { get; set; } = new();
}

// Сериализация и десериализация состояния игры в формате JSON,
// а также сохранение состояния в файл и загрузка из файла.
public static class GameStateSerializer
{
    private static readonly JsonSerializerOptions Options = new()
    {
        WriteIndented = true,
        Converters = { new JsonStringEnumConverter() },
    };

    // Сериализует состояние игры в JSON-строку.
    public static string Serialize(GameState state)
    {
        ArgumentNullException.ThrowIfNull(state);
        return JsonSerializer.Serialize(state, Options);
    }

    // Десериализует состояние игры из JSON-строки.
    // Кидает JsonException, если строка не является корректным сохранением.
    public static GameState Deserialize(string json)
    {
        ArgumentNullException.ThrowIfNull(json);
        return JsonSerializer.Deserialize<GameState>(json, Options)
            ?? throw new JsonException("Сохранение не содержит данных.");
    }

    // Сохраняет состояние игры в файл в формате JSON.
    public static void Save(GameState state, string path)
    {
        ArgumentNullException.ThrowIfNull(state);
        File.WriteAllText(path, Serialize(state));
    }

    // Загружает состояние игры из файла.
    public static GameState Load(string path) => Deserialize(File.ReadAllText(path));
}
