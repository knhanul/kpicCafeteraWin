# 11. NuGet 취약성(NU1903) 검토 및 처리

> 작성일: 2026-08-16

## 1. 결론

**NU1903 취약성 경고는 EF Core 패키지를 10.0.11로 상향하여 완전히 해소되었다.**

`dotnet list package --vulnerable` 결과: **모든 프로젝트에서 취약한 패키지 없음** (2026-08-16 확인)

## 2. 원인 조사

### 2-1. SQLitePCLRaw.lib.e_sqlite3 2.1.11 (GHSA-2m69-gcr7-jv3q)

- **의존성 경로**:
  ```
  Microsoft.EntityFrameworkCore.Sqlite 10.0.0
    → Microsoft.EntityFrameworkCore.Sqlite.Core 10.0.0
      → SQLitePCLRaw.bundle_e_sqlite3 2.1.11
        → SQLitePCLRaw.lib.e_sqlite3 2.1.11
  ```
- **영향 프로젝트**: Infrastructure, Desktop, Tests (Sqlite를 참조하는 모든 프로젝트)
- **심각도**: High (SQLite 네이티브 라이브러리 취약성)

### 2-2. System.Security.Cryptography.Xml 9.0.0 (GHSA 다수)

- **의존성 경로**:
  ```
  Microsoft.EntityFrameworkCore.Design 10.0.0
    → Microsoft.Build 17.7.2
      → System.CodeDom / System.Security.Cryptography.Pkcs 등
        → System.Security.Cryptography.Xml 9.0.0
  ```
- **영향 프로젝트**: Infrastructure만 (Design 패키지는 `PrivateAssets=all`이라 런타임/배포에 미포함)
- **심각도**: High (XML 서명 관련 취약성)

## 3. 해결 방법

### 패치 버전 확인

| 패키지 | 기존 | 최신 패치 |
| --- | --- | --- |
| Microsoft.EntityFrameworkCore.Sqlite | 10.0.0 | **10.0.11** |
| Microsoft.EntityFrameworkCore.Design | 10.0.0 | **10.0.11** |
| Microsoft.Extensions.DependencyInjection | 10.0.0 | 10.0.11 |
| Microsoft.Extensions.Logging.Debug | 10.0.0 | 10.0.11 |

### 적용 내용

1. `Microsoft.EntityFrameworkCore.Sqlite` / `Design` → **10.0.11** (Infrastructure, Tests)
2. EF Core 10.0.11의 전이 의존성이 `Microsoft.Extensions.DependencyInjection >= 10.0.11`을 요구하므로, Desktop의 직접 참조도 **10.0.11**로 상향 (NU1605 다운그레이드 오류 방지)
3. `Microsoft.Extensions.Logging.Debug` → **10.0.11**

### 결과

- EF Core 10.0.11이 SQLitePCLRaw를 패치 버전으로 끌어올려 `SQLitePCLRaw.lib.e_sqlite3` 취약성 해소
- EF Core Design 10.0.11이 Microsoft.Build 체인의 `System.Security.Cryptography.Xml`을 패치 버전으로 끌어올려 해소
- 별도의 직접 버전 고정(`SQLitePCLRaw.bundle_e_sqlite3 3.0.5` 등)은 **불필요** (강제 고정 회피)

## 4. 검증

```powershell
dotnet list .\KpicCafeteria.slnx package --vulnerable
# → 모든 프로젝트: 취약한 패키지 없음

dotnet build .\KpicCafeteria.slnx
# → 경고 0건, 성공

dotnet test .\KpicCafeteria.slnx
# → 98/98 통과
```

## 5. 향후 관리

- EF Core 패치 버전(10.0.x)이 나올 때마다 상향 검토
- `dotnet list package --vulnerable`을 정기 실행하여 신규 취약성 감시
