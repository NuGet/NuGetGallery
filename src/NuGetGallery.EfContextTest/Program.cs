using Microsoft.EntityFrameworkCore;
using NuGet.Services.Entities;
using NuGetGallery;

internal class Program
{
    private static async Task Main(string[] args)
    {
        var optionsBuilder = new DbContextOptionsBuilder<NuGetGalleryContext>();
        optionsBuilder.UseSqlServer(@"Data Source=(localdb)\MSSQLLocalDB;Initial Catalog=NuGetGallery");
        using var ctx = new NuGetGalleryContext(optionsBuilder.Options);

        var packageRegistrations = await ctx.PackageRegistrations.ToListAsync();
        Console.WriteLine(string.Join("\n", packageRegistrations?.Select(pr => $"{pr.Id}") ?? new string[0]));

        var packages = await ctx.Packages.ToListAsync();
        Console.WriteLine(string.Join("\n", packages?.Select(p => $"  {p.Version}") ?? new string[0]));
    }
}