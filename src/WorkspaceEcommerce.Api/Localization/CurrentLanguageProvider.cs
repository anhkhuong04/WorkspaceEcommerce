using WorkspaceEcommerce.Application.Common.Localization;

namespace WorkspaceEcommerce.Api.Localization;

internal sealed class CurrentLanguageProvider(IHttpContextAccessor httpContextAccessor) : ICurrentLanguageProvider
{
    public string CurrentLanguage
    {
        get
        {
            var language = httpContextAccessor.HttpContext?.Request.Headers.AcceptLanguage.ToString();

            return language?.StartsWith("vi", StringComparison.OrdinalIgnoreCase) == true
                ? "vi"
                : "en";
        }
    }
}
