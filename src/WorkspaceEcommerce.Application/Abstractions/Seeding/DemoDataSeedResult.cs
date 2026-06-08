namespace WorkspaceEcommerce.Application.Abstractions.Seeding;

public sealed record DemoDataSeedResult(
    int Categories,
    int Products,
    int Variants,
    int Banners,
    int Carts,
    int Orders);
