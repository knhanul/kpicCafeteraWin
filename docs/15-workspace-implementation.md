# 15. Workspace(주간 급식 운영) 구현 기록

> 4단계(주간 급식 운영 Workspace) 실제 구현 내용.
> 작성일: 2026-08-16

## 1. Repository 구조

| 구성 요소 | 위치 |
| --- | --- |
| `IMealServiceRepository` | `src\KpicCafeteria.Application\Abstractions\Repositories\` |
| `IMealServiceRepositoryFactory` | `src\KpicCafeteria.Application\Abstractions\Repositories\` |
| `MealServiceRepository` | `src\KpicCafeteria.Infrastructure\Repositories\` |
| `MealServiceRepositoryFactory` | `src\KpicCafeteria.Infrastructure\Repositories\` |

- Master Data와 동일하게 **작업 단위별 DbContext** 사용 (`IMasterDataRepositoryFactory` 패턴 재사용)
- 리포지토리는 DbContext 소유 + `IDisposable`, 트랜잭션 메서드 제공
- Generic Repository/CQRS/Mediator 미도입

## 2. WorkspaceService 구조

`src\KpicCafeteria.Application\Workspace\WorkspaceService.cs` + `WorkspaceDtos.cs` + `WorkspaceExceptions.cs`

- 모든 public 메서드가 `using var repository = CreateRepository();`로 작업 단위 시작
- 업무 오류는 `WorkspaceException` 파생 예외로 표현
- `QuantityCalculator`(Domain) 재사용 — 공식 재작성 없음

## 3. Workspace DTO

- `WorkspacePeriodDto` / `WorkspaceWeekDto` / `WorkspaceDayDto` — 기간 조회
- `MealServiceDto` / `MealServiceMenuDto` / `MealServiceIngredientDto` — 배식/메뉴/재료
- `PreservationRecordDto` / `MealActualDto` — 보존식/실제 식수
- `MenuPickerItemDto` / `MenuPickerRecipeDto` / `MenuPickerResultDto` — 메뉴 선택기
- 입력용: `ServiceCreateInput`/`ServiceUpdateInput`/`AddMenuInput`/`BatchAddMenuItemInput`/`ServiceMenuInput`/`IngredientSnapshotInput`/`MealEditorInput`/`PreservationInput`/`ActualInput`

## 4. 기간 처리

- 기준일이 어느 요일이든 **해당 주 월요일** 시작 (`MondayOf`)
- **월~금 5열만** 표시 (주말 컬럼 없음)
- 표시 기간 1~8주 선택 가능, 기본 **2주** (WeekCountOptions: 1/2/4/6/8)
- 이전/다음 이동 폭 = 현재 표시 기간 (2주 표시 → 2주 이동)
- 집중 작성 모드에서는 1주 표시, 이전/다음 1주 이동

## 5. 배식 CRUD

- **생성**: 평일만 허용(토/일 거부), 같은 (날짜, 유형)이면 기존 배식 반환, `MealTypeSetting`의 기본 계획식수/배식시간 복사
- **수정**: 계획식수(0 이상)/배식시간(`TimeInput24`)/콘셉트/비고
- **계획식수 변경 시** `quantity_total = per_100 × planned / 100` 재계산 (per_100이 null이 아닌 행만, `QuantityCalculator` 사용)
- **삭제**: cascade (하위 메뉴/보존식/실제식수 포함)

## 6. 메뉴 추가

- **단건**: 활성 메뉴만, 중복 거부("이미 식단에 추가된 메뉴입니다."), 첫 주찬 자동 대표 지정
- **일괄**: 빈 요청/요청 내 중복 메뉴/중복 정렬순서/기존 중복/비활성 메뉴/잘못된 레시피 검증 후 트랜잭션으로 추가
- **레시피 선택 규칙**: 명시된 레시피 → 활성 기본 → 활성 첫 번째 → 없음
- **다른 메뉴의 레시피** 지정 거부

## 7. Snapshot 처리

- 메뉴 추가 시 `MenuNameSnapshot`/`RecipeId`/`RecipeNameSnapshot`/`RecipeVersionSnapshot` 복사
- 재료는 `IngredientId`/`IngredientNameSnapshot`/`QuantityPer100`/`QuantityTotal`/`Unit`/`SortOrder` 복사
- `QuantityTotal = per_100 × planned / 100`, 단위는 `RecipeIngredient.Unit` → `Ingredient.DefaultUnit` 순
- 이후 기준 메뉴/레시피/재료를 수정해도 스냅샷 불변 (테스트 검증)
- `IngredientId = null`이어도 `IngredientNameSnapshot`으로 보존 (발주 단계 규칙 유지)

## 8. Recipe 변경

- 기존 재료 스냅샷 **전체 삭제 후 새 레시피 재료 전체 복사** (병합 아님)
- 사용자 수정 재료는 레시피 변경으로 교체됨 — UI에서 "기존 재료는 새 레시피 재료로 교체됩니다." 안내

## 9. Meal Editor (식단 편집 일괄 저장)

- 배식 기본정보 + 메뉴별 비고/대표/재료를 한 번에 저장 (트랜잭션)
- 재료는 **전체 교체**(delete + insert)
- `total`만 입력 → `per_100 = total × 100 / planned` 역산 (`QuantityCalculator`)
- 대표 메뉴는 **첫 True만 인정**
- 빈 재료 → 전체 삭제

## 10. 보존식

- MealService당 1건 (1:1)
- 완료 체크 → `CompletedAt = 현재 시각`, 해제 → null
- 기존 `CompletedAt` 있으면 완료 상태 표시

## 11. 실제 식수

- MealService당 1건, 보존식과 **독립 저장**
- 값 입력 → `RecordedAt = 현재 시각`, 비우면 null
- 음수 거부

## 12. WPF 구조

```text
MainWindow
├─ 좌측 네비게이션 (주간 급식 운영 / 기준정보 3종)
└─ ContentControl

WorkspaceView → WorkspaceViewModel
├─ 상단 툴바: 이전/이번 주/다음, 기간 라벨, 표시 기간(1~8주), 집중 작성
├─ 주간 보드: 월~금 컬럼 × N주, 배식 카드(유형/계획/메뉴/상태), + 배식
└─ 편집 패널 (TabControl 4모드)
    ├─ 식단 작성: 계획/시간/콘셉트/비고 + 메뉴 목록(대표/레시피/↑↓/삭제) + 재료 DataGrid
    ├─ 조리지시: 메뉴별 조리지시/조리비고
    ├─ 보존식 기록: 관리자/온도/채수자/시간/일시/비고/완료
    └─ 실제 식수: 식수/비고

MenuPickerDialog → MenuPickerDialogViewModel (검색/역할 필터/복수 선택/레시피 선택)
RecipePickerDialog → RecipePickerDialogViewModel
```

- CommunityToolkit.Mvvm, `AsyncRelayCommand` (DB 작업 비동기, UI 스레드 차단 없음)
- Code-behind: 화면 전환/집중모드 이벤트 전달만
- Dirty 상태에서 다른 배식 선택 시 확인 대화상자
- 업무 오류는 메시지, 예상 외 오류는 로깅
- 재료명 ComboBox는 **단일 Text 바인딩** (ID는 재료명에서 파생) — 이중 바인딩 충돌 제거

## 13. 집중 작성 모드

- 좌측 네비게이션 숨김 (MainWindow NavColumn Width=0)
- 1주만 표시, 월~금 전체 폭 확대
- 이전/다음 주 이동 가능
- On/Off 시 선택 날짜/편집 상태 유지 (주간 로드만 갱신)

## 14. 신규 테스트 (41개)

| 테스트 클래스 | 수 | 검증 |
| --- | --- | --- |
| WorkspaceServiceTests | 18 | 배식 생성(평일/기본값 복사/중복/주말 거부), 기간 조회(월요일 보정/그룹핑), 계획식수 재계산, 배식 수정/삭제, 보존식(저장/완료 토글/1건), 실제식수(recorded_at/음수/독립) |
| WorkspaceMenuTests | 23 | 단건 추가+스냅샷, 주찬 대표, 중복/비활성/타메뉴 레시피 거부, 레시피 없는 메뉴, 일괄 추가(다건/빈/중복/기존중복/비활성/타메뉴 레시피), 스냅샷 불변, 레시피 변경(교체), Meal Editor(콘셉트/per100 역산/재료교체/빈재료/대표 첫True), 삭제/순서, 선택기(검색/역할/AlreadyAdded/250건 검색) |

- 실제 SQLite 엔진 사용, 기존 pytest(test_meal_editor.py/test_menu_picker.py) 시나리오 이식
- 기존 98개 유지 + 신규 41개 = **139개 전부 통과**

## 15. 기존 Python과 동등성

- `weeks`(월요일 보정, 5일, MEAL_SORT 정렬), `create_service`(평일/중복 반환/기본값 복사), `update_service`(재계산), `add_menu`(주찬 대표), `batch_add_menus`(검증 순서/트랜잭션), `_select_recipe`, `_copy_recipe_to_service_menu`, `change_service_menu_recipe`, `update_service_menu`(대표 단일), `update_service_menu_ingredients`(역산), `save_meal_editor`(대표 첫True/재료 교체/역산), `delete_service_menu`(재정렬), `reorder_menus`(목록 일치), `preservation`(completed_at), `actual`(recorded_at), `picker_list_menus`(검색/역할/already_added)

## 16. 의도적으로 다른 UX

| 항목 | 기존 Web | Windows |
| --- | --- | --- |
| 오류 표현 | HTTP 400/404/409 | `WorkspaceException` + 메시지 |
| 화면 | 웹 SPA (픽셀 복제 아님) | WPF 주간 보드 + 편집 패널 (업무 구조 유지) |
| 메뉴 선택기 | 모달 | 별도 Dialog Window |
| 집중 모드 | 좌측 메뉴 숨김 | 네비게이션 숨김 + 1주 표시 |
| 레시피 변경 | 드롭다운 | Dialog 선택 + 교체 안내 메시지 |
| 표시 기간 | 1~8주 (웹 쿼리) | ComboBox 1/2/4/6/8 |

## 17. 미확인 사항

- **M1**: 주말 데이터가 기존 DB에 존재할 경우 조회는 되지만 화면 신규 생성은 평일 제한 — 이관 단계에서 확인 필요
- **M2**: `is_representative`가 문서 출력에 미치는 영향은 문서 단계에서 확인 예정
- **M3**: 집중 모드에서 편집 패널 폭 — 1주 표시 시 보드가 넓어지지만 편집 패널은 고정 430px (추후 조정 가능)
- **M4**: 메뉴 선택기 200건 초과 시 검색어 기반으로 동작 — 전체 목록 스크롤은 미지원 (의도)
