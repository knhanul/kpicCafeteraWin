namespace KpicCafeteria.Application.Abstractions.Repositories;

/// <summary>
/// 작업 단위별 새 문서 템플릿 리포지토리 생성.
/// </summary>
public interface IDocumentTemplateRepositoryFactory
{
    IDocumentTemplateRepository Create();
}
