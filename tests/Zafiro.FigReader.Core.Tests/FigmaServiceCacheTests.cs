using Zafiro.FigReader.Core.Extraction;
using static Zafiro.FigReader.Core.Tests.SyntheticFigFile;

namespace Zafiro.FigReader.Core.Tests;

public class FigmaServiceCacheTests
{
    [Fact]
    public void Reloading_a_file_overwritten_at_the_same_path_returns_the_new_document()
    {
        using var fixture = new SyntheticFigFile([.. Roots(), Node(3, "TEXT", "First", 2)]);
        var service = new FigmaService();
        Assert.Equal(3, service.Load(fixture.Path).NodeCount);

        fixture.Overwrite([.. Roots(), Node(3, "TEXT", "Changed", 2), Node(4, "TEXT", "Added", 2)]);

        Assert.Equal(4, service.Load(fixture.Path).NodeCount);
    }

    [Fact]
    public void Calls_without_a_path_pick_up_changes_to_the_current_file()
    {
        using var fixture = new SyntheticFigFile([.. Roots(), Node(3, "TEXT", "First", 2)]);
        var service = new FigmaService();
        service.Load(fixture.Path);

        fixture.Overwrite([.. Roots(), Node(3, "TEXT", "Changed", 2), Node(4, "TEXT", "Added", 2)]);

        Assert.Equal(4, service.Resolve(path: null).NodeCount);
    }

    [Fact]
    public void Unchanged_files_are_served_from_the_cache_unless_a_reload_is_forced()
    {
        using var fixture = new SyntheticFigFile([.. Roots()]);
        var service = new FigmaService();
        var first = service.Load(fixture.Path);

        Assert.Same(first, service.Load(fixture.Path));
        Assert.NotSame(first, service.Load(fixture.Path, forceReload: true));
    }
}
