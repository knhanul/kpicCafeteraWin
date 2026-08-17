# 02. 기존 프로그램 전체 기능 목록

> 각 기능에 대해 화면 / API / Backend / DB / 테스트를 연결한다.

## 1. 인증 및 사용자

### 1-1. 로그인 / 로그아웃
- **기능 설명**: 사용자 ID/비밀번호로 로그인, 세션 쿠키 발급. 비활성 사용자 차단. 로그인 시 `last_login_at` 갱신.
- **화면**: `login.html`, 사이드바 로그아웃 버튼
- **API**: `POST /api/auth/login`, `POST /api/auth/logout`, `GET /api/auth/me`
- **Backend**: `routers/auth.py`, `security.py` (PBKDF2-SHA256, 240,000회)
- **DB**: `users`
- **테스트**: 없음 (직접 확인 항목)

### 1-2. 비밀번호 변경
- **기능 설명**: 현재 비밀번호 확인, 신규 비밀번호 8자 이상, 사용자 ID와 동일 금지, 확인 일치 검증. 변경 시 `must_change_password=False`.
- **화면**: 사이드바 `비밀번호 변경` 버튼
- **API**: `POST /api/auth/change-password`
- **Backend**: `routers/auth.py`
- **DB**: `users`
- **테스트**: 없음

### 1-3. 사용자 관리 (admin)
- **기능 설명**: 사용자 목록/검색, 생성(초기 비밀번호, `must_change_password=True`), 수정(이름/권한/활성), 비밀번호 초기화. 본인 계정 비활성 금지, 마지막 admin 권한 제거 금지. 모든 변경 감사 로그 기록.
- **화면**: `view-users`
- **API**: `GET/POST /api/users`, `PUT /api/users/{id}`, `POST /api/users/{id}/reset-password`
- **Backend**: `routers/users.py`
- **DB**: `users`, `audit_logs`
- **테스트**: 없음

## 2. 기초 데이터 구축 (XLSX 이관)

### 2-1. XLSX 업로드 및 검증 (preview)
- **기능 설명**: 7개 필수 시트(`01_배식설정`~`07_식단재료_이관`) 존재 확인, 시트별 행 수 집계. 오류 시 `INVALID` 상태.
- **화면**: `view-master-data` → `기초 데이터 구축` 탭
- **API**: `POST /api/setup/import/preview`
- **Backend**: `routers/setup.py`, `importer.py` (`MigrationImporter.preview`), `xlsx_reader.py`
- **DB**: `import_jobs`
- **테스트**: `test_core.py::test_migration_workbook_has_expected_counts`, `test_reader_reads_lunch_and_dinner`

### 2-2. 이관 적용 (replace / merge)
- **기능 설명**: `replace`는 업무 데이터 전체 삭제 후 재구축(사용자/템플릿/감사로그 보존), `merge`는 기존 데이터 유지하며 병합. 배식설정→메뉴→재료→별칭→레시피→식단이력→식단재료 순서로 처리. 레시피는 재료 구성(composition_key) 기준 그룹핑, 수량·단위 변경은 기존 레시피 수정. 식단재료는 당시 수량을 스냅샷으로 보존하고 `quantity_per_100` 역산.
- **화면**: `view-master-data` → `기초 데이터 구축` 탭
- **API**: `POST /api/setup/import/apply`, `GET /api/setup/import/jobs`
- **Backend**: `routers/setup.py`, `importer.py` (`MigrationImporter.apply`)
- **DB**: `meal_type_settings`, `menus`, `ingredients`, `ingredient_aliases`, `recipes`, `recipe_ingredients`, `meal_services`, `meal_service_menus`, `meal_service_menu_ingredients`, `audit_logs`, `import_jobs`
- **테스트**: `test_core.py::test_group_recipe_rows_by_composition_*`

## 3. 기준정보 관리

### 3-1. 메뉴 기준정보
- **기능 설명**: 메뉴 등록/수정/삭제(미사용 처리). 이름 중복 검증(409). `canonical_name`(통계집계메뉴명), `role`(메뉴역할), `review_status`. 삭제 시 레시피도 함께 미사용 처리.
- **화면**: `view-master` → `메뉴·레시피` 탭
- **API**: `GET/POST /api/master/menus`, `GET/PUT/DELETE /api/master/menus/{id}`
- **Backend**: `routers/master.py`
- **DB**: `menus`, `recipes`
- **테스트**: `test_multi_recipe.py` (간접)

### 3-2. 재료 기준정보
- **기능 설명**: 재료 등록/수정/삭제(미사용 처리). 이름 중복 검증(409). `stat_group`(통계분석군), `default_unit`, `kg_factor`, `analysis_excluded`. 별칭 등록(중복 별칭은 소유 재료 변경).
- **화면**: `view-master` → `재료` 탭
- **API**: `GET/POST /api/master/ingredients`, `GET/PUT/DELETE /api/master/ingredients/{id}`, `POST /api/master/ingredients/{id}/aliases`
- **Backend**: `routers/master.py`
- **DB**: `ingredients`, `ingredient_aliases`
- **테스트**: 없음

### 3-3. 다중 레시피
- **기능 설명**: 같은 메뉴에 재료 구성이 다른 여러 레시피 등록. `composition_key` = 재료 ID 정렬 집합 (수량·단위 제외). 같은 구성 중복 등록 409. 버전은 메뉴별 순차 증가. `is_default`는 메뉴당 1개. 기본 레시피 비활성화 시 대체 레시피 자동 지정. 미등록 재료명은 `자동등록-분류필요` 상태로 자동 생성.
- **화면**: `view-master` → `메뉴·레시피` 탭 (레시피 목록 + 엑셀형 재료 그리드)
- **API**: `POST /api/master/menus/{id}/recipes`, `PUT/DELETE /api/master/recipes/{id}`, `POST /api/master/recipes/{id}/default`, `PUT /api/master/menus/{id}/recipe` (legacy)
- **Backend**: `routers/master.py` (`resolve_recipe_items`, `composition_key`, `set_default_recipe`, `replace_recipe_items`)
- **DB**: `recipes`, `recipe_ingredients`, `ingredients`
- **테스트**: `test_multi_recipe.py` (구성 분리, 수량 변경 시 기존 레시피 수정, 중복 409)

### 3-4. 배식 기본값
- **기능 설명**: 배식유형(중식/석식)별 기본 계획식수, 기본 배식시간, 사용여부, 정렬순서 관리. 시간 형식 HH:MM 검증, 음수 인원 금지, 미존재 유형 404.
- **화면**: `view-master-data` → `배식 기본값 관리` 탭
- **API**: `GET/PUT /api/master-data/meal-service-defaults`
- **Backend**: `routers/master_data.py`
- **DB**: `meal_type_settings`
- **테스트**: `test_master_data.py::TestMealServiceDefaults`

## 4. 주간 급식 운영 (Workspace)

### 4-1. 주간 식단 조회
- **기능 설명**: `week_start` 기준 월요일부터 2주(1~8주) 표시. 평일 5열만. 날짜별 중식/석식 배식 카드. 주말 열 없음.
- **화면**: `view-workspace` (week-board)
- **API**: `GET /api/workspace/weeks?week_start=&weeks=`
- **Backend**: `routers/workspace.py::weeks`, `serializers.py::meal_service_dict`
- **DB**: `meal_services`, `meal_service_menus`, `preservation_records`, `meal_actuals`
- **테스트**: 없음 (직접 확인 항목)

### 4-2. 배식 생성/수정/삭제
- **기능 설명**: 평일만 생성 가능(주말 400). 같은 날짜+유형 중복 시 기존 반환. 생성 시 배식유형 기본값(계획식수/시간) 적용. 계획식수 변경 시 재료 `quantity_total` 재계산(`per_100 × planned / 100`).
- **화면**: `view-workspace` → 식단 작성 모드
- **API**: `POST /api/workspace/services`, `GET/PUT/DELETE /api/workspace/services/{id}`
- **Backend**: `routers/workspace.py`
- **DB**: `meal_services`, `meal_service_menu_ingredients`
- **테스트**: `test_meal_editor.py::test_concept_title_saved_via_update_service`

### 4-3. 메뉴 추가 (단건/일괄)
- **기능 설명**: 메뉴 선택기에서 검색/역할 필터, 이미 추가된 메뉴 표시. 추가 시 레시피 선택(기본 레시피 우선). 같은 메뉴 중복 추가 409. 일괄 추가는 요청 내 중복 메뉴/정렬순서 검증, 비활성 메뉴 404, 다른 메뉴의 레시피 400. 추가 시 레시피 재료를 스냅샷 복사하고 `quantity_total = per_100 × planned / 100` 계산. 첫 주찬 메뉴는 `is_representative` 자동 지정.
- **화면**: `view-workspace` → 메뉴 선택기 모달
- **API**: `GET /api/master/menus/picker`, `POST /api/workspace/services/{id}/menus`, `POST /api/workspace/services/{id}/menus/batch`
- **Backend**: `routers/master.py::picker_list_menus`, `routers/workspace.py::add_menu`, `batch_add_menus`, `_select_recipe`, `_copy_recipe_to_service_menu`
- **DB**: `meal_service_menus`, `meal_service_menu_ingredients`, `recipes`, `recipe_ingredients`
- **테스트**: `test_menu_picker.py` (전체), `test_multi_recipe.py` (간접)

### 4-4. 레시피 변경
- **기능 설명**: 식단에 추가된 메뉴의 레시피를 다른 버전으로 교체. 교체 시 기존 재료 스냅샷 삭제 후 새 레시피 재료로 재복사.
- **화면**: `view-workspace` → 식단 작성 모드
- **API**: `PUT /api/workspace/service-menus/{id}/recipe`
- **Backend**: `routers/workspace.py::change_service_menu_recipe`
- **DB**: `meal_service_menus`, `meal_service_menu_ingredients`
- **테스트**: 없음

### 4-5. 식단 편집 일괄 저장 (meal-editor)
- **기능 설명**: 배식 기본정보(계획식수/시간/콘셉트/비고) + 메뉴별 비고/대표메뉴/재료를 한 번에 저장. 재료는 전체 교체 방식(delete + insert). `quantity_total` 입력 시 `per_100` 역산. 대표메뉴는 첫 True만 승인(단일 대표).
- **화면**: `view-workspace` → 식단 작성 모드 (집중 작성 모드 포함)
- **API**: `PUT /api/workspace/services/{id}/meal-editor`
- **Backend**: `routers/workspace.py::save_meal_editor`
- **DB**: `meal_services`, `meal_service_menus`, `meal_service_menu_ingredients`
- **테스트**: `test_meal_editor.py` (콘셉트 저장, 재료 교체, 빈 재료 전체 삭제, per_100 계산)

### 4-6. 메뉴 삭제/순서 변경
- **기능 설명**: 메뉴 삭제 후 남은 메뉴 sort_order 재계산. 순서 변경은 전체 메뉴 ID 목록 일치 검증 후 재정렬.
- **화면**: `view-workspace` → 식단 작성 모드
- **API**: `DELETE /api/workspace/service-menus/{id}`, `POST /api/workspace/services/{id}/reorder`
- **Backend**: `routers/workspace.py`
- **DB**: `meal_service_menus`
- **테스트**: 없음

### 4-7. 조리지시서 작성
- **기능 설명**: 메뉴별 조리지시(`cooking_instruction`)와 조리비고(`cooking_note`) 입력. 필수 입력 아님. 저장 시 `meal-editor` 또는 `PUT /service-menus/{id}` 사용.
- **화면**: `view-workspace` → 조리지시서 모드
- **API**: `PUT /api/workspace/service-menus/{id}`, `PUT /api/workspace/services/{id}/meal-editor`
- **Backend**: `routers/workspace.py`
- **DB**: `meal_service_menus`
- **테스트**: 없음

### 4-8. 보존식 기록
- **기능 설명**: 채수일시, 관리자, 냉동고 온도, 폐기일시, 채수자, 채수시간, 비고. `completed` 체크 시 `completed_at` 기록. 미완료 시 null.
- **화면**: `view-workspace` → 보존식 기록 모드
- **API**: `GET/PUT /api/workspace/services/{id}/preservation`
- **Backend**: `routers/workspace.py`
- **DB**: `preservation_records`
- **테스트**: 없음

### 4-9. 실제 식수
- **기능 설명**: 실제 식수(`actual_count`)와 비고 입력. 입력 시 `recorded_at` 기록, 삭제 시 null. 보존식 기록과 독립 저장.
- **화면**: `view-workspace` → 실제 식수 모드
- **API**: `GET/PUT /api/workspace/services/{id}/actual`
- **Backend**: `routers/workspace.py`
- **DB**: `meal_actuals`
- **테스트**: 없음

### 4-10. 집중 작성 모드
- **기능 설명**: 좌측 시스템 메뉴/상단 헤더 숨김, 선택 주 1개만 표시, 이전/다음 주 이동, 글꼴/카드/입력창 확대, 선택·입력 상태 유지. (프론트엔드 기능)
- **화면**: `view-workspace` → `focus-toggle`
- **API**: 없음 (기존 API 재사용)
- **Backend**: 없음
- **DB**: 없음
- **테스트**: 없음 (docs/focus-mode-layout-analysis.md에 레이아웃 분석 존재)

## 5. 발주 관리

### 5-1. 발주 조회 (식단 집계)
- **기능 설명**: 기간 내 식단 재료를 (사용일, 재료) 기준 집계. 같은 날짜+재료는 여러 메뉴 수량 합산. `ingredient_id` 없는 재료는 `name:` 키로 구분. 저장된 OrderItem과 병합 — 식단 기준 `required_quantity`는 항상 최신, 사용자 입력(`order_quantity`/`order_date`/`delivery_date`/`status`)은 보존. 식단에서 사라진 항목도 `in_plan=false`로 유지(사용자 입력 무시 금지).
- **화면**: `view-orders` (재료별/사용일별 탭)
- **API**: `GET /api/orders?start_date=&end_date=`
- **Backend**: `routers/orders.py::list_orders`, `_load_plan_items`, `_load_stored_items`
- **DB**: `meal_services`, `meal_service_menus`, `meal_service_menu_ingredients`, `order_items`, `order_groups`
- **테스트**: `test_orders.py` (동일 날짜 집계, id/name 키 혼합, 날짜별 분리, 식단 변경 시 사용자 입력 보존, 식단 외 항목 유지)

### 5-2. 발주 항목 저장 (upsert)
- **기능 설명**: (사용일, 재료) 기준 upsert. 상태값은 `pending|ordered|skipped`만 허용. 재료 ID 없으면 (사용일, 이름) 기준.
- **화면**: `view-orders` (인라인 편집)
- **API**: `PUT /api/orders/items`
- **Backend**: `routers/orders.py::save_order_items`, `_upsert_order_item`
- **DB**: `order_items`
- **테스트**: `test_orders.py::test_save_order_items_upserts_and_preserves_user_input`

### 5-3. 묶음 발주
- **기능 설명**: 같은 재료의 여러 사용일 항목을 하나의 OrderGroup으로 묶음. 그룹에 `order_quantity`/`order_unit`/`order_date`/`delivery_date` 저장, 항목들은 `order_group_id` 연결 + `status=ordered` + 날짜 동기화. `total_required_quantity`는 항목 합계.
- **화면**: `view-orders` → 묶음 발주 버튼
- **API**: `POST /api/orders/group`
- **Backend**: `routers/orders.py::create_order_group`
- **DB**: `order_groups`, `order_items`
- **테스트**: `test_orders.py::test_create_order_group_links_rows_and_marks_ordered`

### 5-4. 일괄 변경
- **기능 설명**: 선택 항목의 발주일/배송일/상태 일괄 변경. 변경 항목 없으면 400.
- **화면**: `view-orders` → 일괄 처리 바
- **API**: `PUT /api/orders/bulk`
- **Backend**: `routers/orders.py::bulk_update_items`
- **DB**: `order_items`
- **테스트**: `test_orders.py::test_bulk_update_status_and_dates`

## 6. 문서 출력

### 6-1. 미리보기 토큰 생성
- **기능 설명**: 문서 유형 + service_ids 또는 날짜 범위로 미리보기 생성. payload를 DB에 저장, 6시간 만료 토큰 발급. 사용자 소유 검증.
- **화면**: `view-workspace` → `출력물 생성`
- **API**: `POST /api/documents/preview`, `GET /preview/{token}`
- **Backend**: `routers/documents.py`, `document_service.py::create_preview`
- **DB**: `document_previews`
- **테스트**: `test_document_hwpx.py`, `test_hwpx_pdf_renderer.py`

### 6-2. 식단표 출력
- **기능 설명**: 기간 내 배식을 주 단위(월~금)로 그룹핑. 중식/석식 블록에 계획식수/시간/콘셉트/메뉴명 목록. HTML 미리보기/PDF/HWPX.
- **화면**: `view-workspace` → 출력물 생성 → 식단표
- **API**: `POST /api/documents/meal-plan/preview`, `POST /api/documents/meal-plan/hwpx`
- **Backend**: `document_builders.py::MealPlanDocumentBuilder`, `document_hwpx.py`, `hwpx_engine.py::MealPlanRenderer`
- **DB**: `meal_services`, `meal_service_menus`, `document_templates`, `document_previews`
- **테스트**: `test_document_builders.py::test_meal_plan_builder_*`, `test_document_hwpx.py`, `test_hwpx_engine.py`, `test_hwpx_output_system.py`, `test_hwpx_repeat_pages.py`

### 6-3. 조리지시서 출력
- **기능 설명**: 일자별 중식/석식 블록, 메뉴별 재료(수량/단위/비고) + 조리지시 + 비고. 메뉴당 7슬롯, 초과 시 마지막 슬롯에 나머지 병합.
- **화면**: `view-workspace` → 출력물 생성 → 조리지시서
- **API**: `POST /api/documents/cooking-instruction/preview`, `POST /api/documents/cooking-instruction/hwpx`
- **Backend**: `document_builders.py::CookingInstructionDocumentBuilder`, `hwpx_engine.py::CookingInstructionRenderer`
- **DB**: 상동
- **테스트**: `test_document_builders.py`, `test_document_hwpx.py`, `test_hwpx_output_system.py`, `test_hwpx_repeat_pages.py`

### 6-4. 보존식 기록지 출력
- **기능 설명**: 배식별 기록 블록(날짜/식사명/채수시각/관리자/메뉴/냉동고온도/폐기시각/채수자). 한 페이지 3건, 부족한 칸은 빈 양식 유지.
- **화면**: `view-workspace` → 출력물 생성 → 보존식 기록지
- **API**: `POST /api/documents/preserved-food/preview`, `POST /api/documents/preserved-food/hwpx` (+ `preservation-record` 별칭)
- **Backend**: `document_builders.py::PreservationRecordDocumentBuilder`, `hwpx_engine.py::PreservedFoodRenderer`
- **DB**: 상동
- **테스트**: `test_document_builders.py`, `test_document_hwpx.py`, `test_hwpx_output_system.py`, `test_hwpx_repeat_pages.py`

### 6-5. HWPX 템플릿 관리
- **기능 설명**: 문서 유형별(MEAL_PLAN/COOKING_INSTRUCTION/PRESERVATION_RECORD) HWPX 템플릿 등록/수정/검증/활성화/비활성화/다운로드/삭제. 활성 템플릿은 유형당 1개. 업로드 시 구조 검증(필수 파일, XML 파싱, manifest/spine, 필수 플레이스홀더). SHA-256 체크섬, 버전 관리. 활성 템플릿 삭제 금지.
- **화면**: `view-master-data` → `HWPX 양식 관리` 탭
- **API**: `/api/master-data/document-templates*` (신), `/api/templates*` (구)
- **Backend**: `routers/master_data.py`, `routers/templates.py`, `hwpx_service.py::validate_hwpx`, `hwpx_engine.py::validate_template`
- **DB**: `document_templates`
- **테스트**: `test_master_data.py::TestHwpxValidation`, `TestTemplateManagement`

### 6-6. 출력 이력 표시
- **기능 설명**: 식단표/조리지시서 출력 시 `meal_plan_output_at`/`cooking_output_at` 기록. 주간 카드에 출력 여부 표시.
- **화면**: `view-workspace` 주간 카드
- **API**: 문서 다운로드 시 부수 효과
- **Backend**: `routers/documents.py::_mark_output`, `_mark_output_services`
- **DB**: `meal_services`
- **테스트**: `test_document_hwpx.py` (출력 시각 검증)

## 7. 통계 / 대시보드

### 7-1. 운영 대시보드
- **기능 설명**: KPI(운영일수, 고유 메뉴 수, 중식/석식 계획·실제), 12개월 추세, 이상치(식수 급감/급증, 단기/과다 메뉴 반복, 재료군 사용량 ±25%/±40% 변화, 기록 누락), 메뉴 사용 TOP5, 반복 메뉴 TOP5, 재료군 TOP6, 워크플로 완료율.
- **화면**: `view-dashboard`
- **API**: `GET /api/statistics/dashboard`
- **Backend**: `dashboard_service.py::operations_dashboard` (meal_statistics + meal_trend + legacy_dashboard 조합)
- **DB**: `meal_services`, `meal_service_menus`, `meal_actuals`, `preservation_records`
- **테스트**: 없음

### 7-2. 식수 통계
- **기능 설명**: 기간별 계획/실제 합계, 입력률, 중식/석식 분해, 요일별 평균, 개별 배식 backdata. 이상치: 계획 대비 또는 평소(56일 중앙값) 대비 편차 ±10% "확인", ±15% "중요". 비교 데이터 4건 미만이면 `insufficient_comparison`.
- **화면**: `view-stats-meals`
- **API**: `GET /api/statistics/meals`, `GET /api/statistics/meals/trend`
- **Backend**: `statistics_service.py::meal_statistics`, `meal_trend`
- **DB**: `meal_services`, `meal_actuals`
- **테스트**: 없음

### 7-3. 메뉴 통계
- **기능 설명**: 메뉴별 사용 횟수(중식/석식 분리), 첫/마지막 사용일, 평균 간격, 신규 메뉴, 반복(14일 2회 단기 / 28일 3회 과다), 미사용 메뉴(기본 90일), 상세(월별 사용, 최근 이력, 동시 제공 메뉴).
- **화면**: `view-stats-menus`
- **API**: `GET /api/statistics/menus`, `GET /api/statistics/menus/{id}`
- **Backend**: `menu_statistics.py`
- **DB**: `meal_services`, `meal_service_menus`, `meal_actuals`, `menus`
- **테스트**: 없음

### 7-4. 식재료 통계
- **기능 설명**: 재료별 사용 횟수, 총 사용량(g), 중식/석식 분리, 신규 재료, 미사용 재료(90일), 상세(월별, 최근 이력, 동시 사용 재료). `quantity_total` 없으면 `per_100 × planned / 100` 역산.
- **화면**: `view-stats-ingredients`
- **API**: `GET /api/statistics/ingredients`, `GET /api/statistics/ingredients/{id}`
- **Backend**: `ingredient_statistics.py`
- **DB**: `meal_services`, `meal_service_menus`, `meal_service_menu_ingredients`, `meal_actuals`, `ingredients`
- **테스트**: 없음

### 7-5. 운영 기록 통계
- **기능 설명**: 배식별 실제식수 입력/보존식 완료/식단표 출력/조리지시서 출력 여부와 완료율, 월별 추세, 기록 누락 목록, 지연 입력(기록일 - 서비스일 > 1일), 보존식 관리자별 건수/온도 기록.
- **화면**: `view-stats-operations`
- **API**: `GET /api/statistics/operations`
- **Backend**: `operations_statistics.py`
- **DB**: `meal_services`, `meal_actuals`, `preservation_records`
- **테스트**: 없음

### 7-6. 주간/대시보드 참고 통계 (legacy)
- **기능 설명**: 주간 단백질원 구성, 재료군 사용 빈도/환산 kg, 반복 메뉴(이전 4주), 실제식수 요약, 워크플로. 대시보드의 `menu_usage`/`repeated_menus`/`ingredient_groups`/`workflow` 데이터 소스.
- **화면**: 대시보드 내부 데이터
- **API**: `GET /api/stats/week`, `GET /api/stats/dashboard`
- **Backend**: `stats_service.py::_aggregate`
- **DB**: `meal_services`, `meal_service_menus`, `meal_service_menu_ingredients`, `recipes`, `recipe_ingredients`, `meal_actuals`, `preservation_records`
- **테스트**: 없음

## 8. 시스템 관리 (admin)

### 8-1. 데이터 백업
- **기능 설명**: `pg_dump -F c` 커스텀 포맷 백업. SQLite 환경에서는 400. 백업 목록/다운로드/삭제, SHA-256 체크섬, 감사 로그. 경로 탈출 방지(백업 디렉터리 내 검증).
- **화면**: `view-backup`
- **API**: `GET/POST /api/admin/backups`, `GET /{id}/download`, `DELETE /{id}`
- **Backend**: `routers/admin.py`
- **DB**: `backup_records`, `audit_logs`
- **테스트**: 없음

### 8-2. Excel 데이터 아카이브
- **기능 설명**: 기간(또는 전체)의 업무 데이터를 Excel로 내보내기. 시트: 식단기록/조리지시서/보존식기록/실제식수/메뉴기준정보/재료기준정보/레시피/식사유형설정/사용자목록. 24시간 보존 후 만료 처리, 자동 정리.
- **화면**: `view-archive`
- **API**: `GET/POST /api/admin/archives`, `GET /{id}/download`, `DELETE /{id}`, `POST /api/admin/archives/cleanup`
- **Backend**: `routers/admin.py::_build_excel`
- **DB**: `data_archives`, `audit_logs`
- **테스트**: 없음

## 9. 프론트엔드 공통 기능

### 9-1. 시간 입력 (TimeInput24)
- **기능 설명**: 24시간제 시간 입력 컴포넌트. `1140`→`11:40`, `930`→`09:30`, `9`→`09:00`, `11.40`→`11:40` 등 자유 입력 정규화. 화살표 키 증감, Escape 초기화, 퀵 버튼, aria-invalid 표시.
- **화면**: 식단 작성/배식 기본값 등 시간 입력 전반
- **API**: 없음 (프론트엔드)
- **Backend**: 없음
- **DB**: 없음
- **테스트**: `test_time_input24.py` (Python으로 로직 복제 검증 + JS 소스 존재 확인)

### 9-2. 문서 미리보기 모달
- **기능 설명**: 문서 유형 선택 → PDF 미리보기(iframe) → HWPX/PDF 다운로드. PDF 변환 실패 시에도 HWPX 다운로드 가능.
- **화면**: `view-workspace` → `출력물 생성`
- **API**: 문서 API 전반
- **Backend**: `routers/documents.py`
- **DB**: `document_previews`
- **테스트**: `test_hwpx_pdf_renderer.py`
