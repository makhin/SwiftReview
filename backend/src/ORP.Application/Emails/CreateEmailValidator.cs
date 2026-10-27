using System.Net.Mail;
using FluentValidation;

namespace ORP.Application.Emails;

public sealed class CreateEmailValidator : AbstractValidator<CreateEmailRequest>
{
    public CreateEmailValidator()
    {
        RuleFor(x => x.Subject).Must(x => ValidHeader(x, 500) && !string.IsNullOrWhiteSpace(x))
            .WithMessage("Provide a subject of up to 500 characters without line breaks.");
        RuleFor(x => x.Body).Must(x => !string.IsNullOrWhiteSpace(x) && x.Length <= 100000)
            .WithMessage("Provide a body of up to 100000 characters.");
        RuleFor(x => x.BodyFormat).IsInEnum();
        RuleFor(x => x.Importance).IsInEnum();
        RuleFor(x => x.To).NotEmpty();
        RuleFor(x => x.Cc).NotNull();
        RuleFor(x => x.Bcc).NotNull();
        RuleFor(x => x.ReplyTo).NotNull();
        RuleForEach(x => x.To).Must(ValidRecipient).WithMessage("Provide a valid email address and optional display name.");
        RuleForEach(x => x.Cc).Must(ValidRecipient).WithMessage("Provide a valid email address and optional display name.");
        RuleForEach(x => x.Bcc).Must(ValidRecipient).WithMessage("Provide a valid email address and optional display name.");
        RuleForEach(x => x.ReplyTo).Must(ValidRecipient).WithMessage("Provide a valid email address and optional display name.");
        RuleFor(x => x.ReplyTo).Must(x => x is null || x.Count <= 10).WithMessage("At most 10 ReplyTo addresses are allowed.");
        RuleFor(x => x).Must(x => Recipients(x).Count() <= 100).WithName("To")
            .WithMessage("At most 100 recipients are allowed across To, Cc and Bcc.");
        RuleFor(x => x).Must(x =>
        {
            var addresses = Recipients(x).Select(a => a?.Address?.Trim()).ToArray();
            return addresses.Distinct(StringComparer.OrdinalIgnoreCase).Count() == addresses.Length;
        }).WithName("To").WithMessage("Recipients must be unique across To, Cc and Bcc.");
    }

    private static IEnumerable<EmailAddress> Recipients(CreateEmailRequest x) =>
        (x.To ?? []).Concat(x.Cc ?? []).Concat(x.Bcc ?? []);
    private static bool ValidRecipient(EmailAddress? x) => x is not null && ValidAddress(x.Address) && ValidHeader(x.DisplayName, 200);
    public static bool ValidHeader(string? value, int max) => value is null ||
        (value.Length <= max && !value.Contains('\r') && !value.Contains('\n'));
    public static bool ValidAddress(string? value)
    {
        if (string.IsNullOrWhiteSpace(value) || !ValidHeader(value, 254)) return false;
        var trimmed = value.Trim();
        return MailAddress.TryCreate(trimmed, out var address) && address.DisplayName.Length == 0 && address.Address == trimmed;
    }
}
