using KpicCafeteria.Domain.Entities;

namespace KpicCafeteria.Application.Abstractions.Repositories;

/// <summary>
/// HWPX 문서 템플릿 리포지토리.
/// 리포지토리는 DbContext를 소유하므로 사용 후 Dispose해야 한다.
/// </summary>
public interface IDocumentTemplateRepository : IDisposable
{
    Task<List<DocumentTemplate>> ListAsync(CancellationToken cancellationToken = default);

    Task<DocumentTemplate?> GetAsync(int id, CancellationToken cancellationToken = default);

    Task<DocumentTemplate?> FindActiveAsync(string documentType, CancellationToken cancellationToken = default);

    Task<List<DocumentTemplate>> ListByTypeAsync(string documentType, CancellationToken cancellationToken = default);

    Task<int> MaxVersionAsync(string documentType, CancellationToken cancellationToken = default);

    void Add(DocumentTemplate template);

    void Remove(DocumentTemplate template);

    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}
