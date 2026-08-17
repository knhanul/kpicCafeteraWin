# 07. 이관 대응표 (Migration Mapping)

> 기존 기능 → Python/Web 구현 → Windows 구현 예정 → 재사용/재작성 판단

## 1. 기준정보

| 기존 기능 | Python/Web 구현 | Windows 구현 예정 | 재사용 대상 | 재작성 대상 |
| --- | --- | --- | --- | --- |
| 메뉴 기준정보 CRUD | `routers/master.py` + `view-master` | `MasterDataService` + `MasterMenuView` | 업무 규칙(이름 중복 409, 미사용 처리, canonical_name) | API→서비스, JS→ViewModel, HTML→XAML |
| 재료 기준정보 CRUD | `routers/master.py` + `view-master` | `MasterDataService` + `MasterIngredientView` | 업무 규칙(중복 409, 미사용 처리, stat_group, kg_factor) | 상동 |
| 재료 별칭 | `routers/master.py::add_alias` | `MasterDataService` | 별칭 중복 시 소유 변경 규칙 | 상동 |
| 메뉴 역할/통계분석군/단위 코드 | `routers/master.py::codes` (하드코딩) | Domain enum/코드 목록 | 코드 값 목록 | 하드코딩 → enum/상수 |
| 다중 레시피 | `routers/master.py` (composition_key, version, is_default) | `MasterDataService` + `RecipeEditor` | composition_key 규칙, 버전 규칙, 기본 레시피 대체 규칙 | 상동 |
| 레시피 재료 엑셀형 그리드 | `app.js` (그리드 + 붙여넣기) | `RecipeEditorView` (DataGrid) | 붙여넣기 파싱 규칙 | JS 그리드 → WPF DataGrid |
| 배식 기본값 | `routers/master_data.py::meal-service-defaults` | `MealTypeSettingService` + `MealDefaultsView` | 기본값 규칙(중식 400/11:40, 석식 100/17:30) | 상동 |

## 2. 급식 운영

| 기존 기능 | Python/Web 구현 | Windows 구현 예정 | 재사용 대상 | 재작성 대상 |
| --- | --- | --- | --- | --- |
| 주간 식단 조회 (2주) | `routers/workspace.py::weeks` | `MealServiceService.GetWeeks` + `WorkspaceView` | 월요일 보정, 평일 5열, 2주 기본 규칙 | API→서비스, JS→ViewModel |
| 배식 생성/수정/삭제 | `routers/workspace.py::services` | `MealServiceService` | 평일만 생성, 중복 시 기존 반환, 기본값 복사, 계획식수 변경 시 quantity_total 재계산 | 상동 |
| 메뉴 추가 (단건/일괄) | `routers/workspace.py::add_menu/batch_add_menus` | `MealServiceService.AddMenu` | 레시피 선택 규칙, 중복 409, 스냅샷 복사, 주찬 대표 지정 | 상동 |
| 메뉴 선택기 | `routers/master.py::picker_list_menus` | `MenuPickerView` + `MasterDataService` | 검색/역할 필터, already_added 표시 | JS 모달 → WPF 대화상자 |
| 레시피 변경 | `routers/workspace.py::change_service_menu_recipe` | `MealServiceService.ChangeRecipe` | 교체 방식(전체 삭제 후 재복사) | 상동 |
| 식단 편집 일괄 저장 | `routers/workspace.py::save_meal_editor` | `MealServiceService.SaveMealEditor` | 재료 전체 교체, per_100 역산, 대표 첫 True 승인 | 상동 |
| 메뉴 삭제/순서 변경 | `routers/workspace.py` | `MealServiceService` | sort_order 재계산, 목록 일치 검증 | 상동 |
| 조리지시서 작성 | `routers/workspace.py` (cooking_instruction/note) | `MealServiceService` + `CookingView` | 필수 아님, note fallback 규칙 | 상동 |
| 보존식 기록 | `routers/workspace.py::preservation` | `MealServiceService` + `PreservationView` | completed_at 기록 규칙 | 상동 |
| 실제 식수 | `routers/workspace.py::actual` | `MealServiceService` + `ActualView` | 독립 저장, recorded_at 규칙 | 상동 |
| 집중 작성 모드 | `app.js` (focus mode) | `WorkspaceView` (전체 화면 모드) | UX 개념 | JS 레이아웃 → WPF 레이아웃 |
| 시간 입력 (TimeInput24) | `app.js` (normalizeTime24 등) | `TimeInput24` WPF 커스텀 컨트롤 | 정규화 규칙 (1140→11:40 등) | JS → C# 로직 + WPF 컨트롤 |

## 3. 발주

| 기존 기능 | Python/Web 구현 | Windows 구현 예정 | 재사용 대상 | 재작성 대상 |
| --- | --- | --- | --- | --- |
| 발주 조회 (식단 집계) | `routers/orders.py::list_orders` | `OrderService.GetOrders` + `OrdersView` | (사용일, 재료) 집계, id/name 키, 식단 변경 시 사용자 입력 보존, in_plan=false 유지 | 상동 |
| 발주 항목 저장 | `routers/orders.py::save_order_items` | `OrderService.SaveItems` | upsert 규칙, 상태 3종 검증 | 상동 |
| 묶음 발주 | `routers/orders.py::create_order_group` | `OrderService.CreateGroup` | 그룹 생성 + ordered 전환 + 날짜 동기화 + 합계 | 상동 |
| 일괄 변경 | `routers/orders.py::bulk_update_items` | `OrderService.BulkUpdate` | 발주일/배송일/상태 일괄 | 상동 |
| 재료별/사용일별 보기 | `app.js` (orders-view-tabs) | `OrdersView` (탭/그룹핑) | 표시 규칙 | JS → XAML |

## 4. 문서

| 기존 기능 | Python/Web 구현 | Windows 구현 예정 | 재사용 대상 | 재작성 대상 |
| --- | --- | --- | --- | --- |
| 문서 DTO | `document_dtos.py` | Documents 프로젝트 DTO | DTO 구조/필드 규칙 | pydantic → C# record |
| 문서 빌더 | `document_builders.py` | Documents 프로젝트 빌더 | 주 그룹핑, 정렬, null 보존 | 상동 |
| HWPX 엔진 | `hwpx_engine.py` (1,250줄) | Documents 프로젝트 HWPX 엔진 | 플레이스홀더 규칙, 반복 페이지 규칙, 검증 규칙 | **Python → C# 전체 재작성** |
| HWPX 템플릿 파일 | `docs/template/*.hwpx` 3종 | 동일 파일 재사용 | **파일 그대로 재사용** | 없음 |
| 반복 페이지 설정 | `template-page-config.json` | Documents 프로젝트 설정 | 페이지 용량/로컬 슬롯/페이지 규칙 | JSON → C# 설정 |
| PDF 변환 | `hwpx_pdf_renderer.py` (COM 서브프로세스) | Documents 프로젝트 COM 렌더러 | COM 호출 시퀀스 (Open/SaveAs/RegisterModule) | Python 서브프로세스 → WPF COM |
| HTML 미리보기 | `templates/documents/*.html` | WPF 미리보기 (WebView2 또는 재설계) | 문서 레이아웃 참고 | HTML → WPF |
| HWPX 템플릿 관리 | `routers/master_data.py` | `TemplateManagementService` + `TemplateView` | 검증 규칙, 활성 1개, 버전, SHA-256 | 상동 |
| 출력 이력 표시 | `routers/documents.py::_mark_output` | `DocumentService` | meal_plan_output_at/cooking_output_at | 상동 |
| 문서 파일명 규칙 | `document_hwpx.py::filename_for_dto` | Documents 프로젝트 | `식단표_YYYYMMDD_YYYYMMDD.hwpx` 규칙 | 상동 |

## 5. 통계

| 기존 기능 | Python/Web 구현 | Windows 구현 예정 | 재사용 대상 | 재작성 대상 |
| --- | --- | --- | --- | --- |
| 운영 대시보드 | `dashboard_service.py` | `DashboardService` + `DashboardView` | KPI/이상치/추세 조합 구조 | 상동 |
| 식수 통계 | `statistics_service.py` | `StatisticsService` | ±10%/±15% 판정, 56일 중앙값, 지연 입력 | 상동 |
| 메뉴 통계 | `menu_statistics.py` | `StatisticsService` | 반복(14일 2회/28일 3회), 미사용 90일, 동시 제공 | 상동 |
| 식재료 통계 | `ingredient_statistics.py` | `StatisticsService` | 사용량 g 합산, per_100 역산, 미사용 | 상동 |
| 운영 기록 통계 | `operations_statistics.py` | `StatisticsService` | 완료율, 기록 누락, 지연 입력 | 상동 |
| 주간 참고 통계 (legacy) | `stats_service.py` | `StatisticsService` | 단백질 구성, 재료군 kg 환산 | 상동 |
| 차트 표시 | `app.js` (DOM 렌더링) | WPF 차트 (LiveCharts2 등) | 데이터 구조 | JS 차트 → WPF 차트 |

## 6. 시스템

| 기존 기능 | Python/Web 구현 | Windows 구현 예정 | 재사용 대상 | 재작성 대상 |
| --- | --- | --- | --- | --- |
| XLSX 이관 | `importer.py` + `xlsx_reader.py` | `ImportService` + `MigrationXlsxReader` | 시트 구조, replace/merge, 레시피 그룹핑, 스냅샷 역산 | Python → C# 재작성 |
| DB 백업 | `routers/admin.py` (pg_dump) | `BackupService` (SQLite 파일 복사) | 백업 목록/체크섬/감사 로그 구조 | pg_dump → 파일 복사 |
| Excel 아카이브 | `routers/admin.py::_build_excel` (openpyxl) | `ArchiveService` + `ExcelArchiveExporter` (ClosedXML) | 9시트 구조/컬럼 | openpyxl → ClosedXML |
| 미리보기 토큰 | `document_previews` 테이블 | 불필요 (로컬 앱) | - | **제거 가능** (로컬에서는 즉시 생성) |
| 감사 로그 | `audit_logs` | `AuditLogService` | 구조/액션 명명 | 상동 |
| 사용자 관리 | `routers/users.py` | `UserService` + `UsersView` | 최소 admin 1명, 본인 비활성 금지, must_change_password | 상동 |
| 인증 | 세션 쿠키 + PBKDF2 | 로컬 로그인 + PBKDF2 (포맷 호환) | 해시 포맷 | 세션 → 로컬 세션 |
| import_jobs | `import_jobs` 테이블 | `ImportService` (작업 상태) | 상태 머신 (PREVIEWED/INVALID/COMPLETED/FAILED) | 상동 |

## 7. 테스트

| 기존 테스트 | 검증 내용 | Windows 구현 예정 |
| --- | --- | --- |
| `test_core.py` | 이관 워크북 시트/행 수, 레시피 구성 그룹핑 | `ImportServiceTests` |
| `test_master_data.py` | 배식 기본값, HWPX 검증, 템플릿 관리 | `MealDefaultsTests`, `HwpxValidationTests`, `TemplateManagementTests` |
| `test_meal_editor.py` | 일괄 저장, 재료 교체, per_100 역산 | `MealServiceTests` |
| `test_menu_picker.py` | 선택기 검색/필터, 일괄 추가 검증 | `MenuPickerTests`, `MealServiceTests` |
| `test_multi_recipe.py` | 다중 레시피, 구성 키, 중복 409 | `RecipeTests` |
| `test_orders.py` | 발주 집계/upsert/묶음/일괄 | `OrderServiceTests` |
| `test_time_input24.py` | 시간 정규화 | `TimeInput24Tests` (C# 로직) |
| `test_document_builders.py` | 문서 DTO 생성 | `DocumentBuilderTests` |
| `test_document_hwpx.py` | HWPX 다운로드/파일명/출력 시각 | `HwpxDocumentTests` |
| `test_hwpx_engine.py` | 템플릿 검증/렌더링 round-trip | `HwpxEngineTests` |
| `test_hwpx_output_system.py` | 실제 데이터셋 출력, PDF-first | `HwpxOutputSystemTests` |
| `test_hwpx_pdf_renderer.py` | PDF 파이프라인 | `PdfRendererTests` (Fake 렌더러) |
| `test_hwpx_repeat_pages.py` | 실제 템플릿 반복 페이지 수 | `HwpxRepeatPagesTests` (실제 템플릿 복사본 사용) |

## 8. 재사용/재작성 요약

### 그대로 재사용 (파일/데이터)
- HWPX 반복 페이지 템플릿 3종 (`docs/template/*.hwpx`)
- 참조 PDF 샘플 (`docs/reference/*.pdf`)
- 업무 규칙 (04-business-rules.md에 정리)
- 기존 DB 데이터 (이관 경유)

### 규칙 재사용 + 구현 재작성
- 모든 업무 규칙 (R1~R24)
- 플레이스홀더 규칙/반복 페이지 규칙
- 문서 DTO 구조
- 통계 계산 규칙
- 시간 정규화 규칙
- PBKDF2 해시 포맷

### 전체 재작성
- HWPX 엔진 (Python → C#)
- UI 전체 (HTML/JS → WPF/XAML)
- API 레이어 (FastAPI → Application 서비스)
- XLSX 파서/생성기 (표준 라이브러리/openpyxl → ClosedXML)
- PDF 변환 (Python COM 서브프로세스 → WPF COM)
- 백업 (pg_dump → SQLite 파일 복사)

### 제거 가능
- `document_previews` 토큰 시스템 (로컬 앱에서는 불필요)
- 세션/쿠키 인증 (로컬 로그인으로 대체)
- Nginx/Docker 배포 (설치형 앱)
- `schema_upgrade.py` 인플레이스 업그레이드 (EF Core 마이그레이션으로 대체)
