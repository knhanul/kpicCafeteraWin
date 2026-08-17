namespace KpicCafeteria.Application.MasterData;

/// <summary>
/// 기준정보 코드 목록.
/// 기존 master.py의 하드코딩 목록과 동일하다. DB에는 자유 문자열로 저장된다.
///
/// Reference:
/// C:\Pjt\kpicCafeteria\backend\app\routers\master.py
///   MENU_ROLES / STAT_GROUPS / UNITS
/// </summary>
public static class MasterDataCodes
{
    public static readonly string[] MenuRoles =
    [
        "밥·죽", "면·떡", "국·탕", "찌개·전골", "주찬", "부찬", "김치·절임", "샐러드", "후식·음료", "기타",
    ];

    public static readonly string[] StatGroups =
    [
        "곡류·주식", "면·떡·전분", "소고기", "돼지고기", "닭·오리", "수산물", "달걀", "두류·두부",
        "채소", "버섯·해조", "과일·견과", "유제품", "김치·절임", "가공식품", "장류·소스·조미료", "기타",
    ];

    public static readonly string[] Units =
    [
        "kg", "g", "L", "ml", "개", "봉", "팩", "판", "통", "캔", "병", "박스", "단", "묶음", "장", "줄", "포", "관", "밧트",
    ];
}
