using System.Drawing;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace TaskbarFetch.GeometryTests;

[TestClass]
public sealed class WindowGeometryTests
{
    [TestMethod]
    public void MapRectangle_PreservesRectangle_WhenWorkAreasAreEqual()
    {
        var workArea = new Rectangle(-1920, 0, 1920, 1080);
        var window = new Rectangle(-1500, 160, 900, 700);

        var actual = WindowGeometry.MapRectangle(window, workArea, workArea);

        Assert.AreEqual(window, actual);
    }

    [TestMethod]
    public void MapRectangle_MapsPositionAndSizeProportionally()
    {
        var sourceWork = new Rectangle(0, 0, 1920, 1080);
        var targetWork = new Rectangle(1920, 0, 1280, 720);
        var window = new Rectangle(960, 540, 960, 540);

        var actual = WindowGeometry.MapRectangle(window, sourceWork, targetWork);

        Assert.AreEqual(new Rectangle(2560, 360, 640, 360), actual);
    }

    [TestMethod]
    public void MapRectangle_ClampsWindowInsideNegativeCoordinateWorkArea()
    {
        var sourceWork = new Rectangle(0, 0, 1920, 1080);
        var targetWork = new Rectangle(-1280, -100, 1280, 720);
        var window = new Rectangle(1800, 1000, 900, 600);

        var actual = WindowGeometry.MapRectangle(window, sourceWork, targetWork);

        Assert.IsTrue(targetWork.Contains(actual));
        Assert.IsTrue(actual.Width <= targetWork.Width);
        Assert.IsTrue(actual.Height <= targetWork.Height);
    }

    [TestMethod]
    public void MapRectangle_ReturnsOriginal_WhenWorkAreaHasNoSize()
    {
        var window = new Rectangle(-10, 20, 500, 400);
        var validWorkArea = new Rectangle(0, 0, 1920, 1080);

        Assert.AreEqual(window, WindowGeometry.MapRectangle(window, Rectangle.Empty, validWorkArea));
        Assert.AreEqual(window, WindowGeometry.MapRectangle(window, validWorkArea, new Rectangle(0, 0, 0, 1080)));
    }

    [TestMethod]
    public void MapRectangle_UsesMinimumSizeWithoutExceedingSmallTarget()
    {
        var sourceWork = new Rectangle(0, 0, 1920, 1080);
        var targetWork = new Rectangle(-80, 30, 90, 70);
        var tinyWindow = new Rectangle(10, 10, 1, 1);

        var actual = WindowGeometry.MapRectangle(tinyWindow, sourceWork, targetWork);

        Assert.AreEqual(90, actual.Width);
        Assert.AreEqual(70, actual.Height);
        Assert.IsTrue(targetWork.Contains(actual));
    }
}
