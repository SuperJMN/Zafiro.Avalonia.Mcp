using System.Reflection;
using AvaloniaMcp.Protocol;
using Xunit;

namespace AvaloniaMcp.Tests.Protocol;

public class ProtocolMethodsTests
{
    [Fact]
    public void AllMethodConstants_AreUnique()
    {
        var fields = typeof(ProtocolMethods)
            .GetFields(BindingFlags.Public | BindingFlags.Static)
            .Where(f => f.IsLiteral && f.FieldType == typeof(string))
            .ToList();

        var values = fields.Select(f => (string)f.GetRawConstantValue()!).ToList();
        var duplicates = values
            .GroupBy(v => v)
            .Where(g => g.Count() > 1)
            .Select(g => g.Key)
            .ToList();

        Assert.Empty(duplicates);
    }

    [Fact]
    public void AllMethodConstants_AreNonEmpty()
    {
        var fields = typeof(ProtocolMethods)
            .GetFields(BindingFlags.Public | BindingFlags.Static)
            .Where(f => f.IsLiteral && f.FieldType == typeof(string))
            .ToList();

        Assert.NotEmpty(fields);

        foreach (var field in fields)
        {
            var value = (string)field.GetRawConstantValue()!;
            Assert.False(string.IsNullOrWhiteSpace(value),
                $"ProtocolMethods.{field.Name} must not be null or whitespace");
        }
    }
}
