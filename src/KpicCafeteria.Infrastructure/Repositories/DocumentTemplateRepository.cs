using KpicCafeteria.Application.Abstractions.Repositories;
using KpicCafeteria.Domain.Entities;
using KpicCafeteria.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace KpicCafeteria.Infrastructure.Repositories;

/// <summary>
/// HWPX 문서 템플릿 리포지토리.
/// DbContext를 소유하므로 사용 후 Dispose해야 한다.
/// </summary>
public sealed class DocumentTemplateRepository : IDocumentTemplateRepository
{
    private readonly CafeteriaDbContext _db;

    public DocumentTemplateRepository(CafeteriaDbContext db)
    {
        _db = db;
    }

    public Task<List<DocumentTemplate>> ListAsync(CancellationToken cancellationToken = default)
        => _db.DocumentTemplates
            .AsNoTracking()
            .OrderBy(x => x.DocumentType).ThenByDescending(x => x.Version)
            .ToListAsync(cancellationToken);

    public Task<DocumentTemplate?> GetAsync(int id, CancellationToken cancellationToken = default)
        => _db.DocumentTemplates.FirstOrDefaultAsync(x => x.Id == id, cancellationToken);

    public Task<DocumentTemplate?> FindActiveAsync(string documentType, CancellationToken cancellationToken = default)
        => _db.DocumentTemplates
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.DocumentType == documentType && x.Active, cancellationToken);

    public Task<List<DocumentTemplate>> ListByTypeAsync(string documentType, CancellationToken cancellationToken = default)
        => _db.DocumentTemplates
            .Where(x => x.DocumentType == documentType)
            .OrderByDescending(x => x.Version)
            .ToListAsync(cancellationToken);

    public async Task<int> MaxVersionAsync(string documentType, CancellationToken cancellationToken = default)
        => await _db.DocumentTemplates
            .Where(x => x.DocumentType == documentType)
            .Select(x => (int?)x.Version)
            .MaxAsync(cancellationToken) ?? 0;

    public void Add(DocumentTemplate template) => _db.DocumentTemplates.Add(template);

    public void Remove(DocumentTemplate template) => _db.DocumentTemplates.Remove(template);

    public Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
        => _db.SaveChangesAsync(cancellationToken);

    public void Dispose() => _db.Dispose();
}
