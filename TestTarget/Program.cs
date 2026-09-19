using System.Runtime.InteropServices;

Console.Title = "MemoryLab Test Target";

var player = new PlayerState
{
    Health = 100,
    Speed = 12.5f
};

var holder = new PlayerHolder
{
    Player = player
};

var root = new GameRoot
{
    Holder = holder
};

Console.WriteLine("MemoryLab Test Target");
Console.WriteLine("---------------------");
Console.WriteLine("Target aman untuk menguji scanner, editor, memory viewer, dan pointer tools.");
Console.WriteLine();

while (true)
{
    Console.WriteLine($"Health = {root.Holder.Player.Health}");
    Console.WriteLine($"Speed  = {root.Holder.Player.Speed}");
    Console.WriteLine();
    Console.WriteLine("1 = damage 10");
    Console.WriteLine("2 = heal 25");
    Console.WriteLine("3 = speed + 1");
    Console.WriteLine("4 = set HP 100");
    Console.WriteLine("Q = quit");
    Console.Write("> ");

    var key = Console.ReadKey(true);

    if (key.Key == ConsoleKey.Q)
        break;

    switch (key.Key)
    {
        case ConsoleKey.D1:
        case ConsoleKey.NumPad1:
            root.Holder.Player.Health -= 10;
            break;

        case ConsoleKey.D2:
        case ConsoleKey.NumPad2:
            root.Holder.Player.Health += 25;
            break;

        case ConsoleKey.D3:
        case ConsoleKey.NumPad3:
            root.Holder.Player.Speed += 1.0f;
            break;

        case ConsoleKey.D4:
        case ConsoleKey.NumPad4:
            root.Holder.Player.Health = 100;
            break;
    }

    Console.Clear();
}

GC.KeepAlive(root);

sealed class GameRoot
{
    public PlayerHolder Holder = new();
}

sealed class PlayerHolder
{
    public PlayerState Player = new();
}

sealed class PlayerState
{
    public int Health = 100;
    public float Speed = 12.5f;
}
