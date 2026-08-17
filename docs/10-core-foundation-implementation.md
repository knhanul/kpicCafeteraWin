# 10. Core Foundation 구현 기록

> 2단계(Core Foundation) 실제 구현 내용.
> 작성일: 2026-08-15

## 1. 생성한 Domain Entity (18종)

`src\KpicCafeteria.Domain\Entities\`

| Entity | 테이블 | 비고 |
| --- | --- | --- |
| MealTypeSetting | meal_type_settings | 배식유형 설정 |
| Menu | menus | 메뉴 기준정보 |
| Ingredient | ingredients | 재료 기준정보 |
| IngredientAlias | ingredient_aliases | 재료 별칭 |
| Recipe | recipes | 레시피 (다중 버전) |
| RecipeIngredient | recipe_ingredients | 레시피 재료 (100인 기준) |
| MealService | meal_services | 배식 |
| MealServiceMenu | meal_service_menus | 식단 메뉴 (스냅샷) |
| MealServiceMenuIngredient | meal_service_menu_ingredients | 식단 재료 스냅샷 |
| PreservationRecord | preservation_records | 보존식 기록 (1:1) |
| MealActual | meal_actuals | 실제 식수 (1:1) |
| OrderItem | order_items | 발주 항목 |
| OrderGroup | order_groups | 묶음 발주 그룹 |
| DocumentTemplate | document_templates | HWPX 템플릿 |
| ImportJob | import_jobs | XLSX 이관 작업 |
| BackupRecord | backup_records | 백업 기록 |
| DataArchive | data_archives | Excel 아카이브 기록 |
| AuditLog | audit_logs | 감사 로그 |

Domain 공통/규칙:

- `Common\IHasCreatedAt.cs`, `Common\IHasUpdatedAt.cs` — 타임스탬프 마커
- `Enums\MealType.cs` — LUNCH/DINNER (DB: "LUNCH"/"DINNER")
- `Enums\OrderStatus.cs` — Pending/Ordered/Skipped (DB: "pending"/"ordered"/"skipped")
- `Domain\CompositionKey.cs` — 레시피 구성 키
- `Domain\QuantityCalculator.cs` — 수량 환산 규칙

## 2. 제외한 기존 Table

| 테이블 | 사유 |
| --- | --- |
| users | 1PC·1인 사용자 — 로그인/권한 미사용 (R24 제외) |
| document_previews | 로컬 프로그램 — 미리보기 토큰 불필요 |

## 3. 실제 DB Table (18개)

`audit_logs`, `backup_records`, `data_archives`, `document_templates`, `import_jobs`, `ingredient_aliases`, `ingredients`, `meal_actuals`, `meal_service_menu_ingredients`, `meal_service_menus`, `meal_services`, `meal_type_settings`, `menus`, `order_groups`, `order_items`, `preservation_records`, `recipe_ingredients`, `recipes`

## 4. EF Mapping

- `Persistence\CafeteriaDbContext.cs` — DbSet 18종, `ApplyConfigurationsFromAssembly`, UTC DateTime 컨벤션, CreatedAt/UpdatedAt 자동 기록
- `Persistence\Configurations\*.cs` — 엔티티별 `IEntityTypeConfiguration<T>` 18개
- `Persistence\ValueConverters.cs` — MealType/OrderStatus/UTC DateTime/JSON 변환기
- 테이블명·컬럼명은 기존 Python DB의 **snake_case** 유지 (예: `meal_service_menus`, `menu_name_snapshot`, `quantity_per_100`)

### 타입 매핑

| C# | SQLite |
| --- | --- |
| int | INTEGER |
| double? | REAL |
| bool | INTEGER |
| DateOnly | TEXT |
| TimeOnly | TEXT |
| DateTime (UTC) | TEXT (ISO-8601 "O" 포맷) |
| Dictionary<string, object?> (JSON) | TEXT (System.Text.Json 직렬화) |

### Timestamp 정책

- 시스템 Timestamp는 **UTC로 저장** (`DateTime.UtcNow`), WPF 표시 시 Local Time 변환 예정
- `service_date`는 TimeZone 변환 대상이 아닌 순수 업무 날짜 (DateOnly)
- CreatedAt/UpdatedAt은 DbContext `SaveChanges`에서 자동 기록 (기존 `default=utcnow`/`onupdate=utcnow` 대응)

## 5. Delete Behavior

### Cascade

```text
MealService → MealServiceMenu → MealServiceMenuIngredient
MealService → PreservationRecord
MealService → MealActual
Recipe → RecipeIngredient
Ingredient → IngredientAlias
Menu → Recipe
```

### SET NULL (스냅샷 보존)

```text
MealServiceMenu.MenuId → menus (SET NULL)
MealServiceMenu.RecipeId → recipes (SET NULL)
MealServiceMenuIngredient.IngredientId → ingredients (SET NULL)
OrderItem.IngredientId → ingredients (SET NULL)
OrderItem.OrderGroupId → order_groups (SET NULL)
OrderGroup.IngredientId → ingredients (SET NULL)
```

### Restrict (기존 NO ACTION 대응)

```text
RecipeIngredient.IngredientId → ingredients (Restrict)
```

## 6. Unique Constraint

| 테이블 | 제약 | 이름 |
| --- | --- | --- |
| menus | name UNIQUE | |
| ingredients | name UNIQUE | |
| meal_services | (service_date, meal_type) UNIQUE | uq_meal_service_date_type |
| recipes | (menu_id, version) UNIQUE | uq_recipe_menu_version |
| recipes | (menu_id, composition_key) UNIQUE | uq_recipe_menu_composition |
| ingredient_aliases | alias UNIQUE | |
| order_items | (service_date, ingredient_id) UNIQUE | uq_order_item_date_ingredient |

**OrderItem nullable IngredientId 특성**: SQLite는 UNIQUE 인덱스에서 NULL을 서로 다른 값으로 취급한다. 따라서 `ingredient_id`가 NULL인 행은 같은 `service_date`에 여러 개 존재할 수 있다. 업무 규칙상 `(service_date, 재료명 스냅샷)`으로 구분하며, 이는 기존 Python 시스템과 동일한 동작이다. (테스트로 검증됨)

## 7. DB 저장 위치

- `%LOCALAPPDATA%\KpicCafeteria\Data\cafeteria.db` (WAL 모드 → `-wal`/`-shm` 파일 동반)
- `IAppDataPathProvider`(Application) / `AppDataPathProvider`(Infrastructure) 구현
- 접근 시 디렉터리 자동 생성

## 8. Migration

- 이름: **InitialCreate** (`20260815144059_InitialCreate`)
- 위치: `src\KpicCafeteria.Infrastructure\Persistence\Migrations\`
- 생성 도구: dotnet-ef 10.0.11 (전역 도구 설치)
- Design-time 팩토리: `Persistence\CafeteriaDbContextFactory.cs`
- 앱 시작 시 `DatabaseInitializer.InitializeAsync()`가 신규 SQLite DB에만 적용

## 9. Seed

`DatabaseInitializer.SeedAsync` — 최초 생성 시에만, 재실행 시 중복 없음 (테스트 검증)

| Code | Name | DefaultPlannedCount | DefaultServiceTime | SortOrder | Active |
| --- | --- | --- | --- | --- | --- |
| LUNCH | 중식 | 400 | 11:40 | 1 | true |
| DINNER | 석식 | 100 | 17:30 | 2 | true |

사용자/admin 계정, 기본 메뉴/재료는 생성하지 않는다.

## 10. Desktop DI / 시작 흐름

`src\KpicCafeteria.Desktop\App.xaml.cs`

```text
App Start
   ↓
DI Build (ServiceCollection)
   ↓
App Data Directory 확인 (IAppDataPathProvider)
   ↓
SQLite DB 초기화 (WAL/busy_timeout PRAGMA → Migrate → Seed)
   ↓
MainWindow 표시 (Database: Ready + DB 경로)
```

- `AddDbContextFactory<CafeteriaDbContext>` — EF 권장 데스크톱 패턴
- `AddLogging` + Debug 로거
- MainWindow는 `IAppDataPathProvider` 주입 (최소 상태 확인 UI)

## 11. 테스트 목록 (39개, 전부 통과)

| 테스트 클래스 | 수 | 검증 내용 |
| --- | --- | --- |
| CompositionKeyTests | 5 | [8,1,4]→"1,4,8", []→"EMPTY", 수량·단위 무관, 정렬 |
| QuantityCalculatorTests | 8 | 10kg/100인×400→40kg, 역산 40→10, null/planned=0 경계, round-trip |
| DbContextTests | 17 | 테이블 생성, unique 5종, FK Cascade 3종, FK SET NULL 2종, OrderItem NULL unique 2종, enum 문자열 저장 2종 |
| SnapshotPersistenceTests | 3 | 메뉴/레시피/재료명 변경 후 스냅샷 불변 |
| SeedTests | 4 | 중식/석식 생성, 재실행 중복 없음, 사용자/메뉴 미생성, InitializeAsync |

- 실제 SQLite 엔진 사용 (`SqliteTestDatabase` — in-memory SQLite, EF InMemory Provider 미사용)
- 기존 pytest 출처를 각 테스트 클래스 주석으로 추적

## 12. NuGet 패키지

| 프로젝트 | 패키지 | 버전 |
| --- | --- | --- |
| Infrastructure | Microsoft.EntityFrameworkCore.Sqlite | 10.0.0 |
| Infrastructure | Microsoft.EntityFrameworkCore.Design | 10.0.0 |
| Desktop | Microsoft.Extensions.DependencyInjection | 10.0.0 |
| Desktop | Microsoft.Extensions.Logging.Debug | 10.0.0 |
| Tests | Microsoft.EntityFrameworkCore.Sqlite | 10.0.0 |

## 13. 알려진 경고

- NU1903: EF Core 10.0.0 전이 패키지 취약성 경고
  - `SQLitePCLRaw.lib.e_sqlite3` 2.1.11 (GHSA-2m69-gcr7-jv3q)
  - `System.Security.Cryptography.Xml` 9.0.0 (GHSA 다수)
  - 로컬 단일 사용자 앱으로 실질 위험은 낮으나, 이후 단계에서 패치 버전으로 상향 검토 필요

## 14. 실행 검증 결과

1. WPF 프로그램 실행 — 정상 (창 표시)
2. SQLite DB 생성 — `%LOCALAPPDATA%\KpicCafeteria\Data\cafeteria.db` 확인
3. DB 저장 위치 — LocalApplicationData 확인
4. 중식/석식 Seed 생성 — LUNCH/중식 400/11:40, DINNER/석식 100/17:30 확인
5. 재실행해도 Seed 중복 없음 — 2건 유지 확인
6. 비정상 종료 없음 — 정상 종료 확인
