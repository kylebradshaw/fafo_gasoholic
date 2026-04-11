using Microsoft.EntityFrameworkCore;

public class NamingConventionTests
{
    // Guard rail for the rule in NAMING.md: every Cosmos document property
    // must be camelCase. Catches a future entity or property that forgets to
    // opt into the convention, and keeps hand-written Cosmos SQL honest.
    [Fact]
    public void AllCosmosJsonPropertyNames_AreCamelCase()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseCosmos(
                "AccountEndpoint=https://localhost:8081/;AccountKey=dGVzdA==;",
                databaseName: "naming-convention-test")
            .Options;

        using var db = new AppDbContext(options);

        var violations = new List<string>();
        foreach (var entity in db.Model.GetEntityTypes())
        {
            foreach (var prop in entity.GetProperties())
            {
                if (prop.IsShadowProperty()) continue;
                var jsonName = prop.GetJsonPropertyName();
                if (string.IsNullOrEmpty(jsonName)) continue;
                // `id`, `_etag`, `_ts`, `__type`, etc. are Cosmos reserved system fields.
                if (jsonName == "id" || jsonName.StartsWith('_')) continue;
                if (char.IsUpper(jsonName[0]))
                    violations.Add($"{entity.DisplayName()}.{prop.Name} serializes as \"{jsonName}\" (expected camelCase)");
            }
        }

        Assert.True(
            violations.Count == 0,
            "Cosmos JSON property names must be camelCase. See NAMING.md.\n"
            + string.Join("\n", violations));
    }

    [Fact]
    public void PartitionKeyFields_MatchBicepPaths()
    {
        // The partition-key JSON path on each container MUST match the path
        // declared in infra/main.bicep. When you add a new entity, add an
        // entry here AND update the bicep at the same time.
        //
        // Check by verifying the entity has a property whose CLR name is
        // expected.Pascal and whose JSON name is expected.camel. Avoids
        // depending on EF Core's partition-key metadata API (which has
        // shifted between EF versions).
        var expected = new (string Entity, string ClrProp, string JsonProp)[]
        {
            ("User",              "Id",     "id"),      // bicep: containerUsers              /id
            ("Auto",              "UserId", "userId"),  // bicep: containerAutos              /userId
            ("Fillup",            "AutoId", "autoId"),  // bicep: containerFillups            /autoId
            ("MaintenanceRecord", "AutoId", "autoId"),  // bicep: containerMaintenance        /autoId
            ("VerificationToken", "UserId", "userId"),  // bicep: containerVerificationTokens /userId
        };

        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseCosmos(
                "AccountEndpoint=https://localhost:8081/;AccountKey=dGVzdA==;",
                databaseName: "naming-convention-test")
            .Options;

        using var db = new AppDbContext(options);

        foreach (var (entityName, clrProp, jsonProp) in expected)
        {
            var entity = db.Model.GetEntityTypes().FirstOrDefault(e => e.ClrType.Name == entityName);
            Assert.NotNull(entity);
            var prop = entity!.FindProperty(clrProp);
            Assert.NotNull(prop);
            Assert.Equal(jsonProp, prop!.GetJsonPropertyName());
        }
    }
}
