// Rogue-style dungeon generator.
//
// A runnable version of the algorithm described in "How did Rogue Generate its Random Dungeons?". 
// https://howtomakeanrpg.com/r/a/rogue-dungeon-generation.html
//
// To run:
// ~~~
// dotnet new console
// dotnet run
// ~~~

using System;
using System.Collections.Generic;
using System.Linq;
using static Rnd;

struct Vec2
{
    public int X;
    public int Y;

    public Vec2(int x, int y) { X = x; Y = y; }

    public static readonly Vec2 Left  = new Vec2(-1,  0);
    public static readonly Vec2 Right = new Vec2( 1,  0);
    public static readonly Vec2 Up    = new Vec2( 0, -1);
    public static readonly Vec2 Down  = new Vec2( 0,  1);
}

class Rect
{
    public int X;
    public int Y;
    public int Width;
    public int Height;

    public int Left   { get => X;                 set { Width  += X - value; X = value; } }
    public int Top    { get => Y;                 set { Height += Y - value; Y = value; } }
    public int Right  { get => X + Width  - 1;    set => Width  = value - X + 1; }
    public int Bottom { get => Y + Height - 1;    set => Height = value - Y + 1; }
}

class Room : Rect { }

class Cell : Rect
{
    public int Column;
    public int Row;
    public bool CanPlaceRoom = true;
    public Room? Room;
    public Vec2 RandomPoint;
}

class Link
{
    public Cell A, B;

    public Link(Cell a, Cell b) { A = a; B = b; }

    public (Cell, Cell) Cells => (A, B);
}

static class Rnd
{
    static System.Random _random = new System.Random();

    public static void seed(int value) { _random = new System.Random(value); }

    public static int random(int min, int max) => _random.Next(min, max);

    public static T pick<T>(IReadOnlyList<T> items) => items[_random.Next(items.Count)];

    public static T pick<T>(IEnumerable<T> items, Func<T, bool> predicate)
        => pick(items.Where(predicate).ToList());
}

class Field : Rect
{
    public const int Grid = 3;

    public int CellWidth  => Width  / Grid;
    public int CellHeight => Height / Grid;

    public readonly Cell[] Cells = new Cell[Grid * Grid];

    readonly char[,] _tiles;

    public Field(int width, int height)
    {
        X = 0;
        Y = 0;
        Width = width;
        Height = height;

        _tiles = new char[Width, Height];
        for (int y = 0; y < Height; y++)
            for (int x = 0; x < Width; x++)
                _tiles[x, y] = ' ';

        for (int i = 0; i < Cells.Length; i++)
        {
            int column = i % Grid;
            int row    = i / Grid;
            Cells[i] = new Cell
            {
                Column = column,
                Row    = row,
                X      = column * CellWidth,
                Y      = row * CellHeight,
                Width  = CellWidth,
                Height = CellHeight,
            };
        }
    }

    public void Set(int x, int y, char tile) { _tiles[x, y] = tile; }

    public void DrawRooms()
    {
        foreach (var cell in Cells)
        {
            var room = cell.Room;
            if (room == null) continue;

            for (int x = room.Left; x <= room.Right; x++)
            {
                Set(x, room.Top, '-');
                Set(x, room.Bottom, '-');
            }

            for (int y = room.Top + 1; y < room.Bottom; y++)
            {
                Set(room.Left, y, '|');
                Set(room.Right, y, '|');

                for (int x = room.Left + 1; x < room.Right; x++)
                    Set(x, y, '.');
            }
        }
    }

    public override string ToString()
    {
        var lines = new List<string>();

        for (int y = 0; y < Height; y++)
        {
            var line = new char[Width];
            for (int x = 0; x < Width; x++) line[x] = _tiles[x, y];
            lines.Add(new string(line).TrimEnd());
        }

        return string.Join(Environment.NewLine, lines);
    }
}

static class Generator
{
    static Field field = new Field(80, 24);

    public static Field Generate()
    {
        field = new Field(80, 24);

        MarkGoneCells();
        PlaceRooms();
        field.DrawRooms();

        foreach (var link in CreateCellLinks(field.Cells))
            DigCorridor(link);

        return field;
    }

    static void MarkGoneCells()
    {
        int goneCount = random(0, 3 + 1);

        for (int i = 0; i < goneCount; i++)
        {
            var cell = pick(field.Cells, x => x.CanPlaceRoom);
            cell.CanPlaceRoom = false;

            cell.RandomPoint = new Vec2(cell.Left + random(1, field.CellWidth  - 2 + 1),
                                  cell.Top  + random(1, field.CellHeight - 2 + 1));
        }
    }

    static void PlaceRooms()
    {
        for (int i = 0; i < field.Cells.Length; i++)
        {
            var cell = field.Cells[i];

            if (!cell.CanPlaceRoom)
                continue;

            var room = new Room();
            room.Width  = random(4, field.CellWidth  - 1 + 1);
            room.Height = random(4, field.CellHeight - 1 + 1);

            int slackX = field.CellWidth  - room.Width;
            int slackY = field.CellHeight - room.Height;

            int offsetX = random(0, slackX - 1 + 1);
            int offsetY = random(0, slackY - 1 + 1);

            room.X = cell.Left + 1 + offsetX;
            room.Y = cell.Top      + offsetY;

            if (room.Y == 0)
            {
                room.Y = 1;
                room.Height = Math.Min(room.Height, field.CellHeight - 2);
            }

            cell.Room = room;
        }
    }

    static List<Link> CreateCellLinks(Cell[] cellList)
    {
        var links = new List<Link>();

        var connected   = new List<Cell> { pick(cellList) };
        var unconnected = cellList.Except(connected).ToList();

        while (unconnected.Count > 0)
        {
            var cellA = pick(connected);
            var unconnectedNeighbours = Neighbours(cellA).Where(x => unconnected.Contains(x)).ToList();

            if (unconnectedNeighbours.Count > 0)
            {
                var cellB = pick(unconnectedNeighbours);
                links.Add(new Link(cellA, cellB));
                connected.Add(cellB);
                unconnected.Remove(cellB);
            }
        }

        int extraLinks = random(0, 4 + 1);
        for (int i = 0; i < extraLinks; i++)
        {
            var cellA = pick(cellList);
            var unlinkedNeighbours = Neighbours(cellA)
                .Where(x => !LinkExistsBetween(links, cellA, x)).ToList();

            if (unlinkedNeighbours.Count > 0)
                links.Add(new Link(cellA, pick(unlinkedNeighbours)));
        }

        return links;

        IEnumerable<Cell> Neighbours(Cell cell) => cellList.Where(other =>
            Math.Abs(other.Column - cell.Column) + Math.Abs(other.Row - cell.Row) == 1);
    }

    static bool LinkExistsBetween(List<Link> links, Cell a, Cell b) =>
        links.Any(link => (link.A == a && link.B == b) || (link.A == b && link.B == a));

    static void DigCorridor(Link link)
    {
        var (c1, c2) = link.Cells;

        Cell cellA, cellB;
        Vec2 start, end, digDir, turnDir;
        int  axisDistance;
        int  turnLength;

        if (c1.Y == c2.Y)
        {
            (cellA, cellB) = SortByCellX(c1, c2);
            start        = LinkPoint(cellA, Vec2.Right);
            end          = LinkPoint(cellB, Vec2.Left);
            digDir       = Vec2.Right;
            turnDir      = end.Y < start.Y ? Vec2.Up : Vec2.Down;
            axisDistance = end.X - start.X;
            turnLength   = Math.Abs(end.Y - start.Y);
        }
        else
        {
            (cellA, cellB) = SortByCellY(c1, c2);
            start        = LinkPoint(cellA, Vec2.Down);
            end          = LinkPoint(cellB, Vec2.Up);
            digDir       = Vec2.Down;
            turnDir      = end.X < start.X ? Vec2.Left : Vec2.Right;
            axisDistance = end.Y - start.Y;
            turnLength   = Math.Abs(end.X - start.X);
        }

        int outLength = random(1, axisDistance - 1 + 1);
        int inLength  = axisDistance - outLength;

        var cursor = start;
        cursor = Dig(cursor, digDir,  outLength);
        cursor = Dig(cursor, turnDir, turnLength);
        cursor = Dig(cursor, digDir,  inLength);

        PlaceDoor(cellA, start);
        PlaceDoor(cellB, end);
    }

    static (Cell, Cell) SortByCellX(Cell a, Cell b)
    {
        if (a.X < b.X) return (a, b);
        return (b, a);
    }

    static (Cell, Cell) SortByCellY(Cell a, Cell b)
    {
        if (a.Y < b.Y) return (a, b);
        return (b, a);
    }

    static Vec2 LinkPoint(Cell cell, Vec2 wall)
    {
        if (cell.Room == null)
            return cell.RandomPoint;

        var room = cell.Room;

        if (wall.X ==  1) return new Vec2(room.Right, random(room.Top + 1, room.Bottom - 1 + 1));
        if (wall.X == -1) return new Vec2(room.Left,  random(room.Top + 1, room.Bottom - 1 + 1));
        if (wall.Y ==  1) return new Vec2(random(room.Left + 1, room.Right - 1 + 1), room.Bottom);

        return new Vec2(random(room.Left + 1, room.Right - 1 + 1), room.Top);
    }

    static Vec2 Dig(Vec2 cursor, Vec2 digDir, int length)
    {
        for (int i = 0; i < length; i++)
        {
            cursor = new Vec2(cursor.X + digDir.X, cursor.Y + digDir.Y);
            field.Set(cursor.X, cursor.Y, '#');
        }

        return cursor;
    }

    static void PlaceDoor(Cell cell, Vec2 at)
    {
        if (cell.Room == null) field.Set(at.X, at.Y, '#');
        else field.Set(at.X, at.Y, '+');
    }
}

class Program
{
    static void Main(string[] args)
    {
        int value = Environment.TickCount;
        if (args.Length > 0) value = int.Parse(args[0]);

        seed(value);

        Console.WriteLine(Generator.Generate());
        Console.WriteLine();
        Console.WriteLine($"seed: {value}");
    }
}
