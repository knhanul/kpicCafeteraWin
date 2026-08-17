using KpicCafeteria.Application.Abstractions.Repositories;
using KpicCafeteria.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace KpicCafeteria.Infrastructure.Repositories;

/// <summary>
/// 작업 단위별 새 문서 템플릿 리포지토리 생성.
/// </summary>
public sealed class DocumentTemplateRepositoryFactory : IDocumentTemplateRepositoryFactory
{
    private readonly IDbContextFactory<CafeteriaDbContext> _factory;

    public DocumentTemplateRepositoryFactory(IDbContextFactory<CafeteriaDbContext> factory)
    {
        _factory = factory;
    }

    public IDocumentTemplateRepository Create()
        => new DocumentTemplateRepository(_factory.CreateDbContext());
}
