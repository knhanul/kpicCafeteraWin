namespace KpicCafeteria.Application.DataManagement;

/// <summary>백업/복구 관련 업무 예외.</summary>
public class BackupException : Exception
{
    public BackupException(string message) : base(message) { }

    public BackupException(string message, Exception inner) : base(message, inner) { }
}
