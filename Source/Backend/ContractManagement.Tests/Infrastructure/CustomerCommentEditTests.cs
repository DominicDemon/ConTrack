using ContractManagement.Application.Features.Internal;
using ContractManagement.Domain.Entities.Master;
using ContractManagement.Domain.Internal;
using ContractManagement.Infrastructure.Persistence.Master;
using ContractManagement.Infrastructure.Sms;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace ContractManagement.Tests.Infrastructure;

/// <summary>The customer may change their own comment on an open review until the staff answer or handle it.</summary>
public sealed class CustomerCommentEditTests
{
    private static ContractNegotiationComment Comment(long id, long? author, int? parent = null, int? carriedFrom = null) => new()
    {
        ContractNegotiationCommentId = id, ContractDocumentId = 1, ContractVersionId = 10, ContractReviewRequestId = 20, AuthorCustomerId = author,
        ContractNegotiationCommentParentcommentid = parent, ContractNegotiationCommentCarriedforwardfromcommentid = carriedFrom,
        ContractNegotiationCommentContent = "Góp ý " + id, ContractNegotiationCommentSource = "ExternalFeedback", CreatedAt = DateTime.UtcNow, Rowversion = [1]
    };

    [Fact]
    public async Task EditsAndDeletesOnlyOwnUnansweredComments()
    {
        await using var db = new MasterDbContext(new DbContextOptionsBuilder<MasterDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);
        db.ContractReviewRequests.Add(new ContractReviewRequest { ContractReviewRequestId = 20, ContractDocumentId = 1, ContractVersionId = 10, CustomerId = 3,
            ContractReviewRequestStatus = ContractReviewStatus.Sent, ContractReviewRequestDocumentHash = "h", Rowversion = [1] });
        db.ContractNegotiationComments.AddRange(Comment(1, 3), Comment(2, 3), Comment(3, null, parent: 2), Comment(4, 3, carriedFrom: 99));
        await db.SaveChangesAsync();
        var portal = new CustomerPortalRepository(db, Options.Create(new EsmsOptions()));

        var edited = await portal.EditComment(3, 20, 1, new(null, null, "Sửa đơn giá", "ITEMS"), default);
        Assert.Equal(("Sửa đơn giá", "ITEMS"), (edited.Content, edited.Section));
        // Answered by the staff, carried over from the previous version, or someone else's review: not changeable.
        Assert.Equal(409, (await Assert.ThrowsAsync<InternalAppException>(() => portal.EditComment(3, 20, 2, new(null, null, "x"), default))).Status);
        Assert.Equal(404, (await Assert.ThrowsAsync<InternalAppException>(() => portal.DeleteComment(3, 20, 4, default))).Status);
        Assert.Equal(404, (await Assert.ThrowsAsync<InternalAppException>(() => portal.DeleteComment(8, 20, 1, default))).Status);

        await portal.DeleteComment(3, 20, 1, default);
        Assert.False(await db.ContractNegotiationComments.AnyAsync(c => c.ContractNegotiationCommentId == 1));
    }
}
