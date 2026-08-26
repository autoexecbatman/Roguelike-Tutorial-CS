/*
 * Unit tests for what the player can see. Most of these build a small map as an ASCII picture,
 * compute the field of view from a marked origin, and compare the lit cells against a second
 * picture - so a failure prints as a shape rather than a coordinate.
 *
 * Usage:  dotnet test --filter FullyQualifiedName~FieldOfViewTests
 */

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using RogueTutorial;
using SadRogue.Primitives;
using Xunit;

public sealed class FieldOfViewTests
{
    // Builds a map from rows of text: '#' is wall, anything else is floor.
    private static GameMap MapFrom(params string[] rows)
    {
        GameMap map = new GameMap(rows[0].Length, rows.Length);

        for (int row = 0; row < rows.Length; row++)
        {
            for (int col = 0; col < rows[row].Length; col++)
            {
                map.SetTile(new Point(col, row), rows[row][col] == '#' ? TileTypes.Wall : TileTypes.Floor);
            }
        }

        return map;
    }

    // Renders the lit set back to text, so an expected picture can be written by hand.
    private static string Lit(GameMap map, ISet<Point> visible)
    {
        StringBuilder picture = new StringBuilder();

        for (int row = 0; row < map.Height; row++)
        {
            if (row > 0)
            {
                picture.Append('\n');
            }

            for (int col = 0; col < map.Width; col++)
            {
                picture.Append(visible.Contains(new Point(col, row)) ? '*' : ' ');
            }
        }

        return picture.ToString();
    }

    private static string Picture(params string[] rows)
    {
        return string.Join("\n", rows);
    }

    [Fact]
    public void TheOriginIsAlwaysVisible()
    {
        // True even standing inside a wall, which happens if a later part teleports you badly.
        GameMap map = MapFrom("###", "###", "###");

        ISet<Point> visible = FieldOfView.From(new Point(1, 1), radius: 5, map);

        Assert.Contains(new Point(1, 1), visible);
    }

    [Fact]
    public void ARadiusOfZeroLightsOnlyTheOrigin()
    {
        GameMap map = MapFrom(".....", ".....", ".....");

        ISet<Point> visible = FieldOfView.From(new Point(2, 1), radius: 0, map);

        Assert.Equal(new[] { new Point(2, 1) }, visible.ToArray());
    }

    [Fact]
    public void AnOpenRoomIsLitInACircle()
    {
        // Round rather than square: the corners of the bounding box stay dark, which is what
        // stops sight reaching further along a diagonal than along an axis.
        GameMap map = MapFrom(
            ".......",
            ".......",
            ".......",
            ".......",
            ".......",
            ".......",
            ".......");

        ISet<Point> visible = FieldOfView.From(new Point(3, 3), radius: 3, map);

        Assert.Equal(
            Picture(
                "   *   ",
                " ***** ",
                " ***** ",
                "*******",
                " ***** ",
                " ***** ",
                "   *   "),
            Lit(map, visible));
    }

    [Fact]
    public void AWallCastsAShadowBehindIt()
    {
        // A single pillar directly right of the origin hides the cells behind it.
        GameMap map = MapFrom(
            ".......",
            ".......",
            "...#...",
            ".......",
            ".......");

        ISet<Point> visible = FieldOfView.From(new Point(1, 2), radius: 6, map);

        // The pillar itself is lit; what lies straight behind it is not.
        Assert.Contains(new Point(3, 2), visible);
        Assert.DoesNotContain(new Point(4, 2), visible);
        Assert.DoesNotContain(new Point(5, 2), visible);
        Assert.DoesNotContain(new Point(6, 2), visible);
    }

    [Fact]
    public void SightPassesEitherSideOfAPillar()
    {
        GameMap map = MapFrom(
            ".......",
            ".......",
            "...#...",
            ".......",
            ".......");

        ISet<Point> visible = FieldOfView.From(new Point(1, 2), radius: 6, map);

        // The shadow is a wedge, not a wall: rows above and below stay lit.
        Assert.Contains(new Point(5, 1), visible);
        Assert.Contains(new Point(5, 3), visible);
    }

    [Fact]
    public void AClosedRoomShowsItsOwnWallsAndNothingBeyond()
    {
        GameMap map = MapFrom(
            "#####",
            "#...#",
            "#...#",
            "#...#",
            "#####");

        ISet<Point> visible = FieldOfView.From(new Point(2, 2), radius: 10, map);

        // Every cell of the room, walls included, and nothing outside it - there is nothing
        // outside it here, so the real assertion is that the walls themselves are lit.
        Assert.Equal(
            Picture(
                "*****",
                "*****",
                "*****",
                "*****",
                "*****"),
            Lit(map, visible));
    }

    [Fact]
    public void LightingWallsDoesNotLeakSightAroundCorners()
    {
        // Walls are lit by touching visible floor, which must not let that lighting spread from
        // wall to wall: the far room's inner wall touches no floor the player can see.
        GameMap map = MapFrom(
            "#########",
            "#...#...#",
            "#...#...#",
            "#########");

        ISet<Point> visible = FieldOfView.From(new Point(2, 2), radius: 10, map);

        // The dividing wall is lit from this side, since floor beside it is visible.
        Assert.Contains(new Point(4, 2), visible);

        // Nothing in the far room is, floor or wall.
        Assert.DoesNotContain(new Point(6, 2), visible);
        Assert.DoesNotContain(new Point(8, 2), visible);
    }

    [Fact]
    public void OnlyWallsAreLitByAdjacency()
    {
        // Floor is lit by line of sight alone. If adjacency lit floor too, a cell behind a
        // pillar would light up because its neighbour is visible, and shadows would vanish.
        GameMap map = MapFrom(
            ".......",
            ".......",
            "...#...",
            ".......",
            ".......");

        ISet<Point> visible = FieldOfView.From(new Point(1, 2), radius: 6, map);

        // (4,2) is floor directly behind the pillar, and touches visible floor at (4,1).
        Assert.DoesNotContain(new Point(4, 2), visible);
    }

    [Fact]
    public void YouCannotSeeThroughAClosedDoorway()
    {
        // Two rooms with a solid wall between them: nothing in the far room is lit.
        GameMap map = MapFrom(
            "#######",
            "#..#..#",
            "#..#..#",
            "#######");

        ISet<Point> visible = FieldOfView.From(new Point(1, 1), radius: 10, map);

        Assert.DoesNotContain(new Point(4, 1), visible);
        Assert.DoesNotContain(new Point(5, 2), visible);
    }

    [Fact]
    public void SightReachesThroughAGapInAWall()
    {
        GameMap map = MapFrom(
            "#######",
            "#..#..#",
            "#.....#",
            "#..#..#",
            "#######");

        ISet<Point> visible = FieldOfView.From(new Point(1, 2), radius: 10, map);

        // The gap is the middle row, so the far side of it is lit.
        Assert.Contains(new Point(5, 2), visible);
    }

    [Fact]
    public void VisibilityIsSymmetric()
    {
        // The property the algorithm was chosen for. If A sees B then B must see A, or a
        // monster placed in Part 5 can shoot from a cell the player cannot see into.
        GameMap map = MapFrom(
            "..........",
            "..#....#..",
            "..........",
            "....##....",
            "..........",
            "..#....#..",
            "..........");

        const int radius = 6;

        List<Point> floorCells = new List<Point>();
        for (int row = 0; row < map.Height; row++)
        {
            for (int col = 0; col < map.Width; col++)
            {
                Point cell = new Point(col, row);
                if (map.IsWalkable(cell))
                {
                    floorCells.Add(cell);
                }
            }
        }

        // Every ordered pair of floor cells, checked both ways round.
        foreach (Point viewer in floorCells)
        {
            ISet<Point> seenByViewer = FieldOfView.From(viewer, radius, map);

            foreach (Point target in floorCells)
            {
                if (!seenByViewer.Contains(target))
                {
                    continue;
                }

                ISet<Point> seenByTarget = FieldOfView.From(target, radius, map);

                Assert.True(
                    seenByTarget.Contains(viewer),
                    $"{viewer} sees {target} but {target} does not see {viewer}");
            }
        }
    }

    [Fact]
    public void NothingBeyondTheRadiusIsLit()
    {
        GameMap map = MapFrom(
            "...........",
            "...........",
            "...........",
            "...........",
            "...........");

        ISet<Point> visible = FieldOfView.From(new Point(5, 2), radius: 3, map);

        foreach (Point cell in visible)
        {
            int deltaX = cell.X - 5;
            int deltaY = cell.Y - 2;

            Assert.True((deltaX * deltaX) + (deltaY * deltaY) <= 9, $"{cell} is outside the radius");
        }
    }

    [Fact]
    public void SightStopsAtTheEdgeOfTheMap()
    {
        // Standing in a corner: the radius runs off the map and nothing throws.
        GameMap map = MapFrom("...", "...", "...");

        ISet<Point> visible = FieldOfView.From(new Point(0, 0), radius: 10, map);

        Assert.All(visible, cell => Assert.True(map.IsInBounds(cell), $"{cell} is off the map"));
    }

    [Fact]
    public void ANullMapIsRejected()
    {
        Assert.Throws<ArgumentNullException>(() => FieldOfView.From(Point.Zero, 5, null!));
    }

    [Fact]
    public void ANegativeRadiusIsRejected()
    {
        Assert.Throws<ArgumentOutOfRangeException>(
            () => FieldOfView.From(Point.Zero, -1, new GameMap(5, 5)));
    }
}
