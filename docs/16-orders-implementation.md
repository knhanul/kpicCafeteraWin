# 16. Orders(발주 관리) 구현 기록

> 5단계 발주 관리 실제 구현 내용.
> 작성일: 2026-08-16

## 1. Required / Suggested / OrderQuantity 구조

| 개념 | 필드 | 계산 |
| --- | --- | --- |
| 필요량 | `RequiredQuantity` | 식단 Snapshot(`MealServiceMenuIngredient.QuantityTotal`)을 (사용일, 재료) 기준으로 자동 집계 |
| 추천 발주량 | `SuggestedOrderQuantity` | `OrderQuantityCalculator`로 자동 계산 (판매 포장단위 반영) |
| 실제 발주량 | `OrderQuantity` | 사용자가 자유롭게 수정, 시스템이 덮어쓰지 않음 |

- `RequiredQuantity`는 조회 시마다 **항상 최신 식단에서 재집계**
- `OrderQuantity`/`OrderUnit`/`OrderDate`/`DeliveryDate`/`Status`/`OrderNote`는 저장값 유지
- 신규 항목 기본값: 판매 포장단위 계산 가능 → 추천량, 없으면 필요량. 발주일 = 사용일-1, 배송일 = 사용일, 상태 = pending

## 2. 판매 포장단위

- `Ingredient.PurchasePackageQuantity`(double?) + `Ingredient.PurchasePackageUnit`(string?) 추가 (nullable)
- 예: 데미글라스소스 기본단위 g, 판매 포장수량 2, 판매 포장단위 kg
- 판매 포장 정보가 없는 재료도 정상 사용 (자유 발주 대상)
- IngredientView에 "판매 포장수량 [ ] 판매 포장단위 [ ]" 입력 추가 (필수 아님)

## 3. 단위 환산

`OrderQuantityCalculator` (`src\KpicCafeteria.Domain\Domain\OrderQuantityCalculator.cs`)

- 지원: `g ↔ kg`, `ml ↔ L` (대소문자 무시)
- 미지원: 개/봉/팩/박스/통 등 명확한 변환계수가 없는 단위 → **임의 환산하지 않음**, 추천 null + UI "포장단위 확인 필요"
- `SuggestedOrderQuantity = ceil(필요량(판매단위로 환산) / 포장수량) × 포장수량`
- 예: 800g + 2kg → 2kg, 4.1kg + 2kg → 6kg, 4kg + 2kg → 4kg, 1500ml + 1L → 2L
- 포장단위 없음 → `SuggestedOrderQuantity = RequiredQuantity`

## 4. SourceMenus

- 집계 DTO에 출처 메뉴 제공: `ServiceDate`/`MealType`/`MealTypeName`/`MenuNameSnapshot`/`Quantity`/`Unit`
- 메뉴명은 **`MealServiceMenu.MenuNameSnapshot`** 기준 (현재 Menu.Name 아님)
- 예: 양파 15kg → 제육볶음 10kg + 육개장 5kg
- UI 하단 "사용 메뉴" 패널에 선택 항목의 출처 표시

## 5. 재료별 / 사용일별 UI

- 동일 데이터 모델(`OrderItemDto`) 사용, 중복 저장 테이블 없음
- 재료별: 재료명 → 사용일 정렬 (기본)
- 사용일별: 사용일 → 재료명 정렬
- 추가 정렬: 발주일/배송일/상태

## 6. OrderItem / OrderGroup

- `OrderItem`: (service_date, ingredient_id) UNIQUE — ingredient_id NULL이면 (service_date, 재료명 스냅샷)으로 구분 (기존 규칙)
- `OrderNote` 추가 (nullable, 발주 비고 — 재고관리 용도 아님)
- `OrderGroup`: 같은 재료의 여러 사용일 항목 묶음. `TotalRequiredQuantity` = 항목 필요량 합계
- 묶음 발주 완료 시 그룹 소속 항목 `Status=ordered`, `OrderDate`/`DeliveryDate` 그룹 값 동기화
- **동일 재료 검증**: IngredientId 있으면 ID, 없으면 재료명 스냅샷 기준 — 서로 다른 재료 묶음 거부

## 7. 사용자 입력 보존

- 식단 변경 → `RequiredQuantity`만 최신화, 사용자 입력 필드 유지 (테스트 검증)
- 식단에서 제거된 항목 → 자동 삭제하지 않고 `InPlan=false` + UI "식단에서 제외됨" 표시
- upsert 키: IngredientId 있음 → (ServiceDate, IngredientId), 없음 → (ServiceDate, 재료명 스냅샷)

## 8. Repository

- `IOrderRepository` / `OrderRepository` / `OrderRepositoryFactory`
- 작업 단위별 DbContext (`IDbContextFactory`), 트랜잭션 지원
- `GetServicesWithIngredientsInRangeAsync`: 식단+메뉴+재료 스냅샷+재료 참조 (집계 원본)
- `GetItemsInRangeAsync`: 저장된 발주 항목 + OrderGroup + Ingredient (추천 계산용)
- `FindItemAsync`: upsert 키 조회

## 9. OrderService

`src\KpicCafeteria.Application\Orders\OrderService.cs`

- `GetOrdersAsync`: 기간 조회 → 식단 집계 + 저장 항목 병합 → 정렬 (재료명, 사용일)
- `SaveItemsAsync`: 다건 upsert (Transaction)
- `CreateOrderGroupAsync`: 동일 재료 검증 → 그룹 생성 → 항목 연결/상태 동기화 (Transaction)
- `BulkUpdateAsync`: OrderDate/DeliveryDate/Status 중 하나 이상 변경, 변경 항목 없으면 거부 (Transaction)
- 업무 오류: `OrderException` 파생 (`InvalidOrderStatusException`/`NoChangesToApplyException`/`MixedIngredientGroupException`/`EmptyOrderSelectionException`)

## 10. WPF

```text
MainWindow
├─ 좌측 네비게이션 (주간 급식 운영 / 발주 관리 / 기준정보 3종)
└─ ContentControl

OrdersView → OrdersViewModel
├─ 기간 툴바: 2주(기본)/1주/1개월/직접지정, From/To, 조회
├─ 보기 모드: 재료별 / 사용일별 + 정렬 + 전체 선택
├─ DataGrid: 선택/재료/사용일/필요량/판매단위/추천량/실제발주량/발주일/배송일/상태/비고
├─ 사용 메뉴 패널 (선택 항목의 SourceMenus)
└─ 하단: 묶음 발주 / 일괄 변경 / 변경사항 저장

GroupOrderDialog → GroupOrderDialogViewModel (필요량 합계/추천량 표시 + 발주량/발주일/배송일 확정)
BulkUpdateDialog → BulkUpdateDialogViewModel (발주일/배송일/상태 중 입력 항목만 적용)
```

- 상태 ComboBox: 미처리(pending)/발주완료(ordered)/발주안함(skipped) — DB 코드값 기존 호환
- Dirty 상태: 행 편집 시 `IsDirty`, 화면 이동 시 MainWindow가 확인 대화상자
- 저장 후 재조회 → RequiredQuantity 최신화
- 추천량 열: 호환 불가 시 "포장단위 확인 필요" 표시 (초록)

## 11. Migration

`20260816000000_AddProcurementFields`

- `ingredients.purchase_package_quantity` (REAL, nullable)
- `ingredients.purchase_package_unit` (TEXT, nullable)
- `order_items.order_note` (TEXT, nullable)
- 기존 데이터 삭제/초기화 없음

## 12. 테스트

| 테스트 클래스 | 수 | 검증 |
| --- | --- | --- |
| OrderQuantityCalculatorTests | 18 | 포장단위 A/B/C, 포장단위 없음, g/kg·ml/L 변환, 호환 불가(개/봉/박스/통), 추천 단위, 경계값 |
| OrderServiceTests | 23 | 동일 날짜 집계, 날짜 분리, IngredientId null 집계, 사용자 입력 보존, 식단 제외(InPlan=false), 신규 기본값(추천/필요량/호환불가), 사용자 Override, SourceMenus, 묶음 발주(연결/상태/날짜/동일재료 검증/이름키), 일괄 변경(상태/날짜/변경없음/잘못된상태), OrderNote 저장, 이름키 upsert 중복 방지, 대량 데이터(320재료/305항목), 기간 역전, Migration |

- 기존 139개 유지 + 신규 41개 = **180개 전부 통과**
- 실제 SQLite 엔진(in-memory), EF InMemory Provider 미사용
- `test_orders.py` 7개 시나리오 전부 이식 (원본 경로 주석 명시)

## 13. 재고를 의도적으로 제외한 설계

- 재고량/잔여량/입고/출고/자동 차감/수불/재고이력/창고/유통기한 **미구현**
- `AvailableStockQuantity`/`CurrentStock`/`RemainingStock`/`NetRequiredQuantity`/`StockQuantity` 필드 **미추가** (Domain/DB/DTO/UI 전부)
- 발주 계산은 현재 식단 필요량 기준으로만 수행
- `OrderQuantityCalculator`에 재고 계산 로직 없음

## 14. 기존 Python과 동등성

- `list_orders`(집계/병합/InPlan=false/정렬), `save_order_items`(upsert), `create_order_group`(연결/ordered/날짜 동기화), `bulk_update`(변경 항목 검증/트랜잭션), `_load_plan_items`(ingredient_id 또는 name: 스냅샷 키), `_default_order_date`(사용일-1)
- Windows 추가: 판매 포장단위 기반 `SuggestedOrderQuantity`, `OrderNote`, 재료별/사용일별 보기, Dirty 확인

## 15. 미확인 사항

- **M1**: 실제 UI 클릭 시나리오(묶음 발주 대화상자, 일괄 변경 대화상자, DataGrid 편집)는 자동 클릭 불가 — `docs/17` 수동 체크리스트 참조
- **M2**: 200건 초과 시 목록 전체 표시 (스크롤) — 기간을 좁혀 조회하는 구조 (의도)
- **M3**: 묶음 발주 대화상자의 추천량은 선택 항목의 첫 행 판매 포장단위 기준 — 항목 간 포장단위가 다르면 사용자가 직접 수정
- **M4**: 발주일/배송일은 DatePicker(DateTime)로 표시, 저장 시 DateOnly 변환
