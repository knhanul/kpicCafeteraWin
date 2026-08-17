namespace KpicCafeteria.Application.Documents;

/// <summary>
/// 문서 출력 업무 오류의 기본 타입.
/// UI는 이 예외의 Message를 사용자에게 그대로 표시한다.
/// </summary>
public class DocumentException : Exception
{
    public DocumentException(string message)
        : base(message)
    {
    }

    public DocumentException(string message, Exception inner)
        : base(message, inner)
    {
    }
}

/// <summary>활성 템플릿이 없을 때.</summary>
public sealed class ActiveTemplateNotFoundException : DocumentException
{
    public ActiveTemplateNotFoundException(string documentType)
        : base($"사용 가능한 {DocumentTypeNames.Get(documentType)} 양식이 없습니다. 문서 양식 메뉴에서 양식을 등록해 주세요.")
    {
    }
}

/// <summary>출력할 배식이 없을 때.</summary>
public sealed class NoServicesException : DocumentException
{
    public NoServicesException()
        : base("출력할 배식이 없습니다.")
    {
    }
}

/// <summary>템플릿을 찾을 수 없을 때.</summary>
public sealed class TemplateNotFoundException : DocumentException
{
    public TemplateNotFoundException(int templateId)
        : base($"템플릿을 찾을 수 없습니다: {templateId}")
    {
    }
}

/// <summary>활성 템플릿 삭제 시도 시.</summary>
public sealed class ActiveTemplateDeleteException : DocumentException
{
    public ActiveTemplateDeleteException()
        : base("활성 양식은 삭제할 수 없습니다. 먼저 다른 양식을 활성화해 주세요.")
    {
    }
}

/// <summary>지원하지 않는 문서 유형일 때.</summary>
public sealed class UnsupportedDocumentTypeException : DocumentException
{
    public UnsupportedDocumentTypeException(string documentType)
        : base($"지원하지 않는 문서 유형입니다: {documentType}")
    {
    }
}

/// <summary>문서 유형 이름 표시.</summary>
public static class DocumentTypeNames
{
    public static string Get(string documentType) => documentType switch
    {
        "MEAL_PLAN" => "식단표",
        "COOKING_INSTRUCTION" => "조리지시서",
        "PRESERVATION_RECORD" => "보존식 기록지",
        _ => documentType,
    };
}
