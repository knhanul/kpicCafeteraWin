# 12. DbContext 수명 정책

> 작성일: 2026-08-16

## 1. 원칙

**Windows 애플리케이션 전체에서 하나의 `CafeteriaDbContext`를 Singleton으로 유지하지 않는다.**

Application Service의 한 작업(유스케이스)이 끝나면 DbContext가 장기간 살아남지 않게 한다.

## 2. 선택한 방식

```text
IDbContextFactory<CafeteriaDbContext>   (EF Core, Desktop DI에 등록)
        ↓
IMasterDataRepositoryFactory            (Application 인터페이스)
        ↓
MasterDataService                       (작업마다 Create() → using var)
        ↓
IMasterDataRepository : IDisposable     (DbContext 소유, Dispose 시 해제)
```

### 흐름

1. Desktop DI에 `AddDbContextFactory<CafeteriaDbContext>` 등록 (2단계부터 유지)
2. `MasterDataService`는 `IMasterDataRepositoryFactory.Create()`로 **작업 단위별 새 리포지토리(및 새 DbContext)** 생성
3. 작업이 끝나면 `using var`로 리포지토리 Dispose → DbContext Dispose
4. WPF 화면을 오래 열어 두어도 stale entity가 남지 않음

## 3. 왜 이 방식인가

- **2단계에서 확인된 문제**: 원시 SQL 삭제 후 같은 컨텍스트로 조회하면 변경 추적 캐시가 stale 값을 반환
- **Singleton DbContext 문제**: 장기 보유 시 변경 추적 메모리 증가, stale 데이터, 동시성 이슈
- **작업 단위별 컨텍스트**: 각 작업이 깨끗한 상태로 시작/종료, EF 권장 데스크톱 패턴(`IDbContextFactory`)

## 4. 트랜잭션

- 레시피 생성/수정처럼 **여러 변경이 원자적이어야 하는 작업**은 리포지토리의 `BeginTransactionAsync`/`CommitTransactionAsync`/`RollbackTransactionAsync`를 사용
- 예외 발생 시 롤백 (기존 Python의 세션 롤백과 동일한 의미)

## 5. 구현 위치

| 구성 요소 | 위치 |
| --- | --- |
| `IDbContextFactory<CafeteriaDbContext>` 등록 | `src\KpicCafeteria.Desktop\App.xaml.cs` |
| `IMasterDataRepositoryFactory` | `src\KpicCafeteria.Application\Abstractions\Repositories\` |
| `MasterDataRepositoryFactory` | `src\KpicCafeteria.Infrastructure\Repositories\` |
| `IMasterDataRepository : IDisposable` | `src\KpicCafeteria.Application\Abstractions\Repositories\` |
| 작업 단위별 `using var repository = CreateRepository()` | `src\KpicCafeteria.Application\MasterData\MasterDataService.cs` |

## 6. 테스트 검증

- `MasterDataTestHarness`가 서비스 작업마다 새 DbContext(공유 SQLite 연결 위)를 생성
- 98개 테스트가 실제 SQLite 엔진에서 통과 (stale 문제 재발 없음)
