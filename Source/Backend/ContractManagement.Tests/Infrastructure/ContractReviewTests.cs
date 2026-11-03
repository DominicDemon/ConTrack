using System.Net;
using System.Text;
using System.Text.Json;
using ContractManagement.Application.Features.Internal;
using ContractManagement.Domain.Entities.Master;
using ContractManagement.Domain.Internal;
using ContractManagement.Infrastructure.Persistence.Master;
using ContractManagement.Infrastructure.Sms;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Primitives;

namespace ContractManagement.Tests.Infrastructure;

[Collection("InternalMaster")]
public class ContractReviewTests
{
    private sealed class Handler(HttpStatusCode status, string body) : HttpMessageHandler
    {
        public string? Request; public Uri? Uri;
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            Request = await request.Content!.ReadAsStringAsync(ct); Uri = request.RequestUri;
            return new(status) { Content = new StringContent(body, Encoding.UTF8, "application/json") };
        }
    }

    private static EsmsOptions Options => new() { ApiKey = "K", SecretKey = "S", Brandname = "ConTrack", Sandbox = true, CallbackUrl = "https://cb" };

    [Theory]
    [InlineData("100")] [InlineData("101")] [InlineData("124")] [InlineData("146")]
    public async Task EsmsSenderPostsExpectedFieldsAndReturnsCode(string code)
    {
        var handler = new Handler(HttpStatusCode.OK, $$"""{"CodeResult":"{{code}}","SMSID":"abc","ErrorMessage":null}""");
        var sender = new EsmsSmsSender(new HttpClient(handler), Microsoft.Extensions.Options.Options.Create(Options));
        var result = await sender.SendAsync("0901234484", "Noi dung", "req1", default);
        Assert.Equal(code, result.CodeResult);
        Assert.Equal("abc", result.SmsId);
        Assert.EndsWith("/SendMultipleMessage_V4_post_json/", handler.Uri!.AbsoluteUri);
        var body = JsonDocument.Parse(handler.Request!).RootElement;
        foreach (var (k, v) in new[] { ("ApiKey", "K"), ("SecretKey", "S"), ("Content", "Noi dung"), ("Phone", "0901234484"), ("Brandname", "ConTrack"),
            ("SmsType", "2"), ("IsUnicode", "0"), ("Sandbox", "1"), ("RequestId", "req1"), ("CallbackUrl", "https://cb") })
            Assert.Equal(v, body.GetProperty(k).GetString());
    }

    [Fact]
    public async Task EsmsSenderTreatsServerErrorsAsTransient()
    {
        var sender = new EsmsSmsSender(new HttpClient(new Handler(HttpStatusCode.BadGateway, "")), Microsoft.Extensions.Options.Options.Create(Options));
        await Assert.ThrowsAsync<HttpRequestException>(() => sender.SendAsync("0901234484", "x", "r", default));
    }

    private sealed class StubSender : ISmsSender
    {
        public Func<string, string, SmsSendResult> Reply = (_, _) => new("100", "SMS1", null);
        public List<(string Phone, string Content)> Sent = [];
        public Task<SmsSendResult> SendAsync(string phone, string content, string requestId, CancellationToken ct)
        { Sent.Add((phone, content)); return Task.FromResult(Reply(phone, content)); }
    }

    [MasterDatabaseFact]
    public async Task LiveFullReviewFlowWithSmsWorkerAndCallback()
    {
        var connection = Environment.GetEnvironmentVariable("CONTRACK_MASTER_TEST_CONNECTION")!;
        var root = Environment.GetEnvironmentVariable("CONTRACK_INTERNAL_TEMPLATE_ROOT")
            ?? Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../private-storage/internal-templates"));
        var stub = new StubSender();
        var services = new ServiceCollection().AddLogging()
            .AddDbContext<MasterDbContext>(o => o.UseSqlServer(connection))
            .AddSingleton<IDataProtectionProvider>(new EphemeralDataProtectionProvider())
            .AddSingleton(Microsoft.Extensions.Options.Options.Create(new EsmsOptions()))
            .AddSingleton<SmsOutboxWriter>().AddSingleton<ISmsSender>(stub)
            .AddSingleton(new InternalTemplateStorage(root))
            .AddSingleton<IConfiguration>(new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?> { ["CustomerPortal:BaseUrl"] = "http://portal" }).Build())
            .AddScoped<IInternalRepository, InternalRepository>().AddScoped<InternalDraftService>()
            .AddScoped<ContractReviewRepository>().AddScoped<CustomerPortalRepository>().AddScoped<SmsCallbackProcessor>()
            .BuildServiceProvider();
        await using var scope = services.CreateAsyncScope();
        var sp = scope.ServiceProvider;
        var db = sp.GetRequiredService<MasterDbContext>();
        var repo = sp.GetRequiredService<IInternalRepository>();
        var drafts = sp.GetRequiredService<InternalDraftService>();
        var reviews = sp.GetRequiredService<ContractReviewRepository>();
        var portal = sp.GetRequiredService<CustomerPortalRepository>();
        var writer = sp.GetRequiredService<SmsOutboxWriter>();
        var saleId = await db.Employees.Where(e => e.EmployeeUsername == "sale.local").Select(e => e.EmployeeId).SingleAsync();
        var officeId = await db.Employees.Where(e => e.EmployeeUsername == "office.local").Select(e => e.EmployeeId).SingleAsync();
        var sale = (await repo.GetUser(saleId, default))!; var office = (await repo.GetUser(officeId, default))!;
        var other = new InternalUser(-1, "Other", "other", ["draft.write"]);
        var template = (await repo.Templates(default)).First();
        var phoneA = "09" + Random.Shared.NextInt64(10000000, 99999999); var phoneB = "08" + Random.Shared.NextInt64(10000000, 99999999);
        var customerA = await repo.CreateCustomer(new("TEST_A_" + Guid.NewGuid().ToString("N"), phoneA, null, null, null, null), saleId, default);
        var customerB = await repo.CreateCustomer(new("TEST_B_" + Guid.NewGuid().ToString("N"), phoneB, null, null, null, null), saleId, default);
        var docIds = new List<long>();
        try
        {
            DraftContent Content(long customer)
            {
                var c = new DraftContent { TemplateId = template.Id, CustomerId = customer, Name = "Hợp đồng gửi khách", Terms = template.Terms.Select(t => t with { Content = "Nội dung " + t.Key }).ToList(),
                    Items = [new(null, "Dịch vụ", 1, 1000000, 0, 0)], VatPercent = 10 };
                foreach (var f in template.Fields) c.Fields[f.Key] = "Giá trị";
                return c;
            }
            var draft = await drafts.Save(null, new(Content(customerA.Id), null, null), sale, default); docIds.Add(draft.Id);
            Assert.Empty(draft.Content.MissingFields);
            Assert.Null(draft.SendBlockedReason);
            Assert.Equal(403, (await Assert.ThrowsAsync<InternalAppException>(() => reviews.SendReview(draft.Id, new(null, draft.RowVersion, draft.VersionRowVersion), other, default))).Status);
            Assert.Equal(409, (await Assert.ThrowsAsync<InternalAppException>(() => reviews.SendReview(draft.Id, new(null, "AAAA", draft.VersionRowVersion), sale, default))).Status);

            // 1. Send to a new customer: version locked, temporary password SMS queued.
            var first = await reviews.SendReview(draft.Id, new("Mời anh xem", draft.RowVersion, draft.VersionRowVersion), sale, default);
            Assert.Equal(ContractReviewStatus.Sent, first.Status);
            var sms = await db.SmsOutboxes.AsNoTracking().Where(s => s.CustomerId == customerA.Id).ToListAsync();
            var tempSms = Assert.Single(sms);
            Assert.Equal(SmsPurpose.TemporaryPassword, tempSms.SmsOutboxPurpose);
            Assert.Equal(PhoneNumber.Mask(phoneA), tempSms.SmsOutboxPhoneMasked);
            var text = writer.Decrypt(tempSms.SmsOutboxContentCiphertext!);
            var password = System.Text.RegularExpressions.Regex.Match(text, "mat khau tam (\\w+),").Groups[1].Value;
            Assert.Equal(10, password.Length);
            var locked = await drafts.Get(draft.Id, sale, default);
            Assert.True(locked.Locked); Assert.False(locked.CanEdit); Assert.Equal(1, locked.Status);
            await Assert.ThrowsAsync<InternalAppException>(() => drafts.Save(draft.Id, new(locked.Content, locked.RowVersion, locked.VersionRowVersion), sale, default));
            Assert.Equal(1, (await repo.Drafts(saleId, null, 1, default)).Count(d => d.Id == draft.Id && d.LatestReviewStatus == ContractReviewStatus.Sent));

            // 2. SMS worker sends, clears ciphertext; callback marks delivered idempotently.
            var worker = new SmsOutboxWorker(services.GetRequiredService<IServiceScopeFactory>(), NullLogger<SmsOutboxWorker>.Instance);
            await worker.RunOnce(default);
            Assert.Contains(stub.Sent, s => s.Phone == phoneA);
            db.ChangeTracker.Clear();
            var accepted = await db.SmsOutboxes.AsNoTracking().SingleAsync(s => s.SmsOutboxId == tempSms.SmsOutboxId);
            Assert.Equal("ACCEPTED", accepted.SmsOutboxStatus);
            Assert.Null(accepted.SmsOutboxPhoneCiphertext); Assert.Null(accepted.SmsOutboxContentCiphertext);
            var callback = new QueryCollection(new Dictionary<string, StringValues> { ["SMSID"] = "SMS1", ["RequestId"] = accepted.SmsOutboxRequestId, ["SendStatus"] = "5", ["SendSuccess"] = "1" });
            var processor = sp.GetRequiredService<SmsCallbackProcessor>();
            for (var i = 0; i < 2; i++) { await processor.Process(callback, "?SendStatus=5", "103.29.26.39", default); db.ChangeTracker.Clear(); }
            Assert.Equal("DELIVERED", (await db.SmsOutboxes.AsNoTracking().SingleAsync(s => s.SmsOutboxId == tempSms.SmsOutboxId)).SmsOutboxStatus);
            Assert.Equal(2, await db.SmsProviderCallbacks.CountAsync(c => c.SmsOutboxId == tempSms.SmsOutboxId));

            // 3. Customer logs in with the temporary password and must change it.
            Assert.Null(await portal.Login(phoneA, "wrong-password1", default));
            var login = (await portal.Login(phoneA, password, default))!;
            Assert.True(login.MustChangePassword);
            await Assert.ThrowsAsync<ArgumentException>(() => portal.ChangePassword(customerA.Id, new(password, "short"), default));
            await portal.ChangePassword(customerA.Id, new(password, "MatKhau2026"), default);
            Assert.False((await portal.Get(customerA.Id, default))!.MustChangePassword);
            Assert.Null(await portal.Login(phoneA, password, default));
            Assert.False((await portal.Login(phoneA, "MatKhau2026", default))!.MustChangePassword);

            // 4. View, comment, request changes. Another customer cannot see it.
            Assert.Equal(404, (await Assert.ThrowsAsync<InternalAppException>(() => portal.Review(customerB.Id, first.Id, default))).Status);
            var view = await portal.Review(customerA.Id, first.Id, default);
            Assert.Equal(ContractReviewStatus.Viewed, view.Status);
            Assert.Equal(0, view.Content.CustomerId);
            var term = template.Terms.First().Key;
            var comment = await portal.Comment(customerA.Id, first.Id, new(term, null, "Đề nghị sửa điều khoản này"), default);
            Assert.True(comment.FromCustomer); Assert.Equal(term, comment.TermKey);
            var reply = await reviews.EmployeeComment(first.Id, new(term, comment.Id, "Đã ghi nhận"), sale, default);
            Assert.Equal(comment.Id, reply.ParentId);
            await reviews.ResolveComment(comment.Id, sale, default);
            Assert.Equal(409, (await Assert.ThrowsAsync<InternalAppException>(() => portal.Respond(customerA.Id, first.Id, new("APPROVE", null, "0000", view.RowVersion), null, null, default))).Status);
            await Assert.ThrowsAsync<ArgumentException>(() => portal.Respond(customerA.Id, first.Id, new("REQUEST_CHANGES", " ", view.DocumentHash, view.RowVersion), null, null, default));
            var changes = await portal.Respond(customerA.Id, first.Id, new("REQUEST_CHANGES", "Giảm giá 5%", view.DocumentHash, view.RowVersion), "127.0.0.1", "test", default);
            Assert.Equal(ContractReviewStatus.ChangesRequested, changes.Status);
            Assert.Equal(409, (await Assert.ThrowsAsync<InternalAppException>(() => portal.Respond(customerA.Id, first.Id, new("APPROVE", null, view.DocumentHash, changes.RowVersion), null, null, default))).Status);
            Assert.True(await db.Notifications.AnyAsync(n => n.NotificationEventkey == $"review:{first.Id}:CHANGES_REQUESTED"));

            // 5. Sale creates version 2, edits and resends: first review superseded, notice SMS.
            var afterChanges = await drafts.Get(draft.Id, sale, default);
            var v2 = await reviews.CreateVersion(draft.Id, new(afterChanges.RowVersion, "Giảm giá"), sale, default);
            Assert.Equal(2, v2.VersionNo); Assert.True(v2.CanEdit);
            await Assert.ThrowsAsync<InternalAppException>(() => reviews.CreateVersion(draft.Id, new(v2.RowVersion, null), sale, default));
            v2.Content.Items[0] = v2.Content.Items[0] with { DiscountPercent = 5 };
            var edited = await drafts.Save(draft.Id, new(v2.Content, v2.RowVersion, v2.VersionRowVersion), sale, default);
            var second = await reviews.SendReview(draft.Id, new(null, edited.RowVersion, edited.VersionRowVersion), sale, default);
            var history = await reviews.Reviews(draft.Id, office, default);
            Assert.Equal([ContractReviewStatus.Sent, ContractReviewStatus.Superseded], history.Select(r => r.Status));
            Assert.Equal(2, history[0].VersionNo);
            Assert.Equal(2, history[1].Comments.Count);
            Assert.Equal(SmsPurpose.ReviewNotice, (await db.SmsOutboxes.Where(s => s.CustomerId == customerA.Id).OrderByDescending(s => s.SmsOutboxId).FirstAsync()).SmsOutboxPurpose);

            // 6. Approve version 2: approval request created, document pending approval.
            var view2 = await portal.Review(customerA.Id, second.Id, default);
            Assert.Equal(1000000 * 0.95m * 1.1m, view2.Content.Totals.Total);
            var approved = await portal.Respond(customerA.Id, second.Id, new("APPROVE", null, view2.DocumentHash, view2.RowVersion), null, null, default);
            Assert.Equal(ContractReviewStatus.Approved, approved.Status);
            var approval = await db.ContractApprovalRequests.SingleAsync(a => a.ContractDocumentId == draft.Id);
            Assert.Equal(second.Id, approval.ContractReviewRequestId); Assert.Equal(saleId, approval.SubmittedByEmployeeId);
            Assert.Equal(2, (await drafts.Get(draft.Id, sale, default)).Status);
            Assert.Contains(await repo.Drafts(null, null, 2, default), d => d.Id == draft.Id);
            var approvedNotice = await db.Notifications.SingleAsync(n => n.NotificationEventkey == $"review:{second.Id}:APPROVED");
            Assert.True(await db.NotificationRecipients.AnyAsync(r => r.NotificationId == approvedNotice.NotificationId && r.EmployeeId == officeId));

            // 7. Rejection branch: reject then close.
            var draft2 = await drafts.Save(null, new(Content(customerA.Id), null, null), sale, default); docIds.Add(draft2.Id);
            var r2 = await reviews.SendReview(draft2.Id, new(null, draft2.RowVersion, draft2.VersionRowVersion), sale, default);
            var v = await portal.Review(customerA.Id, r2.Id, default);
            await portal.Respond(customerA.Id, r2.Id, new("REJECT", "Không phù hợp", v.DocumentHash, v.RowVersion), null, null, default);
            Assert.Equal(7, (await drafts.Get(draft2.Id, sale, default)).Status);
            var rejectedDoc = await drafts.Get(draft2.Id, sale, default);
            await Assert.ThrowsAsync<InternalAppException>(() => reviews.CreateVersion(draft2.Id, new(rejectedDoc.RowVersion, null), sale, default));
            var rejected = (await reviews.Reviews(draft2.Id, sale, default))[0];
            var closed = await reviews.Followup(r2.Id, new(ContractReviewStatus.Close, "Khách không tiếp tục", rejected.RowVersion), sale, default);
            Assert.Equal(ContractReviewStatus.Close, closed.FollowupDecision);
            Assert.Equal(6, (await drafts.Get(draft2.Id, sale, default)).Status);
            Assert.Equal(409, (await Assert.ThrowsAsync<InternalAppException>(() => reviews.Followup(r2.Id, new(ContractReviewStatus.Negotiate, null, closed.RowVersion), sale, default))).Status);

            // 8. Reset is refused once the customer changed the temporary password.
            Assert.Equal(409, (await Assert.ThrowsAsync<InternalAppException>(() => reviews.ResetTemporaryPassword(customerA.Id, sale, default))).Status);
        }
        finally
        {
            db.ChangeTracker.Clear();
            foreach (var id in docIds)
            {
                await db.ContractApprovalRequests.Where(x => x.ContractDocumentId == id).ExecuteDeleteAsync();
                await db.ContractNegotiationComments.Where(x => x.ContractDocumentId == id).ExecuteDeleteAsync();
                var reviewIds = await db.ContractReviewRequests.Where(x => x.ContractDocumentId == id).Select(x => x.ContractReviewRequestId).ToListAsync();
                var noticeIds = await db.Notifications.Where(n => n.NotificationSubjecttype == "CONTRACT_DOCUMENT" && n.NotificationSubjectid == id).Select(n => n.NotificationId).ToListAsync();
                await db.NotificationRecipients.Where(x => noticeIds.Contains(x.NotificationId)).ExecuteDeleteAsync();
                await db.Notifications.Where(x => noticeIds.Contains(x.NotificationId)).ExecuteDeleteAsync();
                await db.ContractReviewRequests.Where(x => x.ContractDocumentId == id).ExecuteUpdateAsync(s => s.SetProperty(x => x.PreviousReviewRequestId, (long?)null));
                await db.ContractReviewRequests.Where(x => x.ContractDocumentId == id).ExecuteDeleteAsync();
                await db.AuditLogs.Where(x => x.EntityName == "CONTRACT_REVIEW_REQUEST" && reviewIds.Select(r => r.ToString()).Contains(x.EntityId)).ExecuteDeleteAsync();
                var smsIds = await db.SmsOutboxes.Where(x => x.ContractDocumentId == id).Select(x => x.SmsOutboxId).ToListAsync();
                await db.SmsProviderCallbacks.Where(x => x.SmsOutboxId != null && smsIds.Contains(x.SmsOutboxId.Value)).ExecuteDeleteAsync();
                await db.SmsOutboxes.Where(x => x.ContractDocumentId == id).ExecuteDeleteAsync();
                await db.ContractDynamicValues.Where(x => x.ContractDocumentId == id).ExecuteDeleteAsync();
                await db.ContractTerms.Where(x => x.ContractDocumentId == id).ExecuteDeleteAsync();
                await db.ContractDocumentItems.Where(x => x.ContractDocumentId == id).ExecuteDeleteAsync();
                await db.ContractVersions.Where(x => x.ContractDocumentId == id).ExecuteDeleteAsync();
                await db.ContractDocuments.Where(x => x.ContractDocumentId == id).ExecuteDeleteAsync();
                await db.AuditLogs.Where(x => x.EntityName == "CONTRACT_DOCUMENTS" && x.EntityId == id.ToString()).ExecuteDeleteAsync();
            }
            foreach (var customer in new[] { customerA.Id, customerB.Id })
            {
                await db.SmsOutboxes.Where(x => x.CustomerId == customer).ExecuteDeleteAsync();
                await db.Customers.Where(x => x.CustomerId == customer).ExecuteDeleteAsync();
                await db.AuditLogs.Where(x => x.EntityName == "CUSTOMER" && x.EntityId == customer.ToString()).ExecuteDeleteAsync();
            }
        }
    }
}
