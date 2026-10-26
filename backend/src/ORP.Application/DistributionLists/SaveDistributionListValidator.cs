using System.Net.Mail;
using FluentValidation;

namespace ORP.Application.DistributionLists;

public sealed class SaveDistributionListValidator : AbstractValidator<SaveDistributionListRequest>
{
    public SaveDistributionListValidator()
    {
        RuleFor(x => x.Name).Must(name => !string.IsNullOrWhiteSpace(name) && name.Trim().Length <= 200)
            .WithMessage("Provide a name of up to 200 characters.");
        RuleFor(x => x.Emails).NotNull();
        When(x => x.Emails is not null, () =>
        {
            RuleFor(x => x.Emails).Must(emails => emails.Count <= 1000)
                .WithMessage("A distribution list can contain up to 1000 email addresses.");
            RuleForEach(x => x.Emails).Must(IsEmail)
                .WithMessage("Provide a single email address without a display name, up to 254 characters.");
            RuleFor(x => x.Emails).Must(emails => emails.Select(e => e?.Trim())
                    .Distinct(StringComparer.OrdinalIgnoreCase).Count() == emails.Count)
                .WithMessage("Email addresses must be unique within the list.");
        });
    }

    private static bool IsEmail(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return false;
        var trimmed = value.Trim();
        return trimmed.Length <= 254 && !trimmed.Contains('\r') && !trimmed.Contains('\n') &&
            MailAddress.TryCreate(trimmed, out var address) && address.DisplayName.Length == 0 &&
            string.Equals(address.Address, trimmed, StringComparison.Ordinal);
    }
}
