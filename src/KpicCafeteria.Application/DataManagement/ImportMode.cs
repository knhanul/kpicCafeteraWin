namespace KpicCafeteria.Application.DataManagement;

/// <summary>이관 적용 모드.</summary>
public enum ImportMode
{
    /// <summary>현재 데이터를 초기화하고 새 XLSX로 교체.</summary>
    Replace,

    /// <summary>현재 데이터를 유지하고 XLSX 데이터를 병합.</summary>
    Merge,
}
