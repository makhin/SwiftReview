using ORP.Application.Emails;
using ORP.Domain.Emails;
using Xunit;

namespace ORP.Application.Tests;

public sealed class EmailValidationTests
{
    private static CreateEmailRequest Valid => new() { To = [new("user@example.com")], Subject = "Subject", Body = "Body" };
    private static bool IsValid(CreateEmailRequest request) => new CreateEmailValidator().Validate(request).IsValid;

    [Fact]
    public void DefaultsAndBoundaries()
    {
        Assert.True(IsValid(Valid));
        Assert.Equal(EmailBodyFormat.Text, Valid.BodyFormat);
        Assert.Equal(EmailImportance.Normal, Valid.Importance);
        Assert.False(Valid.IncludeMessagePdf);
        Assert.True(IsValid(Valid with { Subject = new('a', 500), Body = new('b', 100000),
            To = Enumerable.Range(0, 100).Select(i => new EmailAddress($"user{i}@example.com")).ToArray(),
            ReplyTo = Enumerable.Range(0, 10).Select(i => new EmailAddress($"reply{i}@example.com")).ToArray() }));
        Assert.False(IsValid(Valid with { Subject = new('a', 501) }));
        Assert.False(IsValid(Valid with { Body = new('a', 100001) }));
        Assert.False(IsValid(Valid with { To = Enumerable.Range(0, 101).Select(i => new EmailAddress($"user{i}@example.com")).ToArray() }));
        Assert.False(IsValid(Valid with { ReplyTo = Enumerable.Range(0, 11).Select(i => new EmailAddress($"user{i}@example.com")).ToArray() }));
    }

    [Theory]
    [InlineData(null)] [InlineData("")] [InlineData(" ")]
    public void RequiredFields(string? value)
    {
        Assert.False(IsValid(Valid with { Subject = value! }));
        Assert.False(IsValid(Valid with { Body = value! }));
        Assert.False(IsValid(Valid with { To = [new(value!)] }));
    }

    [Fact]
    public void CollectionsDuplicatesAndEnums()
    {
        Assert.False(IsValid(Valid with { To = [] }));
        Assert.False(IsValid(Valid with { To = null! }));
        Assert.False(IsValid(Valid with { Cc = null! }));
        Assert.False(IsValid(Valid with { Bcc = null! }));
        Assert.False(IsValid(Valid with { ReplyTo = null! }));
        Assert.False(IsValid(Valid with { To = [null!] }));
        Assert.False(IsValid(Valid with { Cc = [new(" USER@example.com ")] }));
        Assert.False(IsValid(Valid with { Bcc = [new("user@example.com")] }));
        Assert.False(IsValid(Valid with { To = [new("user@example.com"), new("user@example.com")] }));
        Assert.False(IsValid(Valid with { BodyFormat = (EmailBodyFormat)99 }));
        Assert.False(IsValid(Valid with { Importance = (EmailImportance)99 }));
        Assert.True(IsValid(Valid with { To = [new(" user@example.com ", "Name")] }));
    }

    [Theory]
    [InlineData("bad")] [InlineData("Name <user@example.com>")]
    [InlineData("user@example.com,other@example.com")] [InlineData("user@example.com\r\nBcc:x@example.com")]
    public void InvalidAddresses(string value) => Assert.False(IsValid(Valid with { To = [new(value)] }));

    [Fact]
    public void HeaderInjectionAndAttachmentLimits()
    {
        Assert.False(IsValid(Valid with { Subject = "subject\nBcc:other@example.com" }));
        Assert.False(IsValid(Valid with { To = [new("user@example.com", "Name\r\nInjected")] }));
        EmailLimits.CheckAttachments(10, [EmailLimits.FileBytes, EmailLimits.FileBytes]);
        Assert.Throws<EmailSizeLimitException>(() => EmailLimits.CheckAttachments(11, []));
        Assert.Throws<EmailSizeLimitException>(() => EmailLimits.CheckAttachments(1, [EmailLimits.FileBytes + 1]));
        Assert.Throws<EmailSizeLimitException>(() => EmailLimits.CheckAttachments(3, [EmailLimits.FileBytes, EmailLimits.FileBytes, 1]));
    }
}
