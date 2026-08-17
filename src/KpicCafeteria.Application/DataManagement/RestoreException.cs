namespace KpicCafeteria.Application.DataManagement;

/// <summary>복구 관련 업무 예외.</summary>
public class RestoreException : Exception
{
    public RestoreException(string message) : base(message) { }

    public RestoreException(string message, Exception inner) : base(message, inner) { }
}
