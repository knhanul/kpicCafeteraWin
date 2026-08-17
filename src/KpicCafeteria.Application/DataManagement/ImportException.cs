namespace KpicCafeteria.Application.DataManagement;

/// <summary>이관 중 발생한 업무 예외.</summary>
public class ImportException : Exception
{
    public ImportException(string message) : base(message) { }

    public ImportException(string message, Exception inner) : base(message, inner) { }
}
