using System.Text.Json;
using ContractManagement.Domain.Internal;

namespace ContractManagement.Tests.Domain;

public class PartyTypeFilterTests
{
    [Fact]
    public void MarkerMatchesStoredSnapshotOfThatPartyOnly()
    {
        var web = new JsonSerializerOptions(JsonSerializerDefaults.Web);
        var business = JsonSerializer.Serialize(new DraftContent { PartyType = InternalDraftPolicy.Business }, web);
        var person = JsonSerializer.Serialize(new DraftContent { PartyType = InternalDraftPolicy.Person }, web);
        Assert.Contains(InternalDraftPolicy.PartyTypeMarker("BUSINESS")!, business);
        Assert.DoesNotContain(InternalDraftPolicy.PartyTypeMarker("BUSINESS")!, person);
        Assert.Contains(InternalDraftPolicy.PartyTypeMarker("PERSON")!, person);
        Assert.Null(InternalDraftPolicy.PartyTypeMarker(""));
        Assert.Throws<ArgumentException>(() => InternalDraftPolicy.PartyTypeMarker("OTHER"));
    }
}
