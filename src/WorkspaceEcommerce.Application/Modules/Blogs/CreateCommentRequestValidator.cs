using FluentValidation;

namespace WorkspaceEcommerce.Application.Modules.Blogs;

public sealed class CreateCommentRequestValidator : AbstractValidator<CreateCommentRequest>
{
    public CreateCommentRequestValidator()
    {
        RuleFor(request => request.AuthorName)
            .NotEmpty()
            .MaximumLength(100);

        RuleFor(request => request.AuthorEmail)
            .NotEmpty()
            .EmailAddress()
            .MaximumLength(150);

        RuleFor(request => request.Content)
            .NotEmpty()
            .MaximumLength(2000);
    }
}
