# 13. Master Data 구현 기록

> 3단계(기준정보 Master Data) 실제 구현 내용.
> 작성일: 2026-08-16

## 1. 구현 기능

- **메뉴**: 조회/검색(이름·통계집계명, 역할 필터), 생성, 수정, 미사용 처리
- **식재료**: 조회/검색(이름·별칭, 통계분석군 필터), 생성, 수정, 미사용 처리
- **재료 별칭**: 추가(중복 시 소유 변경), 삭제
- **다중 레시피**: 생성, 수정, 미사용 처리, 기본 레시피 지정, 재료 그리드 편집
- **배식 기본값**: 조회/수정 (기본 계획식수, 배식시간, 사용 여부, 정렬 순서, 설명)
- **시간 입력 정규화**: `TimeInput24` (기존 JS normalizeTime24/addMinutes 이식)

## 2. Repository 구조

| 구성 요소 | 위치 |
| --- | --- |
| `IMasterDataRepository` | `src\KpicCafeteria.Application\Abstractions\Repositories\` |
| `IMasterDataRepositoryFactory` | `src\KpicCafeteria.Application\Abstractions\Repositories\` |
| `MasterDataRepository` | `src\KpicCafeteria.Infrastructure\Repositories\` |
| `MasterDataRepositoryFactory` | `src\KpicCafeteria.Infrastructure\Repositories\` |

- 과도한 범용화 없음: `IGenericRepository<T>`/Specification/CQRS/Mediator 미도입
- 리포지토리는 DbContext를 소유하며 `IDisposable` (작업 단위 수명)
- 트랜잭션 메서드 제공 (레시피 저장의 all-or-nothing)

## 3. MasterDataService 구조

`src\KpicCafeteria.Application\MasterData\MasterDataService.cs`

- 모든 public 메서드가 `using var repository = CreateRepository();`로 작업 단위 시작
- 업무 오류는 `MasterDataException` 파생 예외로 표현 (HTTP 상태코드 미사용)
- DTO: `MasterDataDtos.cs` (MenuDto/IngredientDto/RecipeDto/AliasDto/MealTypeSettingDto 등)
- 코드 목록: `MasterDataCodes.cs` (메뉴 역할 10종, 통계분석군 16종, 단위 19종 — 기존 master.py와 동일)
- 시간 정규화: `TimeInput24.cs` (독립 로직, UI 미결합)

## 4. 메뉴 규칙

- 메뉴명 중복 금지 → `DuplicateMenuNameException` ("같은 이름의 메뉴가 있습니다.")
- `canonical_name` 미입력 시 메뉴명 사용
- 미사용 처리(`Active=false`) — 물리 삭제 없음, **모든 레시피도 함께 미사용 처리**
- 과거 식단 스냅샷(`menu_name_snapshot`)은 영향 없음 (테스트 검증)

## 5. 재료 규칙

- 재료명 중복 금지 → `DuplicateIngredientNameException` ("같은 이름의 재료가 있습니다.")
- 미사용 처리(`Active=false`) — 물리 삭제 없음
- 검색은 이름 + 별칭, 대소문자 무시

## 6. Alias 규칙

- 별칭은 전체 시스템에서 Unique (`ingredient_aliases.alias` UNIQUE)
- **중복 별칭 등록 시 소유 재료 변경** (기존 규칙 유지)
- 신규 별칭은 `source="사용자"`
- 삭제는 Windows UI 관리용 추가 기능 (기존 Python에는 없음)

## 7. Recipe 규칙

- **CompositionKey**: 재료 ID 정렬 + `,` 결합, 빈 값 `"EMPTY"`, 수량·단위·정렬순서 미포함
- **동일 구성 + 수량 변경** → 새 레시피 생성 X, 기존 레시피 수정 O
- **구성 변경** → CompositionKey 달라짐, 새 레시피 등록 가능
- **동일 CompositionKey 중복** → `DuplicateRecipeCompositionException` ("같은 재료 구성의 레시피가 이미 있습니다: {이름}")
- **Version**: 메뉴별 순차 증가 (다른 메뉴는 v1부터)
- **Default**: 메뉴당 최대 1개. 첫 활성 레시피는 자동 기본 지정. 기본 변경 시 이전 기본 해제
- **Default 비활성화**: `Active=false` + `IsDefault=false`, 버전 순 활성 레시피가 자동 대체
- **미등록 재료명**: 자동 Ingredient 생성 (`StatGroup="기타"`, `ReviewStatus="자동등록-분류필요"`, `Active=true`, `DefaultUnit=항목 단위`)
- **같은 재료 중복**: `DuplicateRecipeIngredientException` ("레시피에 같은 재료가 중복되었습니다: {이름}")
- **단위**: 미지정 시 재료 기본단위 사용
- **수량**: 100인 기준, 소수 자리 제한/반올림 규칙 추가 없음 (기존과 동일)
- 레시피 생성/수정은 **트랜잭션**으로 처리 (자동 생성 재료 포함 all-or-nothing)

## 8. 배식 기본값 규칙

- `default_planned_count` 음수 금지 → `InvalidPlannedCountException`
- 배식시간 HH:MM 검증 (`TimeInput24.Normalize`) → `InvalidTimeFormatException`
- 미존재 코드 → `MealTypeNotFoundException` ("배식유형을 찾을 수 없습니다: {코드}")
- Windows 추가: **정렬 순서·설명 편집 지원** (기존 Python은 계획식수/시간/사용여부만 수정 — 의도적 확장)

## 9. WPF 화면 구조

```text
MainWindow
├─ 좌측 네비게이션 (기준정보: 메뉴·레시피 / 식재료 / 배식 기본값)
└─ ContentControl (View 전환)

MenuRecipeView        → MenuRecipeViewModel
  ├─ 메뉴 목록 (검색 + 역할 필터 + 미사용 포함)
  ├─ 메뉴 편집 (메뉴명/역할/통계집계명/사용)
  └─ 레시피 (목록 + 편집 + 재료 DataGrid)
      └─ 재료명 ComboBox(IsEditable) — 기존 재료 선택 or 신규 입력(자동 생성)

IngredientView        → IngredientViewModel
  ├─ 식재료 목록 (검색 + 통계군 필터 + 미사용 포함)
  ├─ 식재료 편집 (표준재료명/통계분석군/기본단위/kg환산계수/통계제외/사용)
  └─ 별칭 관리 (추가/삭제)

MealDefaultsView      → MealDefaultsViewModel
  └─ 배식유형별 기본값 DataGrid (계획식수/시간/정렬/사용/설명)
```

- CommunityToolkit.Mvvm (`ObservableObject`/`ObservableProperty`/`AsyncRelayCommand`)
- Code-behind에는 화면 전환만, 업무 로직은 ViewModel
- ViewModel → `MasterDataService` → Repository → DbContext (EF 직접 접근 없음)
- 저장 버튼 기반 + Dirty 상태에서 다른 항목 선택 시 확인 대화상자
- 업무 오류는 메시지로, 예상 외 오류는 로깅 후 일반 메시지로 표시

## 10. 신규 테스트 (59개)

| 테스트 클래스 | 수 | 검증 |
| --- | --- | --- |
| TimeInput24Tests | 25 | 정규화(13), 무효 입력(9), addMinutes(8) — test_time_input24.py 이식 |
| RecipeTests | 9 | A 동일구성 수정, B 다른구성 신규, C 중복 거부, D 버전, E Default 1개, F Default 대체, G 자동 재료 생성, H 중복 재료 거부, 메뉴 미사용 시 레시피 미사용 |
| MasterDataServiceTests | 25 | 메뉴(7), 재료(5), 별칭(4), 배식기본값(9) — test_multi_recipe.py/test_master_data.py 이식 |

- 실제 SQLite 엔진 사용 (`SqliteTestDatabase`), EF InMemory Provider 미사용
- 기존 pytest 출처를 각 테스트 클래스 주석으로 추적

## 11. 기존 시스템과 의도적으로 달라진 부분

| 항목 | 기존 | Windows |
| --- | --- | --- |
| 오류 표현 | HTTP 409/400/404 | `MasterDataException` 파생 예외 + 메시지 |
| 별칭 삭제 | 없음 | 추가 (UI 관리용) |
| 배식 기본값 정렬순서/설명 | 수정 불가 | 수정 가능 (요구사항) |
| 시간 입력 | JS TimeInput24 | C# `TimeInput24` (동일 규칙) |
| 배식시간 검증 | `time.fromisoformat` (HH:MM만) | `TimeInput24.Normalize` (HH:MM + 자유입력 "1140" 허용) |
| 레시피 저장 원자성 | FastAPI 세션 롤백 | 명시적 트랜잭션 |
| UI | 웹 SPA | WPF (픽셀 복제 아님, 사용 흐름 참고) |

## 12. 미구현 항목 (이번 단계 제외)

- Workspace(주간 식단/메뉴 선택기/집중작성/조리지시/보존식/실제식수)
- Orders(발주)
- Documents(HWPX/PDF/COM/미리보기/Excel/템플릿)
- Statistics(대시보드/통계)
- XLSX 이관
- 설치 프로그램
- 레시피 재료 Excel 붙여넣기 (향후 확장 가능하도록 ViewModel/Service 미결합 구조)

## 13. 발견된 문제 및 미확인 사항

### 해결됨
- NU1903 취약성 → EF Core 10.0.11 상향으로 완전 해소 (docs/11)
- DbContext stale 문제 → 작업 단위별 컨텍스트 팩토리 (docs/12)

### 미확인 사항
- **M1**: `is_representative`(대표 메뉴)는 이번 단계에서 미사용 — Workspace 단계에서 확인 예정
- **M2**: `review_status` 값 전체 목록 — "정상"/"자동등록-분류필요" 외 값은 이관 데이터에 따라 다름
- **M3**: 레시피 재료 ComboBox의 이중 바인딩(SelectedValue+Text)이 일부 WPF 환경에서 동작 차이 가능 — 실제 사용 시 확인 필요
- **M4**: 메뉴/재료 목록 페이지네이션 — 현재 limit 200 고정 (기존은 offset/limit API). 대량 데이터 시 추가 필요
