using Microsoft.EntityFrameworkCore;
using Volo.Abp;
using Volo.Abp.EntityFrameworkCore.Modeling;

public static class SmartChangeOverDbContextModelCreatingExtensions
{
    public static void ConfigureDemo(
        this ModelBuilder builder)
    {
        Check.NotNull(builder, nameof(builder));

    }
}
