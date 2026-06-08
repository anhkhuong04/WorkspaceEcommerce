using FluentValidation;

namespace WorkspaceEcommerce.Application.Modules.Content.Banners;

public sealed class CreateBannerRequestValidator : AbstractValidator<CreateBannerRequest>
{
    public CreateBannerRequestValidator()
    {
        RuleFor(request => request.Title)
            .NotEmpty()
            .MaximumLength(250);

        RuleFor(request => request.ImageUrl)
            .NotEmpty()
            .MaximumLength(1000);

        RuleFor(request => request.LinkUrl)
            .MaximumLength(1000);
    }
}
